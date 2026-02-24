using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using OpennessCopy.Models;
using OpennessCopy.Services.HardwareCopy;
using OpennessCopy.Utils;

namespace OpennessCopy.Forms.HardwareCopy;

/// <summary>
/// Form for selecting source and target TIA Portal projects for hardware device copying
/// Similar to PlcSelectionForm but for project-level selection
/// Supports cross-instance and archive source projects
/// </summary>
public class HardwareProjectSelectionForm : Form
{
    public TiaPortalInstanceInfo SourceProject { get; private set; }
    public TiaPortalInstanceInfo TargetProject { get; private set; }

    private readonly HardwareDiscoveryData _discoveryData;
    private readonly HardwareWorkflowSTAThread _workflowThread;
    private ListView _listViewSourceProjects;
    private ListView _listViewTargetProjects;
    private Label _lblTitle;
    private Label _lblSourceTitle;
    private Label _lblTargetTitle;
    private Label _lblStatus;
    private Button _btnOk;
    private Button _btnCancel;
    private Button _btnLoadArchive;
    private Label _lblArchiveStatus;

    public HardwareProjectSelectionForm(HardwareDiscoveryData discoveryData, HardwareWorkflowSTAThread workflowThread)
    {
        _discoveryData = discoveryData ?? throw new ArgumentNullException(nameof(discoveryData));
        _workflowThread = workflowThread;
        InitializeComponent();
        LoadAvailableProjects();
    }

    private void LoadAvailableProjects()
    {
        try
        {
            if (_discoveryData.TiaInstances == null || _discoveryData.TiaInstances.Count == 0)
            {
                Logger.LogError("No TIA Portal projects found!");
                DialogResult = DialogResult.Cancel;
                Close();
                return;
            }

            // Populate source list with ALL projects (including archives)
            PopulateListView(_listViewSourceProjects, _discoveryData.TiaInstances, includeArchives: true);

            // Populate target list with ONLY live instances (no archives)
            var liveInstancesOnly = _discoveryData.TiaInstances.Where(inst => !inst.IsArchive).ToList();
            PopulateListView(_listViewTargetProjects, liveInstancesOnly, includeArchives: false);

            if (_discoveryData.TiaInstances.Count == 1 && !_discoveryData.TiaInstances[0].IsArchive)
            {
                // Auto-select if only one live project
                _listViewSourceProjects.Items[0].Selected = true;
                _listViewTargetProjects.Items[0].Selected = true;
                _lblStatus.Text = "Only one project found - automatically selected as both source and target.";
            }
            else
            {
                _lblStatus.Text = $"Found {_discoveryData.TiaInstances.Count} project(s). Please select source and target projects.";
            }
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error loading projects: {ex.Message}");
            DialogResult = DialogResult.Cancel;
            Close();
        }
    }

    private void PopulateListView(ListView listView, System.Collections.Generic.List<TiaPortalInstanceInfo> projects, bool includeArchives)
    {
        listView.Items.Clear();

        foreach (var project in projects)
        {
            // Skip archives in target list
            if (!includeArchives && project.IsArchive)
                continue;

            var item = new ListViewItem([
                project.ProjectName,
                project.IsArchive ? "N/A" : project.ProcessId.ToString()
            ])
            {
                Tag = project
            };

            listView.Items.Add(item);
        }
    }

    private void listViewSourceProjects_SelectedIndexChanged(object sender, EventArgs e)
    {
        // When user selects a source project, auto-select as target if it's not an archive
        if (_listViewSourceProjects.SelectedItems.Count > 0)
        {
            if (_listViewSourceProjects.SelectedItems[0].Tag is TiaPortalInstanceInfo { IsArchive: false } selectedSource)
            {
                // Find matching project in target list and select it
                foreach (ListViewItem item in _listViewTargetProjects.Items)
                {
                    if (item.Tag is TiaPortalInstanceInfo targetProj &&
                        targetProj.ProjectId == selectedSource.ProjectId)
                    {
                        item.Selected = true;
                        _listViewTargetProjects.EnsureVisible(item.Index);
                        break;
                    }
                }
            }
        }
    }

