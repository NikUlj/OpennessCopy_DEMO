using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using OpennessCopy.Models;
using OpennessCopy.Services;
using OpennessCopy.Services.BlockCopy;
using OpennessCopy.Services.CodeBuilders;
using OpennessCopy.Services.CodeBuilders.SimaticSd;
using OpennessCopy.Services.HardwareCopy;
using OpennessCopy.Utils;

namespace OpennessCopy.Forms;

public partial class MainForm : Form
{
    private readonly string _exportDir = Path.Combine(Path.GetTempPath(), "OpennessCopy_Export");
    private IWorkflowThread _currentWorkflow;

    public MainForm()
    {
        InitializeComponent();
        FormClosing += MainForm_FormClosing;
        
        Logger.Initialize(LogMessage, this);
            
        if (Directory.Exists(_exportDir))
        {
            try
            {
                Directory.Delete(_exportDir, true);
            }
            catch (Exception ex)
            {
                // Non-critical error - log but continue
                Logger.LogWarning($"Could not delete existing export directory: {ex.Message}");
            }
        }
    }
    
    private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
    {
        if (_currentWorkflow != null)
        {
            // Block form closing until STA thread cleanup is complete
            e.Cancel = true;
            
            // Start async cleanup process
            StartAsyncCleanupAndClose();
        }
    }
    
    private void StartAsyncCleanupAndClose()
    {
        this.Enabled = false;
        UpdateStatus("Cleaning up resources... Please wait.");
        this.Cursor = Cursors.WaitCursor;
        
        var cleanupThread = new Thread(() =>
        {
            try
            {
                _currentWorkflow?.RequestCleanup();
                
                _currentWorkflow?.Join(TimeSpan.FromSeconds(20));
                
                this.Invoke(new Action(() =>
                {
                    _currentWorkflow = null;
                    this.Close();
                }));
            }
            catch (Exception ex)
            {
                Logger.LogError($"Cleanup error during form closing: {ex.Message}", showMessageBox: false);
                
                this.Invoke(new Action(() =>
                {
                    _currentWorkflow = null;
                    this.Close();
                }));
            }
        })
        {
            IsBackground = true,
            Name = "AsyncCleanup"
        };
        
        cleanupThread.Start();
    }
    
