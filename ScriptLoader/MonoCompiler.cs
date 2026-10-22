// Ported from BepInEx.ScriptLoader (MIT), Copyright (c) 2019 Geoffrey Horsington.
// https://github.com/ghorsington/BepInEx.ScriptLoader
// Compiles against the modified Mono.CSharp in lib/mcs.dll (denikson/mcs-unity).

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using Mono.CSharp;

namespace MelonScriptLoader
{
    internal static class MonoCompiler
    {
        private static readonly HashSet<string> StdLib =
            new HashSet<string>(StringComparer.InvariantCultureIgnoreCase)
            {
                "mscorlib", "System.Core", "System", "System.Xml"
            };

        private static readonly HashSet<string> compiledAssemblies = new HashSet<string>();

        public static Assembly Compile(Dictionary<string, byte[]> sources, TextWriter logger)
        {
            ReportPrinter reporter = logger == null
                ? (ReportPrinter)new ConsoleReportPrinter()
                : new StreamReportPrinter(logger);
            Location.Reset();

            var dllName = "compiled_" + DateTime.Now.Ticks;
            compiledAssemblies.Add(dllName);

            var ctx = CreateContext(reporter);
            ctx.Settings.SourceFiles.Clear();

            var i = 0;

            SeekableStreamReader GetFile(SourceFile file)
            {
                return new SeekableStreamReader(new MemoryStream(sources[file.OriginalFullPathName]), Encoding.UTF8);
            }

            foreach (var source in sources)
            {
                ctx.Settings.SourceFiles.Add(new SourceFile(Path.GetFileName(source.Key), source.Key, i, GetFile));
                i++;
            }

            var container = new ModuleContainer(ctx);

            RootContext.ToplevelTypes = container;
            Location.Initialize(ctx.Settings.SourceFiles);

            var session = new ParserSession { UseJayGlobalArrays = true, LocatedTokens = new LocatedToken[15000] };
            container.EnableRedefinition();

            foreach (var sourceFile in ctx.Settings.SourceFiles)
            {
                var stream = sourceFile.GetInputStream(sourceFile);
                var source = new CompilationSourceFile(container, sourceFile);
                source.EnableRedefinition();
                container.AddTypeContainer(source);
                var parser = new CSharpParser(stream, source, session);
                parser.parse();
            }

            var ass = new AssemblyDefinitionDynamic(container, dllName, dllName + ".dll");
            container.SetDeclaringAssembly(ass);

            var importer = new ReflectionImporter(container, ctx.BuiltinTypes)
            {
                IgnoreCompilerGeneratedField = true,
                IgnorePrivateMembers = false
            };
            ass.Importer = importer;

            var loader = new DynamicLoader(importer, ctx);
            ImportAppdomainAssemblies(delegate(Assembly assembly)
            {
                importer.ImportAssembly(assembly, container.GlobalRootNamespace);
            });

            loader.LoadReferences(container);
            ass.Create(AppDomain.CurrentDomain, AssemblyBuilderAccess.RunAndSave);
            container.CreateContainer();
            loader.LoadModules(ass, container.GlobalRootNamespace);
            container.InitializePredefinedTypes();
            container.Define();

            if (ctx.Report.Errors > 0)
            {
                if (logger != null)
                    logger.WriteLine("Found errors! Aborting compilation...");
                return null;
            }

            try
            {
                ass.Resolve();
                ass.Emit();
                container.CloseContainer();
                ass.EmbedResources();
            }
            catch (Exception e)
            {
                if (logger != null)
                    logger.WriteLine("Failed to compile because " + e);
                return null;
            }

            return ass.Builder;
        }

        private static AssemblyName ParseName(string fullName)
        {
            try
            {
                return new AssemblyName(fullName);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static void ImportAppdomainAssemblies(Action<Assembly> import)
        {
            // Multiple versions of the same assembly can be loaded. Keep the newest.
            var dedupedAssemblies = AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => new { ass = a, name = ParseName(a.FullName) })
                .Where(a => a.name != null)
                .GroupBy(a => a.name.Name)
                .Select(g => g.OrderByDescending(a => a.name.Version).First());
            foreach (var ass in dedupedAssemblies)
            {
                if (StdLib.Contains(ass.name.Name) || compiledAssemblies.Contains(ass.name.Name))
                    continue;
                import(ass.ass);
            }
        }

        private static CompilerContext CreateContext(ReportPrinter reportPrinter)
        {
            var settings = new CompilerSettings
            {
                Version = LanguageVersion.Experimental,
                GenerateDebugInfo = false,
                StdLib = true,
                Target = Target.Library
            };

            return new CompilerContext(settings, reportPrinter);
        }
    }
}
