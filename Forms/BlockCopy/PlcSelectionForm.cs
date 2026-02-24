using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using OpennessCopy.Models;
using OpennessCopy.Services.BlockCopy;
using OpennessCopy.Utils;

namespace OpennessCopy.Forms.BlockCopy;

public class PlcSelectionForm : Form
{
    public PLCInfo SourcePlcInfo { get; private set; }
    public PLCInfo TargetPlcInfo { get; private set; }

    private List<PLCInfo> _availablePLCs;
    private readonly WorkflowStaThread _workflowThread;

    public PlcSelectionForm(List<PLCInfo> availablePLCs, WorkflowStaThread workflowThread = null)
    {
        _workflowThread = workflowThread;
        InitializeComponent();
        LoadAvailablePLCs(availablePLCs);
    }


    private void LoadAvailablePLCs(List<PLCInfo> availablePLCs)
    {
        try
        {
            _availablePLCs = availablePLCs;

            if (_availablePLCs.Count == 0)
            {
                Logger.LogError("No PLCs found in the project!");
                DialogResult = DialogResult.Cancel;
                Close();
                return;
            }

            // Group PLCs by TIA Portal instance
            var plcsByInstance = _availablePLCs.GroupBy(plc => new { plc.TiaInstanceId, plc.ProjectName }).ToList();

            // Populate source tree with ALL PLCs (including archives)
            PopulateTreeView(_treeViewSourcePlcs, plcsByInstance, includeArchives: true);

            // Populate target tree with ONLY live instances (no archives)
            var liveInstancesOnly = _availablePLCs.Where(plc => !plc.IsArchive).GroupBy(plc => new { plc.TiaInstanceId, plc.ProjectName }).ToList();
            PopulateTreeView(_treeViewTargetPlcs, liveInstancesOnly, includeArchives: false);

            if (_availablePLCs.Count == 1)
            {
                // Auto-select if only one PLC - find the first PLC node in both trees
                foreach (TreeNode instanceNode in _treeViewSourcePlcs.Nodes)
                {
                    if (instanceNode.Nodes.Count > 0)
                    {
                        _treeViewSourcePlcs.SelectedNode = instanceNode.Nodes[0];
                        break;
                    }
                }
                foreach (TreeNode instanceNode in _treeViewTargetPlcs.Nodes)
                {
                    if (instanceNode.Nodes.Count > 0)
                    {
                        _treeViewTargetPlcs.SelectedNode = instanceNode.Nodes[0];
                        break;
                    }
                }
                _lblStatus.Text = "Only one PLC found - automatically selected as both source and target.";
            }
            else
            {
                _lblStatus.Text = $"Found {_availablePLCs.Count} PLCs. Please select source and target PLCs.";
            }
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error loading PLCs: {ex.Message}");
            DialogResult = DialogResult.Cancel;
            Close();
        }
    }

    private void PopulateTreeView(TreeView treeView, IEnumerable<IGrouping<object, PLCInfo>> plcsByInstance, bool includeArchives)
    {
        foreach (var instanceGroup in plcsByInstance)
        {
            // Create parent node for TIA Portal instance
            var projectName = instanceGroup.Key.GetType().GetProperty("ProjectName")?.GetValue(instanceGroup.Key);
            var instanceNode = new TreeNode($"TIA Portal - {projectName}")
            {
                Tag = null // Instance nodes are not selectable (no PLCInfo)
            };

            // Add PLC child nodes
            foreach (var plcInfo in instanceGroup)
            {
                // Skip archives in target tree
                if (!includeArchives && plcInfo.IsArchive)
                    continue;

                var plcNode = new TreeNode($"{plcInfo.DeviceName} - {plcInfo.Name}")
                {
                    Tag = plcInfo // PLC nodes are selectable
                };
                instanceNode.Nodes.Add(plcNode);
            }

            // Only add instance node if it has PLC children
            if (instanceNode.Nodes.Count > 0)
            {
                treeView.Nodes.Add(instanceNode);
                instanceNode.Expand(); // Expand by default to show PLCs
            }
        }
    }

