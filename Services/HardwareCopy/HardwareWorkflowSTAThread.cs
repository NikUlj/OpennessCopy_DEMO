using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OpennessCopy.Models;
using OpennessCopy.Utils;
using Siemens.Engineering;
using Siemens.Engineering.HW;
using static OpennessCopy.Services.HardwareCopy.HardwareMasterCopyService;
using static OpennessCopy.Services.HardwareCopy.STAHardwareDataExtractor;
using static OpennessCopy.Services.PlcManagementService;

namespace OpennessCopy.Services.HardwareCopy;

/// <summary>
/// Hardware workflow STA thread implementation for hardware device copying.
/// </summary>
public class HardwareWorkflowSTAThread : IWorkflowThread
{
    private readonly string _exportDir;
    private readonly Action<HardwareDiscoveryData> _discoveryCallback;
    private readonly Action<WorkflowProgress> _progressCallback;

    private Thread _staThread;
    private WorkflowResourceManager _resourceManager;
    private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
    private readonly StaTaskQueue _taskQueue = new StaTaskQueue();
    private volatile bool _isRunning;
    private volatile bool _stopRequested;
    private bool _taskQueueDisposed;
    private bool _success;
    private readonly TiaPortalVersion _tiaVersion;

    private Siemens.Engineering.Library.UserGlobalLibrary _globalLibrary;
    private Siemens.Engineering.Library.UserGlobalLibrary _targetGlobalLibrary;
    private FileInfo _globalLibraryFile;

    // ReSharper disable once ConvertToPrimaryConstructor
    public HardwareWorkflowSTAThread(
        string exportDirectory,
        Action<HardwareDiscoveryData> discoveryCallback,
        Action<WorkflowProgress> progressCallback = null,
        TiaPortalVersion tiaVersion = TiaPortalVersion.V18)
    {
        _exportDir = exportDirectory ?? throw new ArgumentNullException(nameof(exportDirectory));
        _discoveryCallback = discoveryCallback ?? throw new ArgumentNullException(nameof(discoveryCallback));
        _progressCallback = progressCallback;
        _tiaVersion = tiaVersion;
    }