    private void btnStart_Click(object sender, EventArgs e)
    {
        try
        {
            if (_cmbWorkflowType.SelectedIndex == -1)
            {
                MessageBox.Show("Please select a workflow type.", "No Workflow Selected",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var selectedWorkflow = (WorkflowType)_cmbWorkflowType.SelectedIndex;
            var selectedVersion = SelectedTiaPortalVersion;

            if (selectedWorkflow == WorkflowType.ConveyorGenerator &&
                selectedVersion != TiaPortalVersion.V20)
            {
                MessageBox.Show(
                    "The conveyor generator requires the TIA Portal V20 Openness API.\n" +
                    "Please select \"TIA Portal V20\" in the TIA API Version dropdown.",
                    "Unsupported Version",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            _logPanel.Clear();

            _progressBar.Value = 0;
            _progressBar.Visible = false;

            this.Cursor = Cursors.WaitCursor;
            SetWorkflowRunning(true);

            switch (selectedWorkflow)
            {
                case WorkflowType.PlcBlockCopy:
                    _currentWorkflow = new WorkflowStaThread(
                        OnProgressReported,
                        OnBlockDiscoveryCompleted,
                        _exportDir,
                        selectedVersion);
                    _currentWorkflow.Start();
                    break;

                case WorkflowType.HardwareCopy:
                    Logger.LogInfo($"Hardware workflow starting - export directory: {_exportDir}");

                    _currentWorkflow = new HardwareWorkflowSTAThread(
                        _exportDir,
                        OnHardwareDiscoveryCompleted,
                        OnProgressReported,
                        selectedVersion);
                    _currentWorkflow.Start();
                    break;

                case WorkflowType.ConveyorGenerator:
                {
                    const string templatesRoot = "OpennessCopy.Services.CodeBuilders.Templates.Conveyor";

                    _currentWorkflow = new ConveyorWorkflowStaThread(
                        _exportDir,
                        templatesRoot,
                        selectedVersion,
                        RequestConveyorPlcSelection,
                        RequestConveyorBlockSelection,
                        RequestConveyorConfiguration,
                        info => Logger.LogInfo(info),
                        error => Logger.LogError(error, false),
                        OnConveyorWorkflowCompleted);

                    _currentWorkflow.Start();
                    return;
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error starting workflow: {ex.Message}", showMessageBox: true);
            this.Cursor = Cursors.Default;
            SetWorkflowRunning(false);
        }
    }
    
    private void btnCancel_Click(object sender, EventArgs e)
    {
        if (_currentWorkflow != null)
        {
            UpdateStatus("Cancelling workflow...");
            _currentWorkflow.Cancel();
            _btnCancel.Enabled = false;
        }
    }

    private void UpdateStatus(string message)
    {
        if (InvokeRequired)
        {
            Invoke(new Action<string>(UpdateStatus), message);
            return;
        }

        _lblStatus.Text = message;
    }

    private void LogMessage(string message, System.Drawing.Color color)
    {
        if (InvokeRequired)
        {
            Invoke(new Action(() => LogMessage(message, color)));
            return;
        }

        _logPanel.SelectionStart = _logPanel.TextLength;
        _logPanel.SelectionLength = 0;
        _logPanel.SelectionColor = color;
        _logPanel.AppendText(message + Environment.NewLine);
        _logPanel.SelectionColor = _logPanel.ForeColor;

        _logPanel.SelectionStart = _logPanel.TextLength;
        _logPanel.ScrollToCaret();
    }

    private void SetWorkflowRunning(bool isRunning)
    {
        _cmbWorkflowType.Enabled = !isRunning;
        _cmbTiaVersion.Enabled = !isRunning;
        _btnStart.Visible = !isRunning;
        _btnCancel.Visible = isRunning;
        _btnCancel.Enabled = isRunning;
    }

    private void OnProgressReported(WorkflowProgress progress)
    {
        if (InvokeRequired)
        {
            Invoke(new Action<WorkflowProgress>(OnProgressReported), progress);
            return;
        }

        try
        {
            UpdateStatus(progress.Status);

            if (progress.PercentComplete > 0)
            {
                _progressBar.Visible = true;
                _progressBar.Value = Math.Min(100, Math.Max(0, progress.PercentComplete));
            }

            System.Drawing.Color logColor;
            switch (progress.Type)
            {
                case WorkflowProgressType.Success:
                    logColor = System.Drawing.Color.LightGreen;
                    break;
                case WorkflowProgressType.Warning:
                    logColor = System.Drawing.Color.Yellow;
                    break;
                case WorkflowProgressType.Error:
                    logColor = System.Drawing.Color.Red;
                    break;
                case WorkflowProgressType.Cancelled:
                    logColor = System.Drawing.Color.Orange;
                    break;
                default:
                    logColor = System.Drawing.Color.White;
                    break;
            }

            LogMessage(progress.Status, logColor);

            switch (progress.Type)
            {
                case WorkflowProgressType.Success:
                    // ResetUIState();
                    Logger.LogSuccess("Workflow completed successfully!", true);
                    break;
                case WorkflowProgressType.Error:
                case WorkflowProgressType.Cancelled:
                {
                    if (_currentWorkflow == null)
                    {
                        ResetUIState();
                    }

                    if (progress.Type == WorkflowProgressType.Error && progress.Exception != null)
                    {
                        Logger.LogError($"Workflow failed: {progress.Exception.Message}", showMessageBox: true);
                    }

                    break;
                }
                case WorkflowProgressType.CleanupComplete:
                    ResetUIState();
                    break;
            }
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error handling progress report: {ex.Message}", showMessageBox: false);
        }
    }

    private void ResetUIState()
    {
        if (InvokeRequired)
        {
            Invoke(new Action(ResetUIState));
            return;
        }

        _progressBar.Visible = false;
        _progressBar.Value = 0;

        this.Cursor = Cursors.Default;
        SetWorkflowRunning(false);
        UpdateStatus("Ready");

        if (_currentWorkflow != null)
        {
            var cleanupThread = new Thread(() =>
            {
                try
                {
                    _currentWorkflow.Join(TimeSpan.FromSeconds(5));
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Error during workflow thread cleanup: {ex.Message}", showMessageBox: false);
                }
                finally
                {
                    _currentWorkflow = null;
                }
            })
            {
                IsBackground = true,
                Name = "STACleanup"
            };
            cleanupThread.Start();
        }
    }

    private List<TagExample> GetSampleTagDataFromSTA(string tableId)
    {
        try
        {
            return (_currentWorkflow as WorkflowStaThread)?.RequestSampleTagData(tableId) ?? new List<TagExample>();
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"Failed to get sample tag data: {ex.Message}");
            return new List<TagExample>();
        }
    }

    /// <summary>
    /// Validates that source and target projects have compatible active cultures
    /// </summary>
    private bool ValidateCultureCompatibility(PLCInfo sourcePlc, PLCInfo targetPlc)
    {
        try
        {
            if (sourcePlc.PlcId == targetPlc.PlcId && sourcePlc.DeviceId == targetPlc.DeviceId)
            {
                return true;
            }

            var missingInTarget = sourcePlc.ActiveCultures.Except(targetPlc.ActiveCultures).ToList();

            if (missingInTarget.Count == 0)
            {
                Logger.LogInfo("Target project has all required cultures from source project.");
                return true;
            }

            var errorMessage = $"Cannot continue - culture compatibility error!\n\n" +
                              $"Source Project: {sourcePlc.ProjectName}\n" +
                              $"Active Cultures: {string.Join(", ", sourcePlc.ActiveCultures.OrderBy(c => c))}\n\n" +
                              $"Target Project: {targetPlc.ProjectName}\n" +
                              $"Active Cultures: {string.Join(", ", targetPlc.ActiveCultures.OrderBy(c => c))}\n\n" +
                              $"Missing in target: {string.Join(", ", missingInTarget.OrderBy(c => c))}\n\n" +
                              $"The source project has active languages that the target project does not.\n" +
                              $"This will cause import errors and the operation cannot proceed.";

            MessageBox.Show(errorMessage, "Culture Compatibility Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);

            Logger.LogError("Culture compatibility validation failed.");
            return false;
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error validating culture compatibility: {ex.Message}");
            return false;
        }
    }

    public enum WorkflowType
    {
        PlcBlockCopy = 0,
        HardwareCopy = 1,
        ConveyorGenerator = 2
    }

    private TiaPortalVersion SelectedTiaPortalVersion =>
        _cmbTiaVersion.SelectedIndex switch
        {
            0 => TiaPortalVersion.V18,
            1 => TiaPortalVersion.V20,
            _ => TiaPortalVersion.V18
        };
}