    private void treeViewSourcePlcs_AfterSelect(object sender, TreeViewEventArgs e)
    {
        // When user selects a source PLC, automatically set it as target too (unless it's an archive or user has manually changed target)
        if (e.Node?.Tag is PLCInfo { IsArchive: false } selectedPlc)
        {
            // Find the corresponding node in target tree and select it (only if source is not archive)
            foreach (TreeNode instanceNode in _treeViewTargetPlcs.Nodes)
            {
                foreach (TreeNode plcNode in instanceNode.Nodes)
                {
                    if (plcNode.Tag is PLCInfo plcInfo && plcInfo.PlcId == selectedPlc.PlcId)
                    {
                        _treeViewTargetPlcs.SelectedNode = plcNode;
                        return;
                    }
                }
            }
        }
    }

    private void btnOK_Click(object sender, EventArgs e)
    {
        if (_treeViewSourcePlcs.SelectedNode == null || _treeViewSourcePlcs.SelectedNode.Tag == null)
        {
            MessageBox.Show("Please select a source PLC (not a TIA Portal instance).", "Selection Required",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (_treeViewTargetPlcs.SelectedNode == null || _treeViewTargetPlcs.SelectedNode.Tag == null)
        {
            MessageBox.Show("Please select a target PLC (not a TIA Portal instance).", "Selection Required",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            SourcePlcInfo = (PLCInfo)_treeViewSourcePlcs.SelectedNode.Tag;
            TargetPlcInfo = (PLCInfo)_treeViewTargetPlcs.SelectedNode.Tag;

            // Validation: Ensure target is not an archive
            if (TargetPlcInfo.IsArchive)
            {
                MessageBox.Show("Target PLC cannot be from an archive. Archives are read-only.\n\nPlease select a live TIA Portal project as the target.",
                    "Invalid Target", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // Warn if source and target are from DIFFERENT projects
            if (SourcePlcInfo.ProjectName != TargetPlcInfo.ProjectName || SourcePlcInfo.Name != TargetPlcInfo.Name)
            {
                var result = MessageBox.Show(
                    "Source and target PLCs are different.\n\nBlocks will be copied between PLCs.\n\nDo you want to continue?",
                    "Different Projects",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (result != DialogResult.Yes)
                    return;
            }

            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error selecting PLCs: {ex.Message}");
        }
    }

    private void btnCancel_Click(object sender, EventArgs e)
    {
        DialogResult = DialogResult.Cancel;
        Close();
    }

    private void treeViewPlcs_DoubleClick(object sender, EventArgs e)
    {
        // Double-click to select (only if both source and target are selected with PLC nodes)
        if (_treeViewSourcePlcs.SelectedNode is { Tag: not null } &&
            _treeViewTargetPlcs.SelectedNode is { Tag: not null })
        {
            btnOK_Click(sender, e);
        }
    }

    private void btnLoadArchive_Click(object sender, EventArgs e)
    {
        try
        {
            // Show file dialog to select archive or project
            using var openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "TIA Portal Files (*.zap*;*.ap*)|*.zap*;*.ap*|TIA Portal Archive (*.zap*)|*.zap*|TIA Portal Project (*.ap*)|*.ap*|All files (*.*)|*.*";
            openFileDialog.Title = "Select TIA Portal Archive or Project";

            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                LoadArchive(openFileDialog.FileName);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error loading archive: {ex.Message}", false);
            MessageBox.Show($"Error loading archive: {ex.Message}", "Archive Load Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void LoadArchive(string archivePath)
    {
        if (_workflowThread == null)
        {
            MessageBox.Show("Archive loading is not available in this context.", "Not Available",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        // Show loading form (non-modal)
        var loadingForm = new ArchiveLoadingForm(archivePath);
        loadingForm.Show(this);
        loadingForm.CenterToParent(this);

        // Request archive load on background thread (uses dedicated TIA Portal instance)
        Task.Run(() =>
        {
            var (success, discoveryData) = _workflowThread.LoadArchiveForSelection(archivePath);

            // Update UI on UI thread
            this.Invoke((Action)(() =>
            {
                loadingForm.Close();

                if (success && discoveryData != null)
                {
                    AddArchivePLCsToTreeView(discoveryData);
                    _lblArchiveStatus.Text = $"Loaded: {Path.GetFileName(archivePath)}";
                    Logger.LogInfo($"Archive loaded successfully: {Path.GetFileName(archivePath)}");
                }
                else
                {
                    MessageBox.Show($"Failed to load archive: {archivePath}\n\nPlease check the log for details.",
                        "Archive Load Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }));
        });
    }

    private void AddArchivePLCsToTreeView(DiscoveryData discoveryData)
    {
        try
        {
            // Extract the PLCs from discovery data
            if (discoveryData.PLCs.Count == 0)
            {
                Logger.LogWarning("No PLCs found in archive discovery data");
                return;
            }

            // Group archive PLCs by project
            var archivePlcsByProject = discoveryData.PLCs.GroupBy(plc => new { plc.TiaInstanceId, plc.ProjectName }).ToList();

            // Add loaded file PLCs to SOURCE tree only
            foreach (var projectGroup in archivePlcsByProject)
            {
                var projectName = projectGroup.Key.ProjectName;
                var instanceNode = new TreeNode($"TIA Portal - {projectName} (external)")
                {
                    Tag = null // Instance nodes are not selectable (no PLCInfo)
                };

                foreach (var plcInfo in projectGroup)
                {
                    // Add to internal list
                    _availablePLCs.Add(plcInfo);

                    var plcNode = new TreeNode($"{plcInfo.DeviceName} - {plcInfo.Name}")
                    {
                        Tag = plcInfo
                    };
                    instanceNode.Nodes.Add(plcNode);
                }

                _treeViewSourcePlcs.Nodes.Add(instanceNode);
                instanceNode.Expand();
            }

            Logger.LogInfo($"Added {discoveryData.PLCs.Count} archive PLC(s) to source selection");
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error adding archive PLCs to tree view: {ex.Message}", false);
        }
    }

    private void InitializeComponent()
    {
        this._treeViewSourcePlcs = new TreeView();
        this._treeViewTargetPlcs = new TreeView();
        this._lblTitle = new Label();
        this._lblSourceTitle = new Label();
        this._lblTargetTitle = new Label();
        this._lblStatus = new Label();
        this._btnOk = new Button();
        this._btnCancel = new Button();
        this._btnLoadArchive = new Button();
        this._lblArchiveStatus = new Label();
        this.SuspendLayout();
            
        // 
        // lblTitle
        // 
        this._lblTitle.AutoSize = true;
        this._lblTitle.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Bold);
        this._lblTitle.Location = new System.Drawing.Point(20, 20);
        this._lblTitle.Name = "_lblTitle";
        this._lblTitle.Size = new System.Drawing.Size(120, 17);
        this._lblTitle.TabIndex = 0;
        this._lblTitle.Text = "Select Source and Target PLCs";
            
        // 
        // lblSourceTitle
        // 
        this._lblSourceTitle.AutoSize = true;
        this._lblSourceTitle.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Bold);
        this._lblSourceTitle.Location = new System.Drawing.Point(20, 50);
        this._lblSourceTitle.Name = "_lblSourceTitle";
        this._lblSourceTitle.Size = new System.Drawing.Size(80, 15);
        this._lblSourceTitle.TabIndex = 1;
        this._lblSourceTitle.Text = "Source PLC:";
            
        // 
        // treeViewSourcePlcs
        // 
        this._treeViewSourcePlcs.HideSelection = false;
        this._treeViewSourcePlcs.Location = new System.Drawing.Point(20, 70);
        this._treeViewSourcePlcs.Name = "_treeViewSourcePlcs";
        this._treeViewSourcePlcs.Size = new System.Drawing.Size(390, 200);
        this._treeViewSourcePlcs.TabIndex = 2;
        this._treeViewSourcePlcs.ShowLines = true;
        this._treeViewSourcePlcs.ShowPlusMinus = true;
        this._treeViewSourcePlcs.ShowRootLines = true;
        this._treeViewSourcePlcs.AfterSelect += this.treeViewSourcePlcs_AfterSelect;
        this._treeViewSourcePlcs.DoubleClick += this.treeViewPlcs_DoubleClick;
            
        // 
        // lblTargetTitle
        // 
        this._lblTargetTitle.AutoSize = true;
        this._lblTargetTitle.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Bold);
        this._lblTargetTitle.Location = new System.Drawing.Point(430, 50);
        this._lblTargetTitle.Name = "_lblTargetTitle";
        this._lblTargetTitle.Size = new System.Drawing.Size(75, 15);
        this._lblTargetTitle.TabIndex = 3;
        this._lblTargetTitle.Text = "Target PLC:";
            
        // 
        // treeViewTargetPlcs
        // 
        this._treeViewTargetPlcs.HideSelection = false;
        this._treeViewTargetPlcs.Location = new System.Drawing.Point(430, 70);
        this._treeViewTargetPlcs.Name = "_treeViewTargetPlcs";
        this._treeViewTargetPlcs.Size = new System.Drawing.Size(390, 200);
        this._treeViewTargetPlcs.TabIndex = 4;
        this._treeViewTargetPlcs.ShowLines = true;
        this._treeViewTargetPlcs.ShowPlusMinus = true;
        this._treeViewTargetPlcs.ShowRootLines = true;
        this._treeViewTargetPlcs.DoubleClick += this.treeViewPlcs_DoubleClick;
            
        // 
        // lblStatus
        // 
        this._lblStatus.AutoSize = true;
        this._lblStatus.Location = new System.Drawing.Point(20, 280);
        this._lblStatus.Name = "_lblStatus";
        this._lblStatus.Size = new System.Drawing.Size(100, 13);
        this._lblStatus.TabIndex = 5;
        this._lblStatus.Text = "Loading PLCs...";
            
        // 
        // btnOK
        // 
        this._btnOk.Location = new System.Drawing.Point(660, 310);
        this._btnOk.Name = "_btnOk";
        this._btnOk.Size = new System.Drawing.Size(75, 30);
        this._btnOk.TabIndex = 6;
        this._btnOk.Text = "OK";
        this._btnOk.UseVisualStyleBackColor = true;
        this._btnOk.Click += this.btnOK_Click;
            
        //
        // btnCancel
        //
        this._btnCancel.Location = new System.Drawing.Point(745, 310);
        this._btnCancel.Name = "_btnCancel";
        this._btnCancel.Size = new System.Drawing.Size(75, 30);
        this._btnCancel.TabIndex = 7;
        this._btnCancel.Text = "Cancel";
        this._btnCancel.UseVisualStyleBackColor = true;
        this._btnCancel.Click += this.btnCancel_Click;

        //
        // btnLoadArchive
        //
        this._btnLoadArchive.Location = new System.Drawing.Point(20, 310);
        this._btnLoadArchive.Name = "_btnLoadArchive";
        this._btnLoadArchive.Size = new System.Drawing.Size(120, 30);
        this._btnLoadArchive.TabIndex = 8;
        this._btnLoadArchive.Text = "Load File...";
        this._btnLoadArchive.UseVisualStyleBackColor = true;
        this._btnLoadArchive.Click += this.btnLoadArchive_Click;

        //
        // lblArchiveStatus
        //
        this._lblArchiveStatus.AutoSize = false;
        this._lblArchiveStatus.Location = new System.Drawing.Point(150, 315);
        this._lblArchiveStatus.Name = "_lblArchiveStatus";
        this._lblArchiveStatus.Size = new System.Drawing.Size(500, 20);
        this._lblArchiveStatus.TabIndex = 9;
        this._lblArchiveStatus.Text = "No files loaded";
        this._lblArchiveStatus.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
        this._lblArchiveStatus.ForeColor = System.Drawing.SystemColors.GrayText;

        //
        // PlcSelectionForm
        //
        this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
        this.AutoScaleMode = AutoScaleMode.Font;
        this.ClientSize = new System.Drawing.Size(840, 360);
        this.Controls.Add(this._lblTitle);
        this.Controls.Add(this._lblSourceTitle);
        this.Controls.Add(this._treeViewSourcePlcs);
        this.Controls.Add(this._lblTargetTitle);
        this.Controls.Add(this._treeViewTargetPlcs);
        this.Controls.Add(this._lblStatus);
        this.Controls.Add(this._btnOk);
        this.Controls.Add(this._btnCancel);
        this.Controls.Add(this._btnLoadArchive);
        this.Controls.Add(this._lblArchiveStatus);
        this.FormBorderStyle = FormBorderStyle.FixedDialog;
        this.MaximizeBox = false;
        this.MinimizeBox = false;
        this.Name = "PlcSelectionForm";
        this.StartPosition = FormStartPosition.CenterParent;
        this.Text = "Select PLC";
        this.ResumeLayout(false);
        this.PerformLayout();
    }

    private TreeView _treeViewSourcePlcs;
    private TreeView _treeViewTargetPlcs;
    private Label _lblTitle;
    private Label _lblSourceTitle;
    private Label _lblTargetTitle;
    private Label _lblStatus;
    private Button _btnOk;
    private Button _btnCancel;
    private Button _btnLoadArchive;
    private Label _lblArchiveStatus;
}