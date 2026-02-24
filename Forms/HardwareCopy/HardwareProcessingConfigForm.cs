using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using OpennessCopy.Models;
using OpennessCopy.Utils;
using Siemens.Engineering.HW;

namespace OpennessCopy.Forms.HardwareCopy;

/// <summary>
/// Form for configuring hardware device name find/replace transformations
/// Allows users to set up find/replace pairs for device names before AML export
/// </summary>
public class HardwareProcessingConfigForm : Form
{
    public List<FindReplacePair> DeviceNameFindReplacePairs { get; private set; } = new List<FindReplacePair>();
    public int IpAddressOffset { get; private set; }
    public List<TagAddressReplacePair> ETAddressReplacements { get; private set; } = new List<TagAddressReplacePair>();
    public IoSystemInfo SelectedIoSystem { get; private set; }

    private readonly List<HardwareDeviceInfo> _selectedDevices;
    private readonly HashSet<string> _existingDeviceNames;
    private readonly Dictionary<string, string> _existingIpAddresses;
    private readonly Dictionary<int, Dictionary<int, string>> _existingDeviceNumbers; // Key: IoSystemHash
    // Dictionary structure: PLC Name -> Address Type -> Address -> Device.Module Name
    private readonly Dictionary<string, Dictionary<AddressIoType, Dictionary<int, string>>> _existingAddressesByPlc;
    // Cached conflict detection results
    private List<string> _currentNamingConflicts = new List<string>();
    private List<string> _currentIpConflicts = new List<string>();
    private List<string> _currentETConflicts = new List<string>();

    private readonly List<IoSystemInfo> _ioSystems;

    private Label _labelTitle;
    private Label _labelSelectedDevices;
    private GroupBox _groupBoxDeviceNames;
    private Label _labelDeviceNameInstructions;
    private Label _labelFindReplacePairs;
    private DataGridView _dataGridViewPairs;
    private Button _buttonAddPair;
    private Button _buttonRemovePair;
    private GroupBox _groupBoxIpConfig;
    private Label _labelIpInstructions;
    private Label _labelIpAddressOffset;
    private NumericUpDown _numericUpDownIpOffset;
    private Label _labelIoSystem;
    private ComboBox _comboBoxIoSystem;
    private GroupBox _groupBoxETModules;
    private Label _labelETModulesInstructions;
    private Button _buttonConfigureETModules;
    private Label _labelPreviewTitle;
    private TextBox _textBoxPreview;
    private Button _buttonOk;
    private Button _buttonCancel;

    public HardwareProcessingConfigForm(List<HardwareDeviceInfo> selectedDevices, HashSet<string> existingDeviceNames,
        Dictionary<string, string> existingIpAddresses, Dictionary<int, Dictionary<int, string>> existingDeviceNumbers,
        Dictionary<string, Dictionary<AddressIoType, Dictionary<int, string>>> existingAddressesByPlc,
        List<IoSystemInfo> ioSystems)
    {
        _selectedDevices = selectedDevices ?? throw new ArgumentNullException(nameof(selectedDevices));
        _existingDeviceNames = existingDeviceNames ?? new HashSet<string>();
        _existingIpAddresses = existingIpAddresses ?? new Dictionary<string, string>();
        _existingDeviceNumbers = existingDeviceNumbers ?? new Dictionary<int, Dictionary<int, string>>();
        _existingAddressesByPlc = existingAddressesByPlc ?? new Dictionary<string, Dictionary<AddressIoType, Dictionary<int, string>>>();
        _ioSystems = ioSystems ?? new List<IoSystemInfo>();
        InitializeComponent();
        PopulateSelectedDevices();
        LoadIoSystems();
        UpdateETModulesButtonVisibility();
        UpdatePreview();
    }

