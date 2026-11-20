using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;

// .NET Framework 4.8 has no ModuleInitializerAttribute in mscorlib; the compiler
// honours a user-declared one with this exact name/shape (same trick as IsExternalInit).
namespace System.Runtime.CompilerServices
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    internal sealed class ModuleInitializerAttribute : Attribute { }
}

namespace ApkShellext2
{
    /// <summary>
    /// This shell extension runs in-process inside explorer.exe (and every other
    /// shell host), so ApkShellext2.dll.config is NEVER read — the host application's
    /// config (explorer.exe.config) owns binding redirects and does not know about our
    /// NuGet facades. Svg 3.5.0 / System.Memory reference e.g.
    /// System.Runtime.CompilerServices.Unsafe v4.0.4.1 while we ship the newer 6.0.0.x
    /// facade, which without a redirect fails with FileLoadException ("manifest
    /// definition does not match") — silently killing all vector-icon rendering.
    /// Resolve facade assemblies ourselves from the directory that contains this
    /// assembly. Module initializers run once, before any of our code executes.
    /// </summary>
    internal static class FacadeResolver
    {
        private static readonly string[] facades = {
            "System.Runtime.CompilerServices.Unsafe",
            "System.Memory",
            "System.Buffers",
            "System.Numerics.Vectors",
        };

        private static string baseDir;

        [ModuleInitializer]
        internal static void Init()
        {
            try {
                baseDir = Path.GetDirectoryName(typeof(FacadeResolver).Assembly.Location);
            } catch { baseDir = null; }
            AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyResolve;
        }

        private static Assembly OnAssemblyResolve(object sender, ResolveEventArgs args)
        {
            try {
                if (baseDir == null) return null;
                string name = new AssemblyName(args.Name).Name;
                foreach (string f in facades) {
                    if (string.Equals(name, f, StringComparison.OrdinalIgnoreCase)) {
                        string candidate = Path.Combine(baseDir, name + ".dll");
                        if (File.Exists(candidate))
                            return Assembly.LoadFrom(candidate);
                        return null;
                    }
                }
            } catch { /* never let the resolver itself crash a shell host */ }
            return null;
        }
    }
}
