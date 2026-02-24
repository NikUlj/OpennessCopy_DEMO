using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using OpennessCopy.Models;
using OpennessCopy.Utils;

namespace OpennessCopy.Forms.HardwareCopy;

/// <summary>
/// Form for selecting hardware devices for export
/// Displays devices with details and export confirmation
/// </summary>
public class HardwareDeviceSelectionForm : Form
{
    private TreeView _treeViewDevices;
    private Button _buttonConfigureExport;
    private Button _buttonCancel;
    private Button _buttonSelectAll;
    private Button _buttonSelectNone;
    private Button _buttonExpandAll;
    private Button _buttonCollapseAll;
    private TextBox _textBoxSearch;
    private Label _labelSearch;
    private Label _labelInstructions;
    private Label _labelInstanceInfo;
    private Label _labelSelectionStatus;

    private readonly TiaPortalInstanceInfo _instanceInfo;
    public List<HardwareDeviceInfo> SelectedDevices { get; private set; } = new List<HardwareDeviceInfo>();
    private string _currentSearchFilter = "";

    public HardwareDeviceSelectionForm(TiaPortalInstanceInfo instanceInfo)
    {
        _instanceInfo = instanceInfo ?? throw new ArgumentNullException(nameof(instanceInfo));
        InitializeComponent();
        PopulateDevices();
    }

    private void InitializeComponent()
    {
        this._treeViewDevices = new TreeView();
        this._buttonConfigureExport = new Button();
        this._buttonCancel = new Button();
        this._buttonSelectAll = new Button();
        this._buttonSelectNone = new Button();
        this._buttonExpandAll = new Button();
        this._buttonCollapseAll = new Button();
        this._textBoxSearch = new TextBox();
        this._labelSearch = new Label();
        this._labelInstructions = new Label();
        this._labelInstanceInfo = new Label();
        this._labelSelectionStatus = new Label();
        this.SuspendLayout();

        // Form properties
        this.Text = "Select Hardware Devices - Hardware Export";
        this.Size = new Size(500, 580);
        this.StartPosition = FormStartPosition.CenterParent;
        this.MinimizeBox = false;
        this.MaximizeBox = false;
        this.FormBorderStyle = FormBorderStyle.FixedDialog;

        // Instance info label
        this._labelInstanceInfo.Text = $"Project: {_instanceInfo.ProjectName} | Devices: {_instanceInfo.HardwareDevices?.Count ?? 0}";
        this._labelInstanceInfo.Location = new Point(12, 15);
        this._labelInstanceInfo.Size = new Size(460, 20);
        this._labelInstanceInfo.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        this._labelInstanceInfo.Font = new Font(this._labelInstanceInfo.Font, FontStyle.Bold);

        // Instructions label
        this._labelInstructions.Text = "Select the hardware devices you want to export to AML format:";
        this._labelInstructions.Location = new Point(12, 40);
        this._labelInstructions.Size = new Size(460, 20);
        this._labelInstructions.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

        // Search label
        this._labelSearch.Text = "Filter:";
        this._labelSearch.Location = new Point(12, 65);
        this._labelSearch.Size = new Size(40, 20);
        this._labelSearch.Anchor = AnchorStyles.Top | AnchorStyles.Left;
        this._labelSearch.TextAlign = ContentAlignment.MiddleLeft;

        // Search textbox
        this._textBoxSearch.Location = new Point(55, 65);
        this._textBoxSearch.Size = new Size(417, 20);
        this._textBoxSearch.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        this._textBoxSearch.TextChanged += TextBoxSearch_TextChanged;

        // TreeView for hardware devices
        this._treeViewDevices.Location = new Point(12, 95);
        this._treeViewDevices.Size = new Size(460, 320);
        this._treeViewDevices.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        this._treeViewDevices.HideSelection = false;
        this._treeViewDevices.FullRowSelect = true;
        this._treeViewDevices.ShowLines = true;
        this._treeViewDevices.ShowPlusMinus = true;
        this._treeViewDevices.ShowRootLines = true;
        this._treeViewDevices.CheckBoxes = true;
        this._treeViewDevices.AfterCheck += TreeView_AfterCheck;
        this._treeViewDevices.BeforeCheck += TreeView_BeforeCheck;
        this._treeViewDevices.MouseDoubleClick += TreeView_MouseDoubleClick;

        // Expand/Collapse buttons
        this._buttonExpandAll.Text = "Expand All";
        this._buttonExpandAll.Location = new Point(12, 420);
        this._buttonExpandAll.Size = new Size(80, 23);
        this._buttonExpandAll.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
        this._buttonExpandAll.Click += ButtonExpandAll_Click;

        this._buttonCollapseAll.Text = "Collapse All";
        this._buttonCollapseAll.Location = new Point(97, 420);
        this._buttonCollapseAll.Size = new Size(90, 23);
        this._buttonCollapseAll.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
        this._buttonCollapseAll.Click += ButtonCollapseAll_Click;

        // Selection buttons
        this._buttonSelectAll.Text = "Select All";
        this._buttonSelectAll.Location = new Point(12, 448);
        this._buttonSelectAll.Size = new Size(80, 23);
        this._buttonSelectAll.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
        this._buttonSelectAll.Click += ButtonSelectAll_Click;

        this._buttonSelectNone.Text = "Select None";
        this._buttonSelectNone.Location = new Point(97, 448);
        this._buttonSelectNone.Size = new Size(90, 23);
        this._buttonSelectNone.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
        this._buttonSelectNone.Click += ButtonSelectNone_Click;

        // Selection status label
        this._labelSelectionStatus.Text = "No devices selected";
        this._labelSelectionStatus.Location = new Point(200, 452);
        this._labelSelectionStatus.Size = new Size(200, 15);
        this._labelSelectionStatus.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
        this._labelSelectionStatus.ForeColor = Color.DarkBlue;

        // Configure & Export button
        this._buttonConfigureExport.Text = "Configure & Export";
        this._buttonConfigureExport.Location = new Point(268, 510);
        this._buttonConfigureExport.Size = new Size(125, 23);
        this._buttonConfigureExport.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
        this._buttonConfigureExport.Enabled = false;
        this._buttonConfigureExport.Click += ButtonConfigureExport_Click;

        // Cancel button
        this._buttonCancel.Text = "Cancel";
        this._buttonCancel.Location = new Point(398, 510);
        this._buttonCancel.Size = new Size(75, 23);
        this._buttonCancel.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
        this._buttonCancel.Click += ButtonCancel_Click;

        // Add controls to form
        this.Controls.Add(this._labelInstanceInfo);
        this.Controls.Add(this._labelInstructions);
        this.Controls.Add(this._labelSearch);
        this.Controls.Add(this._textBoxSearch);
        this.Controls.Add(this._treeViewDevices);
        this.Controls.Add(this._buttonExpandAll);
        this.Controls.Add(this._buttonCollapseAll);
        this.Controls.Add(this._buttonSelectAll);
        this.Controls.Add(this._buttonSelectNone);
        this.Controls.Add(this._labelSelectionStatus);
        this.Controls.Add(this._buttonConfigureExport);
        this.Controls.Add(this._buttonCancel);

        this.ResumeLayout(false);
        this.PerformLayout();
    }