    private void btnOK_Click(object sender, EventArgs e)
    {
        if (_listViewSourceProjects.SelectedItems.Count == 0)
        {
            MessageBox.Show("Please select a source project.", "Selection Required",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (_listViewTargetProjects.SelectedItems.Count == 0)
        {
            MessageBox.Show("Please select a target project.", "Selection Required",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            SourceProject = (TiaPortalInstanceInfo)_listViewSourceProjects.SelectedItems[0].Tag;
            TargetProject = (TiaPortalInstanceInfo)_listViewTargetProjects.SelectedItems[0].Tag;

            // Validation: Ensure target is not an archive
            if (TargetProject.IsArchive)
            {
                MessageBox.Show("Target project cannot be an archive. Archives are read-only.\n\nPlease select a live TIA Portal project as the target.",
                    "Invalid Target", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // Warn if source and target are DIFFERENT projects
            if (SourceProject.ProjectId != TargetProject.ProjectId)
            {
                var result = MessageBox.Show(
                    "Source and target are different projects.\n\nDevices will be copied between projects.\n\nDo you want to continue?",
                    "Different Projects",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (result != DialogResult.Yes)
                    return;
            }

            Logger.LogInfo($"Projects selected - Source: '{SourceProject.ProjectName}', Target: '{TargetProject.ProjectName}'");

            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error selecting projects: {ex.Message}");
        }
    }

    private void btnCancel_Click(object sender, EventArgs e)
    {
        DialogResult = DialogResult.Cancel;
        Close();
    }

    private void listView_DoubleClick(object sender, EventArgs e)
    {
        // Double-click to select (only if both source and target are selected)
        if (_listViewSourceProjects.SelectedItems.Count > 0 &&
            _listViewTargetProjects.SelectedItems.Count > 0)
        {
            btnOK_Click(sender, e);
        }
    }

    private void InitializeComponent()
    {
        this._listViewSourceProjects = new ListView();
        this._listViewTargetProjects = new ListView();
        this._lblTitle = new Label();
        this._lblSourceTitle = new Label();
        this._lblTargetTitle = new Label();
        this._lblStatus = new Label();
        this._btnOk = new Button();
        this._btnCancel = new Button();
        this._btnLoadArchive = new Button();
        this._lblArchiveStatus = new Label();
        this.SuspendLayout();

        // Form properties
        this.Text = "Select Source and Target Projects - Hardware Workflow";
        this.Size = new Size(910, 450);
        this.StartPosition = FormStartPosition.CenterParent;
        this.MinimizeBox = false;
        this.MaximizeBox = false;
        this.FormBorderStyle = FormBorderStyle.FixedDialog;

        // lblTitle
        this._lblTitle.AutoSize = true;
        this._lblTitle.Font = new Font("Microsoft Sans Serif", 10F, FontStyle.Bold);
        this._lblTitle.Location = new Point(20, 20);
        this._lblTitle.Name = "_lblTitle";
        this._lblTitle.Size = new Size(300, 17);
        this._lblTitle.TabIndex = 0;
        this._lblTitle.Text = "Select Source and Target Projects";

        // lblSourceTitle
        this._lblSourceTitle.AutoSize = true;
        this._lblSourceTitle.Font = new Font("Microsoft Sans Serif", 9F, FontStyle.Bold);
        this._lblSourceTitle.Location = new Point(20, 50);
        this._lblSourceTitle.Name = "_lblSourceTitle";
        this._lblSourceTitle.Size = new Size(120, 15);
        this._lblSourceTitle.TabIndex = 1;
        this._lblSourceTitle.Text = "Source Project:";

        // listViewSourceProjects
        this._listViewSourceProjects.Location = new Point(20, 70);
        this._listViewSourceProjects.Name = "_listViewSourceProjects";
        this._listViewSourceProjects.Size = new Size(420, 240);
        this._listViewSourceProjects.TabIndex = 2;
        this._listViewSourceProjects.View = View.Details;
        this._listViewSourceProjects.FullRowSelect = true;
        this._listViewSourceProjects.GridLines = true;
        this._listViewSourceProjects.MultiSelect = false;
        this._listViewSourceProjects.HideSelection = false;
        this._listViewSourceProjects.Columns.Add("Project Name", 280);
        this._listViewSourceProjects.Columns.Add("Process ID", 120);
        this._listViewSourceProjects.SelectedIndexChanged += this.listViewSourceProjects_SelectedIndexChanged;
        this._listViewSourceProjects.DoubleClick += this.listView_DoubleClick;

        // lblTargetTitle
        this._lblTargetTitle.AutoSize = true;
        this._lblTargetTitle.Font = new Font("Microsoft Sans Serif", 9F, FontStyle.Bold);
        this._lblTargetTitle.Location = new Point(460, 50);
        this._lblTargetTitle.Name = "_lblTargetTitle";
        this._lblTargetTitle.Size = new Size(115, 15);
        this._lblTargetTitle.TabIndex = 3;
        this._lblTargetTitle.Text = "Target Project:";

        // listViewTargetProjects
        this._listViewTargetProjects.Location = new Point(460, 70);
        this._listViewTargetProjects.Name = "_listViewTargetProjects";
        this._listViewTargetProjects.Size = new Size(420, 240);
        this._listViewTargetProjects.TabIndex = 4;
        this._listViewTargetProjects.View = View.Details;
        this._listViewTargetProjects.FullRowSelect = true;
        this._listViewTargetProjects.GridLines = true;
        this._listViewTargetProjects.MultiSelect = false;
        this._listViewTargetProjects.HideSelection = false;
        this._listViewTargetProjects.Columns.Add("Project Name", 280);
        this._listViewTargetProjects.Columns.Add("Process ID", 120);
        this._listViewTargetProjects.DoubleClick += this.listView_DoubleClick;

        // lblStatus
        this._lblStatus.AutoSize = false;
        this._lblStatus.Location = new Point(20, 320);
        this._lblStatus.Name = "_lblStatus";
        this._lblStatus.Size = new Size(880, 20);
        this._lblStatus.TabIndex = 5;
        this._lblStatus.Text = "Loading projects...";
        this._lblStatus.TextAlign = ContentAlignment.MiddleLeft;

        // btnOK
        this._btnOk.Location = new Point(720, 370);
        this._btnOk.Name = "_btnOk";
        this._btnOk.Size = new Size(75, 30);
        this._btnOk.TabIndex = 6;
        this._btnOk.Text = "OK";
        this._btnOk.UseVisualStyleBackColor = true;
        this._btnOk.Click += this.btnOK_Click;

        // btnCancel
        this._btnCancel.Location = new Point(805, 370);
        this._btnCancel.Name = "_btnCancel";
        this._btnCancel.Size = new Size(75, 30);
        this._btnCancel.TabIndex = 7;
        this._btnCancel.Text = "Cancel";
        this._btnCancel.UseVisualStyleBackColor = true;
        this._btnCancel.Click += this.btnCancel_Click;

        // btnLoadArchive
        this._btnLoadArchive.Location = new Point(20, 370);
        this._btnLoadArchive.Name = "_btnLoadArchive";
        this._btnLoadArchive.Size = new Size(120, 30);
        this._btnLoadArchive.TabIndex = 8;
        this._btnLoadArchive.Text = "Load File...";
        this._btnLoadArchive.UseVisualStyleBackColor = true;
        this._btnLoadArchive.Click += this.btnLoadArchive_Click;

        // lblArchiveStatus
        this._lblArchiveStatus.AutoSize = false;
        this._lblArchiveStatus.Location = new Point(150, 375);
        this._lblArchiveStatus.Name = "_lblArchiveStatus";
        this._lblArchiveStatus.Size = new Size(570, 20);
        this._lblArchiveStatus.TabIndex = 9;
        this._lblArchiveStatus.Text = "No files loaded";
        this._lblArchiveStatus.TextAlign = ContentAlignment.MiddleLeft;
        this._lblArchiveStatus.ForeColor = SystemColors.GrayText;

        // HardwareProjectSelectionForm
        this.Controls.Add(this._lblTitle);
        this.Controls.Add(this._lblSourceTitle);
        this.Controls.Add(this._listViewSourceProjects);
        this.Controls.Add(this._lblTargetTitle);
        this.Controls.Add(this._listViewTargetProjects);
        this.Controls.Add(this._lblStatus);
        this.Controls.Add(this._btnOk);
        this.Controls.Add(this._btnCancel);
        this.Controls.Add(this._btnLoadArchive);
        this.Controls.Add(this._lblArchiveStatus);

        this.ResumeLayout(false);
        this.PerformLayout();
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
                    AddArchiveInstanceToList(discoveryData);
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

    private void AddArchiveInstanceToList(HardwareDiscoveryData discoveryData)
    {
        try
        {
            // Extract the single TiaPortalInstanceInfo from discovery data
            if (discoveryData.TiaInstances.Count == 0)
            {
                Logger.LogWarning("No instances found in archive discovery data");
                return;
            }

            var archiveInstance = discoveryData.TiaInstances.First();

            // Create ListViewItem for loaded file - ADD TO SOURCE LIST ONLY
            var item = new ListViewItem([
                $"{archiveInstance.ProjectName} (external)", // Add (external) indicator
                "N/A" // No process ID for loaded files
            ])
            {
                Tag = archiveInstance
            };

            _listViewSourceProjects.Items.Add(item);

            Logger.LogInfo($"Added external file to source selection: {archiveInstance.ProjectName}");
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error adding archive instance to list: {ex.Message}", false);
        }
    }
}
