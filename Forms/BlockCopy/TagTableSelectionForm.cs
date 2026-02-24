using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using OpennessCopy.Models;
using OpennessCopy.Utils;

namespace OpennessCopy.Forms.BlockCopy;

public delegate List<TagExample> GetSampleTagDataDelegate(string tableId);

public class TagTableSelectionForm : Form
{
    public List<TagTableConfig> SelectedTables { get; private set; }

    private readonly TagTableSelectionData _tagTableData;
    private List<TagTableConfig> _allTables;
    private readonly GetSampleTagDataDelegate _getSampleTagData;
    private readonly List<FindReplacePair> _blockFindReplacePairs;

    public TagTableSelectionForm(TagTableSelectionData tagTableData, List<FindReplacePair> blockFindReplacePairs = null, GetSampleTagDataDelegate getSampleTagData = null)
    {
        _tagTableData = tagTableData;
        _blockFindReplacePairs = blockFindReplacePairs ?? new List<FindReplacePair>();
        _getSampleTagData = getSampleTagData;
        SelectedTables = new List<TagTableConfig>();
        InitializeComponent();
        LoadTagTables();
    }

    private void LoadTagTables()
    {
        try
        {
            _allTables = new List<TagTableConfig>();
            _listViewTables.Items.Clear();

            // Load tag tables from DTO data (sorted alphabetically by name)
            var sortedTagTables = _tagTableData.TagTables.OrderBy(t => t.Name).ToList();
            foreach (var tagTableInfo in sortedTagTables)
            {
                var config = new TagTableConfig
                {
                    // Will be resolved later on STA thread
                    TableName = tagTableInfo.Name,
                    TagCount = tagTableInfo.TagCount,
                    TableId = tagTableInfo.TableId
                };

                // Pre-populate with block find/replace pairs as defaults
                foreach (var blockPair in _blockFindReplacePairs)
                {
                    config.NameReplacements.Add(new FindReplacePair(blockPair.FindString, blockPair.ReplaceString));
                }
                    
                _allTables.Add(config);

                // Create ListView item with name as main text
                var item = new ListViewItem(tagTableInfo.Name)
                {
                    UseItemStyleForSubItems = false,
                    Checked = false,
                    Tag = config
                };

                // Add sub-items (columns)
                item.SubItems.Add(tagTableInfo.TagCount.ToString());
                item.SubItems.Add("Not configured"); // Configuration status
                
                _listViewTables.Items.Add(item);
            }

            _lblStatus.Text = $"Found {_allTables.Count} tag tables. Check tables to select and use Configure button for advanced options.";
            UpdateSelectionInfo();
            UpdateConfigurationStatus();
            UpdateConfigureButtonState();
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error loading tag tables: {ex.Message}");
            DialogResult = DialogResult.Cancel;
            Close();
        }
    }

    private void listViewTables_ItemChecked(object sender, ItemCheckedEventArgs e)
    {
        UpdateSelectionInfo();
        UpdateConfigurationStatus();
    }

    private void listViewTables_SelectedIndexChanged(object sender, EventArgs e)
    {
        UpdateConfigureButtonState();
    }

    private void UpdateSelectionInfo()
    {
        int selectedCount = _listViewTables.CheckedItems.Count;
        int totalTags = 0;
            
        foreach (ListViewItem item in _listViewTables.CheckedItems)
        {
            if (item.Tag is TagTableConfig config)
            {
                totalTags += config.TagCount;
            }
        }

        _lblSelectionInfo.Text = selectedCount > 0 
            ? $"Selected: {selectedCount} tables ({totalTags} total tags)"
            : "No tables selected (optional - you can continue without tag tables)";
    }

