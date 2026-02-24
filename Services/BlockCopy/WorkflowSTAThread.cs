using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using OpennessCopy.Models;
using OpennessCopy.Utils;
using static OpennessCopy.Services.BlockCopy.BlockXmlProcessingService;
using static OpennessCopy.Services.BlockCopy.ExportService;
using static OpennessCopy.Services.BlockCopy.ImportService;
using static OpennessCopy.Services.PlcManagementService;
using static OpennessCopy.Services.SecurityManagementService;
using static OpennessCopy.Services.TagCopyService;
using static OpennessCopy.Services.ValidationService;

namespace OpennessCopy.Services.BlockCopy;

/// <summary>
/// PLC block copy workflow STA thread.
/// </summary>
public class WorkflowStaThread : IWorkflowThread
{
    private readonly Action<WorkflowProgress> _progressCallback;
    private readonly Action<DiscoveryData> _discoveryCallback;
    private readonly string _exportDir;

    private Thread _staThread;
    private WorkflowResourceManager _resourceManager;
    private volatile bool _isRunning;
    private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
    private readonly StaTaskQueue _taskQueue = new StaTaskQueue();
    private volatile bool _stopRequested;
    private bool _taskQueueDisposed;
    private readonly TiaPortalVersion _tiaVersion;

    // ReSharper disable once ConvertToPrimaryConstructor
    public WorkflowStaThread(
        Action<WorkflowProgress> progressCallback,
        Action<DiscoveryData> discoveryCallback,
        string exportDir,
        TiaPortalVersion tiaVersion)
    {
        _progressCallback = progressCallback;
        _discoveryCallback = discoveryCallback;
        _exportDir = exportDir;
        _tiaVersion = tiaVersion;
    }

    public void Start()
    {
        if (_isRunning)
        {
            throw new InvalidOperationException("Workflow is already running");
        }

        _isRunning = true;

        _staThread = new Thread(STAThreadBegin)
        {
            IsBackground = false,
            Name = "OpennessSTA"
        };
        _staThread.SetApartmentState(ApartmentState.STA);
        _staThread.Start();
    }

    private void STAThreadBegin()
    {
        if (!DependencyManagementService.VerifyDependencies(_tiaVersion))
        {
            _isRunning = false;
            return;
        }

        ExecuteOnStaThread();
    }

    public void SetUserSelections(UserSelections userSelections)
    {
        if (userSelections == null)
        {
            Logger.LogWarning("User selections are null - ignoring.");
            return;
        }

        if (!TryEnqueue(() => ProcessUserSelections(userSelections)))
        {
            Logger.LogWarning("Ignoring user selections because workflow is shutting down.");
        }
    }

    public bool ValidateSafetyPassword(string deviceId, string password, bool isAutoCheck = false)
    {
        try
        {
            return ExecuteOnSta(
                () => SecurityManagementService.ValidateSafetyPassword(deviceId, password, isAutoCheck),
                TimeSpan.FromSeconds(10),
                bufferLogs: false,
                timeoutMessage: $"Timeout validating safety password for device {deviceId}");
        }
        catch (TimeoutException)
        {
            Logger.LogError("[UI THREAD] Timeout waiting for safety password validation", false);
        }
        catch (Exception ex)
        {
            Logger.LogError($"[UI THREAD] Safety password validation failed: {ex.Message}", false);
        }

        return false;
    }

    public bool LoadDetailedDataForPlc(PLCInfo plcInfo)
    {
        string timeoutMessage = plcInfo == null
            ? "[UI THREAD] Timeout waiting for detailed data loading"
            : $"[UI THREAD] Timeout waiting for detailed data loading for PLC {plcInfo.Name}";

        try
        {
            return ExecuteOnSta(
                () => LoadDetailedDataInternal(plcInfo),
                TimeSpan.FromSeconds(30),
                bufferLogs: false,
                timeoutMessage: timeoutMessage);
        }
        catch (TimeoutException)
        {
            Logger.LogError(timeoutMessage, false);
        }
        catch (Exception ex)
        {
            Logger.LogError($"[UI THREAD] Failed to load detailed data: {ex.Message}", false);
        }

        return false;
    }

    public List<int> ValidatePrefixConflicts(int prefixNumber, string selectedGroupId, HashSet<int> existingBlockNumbers)
    {
        try
        {
            return ExecuteOnSta(
                () => ValidatePrefixConflictsInternal(prefixNumber, selectedGroupId, existingBlockNumbers),
                TimeSpan.FromSeconds(10),
                bufferLogs: true,
                timeoutMessage: "[UI THREAD] Prefix validation timeout");
        }
        catch (TimeoutException)
        {
            Logger.LogError("[UI THREAD] Prefix validation timeout");
        }
        catch (Exception ex)
        {
            Logger.LogError($"[UI THREAD] Prefix validation failed: {ex.Message}", false);
        }

        return new List<int>();
    }

