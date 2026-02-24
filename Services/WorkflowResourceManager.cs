using System;
using System.Collections.Generic;
using System.IO;
using OpennessCopy.Utils;
using Siemens.Engineering;

namespace OpennessCopy.Services;

public sealed class WorkflowResourceManager(string exportDirectory) : IDisposable
{
    public List<(TiaPortal portal, Project project)> AllProjects { get; set; }
    private string ExportDirectory { get; } = exportDirectory;
    private bool _disposed;

    // Dedicated TIA Portal instance for archive retrieval (created on-demand)
    private TiaPortal _archiveTiaPortal;
    public TiaPortal ArchiveTiaPortal
    {
        get
        {
            if (_archiveTiaPortal == null)
            {
                Logger.LogInfo("Creating dedicated TIA Portal instance for archive retrieval (WithoutUserInterface mode)");
                _archiveTiaPortal = new TiaPortal();
                Logger.LogInfo("Archive TIA Portal instance created successfully");
            }
            return _archiveTiaPortal;
        }
    }

    // Dedicated TIA Portal instance for global library operations (created on-demand)
    private TiaPortal _globalLibraryPortal;

    // Lazy-initialized directory for retrieved archives
    private DirectoryInfo _retrievedArchivesDir;
    public DirectoryInfo RetrievedArchivesDir
    {
        get
        {
            if (_retrievedArchivesDir == null)
            {
                var path = Path.Combine(ExportDirectory, "Retrieved_Archives");
                _retrievedArchivesDir = new DirectoryInfo(path);
                if (!_retrievedArchivesDir.Exists)
                {
                    _retrievedArchivesDir.Create();
                    Logger.LogInfo($"Created retrieved archives directory: {path}");
                }
            }
            return _retrievedArchivesDir;
        }
    }

    // Track retrieved projects for cleanup
    private readonly List<(Project project, string archivePath)> _retrievedProjects = new List<(Project, string)>();

    public void AddRetrievedProject(Project project, string archivePath)
    {
        _retrievedProjects.Add((project, archivePath));
        Logger.LogInfo($"Tracking retrieved project for cleanup: {project.Name} (from {Path.GetFileName(archivePath)})");
    }

    public void Dispose()
    {
        Logger.LogInfo("Public Dispose() called");
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        Logger.LogInfo($"Dispose() called - _disposed: {_disposed}, disposing: {disposing}");
        if (_disposed || !disposing) return;

        Logger.LogInfo("WorkflowResourceManager.Dispose() starting...");
        try
        {
            // Close retrieved projects FIRST (before disposing portal or deleting directories)
            if (_retrievedProjects.Count > 0)
            {
                Logger.LogInfo($"Closing {_retrievedProjects.Count} retrieved project(s)...");
                foreach (var (project, archivePath) in _retrievedProjects)
                {
                    try
                    {
                        Logger.LogInfo($"Closing retrieved project: {project.Name} (from {Path.GetFileName(archivePath)})");
                        project.Close();
                    }
                    catch (Exception ex)
                    {
                        Logger.LogWarning($"Error closing retrieved project {project.Name}: {ex.Message}");
                    }
                }
                _retrievedProjects.Clear();
            }

            // Clean up export directory (includes retrieved archives subdirectory)
            if (!string.IsNullOrEmpty(ExportDirectory) && Directory.Exists(ExportDirectory))
            {
                Logger.LogInfo($"Cleaning up export directory: {ExportDirectory}");
                // Directory.Delete(ExportDirectory, true);
            }

            // Dispose archive TIA Portal instance (if created)
            if (_archiveTiaPortal != null)
            {
                Logger.LogInfo("Disposing archive TIA Portal instance");
                try
                {
                    _archiveTiaPortal.Dispose();
                    _archiveTiaPortal = null;
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Error disposing archive TIA Portal: {ex.Message}", false);
                }
            }

            // Dispose global library TIA Portal instance (if created)
            if (_globalLibraryPortal != null)
            {
                Logger.LogInfo("Disposing global library TIA Portal instance");
                try
                {
                    _globalLibraryPortal.Dispose();
                    _globalLibraryPortal = null;
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Error disposing global library TIA Portal: {ex.Message}", false);
                }
            }

            // Dispose TIA Portal connections LAST
            if (AllProjects != null)
            {
                Logger.LogInfo("Disposing TIA Portal connections");
                try
                {
                    foreach (var (portal, _) in AllProjects)
                    {
                        portal.Dispose();
                    }
                    AllProjects = null;
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Error disposing TIA Portal: {ex.Message}", false);
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error during resource cleanup: {ex.Message}", false);
        }
        finally
        {
            _disposed = true;
        }
    }

    ~WorkflowResourceManager()
    {
        Dispose(false);
    }
}