    private void UpdateConfigurationStatus()
    {
        // Update configuration status display in ListView
        foreach (ListViewItem item in _listViewTables.Items)
        {
            if (item.Tag is TagTableConfig config)
            {
                int nameCount = config.NameReplacements.Count;
                int addressCount = config.AddressReplacements.Count;
                
                if (nameCount == 0 && addressCount == 0)
                {
                    item.SubItems[2].Text = "Not configured";
                }
                else if (!config.IsUserConfigured && nameCount > 0 && addressCount == 0)
                {
                    // Has defaults from block processing but user hasn't explicitly configured
                    item.SubItems[2].Text = $"Default (from blocks) - {nameCount} name replacement(s)";
                }
                else
                {
                    // User has explicitly configured this table
                    var parts = new List<string>();
                    if (nameCount > 0) parts.Add($"{nameCount} name");
                    if (addressCount > 0) parts.Add($"{addressCount} address");
                    string prefix = config.IsUserConfigured ? "User configured" : "Default (from blocks)";
                    item.SubItems[2].Text = $"{prefix} - {string.Join(" + ", parts)} replacement(s)";
                }
            }
        }
    }

    private void UpdateConfigureButtonState()
    {
        _btnConfigure.Enabled = _listViewTables.SelectedItems.Count == 1;
    }

    private void btnConfigure_Click(object sender, EventArgs e)
    {
        if (_listViewTables.SelectedItems.Count == 1)
        {
            var selectedItem = _listViewTables.SelectedItems[0];
            if (selectedItem.Tag is TagTableConfig config)
            {
                List<TagExample> sampleTags = null;
                
                // Try to get sample tag data via callback if available
                if (_getSampleTagData != null)
                {
                    Cursor = Cursors.WaitCursor;
                    try
                    {
                        sampleTags = _getSampleTagData(config.TableId);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogWarning($"Failed to extract sample tag data: {ex.Message}");
                        // Continue with null, will use default examples
                    }
                    finally
                    {
                        Cursor = Cursors.Default;
                    }
                }
                
                using var configForm = new TagTableConfigurationForm(config.TableName, config.TagCount, sampleTags);
                // Pre-populate with existing configuration
                configForm.LoadConfiguration(config.NameReplacements, config.AddressReplacements);
                    
                if (configForm.ShowDialog() == DialogResult.OK)
                {
                    // Update configuration
                    config.NameReplacements.Clear();
                    config.NameReplacements.AddRange(configForm.NameReplacements);
                    config.AddressReplacements.Clear();
                    config.AddressReplacements.AddRange(configForm.AddressReplacements);
                    
                    // Mark as user-configured since they clicked OK
                    config.IsUserConfigured = true;
                        
                    // Update status display
                    UpdateConfigurationStatus();
                }
            }
        }
    }

    private void btnSelectAll_Click(object sender, EventArgs e)
    {
        foreach (ListViewItem item in _listViewTables.Items)
        {
            item.Checked = true;
        }
    }

    private void btnSelectNone_Click(object sender, EventArgs e)
    {
        foreach (ListViewItem item in _listViewTables.Items)
        {
            item.Checked = false;
        }
    }