    public void Start()
    {
        if (_isRunning)
        {
            Logger.LogWarning("Hardware workflow is already running");
            return;
        }

        _isRunning = true;

        _staThread = new Thread(STAThreadBegin)
        {
            Name = "HardwareWorkflowSTAThread",
            IsBackground = false
        };
        _staThread.SetApartmentState(ApartmentState.STA);
        _staThread.Start();

        Logger.LogInfo("Hardware workflow STA thread started");
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

    public void Cancel()
    {
        if (_cancellationTokenSource.IsCancellationRequested)
            return;

        Logger.LogInfo("Cancelling hardware workflow...");
        _cancellationTokenSource.Cancel();
        _stopRequested = true;

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
        if (_stopRequested || _taskQueueDisposed)
            return;

        if (!TryEnqueue(() =>
            {
                Logger.LogInfo("Cleanup requested for hardware workflow...");
                _stopRequested = true;
                _taskQueue.Complete();
            }))
        {
            _stopRequested = true;
        }
    }

    public void Join(TimeSpan timeout)
    {
        _staThread?.Join(timeout);
    }

    public void SetUserSelections(HardwareUserSelections selections)
    {
        if (selections == null)
        {
            Logger.LogWarning("Hardware user selections were null - ignoring.");
            return;
        }

        Logger.LogInfo(
            $"Hardware user selections received: Source='{selections.SourceProjectId}', Target='{selections.TargetProjectId}', " +
            $"Devices={selections.SelectedDevices?.Count ?? 0}, FindReplace pairs={selections.DeviceNameFindReplacePairs?.Count ?? 0}");

        if (!TryEnqueue(() => ProcessUserSelections(selections)))
        {
            Logger.LogWarning("Ignoring hardware user selections because workflow is shutting down.");
        }
    }

    public List<HardwareDeviceInfo> ExtractDevicesFromInstance(
        TiaPortalInstanceInfo instanceInfo,
        Action<int, int> progressCallback = null,
        bool lightweight = false)
    {
        if (instanceInfo == null)
            return new List<HardwareDeviceInfo>();

        try
        {
            return ExecuteOnSta(
                () => ExtractDevicesInternal(instanceInfo, progressCallback, lightweight),
                timeout: TimeSpan.FromSeconds(300),
                bufferLogs: false,
                timeoutMessage: "Timeout waiting for device extraction") ?? new List<HardwareDeviceInfo>();
        }
        catch (TimeoutException)
        {
            Logger.LogError("Timeout waiting for device extraction", false);
            return new List<HardwareDeviceInfo>();
        }
        catch (Exception ex)
        {
            Logger.LogError($"Device extraction failed: {ex.Message}", false);
            return new List<HardwareDeviceInfo>();
        }
    }

    public List<HardwareDeviceInfo> EnrichSelectedDevices(
        List<HardwareDeviceInfo> lightweightDevices,
        Action<int, int> progressCallback = null)
    {
        if (lightweightDevices == null || lightweightDevices.Count == 0)
            return lightweightDevices ?? new List<HardwareDeviceInfo>();

        try
        {
            return ExecuteOnSta(
                () => EnrichDevicesInternal(lightweightDevices, progressCallback),
                timeout: TimeSpan.FromSeconds(300),
                bufferLogs: false,
                timeoutMessage: "Timeout waiting for device enrichment") ?? lightweightDevices;
        }
        catch (TimeoutException)
        {
            Logger.LogError("Timeout waiting for device enrichment", false);
            return lightweightDevices;
        }
        catch (Exception ex)
        {
            Logger.LogError($"Device enrichment failed: {ex.Message}", false);
            return lightweightDevices;
        }
    }

    public bool ValidateSafetyPassword(string deviceId, string password, bool isAutoCheck = false)
    {
        try
        {
            return ExecuteOnSta(
                () => SecurityManagementService.ValidateSafetyPassword(deviceId, password, isAutoCheck),
                timeout: TimeSpan.FromSeconds(10),
                bufferLogs: false,
                timeoutMessage: "Timeout waiting for safety password validation");
        }
        catch (TimeoutException)
        {
            Logger.LogError("Timeout waiting for safety password validation", false);
            return false;
        }
        catch (Exception ex)
        {
            Logger.LogError($"Safety password validation failed: {ex.Message}", false);
            return false;
        }
    }

    public (string plcDeviceId, SafetyPasswordData safetyData)? ExtractPlcSafetyDataFromIoSystem(string ioSystemId)
    {
        if (string.IsNullOrWhiteSpace(ioSystemId))
            return null;

        try
        {
            return ExecuteOnSta(
                () => ExtractPlcSafetyDataInternal(ioSystemId),
                timeout: TimeSpan.FromSeconds(10),
                bufferLogs: false,
                timeoutMessage: "Timeout waiting for PLC safety data extraction from IoSystem");
        }
        catch (TimeoutException)
        {
            Logger.LogError("Timeout waiting for PLC safety data extraction from IoSystem", false);
            return null;
        }
        catch (Exception ex)
        {
            Logger.LogError($"PLC safety data extraction failed: {ex.Message}", false);
            return null;
        }
    }

    public List<IoSystemInfo> ExtractIoSystemsFromProject(string projectId)
    {
        if (string.IsNullOrWhiteSpace(projectId))
            return new List<IoSystemInfo>();

        try
        {
            return ExecuteOnSta(
                () => ExtractIoSystemsInternal(projectId),
                timeout: TimeSpan.FromSeconds(10),
                bufferLogs: false,
                timeoutMessage: "Timeout waiting for IoSystem extraction") ?? new List<IoSystemInfo>();
        }
        catch (TimeoutException)
        {
            Logger.LogError("Timeout waiting for IoSystem extraction", false);
            return new List<IoSystemInfo>();
        }
        catch (Exception ex)
        {
            Logger.LogError($"IoSystem extraction failed: {ex.Message}", false);
            return new List<IoSystemInfo>();
        }
    }

    public (bool success, HardwareDiscoveryData discoveryData) LoadArchiveForSelection(string archivePath)
    {
        if (string.IsNullOrWhiteSpace(archivePath))
            return (false, null);

        try
        {
            return ExecuteOnSta(
                () => LoadArchiveInternal(archivePath),
                timeout: TimeSpan.FromSeconds(120),
                bufferLogs: false,
                timeoutMessage: "[CALLER THREAD] Archive load timeout");
        }
        catch (TimeoutException)
        {
            Logger.LogError("[CALLER THREAD] Archive load timeout", false);
            return (false, null);
        }
        catch (Exception ex)
        {
            Logger.LogError($"[CALLER THREAD] Archive load failed: {ex.Message}", false);
            return (false, null);
        }
    }

    private void ExecuteOnStaThread()
    {
        var staThreadId = Thread.CurrentThread.ManagedThreadId;
        Logger.LogInfo($"[HW STA THREAD {staThreadId}] Hardware workflow started at {DateTime.Now:HH:mm:ss.fff}");

        try
        {
            Directory.CreateDirectory(_exportDir);
            ReportProgress(new WorkflowProgress("Connecting to TIA Portal instances...", 10));

            _resourceManager = new WorkflowResourceManager(_exportDir);
            _resourceManager.AllProjects = GetAllProjectsFromAllInstances();
            if (_resourceManager.AllProjects.Count == 0)
            {
                ReportProgress(WorkflowProgress.Error("Failed to connect to any TIA Portal instances or no projects found"));
                _stopRequested = true;
                _taskQueue.Complete();
                return;
            }

            ReportProgress(new WorkflowProgress($"Found {_resourceManager.AllProjects.Count} project(s) across TIA Portal instances", 20));

            ReportProgress(new WorkflowProgress("Extracting hardware devices from all instances...", 30));
            var discoveryData = ExtractHardwareDiscoveryDataFromAllInstances(
                _resourceManager.AllProjects,
                status => ReportProgress(new WorkflowProgress(status, 35)));

            Task.Run(() => _discoveryCallback?.Invoke(discoveryData));

            ProcessMessageLoop();

            if (_success)
            {
                ReportProgress(WorkflowProgress.Success("Workflow completed successfully!"));
                _success = false;
            }
        }
        catch (OperationCanceledException)
        {
            Logger.LogWarning("Hardware workflow was cancelled by user request");
            ReportProgress(WorkflowProgress.Cancelled("Hardware workflow cancelled by user"));
        }
        catch (Exception ex)
        {
            ReportProgress(WorkflowProgress.Error("Hardware workflow execution failed", ex));
        }
        finally
        {
            CleanupResources();
            _isRunning = false;
        }

        Logger.LogInfo($"[HW STA THREAD {staThreadId}] Hardware workflow completed at {DateTime.Now:HH:mm:ss.fff}");
    }

    private void ProcessMessageLoop()
    {
        while (true)
        {
            if (!_taskQueue.TryTake(out var workItem, _cancellationTokenSource.Token))
            {
                if (_stopRequested || _taskQueue.IsAddingCompleted || _cancellationTokenSource.IsCancellationRequested)
                    break;

                continue;
            }

            if (workItem == null)
                continue;

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
                Logger.LogError($"[HW STA LOOP] Unhandled exception: {ex.Message}", false);
                ReportProgress(WorkflowProgress.Error("Hardware workflow execution failed", ex));
            }

            if (_stopRequested && _taskQueue.IsAddingCompleted)
                break;
        }
    }