    private void InitializeComponent()
    {
        this._labelTitle = new Label();
        this._labelSelectedDevices = new Label();
        this._groupBoxDeviceNames = new GroupBox();
        this._labelDeviceNameInstructions = new Label();
        this._labelFindReplacePairs = new Label();
        this._dataGridViewPairs = new DataGridView();
        this._buttonAddPair = new Button();
        this._buttonRemovePair = new Button();
        this._groupBoxIpConfig = new GroupBox();
        this._labelIpInstructions = new Label();
        this._labelIpAddressOffset = new Label();
        this._numericUpDownIpOffset = new NumericUpDown();
        this._labelIoSystem = new Label();
        this._comboBoxIoSystem = new ComboBox();
        this._groupBoxETModules = new GroupBox();
        this._labelETModulesInstructions = new Label();
        this._buttonConfigureETModules = new Button();
        this._labelPreviewTitle = new Label();
        this._textBoxPreview = new TextBox();
        this._buttonOk = new Button();
        this._buttonCancel = new Button();
        this._groupBoxDeviceNames.SuspendLayout();
        this._groupBoxIpConfig.SuspendLayout();
        this._groupBoxETModules.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)(this._dataGridViewPairs)).BeginInit();
        ((System.ComponentModel.ISupportInitialize)(this._numericUpDownIpOffset)).BeginInit();
        this.SuspendLayout();

        // Form properties
        this.Text = "Configure Hardware Device Export";
        this.ClientSize = new Size(960, 750);
        this.StartPosition = FormStartPosition.CenterParent;
        this.MinimizeBox = false;
        this.MaximizeBox = false;
        this.FormBorderStyle = FormBorderStyle.FixedDialog;

        // Title label
        this._labelTitle.Text = "Hardware Device Export Configuration";
        this._labelTitle.Location = new Point(20, 20);
        this._labelTitle.Size = new Size(300, 20);
        this._labelTitle.Font = new Font("Microsoft Sans Serif", 10F, FontStyle.Bold);
        this._labelTitle.AutoSize = true;

        // Selected devices label
        this._labelSelectedDevices.Text = $"Selected Devices: {_selectedDevices.Count} devices";
        this._labelSelectedDevices.Location = new Point(20, 50);
        this._labelSelectedDevices.Size = new Size(400, 20);
        this._labelSelectedDevices.AutoSize = true;

        // Device Names GroupBox
        this._groupBoxDeviceNames.Text = "Device Name Transformations";
        this._groupBoxDeviceNames.Location = new Point(20, 80);
        this._groupBoxDeviceNames.Size = new Size(420, 180);
        this._groupBoxDeviceNames.TabStop = false;

        // Device name instructions
        this._labelDeviceNameInstructions.Text = "Optional: Add find/replace pairs for device names. Replacements are applied in order.";
        this._labelDeviceNameInstructions.Location = new Point(15, 25);
        this._labelDeviceNameInstructions.Size = new Size(380, 26);
        this._labelDeviceNameInstructions.ForeColor = Color.Gray;
        this._labelDeviceNameInstructions.AutoSize = true;
        this._labelDeviceNameInstructions.MaximumSize = new Size(380, 0);

        // Find/replace pairs label
        this._labelFindReplacePairs.Text = "Find/Replace Pairs:";
        this._labelFindReplacePairs.Location = new Point(15, 60);
        this._labelFindReplacePairs.Size = new Size(110, 13);
        this._labelFindReplacePairs.AutoSize = true;

        // DataGridView for find/replace pairs (smaller)
        this._dataGridViewPairs.AllowUserToResizeRows = false;
        this._dataGridViewPairs.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
        this._dataGridViewPairs.Location = new Point(15, 80);
        this._dataGridViewPairs.Name = "_dataGridViewPairs";
        this._dataGridViewPairs.Size = new Size(320, 80);
        this._dataGridViewPairs.TabIndex = 5;
        this._dataGridViewPairs.CellValueChanged += DataGridViewPairs_CellValueChanged;
        this._dataGridViewPairs.CurrentCellDirtyStateChanged += DataGridViewPairs_CurrentCellDirtyStateChanged;
        this._dataGridViewPairs.ScrollBars = ScrollBars.Vertical;

        // Add columns to DataGridView
        var findColumn = new DataGridViewTextBoxColumn();
        findColumn.Name = "findColumn";
        findColumn.HeaderText = "Find";
        findColumn.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
        findColumn.FillWeight = 50;
        this._dataGridViewPairs.Columns.Add(findColumn);

        var replaceColumn = new DataGridViewTextBoxColumn();
        replaceColumn.Name = "replaceColumn";
        replaceColumn.HeaderText = "Replace With";
        replaceColumn.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
        replaceColumn.FillWeight = 50;
        this._dataGridViewPairs.Columns.Add(replaceColumn);

        // Add pair button
        this._buttonAddPair.Text = "Add";
        this._buttonAddPair.Location = new Point(350, 80);
        this._buttonAddPair.Size = new Size(60, 25);
        this._buttonAddPair.UseVisualStyleBackColor = true;
        this._buttonAddPair.Click += ButtonAddPair_Click;

        // Remove pair button
        this._buttonRemovePair.Text = "Remove";
        this._buttonRemovePair.Location = new Point(350, 110);
        this._buttonRemovePair.Size = new Size(60, 25);
        this._buttonRemovePair.UseVisualStyleBackColor = true;
        this._buttonRemovePair.Click += ButtonRemovePair_Click;

        // Add controls to Device Names GroupBox
        this._groupBoxDeviceNames.Controls.Add(this._labelDeviceNameInstructions);
        this._groupBoxDeviceNames.Controls.Add(this._labelFindReplacePairs);
        this._groupBoxDeviceNames.Controls.Add(this._dataGridViewPairs);
        this._groupBoxDeviceNames.Controls.Add(this._buttonAddPair);
        this._groupBoxDeviceNames.Controls.Add(this._buttonRemovePair);

        // IP Configuration GroupBox
        this._groupBoxIpConfig.Text = "IP Address Configuration";
        this._groupBoxIpConfig.Location = new Point(20, 280);
        this._groupBoxIpConfig.Size = new Size(420, 120);
        this._groupBoxIpConfig.TabStop = false;

        // IP instructions
        this._labelIpInstructions.Text = "Configure offset to apply to the last byte of IP addresses (e.g., 192.168.1.100 -> 192.168.1.105 with offset +5).";
        this._labelIpInstructions.Location = new Point(15, 25);
        this._labelIpInstructions.Size = new Size(380, 26);
        this._labelIpInstructions.ForeColor = Color.Gray;
        this._labelIpInstructions.AutoSize = true;
        this._labelIpInstructions.MaximumSize = new Size(380, 0);

        // IP Address Offset label
        this._labelIpAddressOffset.Text = "IP Address Offset (last byte):";
        this._labelIpAddressOffset.Location = new Point(15, 70);
        this._labelIpAddressOffset.Size = new Size(150, 20);
        this._labelIpAddressOffset.AutoSize = true;

        // IP Address Offset numeric control
        this._numericUpDownIpOffset.Location = new Point(180, 68);
        this._numericUpDownIpOffset.Size = new Size(80, 23);
        this._numericUpDownIpOffset.Minimum = -255;
        this._numericUpDownIpOffset.Maximum = 255;
        this._numericUpDownIpOffset.Value = 0;
        this._numericUpDownIpOffset.ValueChanged += NumericUpDownIpOffset_ValueChanged;
        
        // Add text changed event for real-time updates while typing
        this._numericUpDownIpOffset.Controls[1].TextChanged += (_, _) => UpdatePreview();

        // IoSystem label
        this._labelIoSystem.Text = "IO System:";
        this._labelIoSystem.Location = new Point(15, 100);
        this._labelIoSystem.Size = new Size(150, 20);
        this._labelIoSystem.AutoSize = true;

        // IoSystem ComboBox
        this._comboBoxIoSystem.Location = new Point(180, 98);
        this._comboBoxIoSystem.Size = new Size(220, 23);
        this._comboBoxIoSystem.DropDownStyle = ComboBoxStyle.DropDownList;
        this._comboBoxIoSystem.SelectedIndexChanged += ComboBoxIoSystem_SelectedIndexChanged;

        // Add controls to IP Config GroupBox
        this._groupBoxIpConfig.Controls.Add(this._labelIpInstructions);
        this._groupBoxIpConfig.Controls.Add(this._labelIpAddressOffset);
        this._groupBoxIpConfig.Controls.Add(this._numericUpDownIpOffset);
        this._groupBoxIpConfig.Controls.Add(this._labelIoSystem);
        this._groupBoxIpConfig.Controls.Add(this._comboBoxIoSystem);


        // ET Modules GroupBox
        this._groupBoxETModules.Text = "ET Module Address Configuration";
        this._groupBoxETModules.Location = new Point(20, 420);
        this._groupBoxETModules.Size = new Size(420, 80);
        this._groupBoxETModules.TabStop = false;

        // ET modules instructions
        this._labelETModulesInstructions.Text = "Optional: Configure address transformations for ET module start addresses (I/O addresses).";
        this._labelETModulesInstructions.Location = new Point(15, 25);
        this._labelETModulesInstructions.Size = new Size(380, 26);
        this._labelETModulesInstructions.ForeColor = Color.Gray;
        this._labelETModulesInstructions.AutoSize = true;
        this._labelETModulesInstructions.MaximumSize = new Size(380, 0);

        // Configure ET Modules button
        this._buttonConfigureETModules.Text = "Configure ET Modules...";
        this._buttonConfigureETModules.Location = new Point(15, 50);
        this._buttonConfigureETModules.Size = new Size(200, 25);
        this._buttonConfigureETModules.UseVisualStyleBackColor = true;
        this._buttonConfigureETModules.Click += ButtonConfigureETModules_Click;

        // Add controls to ET Modules GroupBox
        this._groupBoxETModules.Controls.Add(this._labelETModulesInstructions);
        this._groupBoxETModules.Controls.Add(this._buttonConfigureETModules);

        // Preview title label
        this._labelPreviewTitle.Text = "Preview:";
        this._labelPreviewTitle.Location = new Point(460, 80);
        this._labelPreviewTitle.Size = new Size(55, 13);
        this._labelPreviewTitle.Font = new Font(this._labelPreviewTitle.Font, FontStyle.Bold);
        this._labelPreviewTitle.AutoSize = true;

        // Preview text box
        this._textBoxPreview.Location = new Point(460, 100);
        this._textBoxPreview.Size = new Size(480, 500);
        this._textBoxPreview.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        this._textBoxPreview.Multiline = true;
        this._textBoxPreview.ScrollBars = ScrollBars.Vertical;
        this._textBoxPreview.ReadOnly = true;
        this._textBoxPreview.BackColor = SystemColors.Control;
        this._textBoxPreview.Font = new Font("Consolas", 8.25F);
        this._textBoxPreview.Text = "Preview will appear here...";

        // OK button
        this._buttonOk.Text = "OK";
        this._buttonOk.Location = new Point(780, 700);
        this._buttonOk.Size = new Size(75, 30);
        this._buttonOk.TabIndex = 6;
        this._buttonOk.UseVisualStyleBackColor = true;
        this._buttonOk.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
        this._buttonOk.Click += ButtonOK_Click;

        // Cancel button
        this._buttonCancel.Text = "Cancel";
        this._buttonCancel.Location = new Point(865, 700);
        this._buttonCancel.Size = new Size(75, 30);
        this._buttonCancel.TabIndex = 7;
        this._buttonCancel.UseVisualStyleBackColor = true;
        this._buttonCancel.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
        this._buttonCancel.Click += ButtonCancel_Click;

        // Add controls to form
        this.Controls.Add(this._labelTitle);
        this.Controls.Add(this._labelSelectedDevices);
        this.Controls.Add(this._groupBoxDeviceNames);
        this.Controls.Add(this._groupBoxIpConfig);
        this.Controls.Add(this._groupBoxETModules);
        this.Controls.Add(this._labelPreviewTitle);
        this.Controls.Add(this._textBoxPreview);
        this.Controls.Add(this._buttonOk);
        this.Controls.Add(this._buttonCancel);

        this._groupBoxDeviceNames.ResumeLayout(false);
        this._groupBoxDeviceNames.PerformLayout();
        this._groupBoxIpConfig.ResumeLayout(false);
        this._groupBoxIpConfig.PerformLayout();
        this._groupBoxETModules.ResumeLayout(false);
        this._groupBoxETModules.PerformLayout();
        ((System.ComponentModel.ISupportInitialize)(this._dataGridViewPairs)).EndInit();
        ((System.ComponentModel.ISupportInitialize)(this._numericUpDownIpOffset)).EndInit();
        this.ResumeLayout(false);
        this.PerformLayout();
    }

    private void PopulateSelectedDevices()
    {
        try
        {
            var deviceNames = _selectedDevices.Select(d => !string.IsNullOrWhiteSpace(d.ItemName) ? d.ItemName : d.Name).ToList();
            var deviceSummary = deviceNames.Count <= 5
                ? string.Join(", ", deviceNames)
                : $"{string.Join(", ", deviceNames.Take(3))}, ... and {deviceNames.Count - 3} more";

            _labelSelectedDevices.Text = $"Selected Devices ({_selectedDevices.Count}): {deviceSummary}";
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error populating selected devices: {ex.Message}", false);
            _labelSelectedDevices.Text = $"Selected Devices: {_selectedDevices.Count} devices";
        }
    }


    private void DataGridViewPairs_CellValueChanged(object sender, DataGridViewCellEventArgs e)
    {
        UpdateFindReplacePairsFromGrid();
        UpdatePreview();
    }

    private void DataGridViewPairs_CurrentCellDirtyStateChanged(object sender, EventArgs e)
    {
        if (_dataGridViewPairs.IsCurrentCellDirty)
        {
            _dataGridViewPairs.CommitEdit(DataGridViewDataErrorContexts.Commit);
        }
    }

    private void ButtonAddPair_Click(object sender, EventArgs e)
    {
        _dataGridViewPairs.Rows.Add("", "");
        UpdateFindReplacePairsFromGrid();
        UpdatePreview();
    }

    private void ButtonRemovePair_Click(object sender, EventArgs e)
    {
        if (_dataGridViewPairs.SelectedRows.Count > 0)
        {
            int selectedIndex = _dataGridViewPairs.SelectedRows[0].Index;
            if (selectedIndex < _dataGridViewPairs.Rows.Count - 1) // Don't remove the new row
            {
                _dataGridViewPairs.Rows.RemoveAt(selectedIndex);
                UpdateFindReplacePairsFromGrid();
                UpdatePreview();
            }
        }
    }

    private void ButtonOK_Click(object sender, EventArgs e)
    {
        try
        {
            // Validate find/replace pairs
            UpdateFindReplacePairsFromGrid();

            foreach (var pair in DeviceNameFindReplacePairs)
            {
                if (!string.IsNullOrWhiteSpace(pair.FindString) && pair.ReplaceString == null)
                {
                    MessageBox.Show($"If you specify a 'Find' string ('{pair.FindString}'), you must also specify a 'Replace' string (can be empty to remove text).",
                        "Invalid Configuration", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    _dataGridViewPairs.Focus();
                    return;
                }
            }

            // Check for naming conflicts
            var conflicts = GetNamingConflicts();
            if (conflicts.Count > 0)
            {
                var conflictNames = string.Join(", ", conflicts.Take(5));
                if (conflicts.Count > 5)
                    conflictNames += $", and {conflicts.Count - 5} more";

                MessageBox.Show($"Naming conflicts detected! The following device names would be duplicated after transformation:\n\n{conflictNames}\n\nPlease adjust your find/replace rules to resolve all conflicts.",
                    "Naming Conflicts", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _dataGridViewPairs.Focus();
                return;
            }

            // Capture selections
            IpAddressOffset = (int)_numericUpDownIpOffset.Value;

            var selectedIoSystemItem = _comboBoxIoSystem.SelectedItem;
            var ioSystemInfo = selectedIoSystemItem?.GetType().GetProperty("Data")?.GetValue(selectedIoSystemItem) as IoSystemInfo;
            SelectedIoSystem = ioSystemInfo;

            Logger.LogInfo($"Hardware processing configuration confirmed with {DeviceNameFindReplacePairs.Count} find/replace pairs, IP offset {IpAddressOffset}, and IoSystem: {ioSystemInfo?.Name ?? "None"}");

            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error processing configuration: {ex.Message}", false);
            MessageBox.Show($"Error processing configuration: {ex.Message}", "Configuration Error",
                          MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ButtonCancel_Click(object sender, EventArgs e)
    {
        DialogResult = DialogResult.Cancel;
        Close();
    }

    private void ButtonConfigureETModules_Click(object sender, EventArgs e)
    {
        try
        {
            // Get only ET devices from selected devices
            var etDevices = _selectedDevices.Where(d => d.IsETDevice).ToList();

            if (etDevices.Count == 0)
            {
                MessageBox.Show("No ET modules found in selected devices.", "No ET Modules",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var selectedIoSystemItem = _comboBoxIoSystem.SelectedItem;
            var ioSystemInfo = selectedIoSystemItem?.GetType().GetProperty("Data")?.GetValue(selectedIoSystemItem) as IoSystemInfo;
            
            using var etConfigForm = new ETModuleConfigurationForm(etDevices, _existingAddressesByPlc, ioSystemInfo?.ControllingPlcName);
            // Load existing configuration if any
            if (ETAddressReplacements.Count > 0)
            {
                etConfigForm.LoadConfiguration(ETAddressReplacements);
            }

            if (etConfigForm.ShowDialog(this) == DialogResult.OK)
            {
                ETAddressReplacements.Clear();
                ETAddressReplacements.AddRange(etConfigForm.ETAddressReplacements);

                Logger.LogInfo($"ET module configuration updated: {ETAddressReplacements.Count} address transformations configured");
                UpdatePreview(); // Update preview to show new configuration
            }
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error opening ET module configuration: {ex.Message}");
            MessageBox.Show($"Error opening ET module configuration: {ex.Message}", "Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void LoadIoSystems()
    {
        try
        {
            _comboBoxIoSystem.Items.Clear();

            foreach (var ioSystem in _ioSystems)
            {
                var displayText = $"{ioSystem.Name} ({ioSystem.SubnetName}) - {ioSystem.NetworkAddress}";
                _comboBoxIoSystem.Items.Add(new { Display = displayText, Data = ioSystem });
            }

            _comboBoxIoSystem.DisplayMember = "Display";

            if (_ioSystems.Count > 0)
            {
                _comboBoxIoSystem.SelectedIndex = 0;
            }

            Logger.LogInfo($"Populated {_ioSystems.Count} IoSystem(s) in dropdown");
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error populating IoSystems dropdown: {ex.Message}", false);
            MessageBox.Show($"Error populating IoSystems dropdown: {ex.Message}", "Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ComboBoxIoSystem_SelectedIndexChanged(object sender, EventArgs e)
    {
        UpdatePreview();
    }

    private void UpdateETModulesButtonVisibility()
    {
        try
        {
            var etDevices = _selectedDevices.Where(d => d.IsETDevice).ToList();
            var hasETDevices = etDevices.Count > 0;

            // Show/hide the entire ET modules groupbox based on whether ET devices are present
            _groupBoxETModules.Visible = hasETDevices;

            if (hasETDevices)
            {
                var totalModules = etDevices.SelectMany(d => d.AddressModules).Count();
                _buttonConfigureETModules.Text = $"Configure ET Modules ({totalModules} modules)";
                Logger.LogInfo($"ET modules detected: {etDevices.Count} ET devices with {totalModules} total modules");
            }
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error updating ET modules button visibility: {ex.Message}", false);
            _groupBoxETModules.Visible = false;
        }
    }

    private void UpdateFindReplacePairsFromGrid()
    {
        DeviceNameFindReplacePairs.Clear();
        foreach (DataGridViewRow row in _dataGridViewPairs.Rows)
        {
            if (row.IsNewRow) continue;

            var findString = row.Cells[0].Value?.ToString() ?? "";
            var replaceString = row.Cells[1].Value?.ToString() ?? "";

            if (!string.IsNullOrWhiteSpace(findString) || !string.IsNullOrWhiteSpace(replaceString))
            {
                DeviceNameFindReplacePairs.Add(new FindReplacePair(findString, replaceString));
            }
        }
    }

    /// <summary>
    /// Creates a new IP address using network address, subnet mask, and offset
    /// Matches the calculation logic in STAHardwareDataExtractor.CalculateNewIp
    /// </summary>
    private string CalculateNewIp(string originalIp, string networkAddress, string subnetMask, int offset)
    {
        try
        {
            var originalParts = originalIp.Split('.');
            var networkParts = networkAddress.Split('.');
            var maskParts = subnetMask.Split('.');

            if (originalParts.Length != 4 || networkParts.Length != 4 || maskParts.Length != 4)
            {
                return originalIp;
            }

            var newParts = new int[4];

            for (int i = 0; i < 4; i++)
            {
                int mask = int.Parse(maskParts[i]);
                int network = int.Parse(networkParts[i]);
                int originalHost = int.Parse(originalParts[i]);

                if (mask == 255)
                {
                    // Network portion - use network address
                    newParts[i] = network;
                }
                else
                {
                    // Host portion - apply offset to original host value
                    int hostMask = 255 - mask;
                    int hostValue = originalHost & hostMask;
                    newParts[i] = network + ((hostValue + offset) & hostMask);
                }
            }

            return $"{newParts[0]}.{newParts[1]}.{newParts[2]}.{newParts[3]}";
        }
        catch
        {
            return originalIp;
        }
    }

    /// <summary>
    /// Validates IP addresses and device numbers, checking for conflicts and bounds
    /// Uses selected IoSystem's network address and subnet mask for IP calculation
    /// Only checks device number conflicts within the same IoSystem (by hash)
    /// </summary>
    private List<string> GetIpAddressConflicts()
    {
        var conflicts = new List<string>();
        var offset = (int)_numericUpDownIpOffset.Value;

        // Get selected IoSystem information
        var selectedIoSystemItem = _comboBoxIoSystem.SelectedItem;
        var selectedIoSystem = selectedIoSystemItem?.GetType().GetProperty("Data")?.GetValue(selectedIoSystemItem) as IoSystemInfo;

        if (selectedIoSystem == null)
        {
            conflicts.Add("No IoSystem selected - cannot validate IP addresses and device numbers");
            return conflicts;
        }

        var networkAddress = selectedIoSystem.NetworkAddress;
        var subnetMask = selectedIoSystem.SubnetMask;

        if (string.IsNullOrWhiteSpace(networkAddress) || string.IsNullOrWhiteSpace(subnetMask))
        {
            conflicts.Add("Selected IoSystem has invalid network configuration");
            return conflicts;
        }

        var modifiedIpAddresses = new HashSet<string>();
        var modifiedDeviceNumbers = new HashSet<int>();

        foreach (var device in _selectedDevices)
        {
            var deviceName = !string.IsNullOrWhiteSpace(device.ItemName) ? device.ItemName : device.Name;

            foreach (var ipAddress in device.IpAddresses)
            {
                // Calculate new IP using network address and subnet mask
                var newIpAddress = CalculateNewIp(ipAddress, networkAddress, subnetMask, offset);

                if (newIpAddress == ipAddress && offset != 0)
                {
                    conflicts.Add($"{deviceName}: IP calculation failed for '{ipAddress}'");
                    continue;
                }

                // Validate IP format and bounds
                var parts = newIpAddress.Split('.');
                if (parts.Length != 4)
                {
                    conflicts.Add($"{deviceName}: Invalid IP format '{newIpAddress}'");
                    continue;
                }

                bool invalidOctet = false;
                for (int i = 0; i < 4; i++)
                {
                    if (!int.TryParse(parts[i], out int octet) || octet < 0 || octet > 255)
                    {
                        conflicts.Add($"{deviceName}: IP {newIpAddress} has invalid octet at position {i + 1}");
                        invalidOctet = true;
                        break;
                    }
                }
                if (invalidOctet) continue;

                // Check IP conflicts with existing devices
                if (_existingIpAddresses.TryGetValue(newIpAddress, out var existingDevice))
                {
                    conflicts.Add($"{deviceName}: New IP {newIpAddress} conflicts with existing device '{existingDevice}'");
                }

                // Check IP conflicts with other selected devices
                if (!modifiedIpAddresses.Add(newIpAddress))
                {
                    conflicts.Add($"{deviceName}: New IP {newIpAddress} conflicts with another selected device");
                }

                // Calculate device number from IP (last byte)
                if (int.TryParse(parts[3], out int deviceNumber))
                {
                    // Check device number conflicts ONLY within the selected IoSystem (by hash)
                    if (_existingDeviceNumbers.TryGetValue(selectedIoSystem.IoSystemHash, out var ioSystemDeviceNumbers))
                    {
                        if (ioSystemDeviceNumbers.TryGetValue(deviceNumber, out var conflictingDevice))
                        {
                            conflicts.Add($"{deviceName}: New device number {deviceNumber} conflicts with existing device '{conflictingDevice}' in IoSystem '{selectedIoSystem.Name}'");
                        }
                    }

                    // Check device number conflicts with other selected devices
                    if (!modifiedDeviceNumbers.Add(deviceNumber))
                    {
                        conflicts.Add($"{deviceName}: New device number {deviceNumber} conflicts with another selected device");
                    }
                }
            }
        }

        return conflicts;
    }

    /// <summary>
    /// Validates ET module addresses, checking for conflicts and transformation issues
    /// Uses PLC-scoped conflict checking - checks addresses against the TARGET IoSystem's controlling PLC
    /// </summary>
    private List<string> GetETAddressConflicts()
    {
        var conflicts = new List<string>();

        // Get selected IoSystem's controlling PLC name (from TARGET project)
        var selectedIoSystemItem = _comboBoxIoSystem.SelectedItem;
        var selectedIoSystem = selectedIoSystemItem?.GetType().GetProperty("Data")?.GetValue(selectedIoSystemItem) as IoSystemInfo;

        if (selectedIoSystem == null)
        {
            // Can't validate without knowing target PLC
            return conflicts;
        }

        var targetPlcName = selectedIoSystem.ControllingPlcName;
        if (string.IsNullOrWhiteSpace(targetPlcName))
        {
            // No controlling PLC found for target IoSystem
            return conflicts;
        }

        // Track addresses from selected devices to detect conflicts among them
        var selectedETAddresses = new Dictionary<AddressIoType, HashSet<int>>();

        // Check all ET devices (regardless of their source PLC) against the TARGET PLC
        foreach (var device in _selectedDevices.Where(d => d.IsETDevice))
        {
            var deviceName = !string.IsNullOrWhiteSpace(device.ItemName) ? device.ItemName : device.Name;

            foreach (var module in device.AddressModules)
            {
                foreach (var addressInfo in module.AddressInfos)
                {
                    // Apply transformations to get new start address
                    int transformedStartAddress = ApplyETTransformations(addressInfo.StartAddress);

                    // Calculate how many addresses this I/O occupies (length is in bits, convert to bytes)
                    int addressCount = (addressInfo.Length + 7) / 8; // Convert bits to bytes (round up)

                    // Check each address that this I/O occupies
                    for (int offset = 0; offset < addressCount; offset++)
                    {
                        int address = transformedStartAddress + offset;

                        // Check against existing TARGET project addresses for the TARGET PLC
                        if (_existingAddressesByPlc.TryGetValue(targetPlcName, out var plcAddresses) &&
                            plcAddresses.ContainsKey(addressInfo.Type) &&
                            plcAddresses[addressInfo.Type].ContainsKey(address))
                        {
                            var conflictingModule = plcAddresses[addressInfo.Type][address];
                            conflicts.Add($"{deviceName}.{module.ModuleName}: Address {address} ({addressInfo.Type}) conflicts with existing module '{conflictingModule}' (Target PLC: {targetPlcName})");
                        }

                        // Check against other selected devices (they'll all connect to same TARGET PLC)
                        if (!selectedETAddresses.ContainsKey(addressInfo.Type))
                            selectedETAddresses[addressInfo.Type] = new HashSet<int>();

                        if (!selectedETAddresses[addressInfo.Type].Add(address))
                        {
                            conflicts.Add($"{deviceName}.{module.ModuleName}: Address {address} ({addressInfo.Type}) conflicts with another selected device (Target PLC: {targetPlcName})");
                        }
                    }
                }
            }
        }

        return conflicts;
    }

    /// <summary>
    /// Updates the cached conflict detection results
    /// </summary>
    private void UpdateConflicts()
    {
        _currentNamingConflicts = GetNamingConflicts();
        _currentIpConflicts = GetIpAddressConflicts();
        _currentETConflicts = GetETAddressConflicts();
    }

    private void UpdatePreview()
    {
        try
        {
            // Update conflicts first
            UpdateConflicts();

            var preview = "Hardware Device Export Configuration Preview:" + Environment.NewLine + Environment.NewLine;

            // Show IP address offset configuration
            var ipOffset = (int)_numericUpDownIpOffset.Value;
            preview += $"IP Address Offset: {ipOffset:+0;-0;0}" + Environment.NewLine;

            // Get selected IoSystem for IP calculation
            var selectedIoSystemItem = _comboBoxIoSystem.SelectedItem;
            var selectedIoSystem = selectedIoSystemItem?.GetType().GetProperty("Data")?.GetValue(selectedIoSystemItem) as IoSystemInfo;
            var networkAddress = selectedIoSystem?.NetworkAddress;
            var subnetMask = selectedIoSystem?.SubnetMask;

            preview += $"Selected IoSystem: {selectedIoSystem?.Name ?? "None"}" + Environment.NewLine;
            if (selectedIoSystem != null)
            {
                preview += $"  Network: {networkAddress}/{subnetMask}" + Environment.NewLine;
            }
            preview += Environment.NewLine;

            // Show Find/Replace Rules (if any)
            if (DeviceNameFindReplacePairs.Count > 0)
            {
                preview += "Find/Replace Rules:" + Environment.NewLine;
                foreach (var pair in DeviceNameFindReplacePairs)
                {
                    if (!string.IsNullOrWhiteSpace(pair.FindString))
                    {
                        preview += $"  '{pair.FindString}' -> '{pair.ReplaceString}'" + Environment.NewLine;
                    }
                }
                preview += Environment.NewLine;
            }
            else
            {
                preview += "Device Name Transformations: None configured (names exported as-is)" + Environment.NewLine + Environment.NewLine;
            }

            // Always show transformation examples
            preview += "Transformation Examples (first 8 devices):" + Environment.NewLine;
            foreach (var device in _selectedDevices.Take(8))
            {
                // Use ItemName if available, otherwise use device Name (matches transformation logic)
                var originalName = !string.IsNullOrWhiteSpace(device.ItemName) ? device.ItemName : device.Name;
                var transformedName = ApplyTransformations(originalName);

                // Check if this specific device has a conflict
                var hasNamingConflict = _currentNamingConflicts.Contains(transformedName);
                var conflictIndicator = hasNamingConflict ? " ⚠️ CONFLICT" : "";

                // Show device name transformation
                if (originalName != transformedName)
                {
                    preview += $"  {originalName} -> {transformedName}{conflictIndicator}" + Environment.NewLine;
                }
                else
                {
                    preview += $"  {originalName} (unchanged){conflictIndicator}" + Environment.NewLine;
                }

                // Always show IP address transformations
                if (selectedIoSystem != null && !string.IsNullOrWhiteSpace(networkAddress) && !string.IsNullOrWhiteSpace(subnetMask))
                {
                    foreach (var ipAddress in device.IpAddresses)
                    {
                        var newIpAddress = CalculateNewIp(ipAddress, networkAddress, subnetMask, ipOffset);

                        // Check if the new IP address conflicts with existing devices
                        var hasIpConflict = _existingIpAddresses.ContainsKey(newIpAddress);
                        var ipConflictIndicator = hasIpConflict ? " ⚠️ CONFLICT" : "";

                        if (ipAddress != newIpAddress)
                        {
                            preview += $"    IP: {ipAddress} -> {newIpAddress}{ipConflictIndicator}" + Environment.NewLine;
                        }
                        else
                        {
                            preview += $"    IP: {ipAddress} (unchanged){ipConflictIndicator}" + Environment.NewLine;
                        }
                    }
                }
                else
                {
                    preview += $"    IP: (IoSystem not selected - cannot calculate)" + Environment.NewLine;
                }
            }

            if (_selectedDevices.Count > 8)
            {
                preview += $"  ... and {_selectedDevices.Count - 8} more devices" + Environment.NewLine;
            }

            // Conflict summary
            var totalConflicts = _currentNamingConflicts.Count + _currentIpConflicts.Count + _currentETConflicts.Count;
            preview += Environment.NewLine;
            if (totalConflicts > 0)
            {
                preview += $"⚠️ WARNING: {totalConflicts} conflicts detected across all {_selectedDevices.Count} devices!" + Environment.NewLine + Environment.NewLine;

                if (_currentNamingConflicts.Count > 0)
                {
                    preview += $"Device Name Conflicts ({_currentNamingConflicts.Count}): ";
                    preview += string.Join(", ", _currentNamingConflicts.Distinct().Take(3));
                    if (_currentNamingConflicts.Distinct().Count() > 3)
                        preview += $", and {_currentNamingConflicts.Distinct().Count() - 3} more";
                    preview += Environment.NewLine;
                }

                preview += Environment.NewLine;

                if (_currentIpConflicts.Count > 0)
                {
                    preview += $"IP/Device Number Conflicts ({_currentIpConflicts.Count}): " + Environment.NewLine;
                    foreach (var conflict in _currentIpConflicts.Take(3))
                    {
                        preview += $"  • {conflict}" + Environment.NewLine;
                    }
                    if (_currentIpConflicts.Count > 3)
                        preview += $"  ... and {_currentIpConflicts.Count - 3} more IP/device number conflicts" + Environment.NewLine;
                }

                if (_currentETConflicts.Count > 0)
                {
                    preview += Environment.NewLine + $"ET Module Addresses: ⚠️ {_currentETConflicts.Count} conflicts detected - configure in ET Modules section" + Environment.NewLine;
                }

                preview += Environment.NewLine + "Please resolve all conflicts before proceeding." + Environment.NewLine;
            }
            else
            {
                preview +=  $"✓ No conflicts detected across all {_selectedDevices.Count} devices." + Environment.NewLine;
            }

            _textBoxPreview.Text = preview;

            // Update OK button state based on conflicts
            _buttonOk.Enabled = !HasAnyConflicts();
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error updating preview: {ex.Message}", false);
            _textBoxPreview.Text = "Error generating preview.";
            _buttonOk.Enabled = false;
        }
    }

    private string ApplyTransformations(string originalName)
    {
        if (string.IsNullOrWhiteSpace(originalName))
            return originalName;

        var result = originalName;
        foreach (var pair in DeviceNameFindReplacePairs)
        {
            if (!string.IsNullOrWhiteSpace(pair.FindString))
            {
                result = result.Replace(pair.FindString, pair.ReplaceString ?? "");
            }
        }
        return result;
    }

    /// <summary>
    /// Checks for naming conflicts between transformed device names and existing devices
    /// Returns list of conflicting device names
    /// </summary>
    private List<string> GetNamingConflicts()
    {
        try
        {
            var allNames = new HashSet<string>(_existingDeviceNames); // Start with existing device names
            var conflicts = new List<string>();

            foreach (var device in _selectedDevices)
            {
                // Use ItemName if available, otherwise use device Name (matches transformation logic)
                var originalName = !string.IsNullOrWhiteSpace(device.ItemName) ? device.ItemName : device.Name;
                var transformedName = ApplyTransformations(originalName);

                // Check if transformed name conflicts with existing devices or other transformed names
                if (!allNames.Add(transformedName)) // Returns false if already exists
                {
                    conflicts.Add(transformedName);
                }
            }

            return conflicts;
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error checking naming conflicts: {ex.Message}", false);
            return new List<string>(); // Return empty list on error
        }
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);

        // Focus the DataGridView for immediate editing
        _dataGridViewPairs.Focus();

        // Set up keyboard shortcuts
        this.KeyPreview = true;
        this.KeyDown += (sender, args) =>
        {
            if (args.KeyCode == Keys.Enter && args.Control)
            {
                ButtonOK_Click(sender, args);
                args.Handled = true;
            }
            else if (args.KeyCode == Keys.Escape)
            {
                ButtonCancel_Click(sender, args);
                args.Handled = true;
            }
        };
    }

    private void NumericUpDownIpOffset_ValueChanged(object sender, EventArgs e)
    {
        // Update preview when IP offset changes
        UpdatePreview();
    }


    /// <summary>
    /// Applies ET address transformations to a given address
    /// Based on the same logic as ProcessAddressTransformation from ETModuleConfigurationForm
    /// </summary>
    private int ApplyETTransformations(int address)
    {
        foreach (var pair in ETAddressReplacements)
        {
            address = ProcessAddressTransformation(address, pair);
        }
        return address;
    }

    /// <summary>
    /// Processes a single address transformation pair
    /// </summary>
    private int ProcessAddressTransformation(int address, TagAddressReplacePair pair)
    {
        // Convert address to string for processing (similar to tag address processing)
        string addressString = address.ToString();

        // Check length filter - only process addresses with specified digit count
        if (addressString.Length == pair.LengthFilter)
        {
            // Apply replacement to specific digit position (right-to-left counting)
            if (pair.DigitPosition > 0 && pair.DigitPosition <= addressString.Length && !string.IsNullOrEmpty(pair.FindString))
            {
                int findLength = pair.FindString.Length;
                int rightmostIndex = addressString.Length - pair.DigitPosition; // Where rightmost digit of find string should be
                int leftmostIndex = rightmostIndex - findLength + 1; // Where leftmost digit of find string should be

                // Check if we have enough digits to the left for the find string
                if (leftmostIndex >= 0 && rightmostIndex < addressString.Length)
                {
                    // Extract the substring to compare
                    string currentSubstring = addressString.Substring(leftmostIndex, findLength);

                    if (currentSubstring == pair.FindString)
                    {
                        // Replace the multi-digit sequence
                        string beforeReplacement = addressString.Substring(0, leftmostIndex);
                        string afterReplacement = addressString.Substring(rightmostIndex + 1);
                        string replacement = pair.ReplaceString ?? "";

                        string newAddressString = beforeReplacement + replacement + afterReplacement;

                        // Convert back to integer
                        if (int.TryParse(newAddressString, out int newAddress))
                        {
                            return newAddress;
                        }
                    }
                }
            }
        }

        return address; // Return unchanged if processing fails or criteria not met
    }

    /// <summary>
    /// Checks if there are any conflicts (naming, IP addresses, device numbers, or ET addresses)
    /// Uses cached conflict results
    /// </summary>
    private bool HasAnyConflicts()
    {
        return _currentNamingConflicts.Count > 0 || _currentIpConflicts.Count > 0 || _currentETConflicts.Count > 0;
    }
}
