using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading;
using MelonLoader;
using MelonLoader.Utils;

[assembly: MelonInfo(typeof(MelonScriptLoader.ScriptLoaderMod), "ScriptLoader", "1.0.0", "MelonScriptLoader")]

namespace MelonScriptLoader
{
    /// <summary>
    /// MelonLoader Mono port of BepInEx.ScriptLoader.
    /// Compiles UserData/scripts/*.cs while the game is running.
    /// </summary>
    public sealed class ScriptLoaderMod : MelonMod
    {
        private const long DebounceTicks = 600L * TimeSpan.TicksPerMillisecond;

        private string scriptsPath;
        private FileSystemWatcher watcher;
        private Assembly lastCompilationAssembly;
        private string lastCompilationHash;
        private LogWriter loggerTextWriter;
        private int dirty;
        private long dirtyAt;

        public override void OnInitializeMelon()
        {
            scriptsPath = Path.Combine(MelonEnvironment.UserDataDirectory, "scripts");
            Directory.CreateDirectory(scriptsPath);
            Utilities.Init(scriptsPath);
            loggerTextWriter = new LogWriter(LoggerInstance);

            LoggerInstance.Msg("ScriptLoader is loaded.");
            LoggerInstance.Msg("Watching " + scriptsPath);
            CompileScripts();

            watcher = new FileSystemWatcher(scriptsPath);
            watcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size;
            watcher.Filter = "*.cs";
            watcher.Changed += OnFileEvent;
            watcher.Created += OnFileEvent;
            watcher.Deleted += OnFileEvent;
            watcher.Renamed += OnFileEvent;
            watcher.EnableRaisingEvents = true;
        }

        public override void OnUpdate()
        {
            if (Interlocked.CompareExchange(ref dirty, 0, 0) == 0)
                return;

            var at = Interlocked.Read(ref dirtyAt);
            if (DateTime.UtcNow.Ticks - at < DebounceTicks)
                return;

            if (Interlocked.Exchange(ref dirty, 0) == 0)
                return;

            try
            {
                CompileScripts();
            }
            catch (Exception ex)
            {
                LoggerInstance.Error(ex.ToString());
            }
        }

        public override void OnDeinitializeMelon()
        {
            if (watcher != null)
            {
                watcher.EnableRaisingEvents = false;
                watcher.Dispose();
                watcher = null;
            }

            InvokeAll(lastCompilationAssembly, "Unload");
            lastCompilationAssembly = null;
        }

        private void OnFileEvent(object sender, FileSystemEventArgs args)
        {
            Interlocked.Exchange(ref dirtyAt, DateTime.UtcNow.Ticks);
            Interlocked.Exchange(ref dirty, 1);
        }