    public (string name, int number) GetFirstBlockFromGroup(string selectedGroupId)
    {
        if (string.IsNullOrEmpty(selectedGroupId))
        {
            return ("FB_Example_Block", 1234);
        }

        try
        {
            return ExecuteOnSta(
                () => GetFirstBlockInternal(selectedGroupId),
                TimeSpan.FromSeconds(5),
                bufferLogs: true,
                timeoutMessage: "[UI THREAD] First block request timeout");
        }
        catch (TimeoutException)
        {
            Logger.LogError("[UI THREAD] First block request timeout", false);
        }
        catch (Exception ex)
        {
            Logger.LogError($"[UI THREAD] First block request failed: {ex.Message}", false);
        }

        return ("FB_Example_Block", 1234);
    }

    public List<TagExample> RequestSampleTagData(string tableId)
    {
        if (string.IsNullOrEmpty(tableId))
        {
            return new List<TagExample>();
        }

        try
        {
            return ExecuteOnSta(
                () => RequestSampleTagDataInternal(tableId),
                TimeSpan.FromSeconds(30),
                bufferLogs: true,
                timeoutMessage: "[UI THREAD] Sample tag data request timeout");
        }
        catch (TimeoutException)
        {
            Logger.LogError("[UI THREAD] Sample tag data request timeout", false);
        }
        catch (Exception ex)
        {
            Logger.LogError($"[UI THREAD] Sample tag data request failed: {ex.Message}", false);
        }

        return new List<TagExample>();
    }

    public (bool success, DiscoveryData discoveryData) LoadArchiveForSelection(string archivePath)
    {
        if (string.IsNullOrWhiteSpace(archivePath))
        {
            return (false, null);
        }

        try
        {
            return ExecuteOnSta(
                () => LoadArchiveInternal(archivePath),
                TimeSpan.FromSeconds(120),
                bufferLogs: false,
                timeoutMessage: "[CALLER THREAD] Archive load timeout");
        }
        catch (TimeoutException)
        {
            Logger.LogError("[CALLER THREAD] Archive load timeout", false);
        }
        catch (Exception ex)
        {
            Logger.LogError($"[CALLER THREAD] Archive load failed: {ex.Message}", false);
        }

        return (false, null);
    }

    public void Cancel()
    {
        if (_cancellationTokenSource.IsCancellationRequested)
        {
            return;
        }

        Logger.LogWarning("Cancellation requested - stopping workflow...");
        _cancellationTokenSource.Cancel();

        try
        {
            _taskQueue.Complete();
        }
        catch (ObjectDisposedException)
        {
            // Queue already disposed during shutdown.
        }
    }

    public void RequestCleanup()
    {
        if (_stopRequested)
        {
            return;
        }

        if (!_taskQueue.IsAddingCompleted && !_taskQueueDisposed)
        {
            if (!TryEnqueue(() =>
            {
                Logger.LogInfo("Cleanup requested for workflow...");
                _stopRequested = true;
                _taskQueue.Complete();
            }))
            {
                _stopRequested = true;
            }
        }
        else
        {
            _stopRequested = true;
        }
    }

    public void Join(TimeSpan timeout)
    {
        _staThread?.Join(timeout);
    }

    private void ExecuteOnStaThread()
    {
        var staThreadId = Thread.CurrentThread.ManagedThreadId;
        Logger.LogInfo($"[STA THREAD {staThreadId}] ExecuteOnStaThread started at {DateTime.Now:HH:mm:ss.fff}");

        try
        {
            ReportProgress(new WorkflowProgress("Connecting to TIA Portal instances...", 5));

            _resourceManager = new WorkflowResourceManager(_exportDir);
            _resourceManager.AllProjects = GetAllProjectsFromAllInstances();
            if (_resourceManager.AllProjects.Count == 0)
            {
                ReportProgress(WorkflowProgress.Error("Failed to connect to any TIA Portal instances or no projects found"));
                _stopRequested = true;
                _taskQueue.Complete();
                return;
            }

            ReportProgress(new WorkflowProgress($"Found {_resourceManager.AllProjects.Count} project(s) across TIA Portal instances", 8));

            ReportProgress(new WorkflowProgress("Extracting project data from all instances...", 10));
            var discoveryData = STADataExtractor.ExtractDiscoveryDataFromAllInstances(
                _resourceManager.AllProjects,
                status => ReportProgress(new WorkflowProgress(status, 12)));

            Task.Run(() => _discoveryCallback?.Invoke(discoveryData));

            ProcessMessageLoop();
        }
        catch (OperationCanceledException)
        {
            ReportProgress(WorkflowProgress.Cancelled("Workflow cancelled by user"));
        }
        catch (Exception ex)
        {
            ReportProgress(WorkflowProgress.Error("Workflow execution failed", ex));
        }
        finally
        {
            CleanupResources();
            _isRunning = false;
        }
    }

