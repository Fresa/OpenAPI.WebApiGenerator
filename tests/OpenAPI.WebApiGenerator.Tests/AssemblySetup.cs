using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace OpenAPI.WebApiGenerator.Tests;

internal static class AssemblySetup
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        AppDomain.CurrentDomain.AssemblyResolve += (_, args) =>
        {
            var assemblyName = new AssemblyName(args.Name).Name;
            var assemblyPath = Path.Combine(
                AppContext.BaseDirectory,
                $"{assemblyName}.dll");

            return File.Exists(assemblyPath)
                ? Assembly.LoadFrom(assemblyPath)
                : null;
        };
    }
}
