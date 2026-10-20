using System.Reflection;
using System.Runtime.Loader;

namespace MelonAssemblyLoader;

/// <summary>
/// Collectible context so a payload can actually unload on CoreCLR.
/// Framework, MelonLoader and Il2CppInterop assemblies stay in the default context.
/// </summary>
internal sealed class PayloadLoadContext : AssemblyLoadContext
{
    private readonly string directory;
    private readonly AssemblyDependencyResolver resolver;

    public PayloadLoadContext(string payloadPath) : base(isCollectible: true)
    {
        directory = Path.GetDirectoryName(payloadPath) ?? ".";
        resolver = new AssemblyDependencyResolver(payloadPath);
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        if (IsInDefaultContext(assemblyName))
            return null;

        string? path = resolver.ResolveAssemblyToPath(assemblyName);
        if (path == null && !string.IsNullOrEmpty(assemblyName.Name))
        {
            string beside = Path.Combine(directory, assemblyName.Name + ".dll");
            if (File.Exists(beside))
                path = beside;
        }

        if (path == null)
            return null;

        return LoadFromStream(new MemoryStream(File.ReadAllBytes(path)));
    }

    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        string? path = resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        if (path == null)
            return IntPtr.Zero;

        return LoadUnmanagedDllFromPath(path);
    }

    private static bool IsInDefaultContext(AssemblyName assemblyName)
    {
        foreach (Assembly loaded in Default.Assemblies)
        {
            if (string.Equals(loaded.GetName().Name, assemblyName.Name, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
