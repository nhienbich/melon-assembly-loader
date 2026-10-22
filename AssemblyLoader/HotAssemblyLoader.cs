using System.Reflection;
using System.Security.Cryptography;
using MelonLoader.Utils;

namespace MelonAssemblyLoader;

/// <summary>
/// Watches UserData/assembly/Payload.dll and runs public static void Main() / Unload().
/// File events are queued. Tick() must run on the Unity main thread (MelonMod.OnUpdate).
/// </summary>
internal sealed class HotAssemblyLoader : IDisposable
{
    private const string DirectoryName = "assembly";
    private const string DllName = "Payload.dll";
    private const string MainMethod = "Main";
    private const string UnloadMethod = "Unload";
    private static readonly TimeSpan Debounce = TimeSpan.FromMilliseconds(600);

    private readonly ILog log;
    private readonly string assemblyPath;
    private FileSystemWatcher? watcher;
    private PayloadLoadContext? context;
    private Assembly? assembly;
    private string? hash;
    private WeakReference? pendingCollect;
    private int dirty;
    private long dirtyAt;
    private int removed;
    private bool disposed;
    private bool announcedLock;

    public HotAssemblyLoader(ILog log)
    {
        this.log = log;
        string directory = Path.Combine(MelonEnvironment.UserDataDirectory, DirectoryName);
        Directory.CreateDirectory(directory);
        assemblyPath = Path.Combine(directory, DllName);

        watcher = new FileSystemWatcher(directory)
        {
            Filter = DllName,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.CreationTime,
            IncludeSubdirectories = false,
        };
        watcher.Changed += OnFileEvent;
        watcher.Created += OnFileEvent;
        watcher.Renamed += OnFileEvent;
        watcher.Deleted += OnDeleted;
        watcher.EnableRaisingEvents = true;
    }

    public string AssemblyPath => assemblyPath;

    public void RequestInitialLoad()
    {
        if (disposed)
            return;

        if (File.Exists(assemblyPath))
            MarkDirty(immediate: true);
        else
            log.Info($"Waiting for {assemblyPath}");
    }

    public void Tick()
    {
        if (disposed || Volatile.Read(ref dirty) == 0)
            return;

        long at = Interlocked.Read(ref dirtyAt);
        if (TimeSpan.FromTicks(DateTime.UtcNow.Ticks - at) < Debounce)
            return;

        if (Interlocked.Exchange(ref dirty, 0) == 0)
            return;

        try
        {
            Reload();
        }
        catch (Exception ex)
        {
            log.Error(ex.ToString());
        }
        finally
        {
            DrainCollect();
        }
    }

    public void Dispose()
    {
        if (disposed)
            return;

        disposed = true;
        if (watcher != null)
        {
            watcher.EnableRaisingEvents = false;
            watcher.Dispose();
            watcher = null;
        }

        UnloadPrevious();
        DrainCollect();
    }

    private void OnFileEvent(object sender, FileSystemEventArgs args)
    {
        if (!IsPayloadPath(args.FullPath))
            return;

        MarkDirty(immediate: false);
    }

    private void OnDeleted(object sender, FileSystemEventArgs args)
    {
        if (!IsPayloadPath(args.FullPath))
            return;

        Interlocked.Exchange(ref removed, 1);
        MarkDirty(immediate: false);
    }

    private void MarkDirty(bool immediate)
    {
        Interlocked.Exchange(ref dirtyAt, immediate ? 0 : DateTime.UtcNow.Ticks);
        Volatile.Write(ref dirty, 1);
    }

