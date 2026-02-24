using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using OpennessCopy.Forms.HardwareCopy;
using OpennessCopy.Models;
using OpennessCopy.Services;
using OpennessCopy.Services.HardwareCopy;
using OpennessCopy.Utils;
using Siemens.Engineering.HW;

namespace OpennessCopy.Forms;

public partial class MainForm
{
    private async void OnHardwareDiscoveryCompleted(HardwareDiscoveryData discoveryData)
    {
        try
        {
            if (InvokeRequired)
            {
                Invoke(new Action<HardwareDiscoveryData>(OnHardwareDiscoveryCompleted), discoveryData);
                return;
            }

            Logger.LogInfo("Hardware discovery completed, starting hardware device selection dialog sequence");
            this.Cursor = Cursors.Default;

            var workflow = _currentWorkflow as HardwareWorkflowSTAThread;
            var success = await RunHardwareWorkflowAsync(discoveryData, workflow);

            if (!success)
            {
                workflow?.RequestCleanup();
                ResetUIState();
            }
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error during hardware dialog workflow: {ex.Message}", showMessageBox: true);
            _currentWorkflow?.RequestCleanup();
            ResetUIState();
        }
    }

    private async Task<bool> RunHardwareWorkflowAsync(HardwareDiscoveryData discoveryData, HardwareWorkflowSTAThread workflow)
    {
        if (workflow == null)
        {
            Logger.LogError("Current workflow is not configured for hardware copy.", showMessageBox: true);
            return false;
        }

        if (!TrySelectHardwareProjects(discoveryData, workflow, out var sourceProject, out var targetProject))
        {
            return false;
        }

        var isSameProject = sourceProject.ProjectId == targetProject.ProjectId;
        var loadResult = await LoadHardwareDevicesAsync(workflow, sourceProject, targetProject, isSameProject);
        if (!loadResult.Success)
        {
            return false;
        }

        sourceProject.HardwareDevices = loadResult.SourceDevices;

        if (!TrySelectHardwareDevices(sourceProject, out var selectedDevices))
        {
            return false;
        }

        if (!isSameProject)
        {
            var enrichedDevices = await EnrichSelectedDevicesAsync(workflow, selectedDevices);
            if (enrichedDevices == null)
            {
                return false;
            }

            selectedDevices = enrichedDevices;
        }

        var conflictSnapshot = BuildHardwareConflictSnapshot(loadResult.TargetDevices);
        OnProgressReported(new WorkflowProgress("Extracting IO Systems from target project...", 25));
        var ioSystems = await ExtractIoSystemsAsync(workflow, targetProject.ProjectId);
        Logger.LogInfo($"Extracted {ioSystems.Count} IoSystem(s) from project");

        var processingConfig = ShowHardwareProcessingConfiguration(selectedDevices, conflictSnapshot, ioSystems);
        if (processingConfig == null)
        {
            return false;
        }

        var safetyCheckPassed = await ValidateSafetyPasswordIfNeededAsync(
            workflow,
            targetProject,
            processingConfig.SelectedIoSystem);

        if (!safetyCheckPassed)
        {
            return false;
        }

        var userSelections = new HardwareUserSelections
        {
            SourceProjectId = sourceProject.ProjectId,
            SourceInstanceId = sourceProject.InstanceId,
            TargetProjectId = targetProject.ProjectId,
            TargetInstanceId = targetProject.InstanceId,
            SelectedDevices = selectedDevices,
            DeviceNameFindReplacePairs = processingConfig.DeviceNameFindReplacePairs,
            IpAddressOffset = processingConfig.IpAddressOffset,
            ETAddressReplacements = processingConfig.EtAddressReplacements,
            SelectedIoSystem = processingConfig.SelectedIoSystem
        };

        this.Cursor = Cursors.WaitCursor;
        workflow.SetUserSelections(userSelections);
        return true;
    }

