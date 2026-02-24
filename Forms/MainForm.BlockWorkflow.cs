#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using OpennessCopy.Forms.BlockCopy;
using OpennessCopy.Models;
using OpennessCopy.Services;
using OpennessCopy.Services.BlockCopy;
using OpennessCopy.Utils;

namespace OpennessCopy.Forms;

public partial class MainForm
{
    private async void OnBlockDiscoveryCompleted(DiscoveryData discoveryData)
    {
        try
        {
            if (InvokeRequired)
            {
                Invoke(new Action<DiscoveryData>(OnBlockDiscoveryCompleted), discoveryData);
                return;
            }

            if (_currentWorkflow is not WorkflowStaThread workflow)
            {
                Logger.LogError("Current workflow is not configured for block copy.", showMessageBox: true);
                _currentWorkflow?.RequestCleanup();
                ResetUIState();
                return;
            }

            var success = await RunBlockWorkflowAsync(discoveryData, workflow);
            if (!success)
            {
                workflow.RequestCleanup();
                ResetUIState();
            }
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error during dialog workflow: {ex.Message}", showMessageBox: true);
            _currentWorkflow?.RequestCleanup();
            ResetUIState();
        }
    }

    private async Task<bool> RunBlockWorkflowAsync(DiscoveryData discoveryData, WorkflowStaThread? workflow)
    {
        if (workflow == null)
        {
            Logger.LogError("Current workflow is not configured for block copy.", showMessageBox: true);
            return false;
        }

        OnProgressReported(new WorkflowProgress("Source/Target PLC selection", 15));
        if (!TrySelectPlcs(discoveryData, workflow, out var sourcePlcInfo, out var targetPlcInfo))
        {
            return false;
        }

        var isSamePlc = sourcePlcInfo.PlcId == targetPlcInfo.PlcId && sourcePlcInfo.DeviceId == targetPlcInfo.DeviceId;
        var targetPlcForValidation = isSamePlc ? sourcePlcInfo : targetPlcInfo;

        if (!ValidateCultureCompatibility(sourcePlcInfo, targetPlcInfo))
        {
            return false;
        }

        if (!await EnsureDetailedPlcDataAsync(workflow, sourcePlcInfo, targetPlcForValidation, isSamePlc))
        {
            return false;
        }

        OnProgressReported(new WorkflowProgress("Password authentication", 18));
        var (passwordSuccess, sourceSafetyPassword) = await ResolveSourceSafetyPasswordAsync(workflow, sourcePlcInfo);
        if (!passwordSuccess)
        {
            return false;
        }

        OnProgressReported(new WorkflowProgress("Block group selection", 20));
        if (!TrySelectBlockGroup(sourcePlcInfo, out var selectedGroupId, out var selectedGroupPath))
        {
            return false;
        }

        var existingBlockNumbers = targetPlcForValidation.BlockGroupData?.ExistingBlockNumbers ?? new HashSet<int>();

        OnProgressReported(new WorkflowProgress("Block processing configuration", 22));
        var processingConfig = TryConfigureBlockProcessing(existingBlockNumbers, selectedGroupId, workflow);
        if (processingConfig == null)
        {
            return false;
        }

        var existingBlockNames = targetPlcForValidation.BlockGroupData?.ExistingBlockNames ?? new HashSet<string>();

        OnProgressReported(new WorkflowProgress("Tag table selection", 24));
        var selectedTables = TrySelectTagTables(sourcePlcInfo, processingConfig.FindReplacePairs);
        if (selectedTables == null)
        {
            return false;
        }

        var existingTagTableNames = targetPlcForValidation.TagTableData?.ExistingTableNames ?? new HashSet<string>();

        return ConfirmBlockWorkflowExecution(
            workflow,
            sourcePlcInfo,
            targetPlcInfo,
            selectedGroupId,
            selectedGroupPath,
            processingConfig,
            selectedTables,
            existingBlockNames,
            existingTagTableNames,
            sourceSafetyPassword);
    }