    private void Reload()
    {
        if (Interlocked.Exchange(ref removed, 0) == 1 && !File.Exists(assemblyPath))
        {
            log.Info("Payload.dll removed. Unloading.");
            UnloadPrevious();
            return;
        }

        if (!File.Exists(assemblyPath))
            return;

        if (!TryReadBytes(assemblyPath, out byte[] bytes))
            return;

        if (!LooksLikeAssembly(bytes))
        {
            log.Info("Payload.dll is still being written. Retrying.");
            MarkDirty(immediate: false);
            return;
        }

        string newHash = Md5(bytes);
        if (newHash == hash)
        {
            log.Info("File not changed.");
            return;
        }

        log.Info($"File {DllName} changed. Reloading.");
        UnloadPrevious();

        byte[]? symbols = TryReadSymbols();
        var next = new PayloadLoadContext(assemblyPath);
        Assembly loaded;
        try
        {
            loaded = symbols == null
                ? next.LoadFromStream(new MemoryStream(bytes))
                : next.LoadFromStream(new MemoryStream(bytes), new MemoryStream(symbols));
        }
        catch (Exception ex)
        {
            next.Unload();
            log.Error($"Load failed: {ex.Message}");
            return;
        }

        context = next;
        assembly = loaded;
        hash = newHash;
        log.Info($"Loaded {DllName} ({newHash[..8]}).");
        Invoke(loaded, MainMethod);
    }

    private void UnloadPrevious()
    {
        Assembly? previous = assembly;
        PayloadLoadContext? current = context;
        assembly = null;
        context = null;
        hash = null;

        if (previous != null)
            Invoke(previous, UnloadMethod);

        if (current == null)
            return;

        pendingCollect = new WeakReference(current, trackResurrection: true);
        try
        {
            current.Unload();
        }
        catch (Exception ex)
        {
            log.Error($"Context unload failed: {ex.Message}");
        }
    }

    private void DrainCollect()
    {
        WeakReference? weak = pendingCollect;
        pendingCollect = null;
        if (weak == null)
            return;

        for (int i = 0; weak.IsAlive && i < 3; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        if (weak.IsAlive)
            log.Warn("Payload context is still alive. Unload() must drop statics, Harmony patches and Unity objects.");
        else
            log.Info("Previous payload unloaded.");
    }

    private void Invoke(Assembly target, string methodName)
    {
        Type[] types;
        try
        {
            types = target.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            types = ex.Types.Where(type => type != null).Cast<Type>().ToArray();
            log.Warn($"Some payload types failed to load: {ex.LoaderExceptions.FirstOrDefault()?.Message}");
        }

        foreach (Type type in types)
        {
            MethodInfo? method;
            try
            {
                method = type.GetMethods(BindingFlags.Static | BindingFlags.Public)
                    .FirstOrDefault(candidate =>
                        candidate.Name == methodName && candidate.GetParameters().Length == 0);
            }
            catch (Exception ex)
            {
                log.Warn($"Skip {type.FullName}: {ex.Message}");
                continue;
            }

            if (method == null)
                continue;

            log.Info($"Running {type.FullName}.{method.Name}");
            try
            {
                method.Invoke(null, null);
            }
            catch (TargetInvocationException ex)
            {
                log.Error((ex.InnerException ?? ex).ToString());
            }

            return;
        }

        if (methodName == MainMethod)
            log.Warn("No public static void Main() found.");
    }

    private byte[]? TryReadSymbols()
    {
        string pdbPath = Path.ChangeExtension(assemblyPath, ".pdb");
        if (!File.Exists(pdbPath))
            return null;

        try
        {
            return File.ReadAllBytes(pdbPath);
        }
        catch (IOException)
        {
            log.Info("PDB is locked. Loading without symbols.");
            return null;
        }
    }

    private bool TryReadBytes(string path, out byte[] bytes)
    {
        try
        {
            bytes = File.ReadAllBytes(path);
            announcedLock = false;
            return true;
        }
        catch (IOException)
        {
            bytes = Array.Empty<byte>();
            if (!announcedLock)
            {
                log.Info("Payload.dll is locked. Retrying.");
                announcedLock = true;
            }

            MarkDirty(immediate: false);
            return false;
        }
    }

    private static bool IsPayloadPath(string path)
    {
        return string.Equals(Path.GetFileName(path), DllName, StringComparison.OrdinalIgnoreCase);
    }

    private static bool LooksLikeAssembly(byte[] bytes)
    {
        return bytes.Length >= 64 && bytes[0] == (byte)'M' && bytes[1] == (byte)'Z';
    }

    private static string Md5(byte[] bytes)
    {
        return Convert.ToHexString(MD5.HashData(bytes)).ToLowerInvariant();
    }
}