    private void btnOK_Click(object sender, EventArgs e)
    {
        // Validate selections
        var checkedItems = _listViewTables.CheckedItems.Cast<ListViewItem>().ToList();
            
        foreach (var item in checkedItems)
        {
            if (item.Tag is TagTableConfig config)
            {
                // Validate name replacement pairs
                foreach (var pair in config.NameReplacements)
                {
                    if (!string.IsNullOrEmpty(pair.FindString) && string.IsNullOrEmpty(pair.ReplaceString))
                    {
                        MessageBox.Show($"Table '{config.TableName}' has a Find string ('{pair.FindString}') but no Replace string. " +
                                        "Please configure the table properly.", 
                            "Invalid Configuration", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            
                        // Select the problematic item
                        item.Selected = true;
                        _listViewTables.Focus();
                        return;
                    }
                }
            }
        }

        // Collect selected configurations
        SelectedTables = checkedItems
            .Where(item => item.Tag is TagTableConfig)
            .Select(item => (TagTableConfig)item.Tag)
            .ToList();

        DialogResult = DialogResult.OK;
        Close();
    }

    private void btnCancel_Click(object sender, EventArgs e)
    {
        DialogResult = DialogResult.Cancel;
        Close();
    }

    private void InitializeComponent()
    {
        this._lblTitle = new Label();
        this._listViewTables = new ListView();
        this._columnName = new ColumnHeader();
        this._columnTagCount = new ColumnHeader();
        this._columnConfiguration = new ColumnHeader();
        this._lblSelectionInfo = new Label();
        this._btnSelectAll = new Button();
        this._btnSelectNone = new Button();
        this._btnConfigure = new Button();
        this._lblStatus = new Label();
        this._btnOk = new Button();
        this._btnCancel = new Button();
        this.SuspendLayout();
            
        // 
        // lblTitle
        // 
        this._lblTitle.AutoSize = true;
        this._lblTitle.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Bold);
        this._lblTitle.Location = new System.Drawing.Point(20, 20);
        this._lblTitle.Name = "_lblTitle";
        this._lblTitle.Size = new System.Drawing.Size(150, 17);
        this._lblTitle.TabIndex = 0;
        this._lblTitle.Text = "Select Tag Tables";
            
        // 
        // listViewTables
        // 
        this._listViewTables.CheckBoxes = true;
        // ReSharper disable once UseCollectionExpression
        this._listViewTables.Columns.AddRange(new[] {
            this._columnName,
            this._columnTagCount,
            this._columnConfiguration
        });
        this._listViewTables.FullRowSelect = true;
        this._listViewTables.GridLines = true;
        this._listViewTables.HideSelection = false;
        this._listViewTables.Location = new System.Drawing.Point(20, 50);
        this._listViewTables.Name = "_listViewTables";
        this._listViewTables.Size = new System.Drawing.Size(560, 200);
        this._listViewTables.TabIndex = 1;
        this._listViewTables.UseCompatibleStateImageBehavior = false;
        this._listViewTables.View = View.Details;
        this._listViewTables.ItemChecked += this.listViewTables_ItemChecked;
        this._listViewTables.SelectedIndexChanged += this.listViewTables_SelectedIndexChanged;
            
        // 
        // columnName
        // 
        this._columnName.Text = "Table Name";
        this._columnName.Width = 180;
            
        // 
        // columnTagCount
        // 
        this._columnTagCount.Text = "Tags";
        this._columnTagCount.TextAlign = HorizontalAlignment.Right;
        this._columnTagCount.Width = 60;
            
        // 
        // columnConfiguration
        // 
        this._columnConfiguration.Text = "Configuration";
        this._columnConfiguration.Width = -2; // Auto-size to fill remaining space
            
        // 
        // lblSelectionInfo
        // 
        this._lblSelectionInfo.AutoSize = true;
        this._lblSelectionInfo.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold);
        this._lblSelectionInfo.Location = new System.Drawing.Point(20, 260);
        this._lblSelectionInfo.Name = "_lblSelectionInfo";
        this._lblSelectionInfo.Size = new System.Drawing.Size(100, 13);
        this._lblSelectionInfo.TabIndex = 2;
        this._lblSelectionInfo.Text = "No tables selected";
            
        // 
        // btnSelectAll
        // 
        this._btnSelectAll.Location = new System.Drawing.Point(420, 255);
        this._btnSelectAll.Name = "_btnSelectAll";
        this._btnSelectAll.Size = new System.Drawing.Size(75, 25);
        this._btnSelectAll.TabIndex = 3;
        this._btnSelectAll.Text = "Select All";
        this._btnSelectAll.UseVisualStyleBackColor = true;
        this._btnSelectAll.Click += this.btnSelectAll_Click;
            
        // 
        // btnSelectNone
        // 
        this._btnSelectNone.Location = new System.Drawing.Point(505, 255);
        this._btnSelectNone.Name = "_btnSelectNone";
        this._btnSelectNone.Size = new System.Drawing.Size(75, 25);
        this._btnSelectNone.TabIndex = 4;
        this._btnSelectNone.Text = "Select None";
        this._btnSelectNone.UseVisualStyleBackColor = true;
        this._btnSelectNone.Click += this.btnSelectNone_Click;
            