    private bool TryEnqueue(Action action)
    {
        if (_stopRequested || _taskQueueDisposed)
            return false;

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
            tcs.TrySetException(new InvalidOperationException("Hardware workflow is shutting down"));
        }

        if (bufferLogs)
            Logger.EnableQueuedLogging();

        try
        {
            if (timeout.HasValue)
            {
                if (!tcs.Task.Wait(timeout.Value))
                    throw new TimeoutException(timeoutMessage ?? "Hardware STA request timed out.");
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

    private void ProcessUserSelections(HardwareUserSelections selections)
    {
        Logger.LogInfo("[HW STA LOOP] Processing hardware user selections");

        try
        {
            ReportProgress(new WorkflowProgress("Processing user selections...", 40));
            var configuration = BuildHardwareWorkflowConfiguration(selections);

            ExecuteMasterCopyPhase(configuration);
            _success = true;
        }
        catch (OperationCanceledException)
        {
            ReportProgress(WorkflowProgress.Cancelled("Hardware workflow cancelled by user"));
        }
        catch (Exception ex)
        {
            ReportProgress(WorkflowProgress.Error("Hardware workflow execution failed", ex));
        }
        finally
        {
            _stopRequested = true;
            _taskQueue.Complete();
        }
    }

    private List<HardwareDeviceInfo> ExtractDevicesInternal(
        TiaPortalInstanceInfo instanceInfo,
        Action<int, int> progressCallback,
        bool lightweight)
    {
        Logger.LogInfo($"[HW STA LOOP] Processing device extraction request for instance '{instanceInfo.ProjectName}' (lightweight={lightweight})");

        try
        {
            var devices = lightweight
                ? ExtractHardwareDevicesLight(instanceInfo.ProjectId, status => ReportProgress(new WorkflowProgress(status, 35)), progressCallback)
                : ExtractHardwareDevicesFromSelectedInstance(instanceInfo.ProjectId, status => ReportProgress(new WorkflowProgress(status, 35)), progressCallback);

            devices ??= new List<HardwareDeviceInfo>();
            Logger.LogInfo($"[HW STA LOOP] Device extraction completed - found {devices.Count} devices");
            return devices;
        }
        catch (Exception ex)
        {
            Logger.LogError($"[HW STA LOOP] Device extraction failed: {ex.Message}", false);
            ReportProgress(WorkflowProgress.Error("Device extraction failed", ex));
            return new List<HardwareDeviceInfo>();
        }
    }

    private List<HardwareDeviceInfo> EnrichDevicesInternal(
        List<HardwareDeviceInfo> devices,
        Action<int, int> progressCallback)
    {
        Logger.LogInfo($"[HW STA LOOP] Processing device enrichment request for {devices.Count} devices");

        try
        {
            var enrichedDevices = EnrichSelectedDevicesWithDetails(devices, progressCallback) ?? new List<HardwareDeviceInfo>();
            Logger.LogInfo($"[HW STA LOOP] Device enrichment completed - enriched {enrichedDevices.Count} devices");
            return enrichedDevices;
        }
        catch (Exception ex)
        {
            Logger.LogError($"[HW STA LOOP] Device enrichment failed: {ex.Message}", false);
            return devices;
        }
    }

    private (string plcDeviceId, SafetyPasswordData safetyData)? ExtractPlcSafetyDataInternal(string ioSystemId)
    {
        Logger.LogInfo("[HW STA LOOP] Processing PLC safety extraction from IoSystem request");

        try
        {
            var ioSystem = DataCacheUtility.GetCachedObject<IoSystem>(ioSystemId);
            if (ioSystem == null)
            {
                Logger.LogWarning("[HW STA LOOP] IoSystem not found in cache");
                return null;
            }

            if (ioSystem.Parent?.Parent?.Parent?.Parent?.Parent is Device plcDevice)
            {
                var plcDeviceId = DataCacheUtility.CacheObject(plcDevice);
                var safetyData = SecurityManagementService.ExtractSafetyPasswordData(plcDeviceId);

                Logger.LogInfo($"[HW STA LOOP] PLC safety extraction completed for PLC: {plcDevice.Name}");
                return (plcDeviceId, safetyData);
            }

            Logger.LogWarning("[HW STA LOOP] Could not resolve PLC device from IoSystem hierarchy");
            return null;
        }
        catch (Exception ex)
        {
            Logger.LogError($"[HW STA LOOP] PLC safety extraction failed: {ex.Message}", false);
            return null;
        }
    }

    private List<IoSystemInfo> ExtractIoSystemsInternal(string projectId)
    {
        Logger.LogInfo($"[HW STA LOOP] Extracting IoSystems from project: {projectId}");

        try
        {
            var project = DataCacheUtility.GetCachedObject<Project>(projectId);
            if (project == null)
            {
                Logger.LogError("[HW STA LOOP] Project not found in cache", false);
                return new List<IoSystemInfo>();
            }

            var ioSystems = STAHardwareDataExtractor.ExtractIoSystemsFromProject(project) ?? new List<IoSystemInfo>();
            Logger.LogInfo($"[HW STA LOOP] IoSystem extraction completed: {ioSystems.Count} systems found");
            return ioSystems;
        }
        catch (Exception ex)
        {
            Logger.LogError($"[HW STA LOOP] IoSystem extraction error: {ex.Message}", false);
            return new List<IoSystemInfo>();
        }
    }

    private (bool success, HardwareDiscoveryData discoveryData) LoadArchiveInternal(string archivePath)
    {
        Logger.LogInfo($"[HW STA THREAD] Loading archive: {archivePath}");

        try
        {
            var archivePortal = _resourceManager.ArchiveTiaPortal;
            var (success, retrievedProject, discoveryData) = LoadAndExtractArchive(
                archivePortal,
                archivePath,
                _resourceManager.RetrievedArchivesDir);

            if (success && retrievedProject != null)
            {
                _resourceManager.AddRetrievedProject(retrievedProject, archivePath);
                Logger.LogInfo($"[HW STA THREAD] Archive loaded: {discoveryData.TiaInstances.Count} instance(s) found");
                return (true, discoveryData);
            }

            Logger.LogError("[HW STA THREAD] Failed to load archive");
            return (false, null);
        }
        catch (Exception ex)
        {
            Logger.LogError($"[HW STA THREAD] Archive load error: {ex.Message}");
            return (false, null);
        }
    }

    private void ExecuteMasterCopyPhase(HardwareWorkflowConfiguration config)
    {
        try
        {
            Logger.LogInfo($"Starting master copy workflow for {config.SelectedDevices.Count} devices");
            Logger.LogInfo($"Source project: {config.SourceProjectId}, Target project: {config.TargetProjectId}");

            var targetProject = DataCacheUtility.GetCachedObject<Project>(config.TargetProjectId)
                                ?? throw new InvalidOperationException("Could not find target project for master copy workflow");

            _cancellationTokenSource.Token.ThrowIfCancellationRequested();

            ReportProgress(new WorkflowProgress("Creating global library from source project...", 60));
            var hardwareExportDir = Path.Combine(_exportDir, "Hardware_Export");

            var sourcePortal = DataCacheUtility.GetCachedObject<TiaPortal>(config.SourceInstanceId)
                               ?? throw new InvalidOperationException("Could not find TIA Portal instance for source project");

            (_globalLibrary, _globalLibraryFile) = CreateGlobalLibrary(sourcePortal, hardwareExportDir);

            _cancellationTokenSource.Token.ThrowIfCancellationRequested();

            ReportProgress(new WorkflowProgress("Creating master copies...", 70));
            var masterCopies = new List<Siemens.Engineering.Library.MasterCopies.MasterCopy>();
            var totalDevices = config.SelectedDevices.Count;

            for (int i = 0; i < config.SelectedDevices.Count; i++)
            {
                var device = config.SelectedDevices[i];
                var currentProgress = 70 + (int)(10 * (double)i / totalDevices);

                ReportProgress(new WorkflowProgress($"Creating master copy for '{device.Name}' ({i + 1}/{totalDevices})...", currentProgress));

                var masterCopy = CreateMasterCopy(_globalLibrary, device);
                masterCopies.Add(masterCopy);

                _cancellationTokenSource.Token.ThrowIfCancellationRequested();
            }

            Logger.LogInfo($"Master copy creation completed. {masterCopies.Count} master copies created");

            _cancellationTokenSource.Token.ThrowIfCancellationRequested();

            bool isDifferentInstance = config.SourceInstanceId != config.TargetInstanceId;

            if (isDifferentInstance)
            {
                ReportProgress(new WorkflowProgress("Opening global library from target TIA Portal instance...", 75));
                Logger.LogInfo("Source and target are in different TIA Portal instances - opening library from target instance");

                Logger.LogInfo("Saving library in source instance...");
                _globalLibrary.Save();
                Logger.LogInfo("Source library saved successfully");

                Logger.LogInfo("Closing library in source instance...");
                _globalLibrary.Close();
                _globalLibrary = null;
                Logger.LogInfo("Source library closed successfully");

                var targetPortal = DataCacheUtility.GetCachedObject<TiaPortal>(config.TargetInstanceId)
                                    ?? throw new InvalidOperationException("Could not find TIA Portal instance for target project");

                Logger.LogInfo($"Opening library from target instance: {_globalLibraryFile.FullName}");

                var absoluteLibraryPath = new FileInfo(_globalLibraryFile.FullName);
                if (!absoluteLibraryPath.Exists)
                    throw new InvalidOperationException($"Library file does not exist: {absoluteLibraryPath.FullName}");

                _targetGlobalLibrary = targetPortal.GlobalLibraries.Open(absoluteLibraryPath, OpenMode.ReadOnly);
                Logger.LogInfo("Library opened successfully from target TIA Portal instance");

                masterCopies.Clear();
                foreach (var deviceInfo in config.SelectedDevices)
                {
                    var transformationSource = !string.IsNullOrWhiteSpace(deviceInfo.ItemName) ? deviceInfo.ItemName : deviceInfo.Name;
                    var masterCopyName = transformationSource;

                    var foundMasterCopy = _targetGlobalLibrary.MasterCopyFolder.MasterCopies.FirstOrDefault(mc => mc.Name == masterCopyName)
                        ?? throw new InvalidOperationException($"Master copy '{masterCopyName}' not found in target library");

                    masterCopies.Add(foundMasterCopy);
                }

                Logger.LogInfo($"Retrieved {masterCopies.Count} master copies from target library");
            }

            _cancellationTokenSource.Token.ThrowIfCancellationRequested();

            ReportProgress(new WorkflowProgress("Creating devices from master copies...", 80));

            IoSystem targetIoSystem = null;
            Subnet targetSubnet = null;
            bool hasNetworkConfig = !string.IsNullOrWhiteSpace(config.SelectedIoSystem.IoSystemId);

            if (hasNetworkConfig)
            {
                var ioSystemInfo = config.SelectedIoSystem;
                targetIoSystem = DataCacheUtility.GetCachedObject<IoSystem>(ioSystemInfo.IoSystemId);
                targetSubnet = DataCacheUtility.GetCachedObject<Subnet>(ioSystemInfo.SubnetId);

                if (targetIoSystem == null || targetSubnet == null)
                {
                    Logger.LogWarning("Could not retrieve IoSystem or Subnet from cache, skipping network configuration");
                    hasNetworkConfig = false;
                }
            }

            bool hasETAddressConfig = config.ETAddressReplacements is { Count: > 0 };
            var devicePairs = masterCopies
                .Select((masterCopy, index) => (MasterCopy: masterCopy, Device: config.SelectedDevices[index], Index: index))
                .OrderByDescending(pair => pair.Device.IsETDevice)
                .ToList();

            var createdDevices = new List<Device>();

            for (int i = 0; i < devicePairs.Count; i++)
            {
                var (masterCopy, deviceInfo, _) = devicePairs[i];
                var currentProgress = 80 + (int)(15 * (double)i / devicePairs.Count);

                try
                {
                    ReportProgress(new WorkflowProgress($"Creating device '{deviceInfo.Name}' ({i + 1}/{devicePairs.Count})...", currentProgress));
                    var newDevice = CreateDeviceFromMasterCopy(targetProject, masterCopy);

                    var transformationSource = !string.IsNullOrWhiteSpace(deviceInfo.ItemName) ? deviceInfo.ItemName : deviceInfo.Name;
                    var transformedDeviceName = ApplyFindReplaceToDeviceName(transformationSource, config.DeviceNameFindReplacePairs);
                    var transformedItemName = ApplyFindReplaceToDeviceName(deviceInfo.ItemName, config.DeviceNameFindReplacePairs);

                    if (transformedDeviceName != newDevice.Name)
                    {
                        Logger.LogInfo($"Renaming device: '{newDevice.Name}' -> '{transformedDeviceName}'");
                        newDevice.Name = transformedDeviceName;
                    }

                    if (newDevice.DeviceItems.Count > 1 && !string.IsNullOrWhiteSpace(transformedItemName))
                    {
                        var deviceItem = newDevice.DeviceItems[1];
                        if (transformedItemName != deviceItem.Name)
                        {
                            Logger.LogInfo($"Renaming device item: '{deviceItem.Name}' -> '{transformedItemName}'");
                            deviceItem.Name = transformedItemName;
                        }
                    }

                    if (hasNetworkConfig)
                    {
                        Logger.LogInfo($"Configuring network for '{newDevice.Name}'...");
                        ConfigureDeviceNetworkAndNumber(
                            newDevice,
                            config.IpAddressOffset,
                            targetIoSystem,
                            targetSubnet,
                            config.SelectedIoSystem.NetworkAddress,
                            config.SelectedIoSystem.SubnetMask);
                    }

                    if (hasETAddressConfig && deviceInfo.IsETDevice)
                    {
                        Logger.LogInfo($"Applying ET address transformations to '{newDevice.Name}'...");
                        ApplyETAddressTransformations(newDevice, deviceInfo, config.ETAddressReplacements);
                    }

                    createdDevices.Add(newDevice);
                    Logger.LogSuccess($"Device '{newDevice.Name}' created and configured successfully ({i + 1}/{devicePairs.Count})");
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Failed to process device '{deviceInfo.Name}': {ex.Message}", false);
                }

                _cancellationTokenSource.Token.ThrowIfCancellationRequested();
            }

            Logger.LogInfo($"Device creation and configuration completed. {createdDevices.Count}/{devicePairs.Count} devices created successfully");

            ReportProgress(new WorkflowProgress($"Master copy workflow completed successfully! {createdDevices.Count} devices created.", 95));
        }
        catch (OperationCanceledException)
        {
            ReportProgress(WorkflowProgress.Cancelled("Master copy workflow cancelled by user"));
            throw;
        }
        catch (Exception ex)
        {
            ReportProgress(WorkflowProgress.Error("Master copy workflow failed", ex));
            throw;
        }
    }

    private static string ApplyFindReplaceToDeviceName(string deviceName, List<FindReplacePair> findReplacePairs)
    {
        if (string.IsNullOrWhiteSpace(deviceName) || findReplacePairs == null || findReplacePairs.Count == 0)
            return deviceName;

        var result = deviceName;

        foreach (var pair in findReplacePairs)
        {
            if (!string.IsNullOrWhiteSpace(pair.FindString))
            {
                result = result.Replace(pair.FindString, pair.ReplaceString ?? "");
            }
        }

        return result;
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

    private void CleanupResources()
    {
        try
        {
            ReportProgress(new WorkflowProgress("Cleaning up temporary resources...", 98));

            if (_targetGlobalLibrary != null)
            {
                try
                {
                    Logger.LogInfo("Closing target global library...");
                    _targetGlobalLibrary.Close();
                    _targetGlobalLibrary = null;
                    Logger.LogInfo("Target global library closed successfully");
                }
                catch (Exception ex)
                {
                    Logger.LogWarning($"Error closing target global library: {ex.Message}");
                }
            }

            if (_globalLibrary != null)
            {
                try
                {
                    Logger.LogInfo("Closing source global library...");
                    _globalLibrary.Close();
                    _globalLibrary = null;
                    Logger.LogInfo("Source global library closed successfully");
                }
                catch (Exception ex)
                {
                    Logger.LogWarning($"Error closing source global library: {ex.Message}");
                }
            }

            _resourceManager?.Dispose();
            DataCacheUtility.ClearCache();
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"Error during hardware workflow resource cleanup: {ex.Message}");
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

            ReportProgress(new WorkflowProgress("Cleanup complete", 100, type: WorkflowProgressType.CleanupComplete));
        }
    }
}