        private void CompileScripts()
        {
            if (!Directory.Exists(scriptsPath))
            {
                Directory.CreateDirectory(scriptsPath);
                return;
            }

            var files = Directory.GetFiles(scriptsPath, "*.cs");
            var availableScripts = new Dictionary<string, ScriptInfo>();
            foreach (var file in files)
                availableScripts[file] = ScriptInfo.FromTextFile(file);

            var ignoresPath = Path.Combine(scriptsPath, "scriptignores");
            if (!File.Exists(ignoresPath))
                File.WriteAllText(ignoresPath, "");

            var processName = Path.GetFileNameWithoutExtension(MelonEnvironment.GameExecutablePath);
            var ignores = new HashSet<string>(File.ReadAllLines(ignoresPath).Select(s => s.Trim()));
            var scriptsToCompile = new List<string>();
            foreach (var file in files)
            {
                if (ignores.Contains(Path.GetFileName(file)))
                    continue;
                if (!IsValidProcess(availableScripts[file], processName))
                    continue;
                scriptsToCompile.Add(file);
            }

            LoggerInstance.Msg("Found " + files.Length + " scripts to compile, skipping " +
                               (files.Length - scriptsToCompile.Count) +
                               " scripts because of `scriptignores` or process filters");

            if (scriptsToCompile.Count == 0)
            {
                if (lastCompilationHash != "empty")
                {
                    InvokeAll(lastCompilationAssembly, "Unload");
                    lastCompilationAssembly = null;
                    lastCompilationHash = "empty";
                }

                LoggerInstance.Msg("No scripts to compile.");
                return;
            }

            var md5 = MD5.Create();
            var scriptDict = new Dictionary<string, byte[]>();
            foreach (var scriptFile in scriptsToCompile)
            {
                byte[] data;
                try
                {
                    data = File.ReadAllBytes(scriptFile);
                }
                catch (IOException)
                {
                    LoggerInstance.Msg(Path.GetFileName(scriptFile) + " is locked. Retrying.");
                    Interlocked.Exchange(ref dirtyAt, DateTime.UtcNow.Ticks);
                    Interlocked.Exchange(ref dirty, 1);
                    return;
                }

                md5.TransformBlock(data, 0, data.Length, null, 0);
                scriptDict[scriptFile] = data;
            }

            md5.TransformFinalBlock(new byte[0], 0, 0);
            var hash = Convert.ToBase64String(md5.Hash);
            md5.Clear();

            if (hash == lastCompilationHash)
            {
                LoggerInstance.Msg("No changes detected. Skipping compilation.");
                return;
            }

            foreach (var scriptFile in scriptsToCompile)
            {
                ScriptInfo info;
                if (!availableScripts.TryGetValue(scriptFile, out info))
                    continue;

                foreach (var reference in info.References)
                {
                    try
                    {
                        Assembly.Load(File.ReadAllBytes(reference));
                    }
                    catch (Exception ex)
                    {
                        LoggerInstance.Error("Failed to reference " + reference + ": " + ex.Message);
                    }
                }
            }

            LoggerInstance.Msg("Compiling " + scriptsToCompile.Count + " scripts.");
            var ass = MonoCompiler.Compile(scriptDict, loggerTextWriter);
            if (ass == null)
            {
                LoggerInstance.Error("Skipping loading scripts because of errors above.");
                return;
            }

            InvokeAll(lastCompilationAssembly, "Unload");
            lastCompilationAssembly = ass;
            lastCompilationHash = hash;
            InvokeAll(ass, "Main");
        }

        private static bool IsValidProcess(ScriptInfo info, string processName)
        {
            if (info.ProcessFilters.Count == 0)
                return true;

            foreach (var filter in info.ProcessFilters)
            {
                var name = filter.ToLowerInvariant().Replace(".exe", "");
                if (string.Equals(name, processName, StringComparison.InvariantCultureIgnoreCase))
                    return true;
            }

            return false;
        }

        private void InvokeAll(Assembly assembly, string methodName)
        {
            if (assembly == null)
                return;

            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                types = ex.Types.Where(type => type != null).ToArray();
                LoggerInstance.Warning("Some script types failed to load: " +
                                        (ex.LoaderExceptions.Length > 0 ? ex.LoaderExceptions[0].Message : ex.Message));
            }

            foreach (var type in types)
            {
                MethodInfo method;
                try
                {
                    method = type.GetMethods(BindingFlags.Static | BindingFlags.Public)
                        .FirstOrDefault(m => m.Name == methodName && m.GetParameters().Length == 0);
                }
                catch (Exception ex)
                {
                    LoggerInstance.Warning("Skip " + type.FullName + ": " + ex.Message);
                    continue;
                }

                if (method == null)
                    continue;

                LoggerInstance.Msg((methodName == "Unload" ? "Unloading " : "Running ") + type.Name);
                try
                {
                    method.Invoke(null, new object[0]);
                }
                catch (TargetInvocationException ex)
                {
                    LoggerInstance.Error((ex.InnerException ?? ex).ToString());
                }
            }
        }
    }
}