    private bool TrySelectPlcs(DiscoveryData discoveryData, WorkflowStaThread workflow, out PLCInfo sourcePlcInfo, out PLCInfo targetPlcInfo)
    {
        using var plcSelectionForm = new PlcSelectionForm(discoveryData.PLCs, workflow);
        if (plcSelectionForm.ShowDialog(this) != DialogResult.OK)
        {
            MessageBox.Show("PLC selection cancelled.", "Cancelled",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            sourcePlcInfo = null!;
            targetPlcInfo = null!;
            return false;
        }

        sourcePlcInfo = plcSelectionForm.SourcePlcInfo;
        targetPlcInfo = plcSelectionForm.TargetPlcInfo;
        return true;
    }

    private async Task<bool> EnsureDetailedPlcDataAsync(WorkflowStaThread workflow, PLCInfo sourcePlcInfo, PLCInfo targetPlcForValidation, bool isSamePlc)
    {
        if (!await LoadDetailedPlcDataAsync(
                workflow,
                sourcePlcInfo,
                $"Loading detailed data for source PLC: {sourcePlcInfo.Name}",
                16,
                $"Loading detailed data for source PLC: {sourcePlcInfo.Name}",
                "Failed to load detailed data for source PLC.",
                "source PLC"))
        {
            return false;
        }

        if (isSamePlc)
        {
            return true;
        }

        return await LoadDetailedPlcDataAsync(
            workflow,
            targetPlcForValidation,
            $"Loading target PLC data: {targetPlcForValidation.Name}",
            17,
            $"Loading target PLC data for validation: {targetPlcForValidation.Name}",
            "Failed to load detailed data for target PLC.",
            "target PLC");
    }

    private async Task<bool> LoadDetailedPlcDataAsync(
        WorkflowStaThread workflow,
        PLCInfo plcInfo,
        string progressStatus,
        int progressValue,
        string logMessage,
        string failureLogMessage,
        string failureDialogSubject)
    {
        OnProgressReported(new WorkflowProgress(progressStatus, progressValue));
        Logger.LogInfo(logMessage);

        try
        {
            var loadResult = await Task.Run(() => workflow.LoadDetailedDataForPlc(plcInfo));
            if (loadResult)
            {
                return true;
            }

            Logger.LogError(failureLogMessage);
            return false;
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error loading {failureDialogSubject} data: {ex.Message}", false);
            MessageBox.Show($"Failed to load {failureDialogSubject} data: {ex.Message}", "Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }
    }

    private async Task<(bool Success, string? Password)> ResolveSourceSafetyPasswordAsync(WorkflowStaThread workflow, PLCInfo sourcePlcInfo)
    {
        string? sourceSafetyPassword = null;
        var safetyPasswordData = sourcePlcInfo.SafetyPasswordData;

        if (safetyPasswordData?.SafetyAdmin?.IsSafetyOfflineProgramPasswordSet != true)
        {
            return (true, null);
        }

        bool passwordValid = false;
        Logger.LogInfo($"Attempting auto-password authentication for source PLC: {safetyPasswordData.DeviceName}");
        var passwordCandidates = SecurityManagementService.GeneratePasswordCandidates(sourcePlcInfo.ProjectName);

        foreach (var candidatePassword in passwordCandidates)
        {
            try
            {
                Logger.LogInfo("Trying auto-password candidate for source PLC...");
                var passwordValidationResult = await Task.Run(() =>
                    workflow.ValidateSafetyPassword(sourcePlcInfo.DeviceId, candidatePassword, true));

                if (passwordValidationResult)
                {
                    sourceSafetyPassword = candidatePassword;
                    passwordValid = true;
                    Logger.LogSuccess("Auto-password authentication successful for source PLC");
                    break;
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Auto-password attempt failed: {ex.Message}");
            }
        }

        while (!passwordValid)
        {
            using var passwordForm = new SimplePasswordForm($"Source PLC - {safetyPasswordData.DeviceName}");
            if (passwordForm.ShowDialog(this) != DialogResult.OK || string.IsNullOrEmpty(passwordForm.Password))
            {
                Logger.LogError("Source safety password input cancelled.");
                return (false, null);
            }

            try
            {
                var passwordValidationResult = await Task.Run(() =>
                    workflow.ValidateSafetyPassword(sourcePlcInfo.DeviceId, passwordForm.Password));

                if (passwordValidationResult)
                {
                    sourceSafetyPassword = passwordForm.Password;
                    passwordValid = true;
                }
                else
                {
                    MessageBox.Show("Invalid source safety password. Please try again.", "Authentication Failed",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error validating safety password: {ex.Message}", false);
                MessageBox.Show($"Error validating password: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        return (true, sourceSafetyPassword);
    }

    private bool TrySelectBlockGroup(PLCInfo sourcePlcInfo, out string selectedGroupId, out string selectedGroupPath)
    {
        using var groupSelectionForm = new BlockGroupSelectionForm(sourcePlcInfo.BlockGroupData);
        if (groupSelectionForm.ShowDialog(this) != DialogResult.OK)
        {
            MessageBox.Show("Block group selection cancelled.", "Cancelled",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            selectedGroupId = string.Empty;
            selectedGroupPath = string.Empty;
            return false;
        }

        selectedGroupId = groupSelectionForm.SelectedGroupId;
        selectedGroupPath = groupSelectionForm.SelectedGroupPath;
        return true;
    }

    private BlockProcessingConfiguration? TryConfigureBlockProcessing(
        HashSet<int> existingBlockNumbers,
        string selectedGroupId,
        WorkflowStaThread workflow)
    {
        using var processingConfigForm = new BlockProcessingConfigForm(existingBlockNumbers, selectedGroupId, workflow);
        if (processingConfigForm.ShowDialog(this) != DialogResult.OK)
        {
            MessageBox.Show("Block processing configuration cancelled.", "Cancelled",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return null;
        }

        return new BlockProcessingConfiguration(
            processingConfigForm.PrefixNumber,
            processingConfigForm.FindReplacePairs,
            processingConfigForm.ContentFindReplacePairs);
    }

    private List<TagTableConfig>? TrySelectTagTables(PLCInfo sourcePlcInfo, List<FindReplacePair> findReplacePairs)
    {
        using var tagTableSelectionForm = new TagTableSelectionForm(sourcePlcInfo.TagTableData, findReplacePairs, GetSampleTagDataFromSTA);
        if (tagTableSelectionForm.ShowDialog(this) != DialogResult.OK)
        {
            MessageBox.Show("Tag table selection cancelled.", "Cancelled",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return null;
        }

        return tagTableSelectionForm.SelectedTables;
    }

    private bool ConfirmBlockWorkflowExecution(
        WorkflowStaThread workflow,
        PLCInfo sourcePlcInfo,
        PLCInfo targetPlcInfo,
        string selectedGroupId,
        string selectedGroupPath,
        BlockProcessingConfiguration processingConfig,
        List<TagTableConfig> selectedTables,
        HashSet<string> existingBlockNames,
        HashSet<string> existingTagTableNames,
        string? sourceSafetyPassword)
    {
        var processingInfo = BuildProcessingInfo(processingConfig);
        var (tagTablesInfo, tagTableDetails) = BuildTagTableSummary(selectedTables);

        var confirmResult = MessageBox.Show(
            $"Configuration Summary:\n" +
            $"──────────────────────\n" +
            $"TIA Portal: Connected\n" +
            $"Source: {sourcePlcInfo.ProjectName} - {sourcePlcInfo.DeviceName}\n" +
            $"Target: {targetPlcInfo.ProjectName} - {targetPlcInfo.DeviceName}\n" +
            $"Safety: {(sourceSafetyPassword != null ? "Authenticated" : "No password required")}\n" +
            $"Selected Group: {selectedGroupPath}\n" +
            $"Block Processing: {processingInfo}\n" +
            $"Tag Tables: {tagTablesInfo}" +
            tagTableDetails + "\n\n" +
            "Do you want to execute the workflow now?",
            "Execute Workflow?", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

        if (confirmResult != DialogResult.Yes)
        {
            MessageBox.Show("Workflow execution cancelled.", "Cancelled",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return false;
        }

        var userSelections = new UserSelections
        {
            SourcePlcId = sourcePlcInfo.PlcId,
            SourceDeviceId = sourcePlcInfo.DeviceId,
            TargetPlcId = targetPlcInfo.PlcId,
            TargetDeviceId = targetPlcInfo.DeviceId,
            SelectedGroupId = selectedGroupId,
            PrefixNumber = processingConfig.PrefixNumber,
            FindReplacePairs = processingConfig.FindReplacePairs,
            ContentFindReplacePairs = processingConfig.ContentFindReplacePairs,
            SelectedTables = selectedTables,
            ExistingTagTableNames = existingTagTableNames,
            ExistingBlockNames = existingBlockNames,
            SourceSafetyPassword = sourceSafetyPassword
        };

        workflow.SetUserSelections(userSelections);
        return true;
    }

    private static string BuildProcessingInfo(BlockProcessingConfiguration processingConfig)
    {
        var findReplacePairs = processingConfig.FindReplacePairs;

        if (findReplacePairs.Count == 0)
        {
            return $"Prefix only: {processingConfig.PrefixNumber}";
        }

        if (findReplacePairs.Count == 1)
        {
            var pair = findReplacePairs[0];
            return $"Find/Replace: '{pair.FindString}' -> '{pair.ReplaceString}' + Prefix: {processingConfig.PrefixNumber}";
        }

        return $"{findReplacePairs.Count} Find/Replace pairs + Prefix: {processingConfig.PrefixNumber}";
    }

    private static (string Summary, string Details) BuildTagTableSummary(List<TagTableConfig> selectedTables)
    {
        if (selectedTables.Count == 0)
        {
            return ("No tag tables", string.Empty);
        }

        var totalTags = selectedTables.Sum(t => t.TagCount);
        var tableDetailsList = new List<string>();

        foreach (var table in selectedTables)
        {
            var nameReplacements = table.NameReplacements.Count;
            var addressReplacements = table.AddressReplacements.Count;

            if (nameReplacements == 0 && addressReplacements == 0)
            {
                tableDetailsList.Add($"• {table.TableName} ({table.TagCount} tags) - No modifications");
            }
            else
            {
                var modifications = new List<string>();
                if (nameReplacements > 0) modifications.Add($"{nameReplacements} name replacement(s)");
                if (addressReplacements > 0) modifications.Add($"{addressReplacements} address replacement(s)");

                tableDetailsList.Add($"• {table.TableName} ({table.TagCount} tags) - {string.Join(", ", modifications)}");
            }
        }

        var details = tableDetailsList.Count > 0
            ? "\n\nTag Table Details:\n" + string.Join("\n", tableDetailsList)
            : string.Empty;

        var summary = $"{selectedTables.Count} tag tables ({totalTags} total tags)";
        return (summary, details);
    }

    private sealed class BlockProcessingConfiguration(
        int prefixNumber,
        List<FindReplacePair>? findReplacePairs,
        List<FindReplacePair>? contentFindReplacePairs)
    {
        public int PrefixNumber { get; } = prefixNumber;
        public List<FindReplacePair> FindReplacePairs { get; } = findReplacePairs ?? new List<FindReplacePair>();
        public List<FindReplacePair> ContentFindReplacePairs { get; } = contentFindReplacePairs ?? new List<FindReplacePair>();
    }
}