    private void ProcessMessageLoop()
    {
        while (true)
        {
            if (!_taskQueue.TryTake(out var workItem, _cancellationTokenSource.Token))
            {
                if (_stopRequested || _taskQueue.IsAddingCompleted || _cancellationTokenSource.IsCancellationRequested)
                {
                    break;
                }

                continue;
            }

            if (workItem == null)
            {
                continue;
            }

            try
            {
                workItem();
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Logger.LogError($"[STA LOOP] Unhandled exception: {ex.Message}", false);
                ReportProgress(WorkflowProgress.Error("Workflow execution failed", ex));
            }

            if (_stopRequested && _taskQueue.IsAddingCompleted)
            {
                break;
            }
        }
    }

    private bool TryEnqueue(Action action)
    {
        if (_stopRequested || _taskQueueDisposed)
        {
            return false;
        }

        try
        {
            return _taskQueue.TryAdd(action);
        }
        catch (ObjectDisposedException)
        {
            return false;
        }
    }

    private T ExecuteOnSta<T>(
        Func<T> func,
        TimeSpan? timeout = null,
        bool bufferLogs = false,
        string timeoutMessage = null)
    {
        var tcs = new TaskCompletionSource<T>();

        if (!TryEnqueue(() =>
            {
                try
                {
                    tcs.TrySetResult(func());
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
            }))
        {
            tcs.TrySetException(new InvalidOperationException("Workflow is shutting down"));
        }

        if (bufferLogs)
        {
            Logger.EnableQueuedLogging();
        }

        try
        {
            if (timeout.HasValue)
            {
                if (!tcs.Task.Wait(timeout.Value))
                {
                    throw new TimeoutException(timeoutMessage ?? "STA request timed out.");
                }
            }
            else
            {
                tcs.Task.Wait();
            }
        }
        finally
        {
            if (bufferLogs)
            {
                Logger.DisableQueuedLogging();
                Logger.FlushQueuedMessages();
            }
        }

        return tcs.Task.GetAwaiter().GetResult();
    }

    private void ProcessUserSelections(UserSelections selections)
    {
        Logger.LogInfo("[STA LOOP] Processing user selections");

        try
        {
            ReportProgress(new WorkflowProgress("Processing user selections...", 25));
            var workflowConfiguration = STADataExtractor.BuildWorkflowConfiguration(selections);

            ExecuteWorkflowPhase(workflowConfiguration);
            ReportProgress(WorkflowProgress.Success("Workflow completed successfully!"));
        }
        catch (OperationCanceledException)
        {
            ReportProgress(WorkflowProgress.Cancelled("Workflow cancelled by user"));
        }
        catch (Exception ex)
        {
            ReportProgress(WorkflowProgress.Error("Workflow execution failed", ex));
        }
        finally
        {
            _stopRequested = true;
            _taskQueue.Complete();
        }
    }

    private bool LoadDetailedDataInternal(PLCInfo plcInfo)
    {
        Logger.LogInfo($"[STA LOOP] Processing detailed data loading request for PLC {plcInfo?.Name ?? "<unknown>"}");

        try
        {
            STADataExtractor.LoadDetailedPlcData(
                plcInfo,
                status => Logger.LogInfo($"[STA DATA LOADING] {status}"));
            Logger.LogInfo("[STA LOOP] Detailed data loading completed successfully");
            return true;
        }
        catch (Exception ex)
        {
            Logger.LogError($"[STA LOOP] Error loading detailed data: {ex.Message}", false);
            return false;
        }
    }

    private List<int> ValidatePrefixConflictsInternal(int prefixNumber, string selectedGroupId, HashSet<int> existingBlockNumbers)
    {
        Logger.LogInfo("[STA LOOP] Processing prefix validation request");
        var conflicts = STADataExtractor.CheckPrefixConflicts(prefixNumber, selectedGroupId, existingBlockNumbers) ?? new List<int>();
        Logger.LogInfo($"[STA LOOP] Prefix validation completed. Conflicts: {conflicts.Count}");
        return conflicts;
    }

    private (string name, int number) GetFirstBlockInternal(string selectedGroupId)
    {
        Logger.LogInfo("[STA LOOP] Processing first block request");
        var result = STADataExtractor.GetFirstBlockFromGroup(selectedGroupId);
        Logger.LogInfo($"[STA LOOP] First block request completed: {result.name} ({result.number})");
        return result;
    }

    private List<TagExample> RequestSampleTagDataInternal(string tableId)
    {
        Logger.LogInfo("[STA LOOP] Processing sample tag data request");

        try
        {
            var samples = STADataExtractor.ExtractSampleTagData(tableId) ?? new List<TagExample>();
            Logger.LogInfo($"[STA LOOP] Sample tag data extracted: {samples.Count} tags");
            return samples;
        }
        catch (Exception ex)
        {
            Logger.LogError($"[STA LOOP] Sample tag data extraction failed: {ex.Message}");
            return new List<TagExample>();
        }
    }

    private (bool success, DiscoveryData discoveryData) LoadArchiveInternal(string archivePath)
    {
        Logger.LogInfo($"[STA THREAD] Loading archive: {archivePath}");

        try
        {
            var archivePortal = _resourceManager.ArchiveTiaPortal;

            var (success, retrievedProject, discoveryData) = STADataExtractor.LoadAndExtractArchive(
                archivePortal,
                archivePath,
                _resourceManager.RetrievedArchivesDir);

            if (success && retrievedProject != null)
            {
                _resourceManager.AddRetrievedProject(retrievedProject, archivePath);
                Logger.LogInfo($"[STA THREAD] Archive loaded: {discoveryData.PLCs.Count} PLC(s) found");
                return (true, discoveryData);
            }

            Logger.LogError("[STA THREAD] Failed to load archive");
            return (false, null);
        }
        catch (Exception ex)
        {
            Logger.LogError($"[STA THREAD] Archive load error: {ex.Message}");
            return (false, null);
        }
    }

    private void CleanupResources()
    {
        try
        {
            ReportProgress(new WorkflowProgress("Cleaning up temporary resources...", 98));
            _resourceManager?.Dispose();
            DataCacheUtility.ClearCache();
        }
        catch (Exception)
        {
            ReportProgress(WorkflowProgress.Warning("Cleanup warning", 100));
        }
        finally
        {
            try
            {
                _taskQueue.Dispose();
            }
            catch
            {
                // Ignore disposal errors.
            }

            _taskQueueDisposed = true;
            _cancellationTokenSource.Dispose();
        }

        ReportProgress(new WorkflowProgress("Cleanup complete", 100, type: WorkflowProgressType.CleanupComplete));
    }

    private void ReportProgress(WorkflowProgress progress)
    {
        try
        {
            _progressCallback?.Invoke(progress);
        }
        catch (Exception ex)
        {
            Logger.LogError(
                $"Progress callback failed: {ex.Message}. Original progress: {progress.Status} ({progress.PercentComplete}%).",
                showMessageBox: false);
        }
    }

    private void ExecuteWorkflowPhase(WorkflowConfiguration config)
    {
        // Validate source PLC compilation before proceeding with export operations
        ReportProgress(new WorkflowProgress("Validating source PLC compilation...", 30));

        _cancellationTokenSource.Token.ThrowIfCancellationRequested();

        var sourceSafetyAdmin = FindSafetyAdministrationForDevice(config.SourceDevice);
        CompileSafe(config.SelectedGroup, sourceSafetyAdmin, config.SourceSafetyPassword);

        ReportProgress(new WorkflowProgress("Exporting selected block group from source PLC...", 45));

        Directory.CreateDirectory(_exportDir);
        string blockDir = Path.Combine(_exportDir, $"{config.SelectedGroup.Name}_Copy");
        Directory.CreateDirectory(blockDir);

        DisplayAndExportGroupContents(
            config.SelectedGroup,
            "",
            blockDir,
            config.SourcePlc,
            sourceSafetyAdmin,
            config.SourceSafetyPassword);

        _cancellationTokenSource.Token.ThrowIfCancellationRequested();

        List<TableReplacementInfo> tagReplacements = null;
        if (config.SelectedTables.Count > 0)
        {
            ReportProgress(new WorkflowProgress("Creating tag table copies on target PLC...", 65));

            _cancellationTokenSource.Token.ThrowIfCancellationRequested();

            tagReplacements = ProcessGUITagTables(
                config.TargetPlc,
                config.SelectedTables,
                config.ExistingTagTableNames,
                _cancellationTokenSource.Token);
        }

        ReportProgress(new WorkflowProgress("Applying block transformations and tag references...", 80));

        _cancellationTokenSource.Token.ThrowIfCancellationRequested();

        UpdateExportedBlocks(
            blockDir,
            tagReplacements,
            config.PrefixNumber,
            config.FindReplacePairs,
            config.ContentFindReplacePairs,
            config.ExistingBlockNames);

        ReportProgress(new WorkflowProgress("Importing transformed blocks to target PLC...", 95));

        _cancellationTokenSource.Token.ThrowIfCancellationRequested();

        ImportFolderStructure(blockDir, config.TargetPlc, _cancellationTokenSource.Token);
    }
}
