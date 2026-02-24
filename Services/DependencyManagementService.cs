using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using OpennessCopy.Utils;

namespace OpennessCopy.Services;

public enum TiaPortalVersion
{
    V18 = 0,
    V20 = 1
}

public static class DependencyManagementService
{
    private static readonly IReadOnlyDictionary<TiaPortalVersion, IReadOnlyList<RequiredDependency>> DependencySets =
        new Dictionary<TiaPortalVersion, IReadOnlyList<RequiredDependency>>
        {
            {
                TiaPortalVersion.V18,
                new List<RequiredDependency>
                {
                    new()
                    {
                        SimpleName = "Siemens.Engineering",
                        ExpectedPath = @"C:\Program Files\Siemens\Automation\Portal V18\PublicAPI\V18\Siemens.Engineering.dll"
                    },
                    new()
                    {
                        SimpleName = "Siemens.Engineering.Hmi",
                        ExpectedPath = @"C:\Program Files\Siemens\Automation\Portal V18\PublicAPI\V18\Siemens.Engineering.Hmi.dll"
                    }
                }
            },
            {
                TiaPortalVersion.V20,
                new List<RequiredDependency>
                {
                    new()
                    {
                        SimpleName = "Siemens.Engineering",
                        ExpectedPath = @"C:\Program Files\Siemens\Automation\Portal V20\PublicAPI\V20\Siemens.Engineering.dll"
                    },
                    new()
                    {
                        SimpleName = "Siemens.Engineering.Hmi",
                        ExpectedPath = @"C:\Program Files\Siemens\Automation\Portal V20\PublicAPI\V20\Siemens.Engineering.Hmi.dll"
                    }
                }
            }
        };

    private static IReadOnlyList<RequiredDependency> _activeDependencies = [];
    private static bool _resolverAttached;

    public static bool VerifyDependencies(TiaPortalVersion version)
    {
        Logger.WriteToDebugFile($"Checking Siemens DLL dependencies for {GetFriendlyName(version)}...");

        if (!_resolverAttached)
        {
            AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyResolve;
            _resolverAttached = true;
        }

        if (!DependencySets.TryGetValue(version, out var dependencies))
        {
            MessageBox.Show($"Unknown TIA Portal version: {version}", "Dependency Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }

        _activeDependencies = dependencies;

        var missing = dependencies.Where(d => !File.Exists(d.ExpectedPath)).ToList();
        if (missing.Count == 0)
        {
            Logger.WriteToDebugFile("All required DLL files are present.");
            return true;
        }

        var missingPaths = string.Join("\n", missing.Select(d => $"• {d.ExpectedPath}"));

        MessageBox.Show(
            $"Missing Siemens DLL dependencies detected for {GetFriendlyName(version)}:\n\n{missingPaths}\n\n" +
            "Please ensure the selected TIA Portal version is properly installed.",
            "Missing Dependencies",
            MessageBoxButtons.OK,
            MessageBoxIcon.Error);

        Logger.WriteToDebugFile("Cannot start application due to missing dependencies.");
        return false;
    }

    private static Assembly OnAssemblyResolve(object sender, ResolveEventArgs args)
    {
        var name = new AssemblyName(args.Name).Name;
        var dep = _activeDependencies.FirstOrDefault(d => d.SimpleName == name);

        if (dep != null && File.Exists(dep.ExpectedPath))
        {
            Logger.WriteToDebugFile($"Resolving {name} to {dep.ExpectedPath}");
            return Assembly.LoadFrom(dep.ExpectedPath);
        }

        Logger.WriteToDebugFile($"Failed to resolve {name}");
        return null;
    }

    private static string GetFriendlyName(TiaPortalVersion version) =>
        version switch
        {
            TiaPortalVersion.V18 => "TIA Portal V18",
            TiaPortalVersion.V20 => "TIA Portal V20",
            _ => version.ToString()
        };

    private class RequiredDependency
    {
        public string SimpleName { get; set; } = string.Empty;
        public string ExpectedPath { get; set; } = string.Empty;
    }
}