    private bool TrySelectHardwareProjects(
        HardwareDiscoveryData discoveryData,
        HardwareWorkflowSTAThread workflow,
        out TiaPortalInstanceInfo sourceProject,
        out TiaPortalInstanceInfo targetProject)
    {
        using var projectSelectionForm = new HardwareProjectSelectionForm(discoveryData, workflow);
        if (projectSelectionForm.ShowDialog(this) != DialogResult.OK)
        {
            Logger.LogInfo("Project selection cancelled by user");
            MessageBox.Show("Hardware workflow cancelled.", "Cancelled",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            sourceProject = null;
            targetProject = null;
            return false;
        }

        sourceProject = projectSelectionForm.SourceProject;
        targetProject = projectSelectionForm.TargetProject;
        Logger.LogInfo($"Projects selected - Source: '{sourceProject.ProjectName}', Target: '{targetProject.ProjectName}'");
        return true;
    }

    private async Task<(bool Success, List<HardwareDeviceInfo> SourceDevices, List<HardwareDeviceInfo> TargetDevices)> LoadHardwareDevicesAsync(
        HardwareWorkflowSTAThread workflow,
        TiaPortalInstanceInfo sourceProject,
        TiaPortalInstanceInfo targetProject,
        bool isSameProject)
    {
        if (isSameProject)
        {
            try
            {
                Logger.LogInfo($"Loading device data from project (source=target): {sourceProject.ProjectName}");
                var devices = await LoadDevicesWithDialogAsync(
                    workflow,
                    sourceProject,
                    $"Loading Devices from '{sourceProject.ProjectName}'",
                    lightweight: false);

                if (devices == null)
                {
                    Logger.LogError("Failed to load device data from project.");
                    return (false, new List<HardwareDeviceInfo>(), new List<HardwareDeviceInfo>());
                }

                return (true, devices, devices);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error during device data extraction: {ex.Message}", false);
                MessageBox.Show($"Error extracting device data: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return (false, new List<HardwareDeviceInfo>(), new List<HardwareDeviceInfo>());
            }
        }

        List<HardwareDeviceInfo> targetDevices;
        try
        {
            Logger.LogInfo($"Loading target devices (full) from: {targetProject.ProjectName}");
            targetDevices = await LoadDevicesWithDialogAsync(
                workflow,
                targetProject,
                $"Loading Target Devices from '{targetProject.ProjectName}'",
                lightweight: false);

            if (targetDevices == null)
            {
                Logger.LogWarning("Failed to load target project devices - conflict detection may be incomplete");
                targetDevices = new List<HardwareDeviceInfo>();
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"Error extracting target devices: {ex.Message} - conflict detection may be incomplete");
            targetDevices = new List<HardwareDeviceInfo>();
        }

        try
        {
            Logger.LogInfo($"Loading source devices (lightweight) from: {sourceProject.ProjectName}");
            var sourceDevices = await LoadDevicesWithDialogAsync(
                workflow,
                sourceProject,
                $"Loading Source Devices from '{sourceProject.ProjectName}'",
                lightweight: true);

            if (sourceDevices == null)
            {
                Logger.LogError("Failed to load source project devices.");
                return (false, new List<HardwareDeviceInfo>(), new List<HardwareDeviceInfo>());
            }

            return (true, sourceDevices, targetDevices);
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error during source device extraction: {ex.Message}", false);
            MessageBox.Show($"Error extracting source device data: {ex.Message}", "Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            return (false, new List<HardwareDeviceInfo>(), new List<HardwareDeviceInfo>());
        }
    }

    private async Task<List<HardwareDeviceInfo>> LoadDevicesWithDialogAsync(
        HardwareWorkflowSTAThread workflow,
        TiaPortalInstanceInfo projectInfo,
        string dialogTitle,
        bool lightweight)
    {
        DeviceLoadingForm loadingForm = null;
        try
        {
            loadingForm = new DeviceLoadingForm(dialogTitle);
            ShowLoadingForm(loadingForm);

            return await Task.Run(() =>
                workflow.ExtractDevicesFromInstance(
                    projectInfo,
                    (current, total) => loadingForm?.UpdateProgress(current, total),
                    lightweight));
        }
        finally
        {
            if (loadingForm != null)
            {
                CloseLoadingForm(loadingForm);
            }
        }
    }

    private bool TrySelectHardwareDevices(TiaPortalInstanceInfo sourceProject, out List<HardwareDeviceInfo> selectedDevices)
    {
        using var deviceSelectionForm = new HardwareDeviceSelectionForm(sourceProject);
        if (deviceSelectionForm.ShowDialog(this) != DialogResult.OK)
        {
            Logger.LogInfo("Hardware device selection cancelled by user");
            MessageBox.Show("Hardware workflow cancelled.", "Cancelled",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            selectedDevices = new List<HardwareDeviceInfo>();
            return false;
        }

        selectedDevices = deviceSelectionForm.SelectedDevices;
        Logger.LogInfo($"Selected {selectedDevices.Count} hardware devices for export");
        return true;
    }

    private async Task<List<HardwareDeviceInfo>> EnrichSelectedDevicesAsync(
        HardwareWorkflowSTAThread workflow,
        List<HardwareDeviceInfo> selectedDevices)
    {
        DeviceLoadingForm enrichmentLoadingForm = null;
        try
        {
            Logger.LogInfo($"Enriching {selectedDevices.Count} selected devices with full details...");
            enrichmentLoadingForm = new DeviceLoadingForm($"Loading Details for {selectedDevices.Count} Selected Devices");
            ShowLoadingForm(enrichmentLoadingForm);

            var enrichedDevices = await Task.Run(() =>
                workflow.EnrichSelectedDevices(
                    selectedDevices,
                    (current, total) => enrichmentLoadingForm?.UpdateProgress(current, total)));

            if (enrichedDevices == null || enrichedDevices.Count == 0)
            {
                Logger.LogError("Failed to enrich selected devices with details.");
                return null;
            }

            Logger.LogInfo($"Successfully enriched {enrichedDevices.Count} devices with full details");
            return enrichedDevices;
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error during device enrichment: {ex.Message}", false);
            MessageBox.Show($"Error enriching device details: {ex.Message}", "Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            return null;
        }
        finally
        {
            if (enrichmentLoadingForm != null)
            {
                CloseLoadingForm(enrichmentLoadingForm);
            }
        }
    }

    private HardwareConflictSnapshot BuildHardwareConflictSnapshot(List<HardwareDeviceInfo> targetDevices)
    {
        var existingDeviceNames = new HashSet<string>();
        var existingDeviceNumbers = new Dictionary<int, Dictionary<int, string>>();
        var existingIpAddresses = new Dictionary<string, string>();
        var existingAddressesByPlc = new Dictionary<string, Dictionary<AddressIoType, Dictionary<int, string>>>();

        foreach (var device in targetDevices)
        {
            existingDeviceNames.Add(device.Name);
            if (!string.IsNullOrWhiteSpace(device.ItemName))
            {
                existingDeviceNames.Add(device.ItemName);
            }

            if (device.IoSystemHash.HasValue && device.DeviceNumber.HasValue)
            {
                if (!existingDeviceNumbers.ContainsKey(device.IoSystemHash.Value))
                {
                    existingDeviceNumbers.Add(device.IoSystemHash.Value, new Dictionary<int, string>());
                }

                if (!existingDeviceNumbers[device.IoSystemHash.Value].ContainsKey(device.DeviceNumber.Value))
                {
                    existingDeviceNumbers[device.IoSystemHash.Value].Add(device.DeviceNumber.Value, device.ItemName);
                }
            }

            foreach (var ipAddress in device.IpAddresses)
            {
                if (!existingIpAddresses.ContainsKey(ipAddress))
                {
                    existingIpAddresses.Add(ipAddress, device.ItemName);
                }
            }

            if (device.AddressModules.Count <= 0 || string.IsNullOrWhiteSpace(device.ControllingPlcName))
            {
                continue;
            }

            if (!existingAddressesByPlc.ContainsKey(device.ControllingPlcName))
            {
                existingAddressesByPlc[device.ControllingPlcName] = new Dictionary<AddressIoType, Dictionary<int, string>>();
            }

            foreach (var module in device.AddressModules)
            {
                foreach (var addressInfo in module.AddressInfos)
                {
                    if (!existingAddressesByPlc[device.ControllingPlcName].ContainsKey(addressInfo.Type))
                    {
                        existingAddressesByPlc[device.ControllingPlcName][addressInfo.Type] = new Dictionary<int, string>();
                    }

                    var addressCount = (addressInfo.Length + 7) / 8;
                    for (var offset = 0; offset < addressCount; offset++)
                    {
                        var address = addressInfo.StartAddress + offset;
                        var deviceDisplayName = !string.IsNullOrWhiteSpace(device.ItemName) ? device.ItemName : device.Name;

                        if (!existingAddressesByPlc[device.ControllingPlcName][addressInfo.Type].ContainsKey(address))
                        {
                            existingAddressesByPlc[device.ControllingPlcName][addressInfo.Type][address] = $"{deviceDisplayName}.{module.ModuleName}";
                        }
                    }
                }
            }
        }

        foreach (var plcEntry in existingAddressesByPlc)
        {
            Logger.LogInfo($"PLC: {plcEntry.Key}");

            foreach (var ioTypeEntry in plcEntry.Value)
            {
                Logger.LogInfo($"  IO Type: {ioTypeEntry.Key}");

                foreach (var addressEntry in ioTypeEntry.Value.Take(10))
                {
                    Logger.LogInfo($"    Address: {addressEntry.Key}, Name: {addressEntry.Value}");
                }
            }
        }

        Logger.LogInfo($"Collected {existingDeviceNames.Count} existing device names for conflict checking");

        return new HardwareConflictSnapshot(
            existingDeviceNames,
            existingDeviceNumbers,
            existingIpAddresses,
            existingAddressesByPlc);
    }

    private async Task<List<IoSystemInfo>> ExtractIoSystemsAsync(HardwareWorkflowSTAThread workflow, string targetProjectId)
    {
        return await Task.Run(() => workflow.ExtractIoSystemsFromProject(targetProjectId) ?? new List<IoSystemInfo>());
    }

    private HardwareProcessingConfiguration ShowHardwareProcessingConfiguration(
        List<HardwareDeviceInfo> selectedDevices,
        HardwareConflictSnapshot conflictSnapshot,
        List<IoSystemInfo> ioSystems)
    {
        using var configForm = new HardwareProcessingConfigForm(
            selectedDevices,
            conflictSnapshot.ExistingDeviceNames,
            conflictSnapshot.ExistingIpAddresses,
            conflictSnapshot.ExistingDeviceNumbers,
            conflictSnapshot.ExistingAddressesByPlc,
            ioSystems);

        if (configForm.ShowDialog(this) != DialogResult.OK)
        {
            Logger.LogInfo("Hardware processing configuration cancelled by user");
            MessageBox.Show("Hardware workflow cancelled.", "Cancelled",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return null;
        }

        Logger.LogInfo($"Hardware processing configuration completed with {configForm.DeviceNameFindReplacePairs.Count} find/replace pairs, {configForm.ETAddressReplacements.Count} ET address transformations, and IO system selected");

        return new HardwareProcessingConfiguration(
            configForm.DeviceNameFindReplacePairs,
            configForm.IpAddressOffset,
            configForm.ETAddressReplacements,
            configForm.SelectedIoSystem);
    }

    private async Task<bool> ValidateSafetyPasswordIfNeededAsync(
        HardwareWorkflowSTAThread workflow,
        TiaPortalInstanceInfo targetProject,
        IoSystemInfo selectedIoSystem)
    {
        if (selectedIoSystem == null || string.IsNullOrWhiteSpace(selectedIoSystem.IoSystemId))
        {
            return true;
        }

        OnProgressReported(new WorkflowProgress("Checking safety password requirements...", 18));

        var plcDeviceIdAndSafetyData = await Task.Run(() =>
            workflow.ExtractPlcSafetyDataFromIoSystem(selectedIoSystem.IoSystemId));

        if (!plcDeviceIdAndSafetyData.HasValue)
        {
            return true;
        }

        var (plcDeviceId, safetyData) = plcDeviceIdAndSafetyData.Value;
        if (safetyData?.SafetyAdmin?.IsSafetyOfflineProgramPasswordSet != true)
        {
            return true;
        }

        Logger.LogInfo($"PLC '{safetyData.DeviceName}' requires safety password");
        var passwordCandidates = SecurityManagementService.GeneratePasswordCandidates(targetProject.ProjectName);

        foreach (var candidatePassword in passwordCandidates)
        {
            try
            {
                Logger.LogInfo("Trying auto-password candidate for PLC...");
                var passwordValidationResult = await Task.Run(() =>
                    workflow.ValidateSafetyPassword(plcDeviceId, candidatePassword, true));

                if (passwordValidationResult)
                {
                    Logger.LogSuccess("Auto-password authentication successful for PLC");
                    return true;
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Auto-password attempt failed: {ex.Message}");
            }
        }

        while (true)
        {
            using var passwordForm = new SimplePasswordForm($"PLC Safety Password - {safetyData.DeviceName}");
            if (passwordForm.ShowDialog(this) != DialogResult.OK || string.IsNullOrEmpty(passwordForm.Password))
            {
                Logger.LogError("Safety password input cancelled.");
                return false;
            }

            try
            {
                var passwordValidationResult = await Task.Run(() =>
                    workflow.ValidateSafetyPassword(plcDeviceId, passwordForm.Password));

                if (passwordValidationResult)
                {
                    Logger.LogInfo("Safety password validated successfully");
                    return true;
                }

                MessageBox.Show("Invalid safety password. Please try again.", "Authentication Failed",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error validating safety password: {ex.Message}", false);
                MessageBox.Show($"Error validating password: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    private void ShowLoadingForm(DeviceLoadingForm loadingForm)
    {
        void ShowForm()
        {
            if (!loadingForm.IsDisposed)
            {
                loadingForm.Show(this);
                loadingForm.CenterToParent(this);
            }
        }

        if (InvokeRequired)
        {
            Invoke(new Action(ShowForm));
        }
        else
        {
            ShowForm();
        }
    }

    private void CloseLoadingForm(DeviceLoadingForm loadingForm)
    {
        void CloseForm()
        {
            if (!loadingForm.IsDisposed)
            {
                loadingForm.Close();
                loadingForm.Dispose();
            }
        }

        try
        {
            if (InvokeRequired)
            {
                Invoke(new Action(CloseForm));
            }
            else
            {
                CloseForm();
            }
        }
        catch (ObjectDisposedException)
        {
            // Ignore disposal races
        }
    }

    private sealed class HardwareConflictSnapshot(
        HashSet<string> existingDeviceNames,
        Dictionary<int, Dictionary<int, string>> existingDeviceNumbers,
        Dictionary<string, string> existingIpAddresses,
        Dictionary<string, Dictionary<AddressIoType, Dictionary<int, string>>> existingAddressesByPlc)
    {
        public HashSet<string> ExistingDeviceNames { get; } = existingDeviceNames;
        public Dictionary<int, Dictionary<int, string>> ExistingDeviceNumbers { get; } = existingDeviceNumbers;
        public Dictionary<string, string> ExistingIpAddresses { get; } = existingIpAddresses;
        public Dictionary<string, Dictionary<AddressIoType, Dictionary<int, string>>> ExistingAddressesByPlc { get; } = existingAddressesByPlc;
    }

    private sealed class HardwareProcessingConfiguration(
        List<FindReplacePair> deviceNameFindReplacePairs,
        int ipAddressOffset,
        List<TagAddressReplacePair> etAddressReplacements,
        IoSystemInfo selectedIoSystem)
    {
        public List<FindReplacePair> DeviceNameFindReplacePairs { get; } = deviceNameFindReplacePairs ?? new List<FindReplacePair>();
        public int IpAddressOffset { get; } = ipAddressOffset;
        public List<TagAddressReplacePair> EtAddressReplacements { get; } = etAddressReplacements ?? new List<TagAddressReplacePair>();
        public IoSystemInfo SelectedIoSystem { get; } = selectedIoSystem;
    }
}
