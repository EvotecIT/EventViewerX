using System;
using System.IO;
using System.Management.Automation.Runspaces;
using System.Reflection;

/// <summary>
/// OnModuleImportAndRemove is a class that implements the IModuleAssemblyInitializer and IModuleAssemblyCleanup interfaces.
/// This class is used to handle the assembly resolve event when the module is imported and removed.
/// </summary>
public class OnModuleImportAndRemove : IModuleAssemblyInitializer, IModuleAssemblyCleanup {
    /// <summary>
    /// OnImport is called when the module is imported.
    /// </summary>
    public void OnImport() {
        PSEventViewer.PowerShellTypeAcceleratorRegistry.Register();
        if (IsNetFramework()) {
            AppDomain.CurrentDomain.AssemblyResolve += MyResolveEventHandler;
        }
    }

    /// <summary>
    /// OnRemove is called when the module is removed.
    /// </summary>
    /// <param name="module"></param>
    public void OnRemove(PSModuleInfo module) {
        Guid runspaceId = Runspace.DefaultRunspace?.InstanceId ?? Guid.Empty;
        PSEventViewer.PowerShellWatcherRegistry.EndModuleInstance(runspaceId, module);
        PSEventViewer.PowerShellTypeAcceleratorRegistry.Unregister();
        if (IsNetFramework()) {
            AppDomain.CurrentDomain.AssemblyResolve -= MyResolveEventHandler;
        }
    }

    /// <summary>
    /// MyResolveEventHandler is a method that handles the AssemblyResolve event.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="args"></param>
    /// <returns></returns>
    private static Assembly? MyResolveEventHandler(object? sender, ResolveEventArgs args) {
        var libDirectory = Path.GetDirectoryName(typeof(OnModuleImportAndRemove).Assembly.Location);
        var directoriesToSearch = new List<string>();

        if (!string.IsNullOrEmpty(libDirectory)) {
            directoriesToSearch.Add(libDirectory);

            if (Directory.Exists(libDirectory)) {
                directoriesToSearch.AddRange(Directory.GetDirectories(libDirectory, "*", SearchOption.AllDirectories));
            }
        }

        var requestedAssemblyName = new AssemblyName(args.Name).Name + ".dll";

        foreach (var directory in directoriesToSearch) {
            var assemblyPath = Path.Combine(directory, requestedAssemblyName);

            if (File.Exists(assemblyPath)) {
                try {
                    return Assembly.LoadFrom(assemblyPath);
                } catch (BadImageFormatException ex) {
                    Console.WriteLine($"Failed to load assembly from {assemblyPath}: {ex.Message}");
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Determine if the current runtime is .NET Framework
    /// </summary>
    /// <returns></returns>
    private bool IsNetFramework() {
        return System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription.StartsWith(".NET Framework", StringComparison.OrdinalIgnoreCase);
    }

}