        // 
        // btnConfigure
        // 
        this._btnConfigure.Enabled = false;
        this._btnConfigure.Location = new System.Drawing.Point(20, 290);
        this._btnConfigure.Name = "_btnConfigure";
        this._btnConfigure.Size = new System.Drawing.Size(100, 30);
        this._btnConfigure.TabIndex = 5;
        this._btnConfigure.Text = "Configure";
        this._btnConfigure.UseVisualStyleBackColor = true;
        this._btnConfigure.Click += this.btnConfigure_Click;
            
        // 
        // lblStatus
        // 
        this._lblStatus.AutoSize = true;
        this._lblStatus.Location = new System.Drawing.Point(20, 340);
        this._lblStatus.MaximumSize = new System.Drawing.Size(560, 0);
        this._lblStatus.Name = "_lblStatus";
        this._lblStatus.Size = new System.Drawing.Size(120, 13);
        this._lblStatus.TabIndex = 6;
        this._lblStatus.Text = "Loading tag tables...";
            
        // 
        // btnOK
        // 
        this._btnOk.Enabled = true;
        this._btnOk.Location = new System.Drawing.Point(420, 380);
        this._btnOk.Name = "_btnOk";
        this._btnOk.Size = new System.Drawing.Size(75, 30);
        this._btnOk.TabIndex = 7;
        this._btnOk.Text = "OK";
        this._btnOk.UseVisualStyleBackColor = true;
        this._btnOk.Click += this.btnOK_Click;
            
        // 
        // btnCancel
        // 
        this._btnCancel.Location = new System.Drawing.Point(505, 380);
        this._btnCancel.Name = "_btnCancel";
        this._btnCancel.Size = new System.Drawing.Size(75, 30);
        this._btnCancel.TabIndex = 8;
        this._btnCancel.Text = "Cancel";
        this._btnCancel.UseVisualStyleBackColor = true;
        this._btnCancel.Click += this.btnCancel_Click;
            
        // 
        // TagTableSelectionForm
        // 
        this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
        this.AutoScaleMode = AutoScaleMode.Font;
        this.ClientSize = new System.Drawing.Size(600, 430);
        this.Controls.Add(this._lblTitle);
        this.Controls.Add(this._listViewTables);
        this.Controls.Add(this._lblSelectionInfo);
        this.Controls.Add(this._btnSelectAll);
        this.Controls.Add(this._btnSelectNone);
        this.Controls.Add(this._btnConfigure);
        this.Controls.Add(this._lblStatus);
        this.Controls.Add(this._btnOk);
        this.Controls.Add(this._btnCancel);
        this.FormBorderStyle = FormBorderStyle.FixedDialog;
        this.MaximizeBox = false;
        this.MinimizeBox = false;
        this.Name = "TagTableSelectionForm";
        this.StartPosition = FormStartPosition.CenterParent;
        this.Text = "Select Tag Tables";
        this.ResumeLayout(false);
        this.PerformLayout();
    }

    private Label _lblTitle;
    private ListView _listViewTables;
    private ColumnHeader _columnName;
    private ColumnHeader _columnTagCount;
    private ColumnHeader _columnConfiguration;
    private Label _lblSelectionInfo;
    private Button _btnSelectAll;
    private Button _btnSelectNone;
    private Button _btnConfigure;
    private Label _lblStatus;
    private Button _btnOk;
    private Button _btnCancel;
}

public class TagTableConfig
{
    public string TableName { get; set; }
    public int TagCount { get; set; }
    public List<FindReplacePair> NameReplacements { get; set; } = new List<FindReplacePair>();
    public List<TagAddressReplacePair> AddressReplacements { get; set; } = new List<TagAddressReplacePair>();
    public string TableId { get; set; } // For STA thread object resolution
    public bool IsUserConfigured { get; set; } // Only true when user clicks OK in configuration dialog
}