    private void PopulateDevices()
    {
        try
        {
            if (_instanceInfo.HardwareDevices == null || _instanceInfo.HardwareDevices.Count == 0)
            {
                Logger.LogWarning($"No hardware devices found in TIA instance: {_instanceInfo.ProjectName}");
                _labelInstructions.Text = "No hardware devices found in this TIA Portal instance.";
                _buttonConfigureExport.Enabled = false;
                return;
            }

            // Filter out multi-port devices and show detailed info
            var allDevices = _instanceInfo.HardwareDevices;
            var multiPortDevices = allDevices.Where(d => d.NetworkPortCount > 1).ToList();

            if (multiPortDevices.Any())
            {
                var deviceNames = string.Join("\n  • ", multiPortDevices.Select(d => $"{d.Name} ({d.NetworkPortCount} ports)"));

                Logger.LogWarning($"Filtered out {multiPortDevices.Count} multi-port device(s): {string.Join(", ", multiPortDevices.Select(d => d.Name))}");

                MessageBox.Show(
                    $"The following device(s) have multiple network ports and cannot be automatically configured:\n\n  • {deviceNames}\n\n" +
                    $"These devices require manual network configuration and will not appear in the selection list.",
                    "Multi-Port Devices Filtered",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }

            var singlePortDevices = allDevices.Where(d => d.NetworkPortCount <= 1).ToList();

            if (singlePortDevices.Count == 0)
            {
                Logger.LogWarning($"No single-port devices available after filtering multi-port devices");
                _labelInstructions.Text = "No devices available - all devices have multiple network ports and require manual configuration.";
                _buttonConfigureExport.Enabled = false;
                return;
            }

            // Update instance info to only include single-port devices
            _instanceInfo.HardwareDevices = singlePortDevices;

            // Step 1: Group devices by type using Dictionary
            var deviceGroups = new Dictionary<string, List<HardwareDeviceInfo>>();
            foreach (var device in _instanceInfo.HardwareDevices)
            {
                var deviceType = device.DeviceType ?? "Unknown";
                if (!deviceGroups.ContainsKey(deviceType))
                    deviceGroups[deviceType] = new List<HardwareDeviceInfo>();
                deviceGroups[deviceType].Add(device);
            }

            // Step 2: Sort device types alphabetically
            var sortedDeviceTypes = deviceGroups.Keys.OrderBy(k => k).ToList();

            // Step 3: Sort devices within each type by name
            foreach (var deviceType in sortedDeviceTypes)
            {
                deviceGroups[deviceType].Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
            }

            // Step 4: Build TreeView structure
            foreach (var deviceType in sortedDeviceTypes)
            {
                var devices = deviceGroups[deviceType];
                var parentNode = new TreeNode($"{deviceType} ({devices.Count})")
                {
                    Tag = deviceType // Store device type for identification
                };

                foreach (var device in devices)
                {
                    // Display ItemName if available, otherwise display device Name
                    var displayName = !string.IsNullOrWhiteSpace(device.ItemName) ? device.ItemName : device.Name;
                    var childNode = new TreeNode(displayName)
                    {
                        Tag = device // Store device info for selection
                    };
                    parentNode.Nodes.Add(childNode);
                }

                _treeViewDevices.Nodes.Add(parentNode);
                // Nodes are collapsed by default
            }

            // Auto-check first device if only one device total
            if (_instanceInfo.HardwareDevices.Count == 1)
            {
                var firstDeviceNode = _treeViewDevices.Nodes[0].Nodes[0];
                firstDeviceNode.Checked = true;
                _treeViewDevices.Focus();
            }

            UpdateSelectionStatus();

            Logger.LogInfo($"Populated hardware device tree with {_instanceInfo.HardwareDevices.Count} devices " +
                          $"in {sortedDeviceTypes.Count} device types from project: {_instanceInfo.ProjectName}");
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error populating hardware devices: {ex.Message}", false);
            MessageBox.Show($"Error loading hardware devices: {ex.Message}", "Error",
                          MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void TreeView_BeforeCheck(object sender, TreeViewCancelEventArgs e)
    {
        // Allow all check operations - we'll handle parent node logic in AfterCheck
    }

    private bool _updatingCheckboxes; // Flag to prevent recursive events

    private void TreeView_AfterCheck(object sender, TreeViewEventArgs e)
    {
        if (_updatingCheckboxes) return; // Prevent recursive calls

        try
        {
            _updatingCheckboxes = true;

            // If a parent node (device type) was checked/unchecked, apply to all children
            if (e.Node.Tag is string) // Parent nodes have Tag = device type string
            {
                // Force all children to match parent state
                foreach (TreeNode childNode in e.Node.Nodes)
                {
                    childNode.Checked = e.Node.Checked;
                }
            }
            // If a child node was checked/unchecked, update parent state
            else if (e.Node.Parent != null)
            {
                UpdateParentNodeState(e.Node.Parent);
            }

            UpdateSelectionStatus();
        }
        finally
        {
            _updatingCheckboxes = false;
        }
    }

    private void TreeView_MouseDoubleClick(object sender, MouseEventArgs e)
    {
        // Disable all double-click behavior to prevent interference with checkbox events
        // Do nothing - this effectively cancels the double click
    }

    private void ButtonConfigureExport_Click(object sender, EventArgs e)
    {
        try
        {
            UpdateSelectedDevices();

            if (SelectedDevices.Count == 0)
            {
                MessageBox.Show("Please select at least one hardware device to export.", "No Selection",
                              MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            Logger.LogInfo($"Hardware devices selected for configuration: {SelectedDevices.Count} devices");

            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error processing device selection: {ex.Message}", false);
            MessageBox.Show($"Error processing selection: {ex.Message}", "Selection Error",
                          MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ButtonSelectAll_Click(object sender, EventArgs e)
    {
        try
        {
            _updatingCheckboxes = true;

            foreach (TreeNode parentNode in _treeViewDevices.Nodes)
            {
                parentNode.Checked = true;
                foreach (TreeNode childNode in parentNode.Nodes)
                {
                    if (childNode.Tag is HardwareDeviceInfo)
                    {
                        childNode.Checked = true;
                    }
                }
            }

            UpdateSelectionStatus();
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error selecting all devices: {ex.Message}", false);
        }
        finally
        {
            _updatingCheckboxes = false;
        }
    }

    private void ButtonSelectNone_Click(object sender, EventArgs e)
    {
        try
        {
            _updatingCheckboxes = true;

            foreach (TreeNode parentNode in _treeViewDevices.Nodes)
            {
                parentNode.Checked = false;
                foreach (TreeNode childNode in parentNode.Nodes)
                {
                    if (childNode.Tag is HardwareDeviceInfo)
                    {
                        childNode.Checked = false;
                    }
                }
            }

            UpdateSelectionStatus();
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error deselecting all devices: {ex.Message}", false);
        }
        finally
        {
            _updatingCheckboxes = false;
        }
    }

    private void ButtonCancel_Click(object sender, EventArgs e)
    {
        DialogResult = DialogResult.Cancel;
        Close();
    }

    private void UpdateSelectionStatus()
    {
        try
        {
            UpdateSelectedDevices();

            var selectedCount = SelectedDevices.Count;
            _labelSelectionStatus.Text = selectedCount switch
            {
                0 => "No devices selected",
                1 => "1 device selected",
                _ => $"{selectedCount} devices selected"
            };

            _buttonConfigureExport.Enabled = selectedCount > 0;
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error updating selection status: {ex.Message}", false);
            _labelSelectionStatus.Text = "Error updating status";
            _buttonConfigureExport.Enabled = false;
        }
    }

    private void UpdateSelectedDevices()
    {
        try
        {
            SelectedDevices.Clear();

            foreach (TreeNode parentNode in _treeViewDevices.Nodes)
            {
                foreach (TreeNode childNode in parentNode.Nodes)
                {
                    if (childNode.Checked && childNode.Tag is HardwareDeviceInfo deviceInfo)
                    {
                        SelectedDevices.Add(deviceInfo);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error updating selected devices: {ex.Message}", false);
            SelectedDevices.Clear();
        }
    }

    /// <summary>
    /// Updates parent node checked state based on children
    /// </summary>
    private void UpdateParentNodeState(TreeNode parentNode)
    {
        if (parentNode == null || !(parentNode.Tag is string)) return; // Only for parent nodes (Tag is device type string)

        var checkedChildren = 0;
        var totalChildren = parentNode.Nodes.Count;

        foreach (TreeNode childNode in parentNode.Nodes)
        {
            if (childNode.Checked)
                checkedChildren++;
        }

        // Update parent state without triggering events
        if (checkedChildren == 0)
        {
            parentNode.Checked = false;
        }
        else if (checkedChildren == totalChildren)
        {
            parentNode.Checked = true;
        }
        else
        {
            // Some children checked, some not - could show intermediate state
            // For now, just uncheck parent
            parentNode.Checked = false;
        }
    }

    private void ButtonExpandAll_Click(object sender, EventArgs e)
    {
        // Save current scroll position before expanding
        var currentTopNode = _treeViewDevices.TopNode;

        _treeViewDevices.BeginUpdate();
        _treeViewDevices.ExpandAll();
        _treeViewDevices.EndUpdate();

        // Restore scroll position
        if (currentTopNode != null)
        {
            try
            {
                _treeViewDevices.TopNode = currentTopNode;
            }
            catch
            {
                // Fallback - if TopNode setting fails, do nothing
            }
        }
    }

    private void ButtonCollapseAll_Click(object sender, EventArgs e)
    {
        _treeViewDevices.CollapseAll();
    }

    private void TextBoxSearch_TextChanged(object sender, EventArgs e)
    {
        _currentSearchFilter = _textBoxSearch.Text.Trim();
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        // Save current expanded state before updating
        var expandedPaths = new HashSet<string>();
        SaveNodeExpandedState(_treeViewDevices.Nodes, "", expandedPaths);

        _treeViewDevices.BeginUpdate();

        // Rebuild tree with filtered nodes
        RebuildTreeWithFilter();

        // Restore expanded state
        if (expandedPaths.Count > 0)
        {
            RestoreNodeExpandedState(_treeViewDevices.Nodes, "", expandedPaths);
        }

        _treeViewDevices.EndUpdate();

        // Scroll to top
        if (_treeViewDevices.Nodes.Count > 0)
        {
            _treeViewDevices.TopNode = _treeViewDevices.Nodes[0];
        }

        UpdateSelectionStatus();
    }

    private void SaveNodeExpandedState(TreeNodeCollection nodes, string parentPath, HashSet<string> expandedPaths)
    {
        foreach (TreeNode node in nodes)
        {
            // For parent nodes, use Tag (device type string); for child nodes, use display name
            string nodeIdentifier = node.Tag is string deviceType ? deviceType : node.Text;
            string nodePath = string.IsNullOrEmpty(parentPath) ? nodeIdentifier : parentPath + "/" + nodeIdentifier;

            if (node.IsExpanded)
            {
                expandedPaths.Add(nodePath);
            }

            if (node.Nodes.Count > 0)
            {
                SaveNodeExpandedState(node.Nodes, nodePath, expandedPaths);
            }
        }
    }

    private void RestoreNodeExpandedState(TreeNodeCollection nodes, string parentPath, HashSet<string> expandedPaths)
    {
        foreach (TreeNode node in nodes)
        {
            // For parent nodes, use Tag (device type string); for child nodes, use display name
            string nodeIdentifier = node.Tag is string deviceType ? deviceType : node.Text;
            string nodePath = string.IsNullOrEmpty(parentPath) ? nodeIdentifier : parentPath + "/" + nodeIdentifier;

            if (expandedPaths.Contains(nodePath))
            {
                node.Expand();
            }

            if (node.Nodes.Count > 0)
            {
                RestoreNodeExpandedState(node.Nodes, nodePath, expandedPaths);
            }
        }
    }

    private void RebuildTreeWithFilter()
    {
        // Save current checked state
        var checkedDeviceIds = new HashSet<string>();
        foreach (TreeNode parentNode in _treeViewDevices.Nodes)
        {
            foreach (TreeNode childNode in parentNode.Nodes)
            {
                if (childNode.Checked && childNode.Tag is HardwareDeviceInfo device)
                {
                    checkedDeviceIds.Add(device.Name + "|" + device.DeviceType);
                }
            }
        }

        // Clear and rebuild
        _treeViewDevices.Nodes.Clear();

        if (_instanceInfo.HardwareDevices == null || _instanceInfo.HardwareDevices.Count == 0)
            return;

        // Group devices by type
        var deviceGroups = new Dictionary<string, List<HardwareDeviceInfo>>();
        foreach (var device in _instanceInfo.HardwareDevices)
        {
            var deviceType = device.DeviceType ?? "Unknown";
            if (!deviceGroups.ContainsKey(deviceType))
                deviceGroups[deviceType] = new List<HardwareDeviceInfo>();
            deviceGroups[deviceType].Add(device);
        }

        var sortedDeviceTypes = deviceGroups.Keys.OrderBy(k => k).ToList();

        foreach (var deviceType in sortedDeviceTypes)
        {
            var devices = deviceGroups[deviceType];
            devices.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));

            // Filter devices based on search text
            var filteredDevices = string.IsNullOrWhiteSpace(_currentSearchFilter)
                ? devices
                : devices.Where(d =>
                {
                    var displayName = !string.IsNullOrWhiteSpace(d.ItemName) ? d.ItemName : d.Name;
                    return displayName.IndexOf(_currentSearchFilter, StringComparison.OrdinalIgnoreCase) >= 0;
                }).ToList();

            // Only add device type node if it has matching devices
            if (filteredDevices.Count > 0)
            {
                var parentNode = new TreeNode($"{deviceType} ({filteredDevices.Count})")
                {
                    Tag = deviceType // Store device type for identification
                };

                foreach (var device in filteredDevices)
                {
                    var displayName = !string.IsNullOrWhiteSpace(device.ItemName) ? device.ItemName : device.Name;
                    var childNode = new TreeNode(displayName)
                    {
                        Tag = device
                    };

                    // Restore checked state
                    var deviceId = device.Name + "|" + device.DeviceType;
                    childNode.Checked = checkedDeviceIds.Contains(deviceId);

                    parentNode.Nodes.Add(childNode);
                }

                // Update parent checked state based on children
                _updatingCheckboxes = true;
                UpdateParentNodeState(parentNode);
                _updatingCheckboxes = false;

                _treeViewDevices.Nodes.Add(parentNode);
            }
        }
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);

        // Focus the search box for immediate filtering
        _textBoxSearch.Focus();

        // Set up keyboard shortcuts
        this.KeyPreview = true;
        this.KeyDown += (sender, args) =>
        {
            if (args.KeyCode == Keys.Enter && _buttonConfigureExport.Enabled)
            {
                ButtonConfigureExport_Click(sender, args);
                args.Handled = true;
            }
            else if (args.KeyCode == Keys.Escape)
            {
                ButtonCancel_Click(sender, args);
                args.Handled = true;
            }
        };
    }
}