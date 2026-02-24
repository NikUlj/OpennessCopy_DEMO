using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using OpennessCopy.Models;
using OpennessCopy.Services.BlockCopy;

namespace OpennessCopy.Forms.BlockCopy;

public class BlockProcessingConfigForm : Form
{
    public int PrefixNumber { get; private set; } = 100;
    public List<FindReplacePair> FindReplacePairs { get; private set; } = new List<FindReplacePair>();
    public List<FindReplacePair> ContentFindReplacePairs { get; private set; } = new List<FindReplacePair>();

    private readonly HashSet<int> _existingBlockNumbers;
    private readonly string _selectedGroupId;
    private readonly WorkflowStaThread _workflowThread;
    
    private string _cachedBlockName = "FB_Example_Block";
    private string _cachedBlockNumber = "1234";

    public BlockProcessingConfigForm(HashSet<int> existingBlockNumbers = null, string selectedGroupId = null, WorkflowStaThread workflowThread = null)
    {
        _existingBlockNumbers = existingBlockNumbers ?? new HashSet<int>();
        _selectedGroupId = selectedGroupId;
        _workflowThread = workflowThread;
        InitializeComponent();
        CacheFirstBlockFromGroup();
        
        // Initialize content grid state and populate from block pairs
        UpdateContentGridState();
        if (_chkApplyContentReplace.Checked)
        {
            PopulateContentPairsFromBlockPairs();
        }
        
        UpdatePreview();
    }

    private void numericPrefix_UpdatePrefix(object sender, EventArgs e)
    {
        PrefixNumber = (int)_numericPrefix.Value;
        UpdatePreview();
    }

    private void dataGridViewPairs_CellValueChanged(object sender, DataGridViewCellEventArgs e)
    {
        UpdateFindReplacePairsFromGrid();
        if (_chkApplyContentReplace.Checked)
        {
            PopulateContentPairsFromBlockPairs();
        }
        UpdatePreview();
    }

    private void dataGridViewPairs_CurrentCellDirtyStateChanged(object sender, EventArgs e)
    {
        if (_dataGridViewPairs.IsCurrentCellDirty)
        {
            _dataGridViewPairs.CommitEdit(DataGridViewDataErrorContexts.Commit);
        }
    }

    private void btnAddPair_Click(object sender, EventArgs e)
    {
        _dataGridViewPairs.Rows.Add("", "");
        UpdateFindReplacePairsFromGrid();
        UpdatePreview();
    }

    private void btnRemovePair_Click(object sender, EventArgs e)
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

    private void chkApplyContentReplace_CheckedChanged(object sender, EventArgs e)
    {
        UpdateContentGridState();
        if (_chkApplyContentReplace.Checked)
        {
            PopulateContentPairsFromBlockPairs();
        }
        UpdatePreview();
    }

    private void dataGridViewContentPairs_CellValueChanged(object sender, DataGridViewCellEventArgs e)
    {
        UpdateContentFindReplacePairsFromGrid();
        UpdatePreview();
    }

    private void dataGridViewContentPairs_CurrentCellDirtyStateChanged(object sender, EventArgs e)
    {
        if (_dataGridViewContentPairs.IsCurrentCellDirty)
        {
            _dataGridViewContentPairs.CommitEdit(DataGridViewDataErrorContexts.Commit);
        }
    }

    private void btnAddContentPair_Click(object sender, EventArgs e)
    {
        if (_chkApplyContentReplace.Checked)
        {
            _dataGridViewContentPairs.Rows.Add("", "");
            UpdateContentFindReplacePairsFromGrid();
            UpdatePreview();
        }
    }

    private void btnRemoveContentPair_Click(object sender, EventArgs e)
    {
        if (_chkApplyContentReplace.Checked && _dataGridViewContentPairs.SelectedRows.Count > 0)
        {
            int selectedIndex = _dataGridViewContentPairs.SelectedRows[0].Index;
            if (selectedIndex < _dataGridViewContentPairs.Rows.Count - 1) // Don't remove the new row
            {
                _dataGridViewContentPairs.Rows.RemoveAt(selectedIndex);
                UpdateContentFindReplacePairsFromGrid();
                UpdatePreview();
            }
        }
    }

    private void UpdateFindReplacePairsFromGrid()
    {
        FindReplacePairs.Clear();
        foreach (DataGridViewRow row in _dataGridViewPairs.Rows)
        {
            if (row.IsNewRow) continue;
            
            string findText = row.Cells[0].Value?.ToString() ?? "";
            string replaceText = row.Cells[1].Value?.ToString() ?? "";
            
            if (!string.IsNullOrEmpty(findText))
            {
                FindReplacePairs.Add(new FindReplacePair(findText, replaceText));
            }
        }
    }

    private void UpdateContentFindReplacePairsFromGrid()
    {
        ContentFindReplacePairs.Clear();
        if (!_chkApplyContentReplace.Checked) return;
        
        foreach (DataGridViewRow row in _dataGridViewContentPairs.Rows)
        {
            if (row.IsNewRow) continue;
            
            string findText = row.Cells[0].Value?.ToString() ?? "";
            string replaceText = row.Cells[1].Value?.ToString() ?? "";
            
            if (!string.IsNullOrEmpty(findText))
            {
                ContentFindReplacePairs.Add(new FindReplacePair(findText, replaceText));
            }
        }
    }

    private void UpdateContentGridState()
    {
        bool isEnabled = _chkApplyContentReplace.Checked;
        
        // Update grid properties
        _dataGridViewContentPairs.Enabled = isEnabled;
        _dataGridViewContentPairs.ReadOnly = !isEnabled;
        _dataGridViewContentPairs.AllowUserToAddRows = isEnabled;
        _dataGridViewContentPairs.AllowUserToDeleteRows = isEnabled;
        _dataGridViewContentPairs.BackColor = isEnabled ? System.Drawing.SystemColors.Window : System.Drawing.SystemColors.Control;
        
        // Update button states
        _btnAddContentPair.Enabled = isEnabled;
        _btnRemoveContentPair.Enabled = isEnabled;
        
        if (!isEnabled)
        {
            // Clear both the visual grid and the data when disabled
            _dataGridViewContentPairs.Rows.Clear();
            ContentFindReplacePairs.Clear();
        }
    }

    private void PopulateContentPairsFromBlockPairs()
    {
        if (!_chkApplyContentReplace.Checked) return;
        
        // Clear existing content pairs
        _dataGridViewContentPairs.Rows.Clear();
        ContentFindReplacePairs.Clear();
        
        // Copy from block pairs
        foreach (var blockPair in FindReplacePairs)
        {
            _dataGridViewContentPairs.Rows.Add(blockPair.FindString, blockPair.ReplaceString);
            ContentFindReplacePairs.Add(new FindReplacePair(blockPair.FindString, blockPair.ReplaceString));
        }
    }
    
    private void CacheFirstBlockFromGroup()
    {
        // Cache the first block from the selected group when form opens
        if (_workflowThread != null && !string.IsNullOrEmpty(_selectedGroupId))
        {
            try
            {
                var (name, number) = _workflowThread.GetFirstBlockFromGroup(_selectedGroupId);
                _cachedBlockName = name;
                _cachedBlockNumber = number.ToString();
            }
            catch
            {
                // Keep defaults if there's any error
            }
        }
    }

    private void UpdatePreview()
    {
        string exampleBlockName = _cachedBlockName;
        string exampleBlockNumber = _cachedBlockNumber;

        // Show block number prefix logic
        string modifiedNumber = ModifyBlockNumber(exampleBlockNumber, PrefixNumber);

        // Build preview components
        var previewParts = new List<string>();
        int totalOperations = 0;

        if (FindReplacePairs.Count == 0)
        {
            previewParts.Add($"Block Name: {exampleBlockName}");
            previewParts.Add($"Block Number: {exampleBlockNumber} -> {modifiedNumber}");
            previewParts.Add($"Final Block: {exampleBlockName} (Number: {modifiedNumber})");
        }
        else
        {
            // Apply multiple find and replace operations + prefix
            string currentName = exampleBlockName;
            previewParts.Add($"Original Block: {exampleBlockName}");
            
            for (int i = 0; i < FindReplacePairs.Count; i++)
            {
                var pair = FindReplacePairs[i];
                string newName = currentName.Replace(pair.FindString, pair.ReplaceString);
                previewParts.Add($"Block Step {i + 1}: {currentName} -> {newName} ('{pair.FindString}' -> '{pair.ReplaceString}')");
                currentName = newName;
                totalOperations++;
            }
            
            previewParts.Add($"Block Number: {exampleBlockNumber} -> {modifiedNumber}");
            previewParts.Add($"Final Block: {currentName} (Number: {modifiedNumber})");
        }

        // Add content info if enabled (without examples)
        if (_chkApplyContentReplace.Checked && ContentFindReplacePairs.Count > 0)
        {
            previewParts.Add("");
            previewParts.Add($"Content replacements: {ContentFindReplacePairs.Count} pairs will be applied to variables and comments");
            totalOperations += ContentFindReplacePairs.Count;
        }

        var preview = string.Join("\n", previewParts);

        // Update title
        if (totalOperations == 0)
        {
            _lblPreviewTitle.Text = "Preview (Block number prefix only):";
        }
        else
        {
            string contentText = _chkApplyContentReplace.Checked ? " + Content" : "";
            _lblPreviewTitle.Text = $"Preview ({totalOperations} replacements{contentText} + Number prefix):";
        }

        _lblPreview.Text = preview;
    }

    private static string ModifyBlockNumber(string originalNumber, int prefix)
    {
        // Insert prefix to the left of the rightmost two digits
        // Example: 1234 with prefix 56 becomes 5634
        if (originalNumber.Length >= 2)
        {
            string rightTwoDigits = originalNumber.Substring(originalNumber.Length - 2);
            return $"{prefix}{rightTwoDigits}";
        }
        else if (originalNumber.Length == 1)
        {
            return $"{prefix}0{originalNumber}";
        }
        else
        {
            return $"{prefix}00";
        }
    }


    private void btnOK_Click(object sender, EventArgs e)
    {
        // Update pairs from grids before validation
        UpdateFindReplacePairsFromGrid();
        UpdateContentFindReplacePairsFromGrid();
        
        // Check for prefix conflicts if we have the necessary data
        if (_workflowThread != null && !string.IsNullOrEmpty(_selectedGroupId) && _existingBlockNumbers != null)
        {
            var conflicts = _workflowThread.ValidatePrefixConflicts(PrefixNumber, _selectedGroupId, _existingBlockNumbers);
            
            if (conflicts.Count > 0)
            {
                var conflictNumbersText = string.Join(", ", conflicts.OrderBy(x => x));
                MessageBox.Show($"Prefix {PrefixNumber} would cause conflicts with existing block numbers: {conflictNumbersText}\n\n" +
                    "Please choose a different prefix number to avoid conflicts.", 
                    "Prefix Conflict Detected", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _numericPrefix.Focus();
                return;
            }
        }
        
        // Validate block find/replace inputs - check for empty replace strings where find strings exist
        foreach (var pair in FindReplacePairs)
        {
            if (!string.IsNullOrEmpty(pair.FindString) && string.IsNullOrEmpty(pair.ReplaceString))
            {
                MessageBox.Show($"If you specify a 'Find' string ('{pair.FindString}'), you must also specify a 'Replace' string.", 
                    "Invalid Block Configuration", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _dataGridViewPairs.Focus();
                return;
            }
        }

        // Validate content find/replace inputs - check for empty replace strings where find strings exist
        if (_chkApplyContentReplace.Checked)
        {
            foreach (var pair in ContentFindReplacePairs)
            {
                if (!string.IsNullOrEmpty(pair.FindString) && string.IsNullOrEmpty(pair.ReplaceString))
                {
                    MessageBox.Show($"If you specify a content 'Find' string ('{pair.FindString}'), you must also specify a 'Replace' string.", 
                        "Invalid Content Configuration", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    _dataGridViewContentPairs.Focus();
                    return;
                }
            }
        }

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
        this._lblPrefix = new Label();
        this._numericPrefix = new NumericUpDown();
        this._lblInstructions = new Label();
        this._lblFindReplacePairs = new Label();
        this._dataGridViewPairs = new DataGridView();
        this._btnAddPair = new Button();
        this._btnRemovePair = new Button();
        this._chkApplyContentReplace = new CheckBox();
        this._lblContentFindReplacePairs = new Label();
        this._dataGridViewContentPairs = new DataGridView();
        this._btnAddContentPair = new Button();
        this._btnRemoveContentPair = new Button();
        this._lblPreviewTitle = new Label();
        this._lblPreview = new Label();
        this._btnOk = new Button();
        this._btnCancel = new Button();
        ((System.ComponentModel.ISupportInitialize)(this._numericPrefix)).BeginInit();
        ((System.ComponentModel.ISupportInitialize)(this._dataGridViewPairs)).BeginInit();
        ((System.ComponentModel.ISupportInitialize)(this._dataGridViewContentPairs)).BeginInit();
        this.SuspendLayout();
            
        // 
        // lblTitle
        // 
        this._lblTitle.AutoSize = true;
        this._lblTitle.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Bold);
        this._lblTitle.Location = new System.Drawing.Point(20, 20);
        this._lblTitle.Name = "_lblTitle";
        this._lblTitle.Size = new System.Drawing.Size(200, 17);
        this._lblTitle.TabIndex = 0;
        this._lblTitle.Text = "Block Processing Configuration";
            
        // 
        // lblPrefix
        // 
        this._lblPrefix.AutoSize = true;
        this._lblPrefix.Location = new System.Drawing.Point(20, 60);
        this._lblPrefix.Name = "_lblPrefix";
        this._lblPrefix.Size = new System.Drawing.Size(120, 13);
        this._lblPrefix.TabIndex = 1;
        this._lblPrefix.Text = "Block Number Prefix:";
            
        // 
        // numericPrefix
        // 
        this._numericPrefix.Location = new System.Drawing.Point(150, 58);
        // ReSharper disable once UseCollectionExpression
        this._numericPrefix.Maximum = new decimal(new[] {9999, 0, 0, 0});
        // ReSharper disable once UseCollectionExpression
        this._numericPrefix.Minimum = new decimal(new[] {1, 0, 0, 0});
        this._numericPrefix.Name = "_numericPrefix";
        this._numericPrefix.Size = new System.Drawing.Size(80, 20);
        this._numericPrefix.TabIndex = 2;
        // ReSharper disable once UseCollectionExpression
        this._numericPrefix.Value = new decimal(new[] {100, 0, 0, 0});
        this._numericPrefix.TextChanged += this.numericPrefix_UpdatePrefix;
        
            
        // 
        // lblInstructions
        // 
        this._lblInstructions.AutoSize = true;
        this._lblInstructions.ForeColor = System.Drawing.Color.Gray;
        this._lblInstructions.Location = new System.Drawing.Point(20, 100);
        this._lblInstructions.MaximumSize = new System.Drawing.Size(500, 0);
        this._lblInstructions.Name = "_lblInstructions";
        this._lblInstructions.Size = new System.Drawing.Size(480, 26);
        this._lblInstructions.TabIndex = 3;
        this._lblInstructions.Text = "Optional: Add multiple find and replace pairs for block names. Replacements are applied in order. Leave grid empty to skip renaming and only apply prefix numbering.";
            
        // 
        // lblFindReplacePairs
        // 
        this._lblFindReplacePairs.AutoSize = true;
        this._lblFindReplacePairs.Location = new System.Drawing.Point(20, 140);
        this._lblFindReplacePairs.Name = "_lblFindReplacePairs";
        this._lblFindReplacePairs.Size = new System.Drawing.Size(110, 13);
        this._lblFindReplacePairs.TabIndex = 4;
        this._lblFindReplacePairs.Text = "Find/Replace Pairs:";
            
        // 
        // dataGridViewPairs
        // 
        this._dataGridViewPairs.AllowUserToResizeRows = false;
        this._dataGridViewPairs.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
        this._dataGridViewPairs.Location = new System.Drawing.Point(20, 160);
        this._dataGridViewPairs.Name = "_dataGridViewPairs";
        this._dataGridViewPairs.Size = new System.Drawing.Size(400, 120);
        this._dataGridViewPairs.TabIndex = 5;
        this._dataGridViewPairs.CellValueChanged += this.dataGridViewPairs_CellValueChanged;
        this._dataGridViewPairs.CurrentCellDirtyStateChanged += this.dataGridViewPairs_CurrentCellDirtyStateChanged;
        this._dataGridViewPairs.ScrollBars = ScrollBars.Vertical;
        
        // Add columns
        var findColumn = new DataGridViewTextBoxColumn();
        findColumn.Name = "findColumn";
        findColumn.HeaderText = "Find";
        findColumn.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
        findColumn.FillWeight = 50; // Equal weight
        this._dataGridViewPairs.Columns.Add(findColumn);
        
        var replaceColumn = new DataGridViewTextBoxColumn();
        replaceColumn.Name = "replaceColumn";
        replaceColumn.HeaderText = "Replace With";
        replaceColumn.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
        replaceColumn.FillWeight = 50; // Equal weight
        this._dataGridViewPairs.Columns.Add(replaceColumn);
            
        // 
        // btnAddPair
        // 
        this._btnAddPair.Location = new System.Drawing.Point(430, 160);
        this._btnAddPair.Name = "_btnAddPair";
        this._btnAddPair.Size = new System.Drawing.Size(75, 25);
        this._btnAddPair.TabIndex = 6;
        this._btnAddPair.Text = "Add";
        this._btnAddPair.UseVisualStyleBackColor = true;
        this._btnAddPair.Click += this.btnAddPair_Click;
            
        // 
        // btnRemovePair
        // 
        this._btnRemovePair.Location = new System.Drawing.Point(430, 190);
        this._btnRemovePair.Name = "_btnRemovePair";
        this._btnRemovePair.Size = new System.Drawing.Size(75, 25);
        this._btnRemovePair.TabIndex = 7;
        this._btnRemovePair.Text = "Remove";
        this._btnRemovePair.UseVisualStyleBackColor = true;
        this._btnRemovePair.Click += this.btnRemovePair_Click;
            
        // 
        // chkApplyContentReplace
        // 
        this._chkApplyContentReplace.AutoSize = true;
        this._chkApplyContentReplace.Checked = true;
        this._chkApplyContentReplace.CheckState = CheckState.Checked;
        this._chkApplyContentReplace.Location = new System.Drawing.Point(20, 290);
        this._chkApplyContentReplace.Name = "_chkApplyContentReplace";
        this._chkApplyContentReplace.Size = new System.Drawing.Size(280, 17);
        this._chkApplyContentReplace.TabIndex = 8;
        this._chkApplyContentReplace.Text = "Apply find/replace to block content (variables && comments)";
        this._chkApplyContentReplace.UseVisualStyleBackColor = true;
        this._chkApplyContentReplace.CheckedChanged += this.chkApplyContentReplace_CheckedChanged;
            
        // 
        // lblContentFindReplacePairs
        // 
        this._lblContentFindReplacePairs.AutoSize = true;
        this._lblContentFindReplacePairs.Location = new System.Drawing.Point(20, 320);
        this._lblContentFindReplacePairs.Name = "_lblContentFindReplacePairs";
        this._lblContentFindReplacePairs.Size = new System.Drawing.Size(170, 13);
        this._lblContentFindReplacePairs.TabIndex = 9;
        this._lblContentFindReplacePairs.Text = "Content Find/Replace Pairs:";
            
        // 
        // dataGridViewContentPairs
        // 
        this._dataGridViewContentPairs.AllowUserToResizeRows = false;
        this._dataGridViewContentPairs.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
        this._dataGridViewContentPairs.Location = new System.Drawing.Point(20, 340);
        this._dataGridViewContentPairs.Name = "_dataGridViewContentPairs";
        this._dataGridViewContentPairs.Size = new System.Drawing.Size(400, 120);
        this._dataGridViewContentPairs.TabIndex = 10;
        this._dataGridViewContentPairs.CellValueChanged += this.dataGridViewContentPairs_CellValueChanged;
        this._dataGridViewContentPairs.CurrentCellDirtyStateChanged += this.dataGridViewContentPairs_CurrentCellDirtyStateChanged;
        this._dataGridViewContentPairs.ScrollBars = ScrollBars.Vertical;
        
        // Add columns for content pairs
        var contentFindColumn = new DataGridViewTextBoxColumn();
        contentFindColumn.Name = "contentFindColumn";
        contentFindColumn.HeaderText = "Find";
        contentFindColumn.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
        contentFindColumn.FillWeight = 50;
        this._dataGridViewContentPairs.Columns.Add(contentFindColumn);
        
        var contentReplaceColumn = new DataGridViewTextBoxColumn();
        contentReplaceColumn.Name = "contentReplaceColumn";
        contentReplaceColumn.HeaderText = "Replace With";
        contentReplaceColumn.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
        contentReplaceColumn.FillWeight = 50;
        this._dataGridViewContentPairs.Columns.Add(contentReplaceColumn);
            
        // 
        // btnAddContentPair
        // 
        this._btnAddContentPair.Location = new System.Drawing.Point(430, 340);
        this._btnAddContentPair.Name = "_btnAddContentPair";
        this._btnAddContentPair.Size = new System.Drawing.Size(75, 25);
        this._btnAddContentPair.TabIndex = 11;
        this._btnAddContentPair.Text = "Add";
        this._btnAddContentPair.UseVisualStyleBackColor = true;
        this._btnAddContentPair.Click += this.btnAddContentPair_Click;
            
        // 
        // btnRemoveContentPair
        // 
        this._btnRemoveContentPair.Location = new System.Drawing.Point(430, 370);
        this._btnRemoveContentPair.Name = "_btnRemoveContentPair";
        this._btnRemoveContentPair.Size = new System.Drawing.Size(75, 25);
        this._btnRemoveContentPair.TabIndex = 12;
        this._btnRemoveContentPair.Text = "Remove";
        this._btnRemoveContentPair.UseVisualStyleBackColor = true;
        this._btnRemoveContentPair.Click += this.btnRemoveContentPair_Click;
            
        // 
        // lblPreviewTitle
        // 
        this._lblPreviewTitle.AutoSize = true;
        this._lblPreviewTitle.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold);
        this._lblPreviewTitle.Location = new System.Drawing.Point(20, 470);
        this._lblPreviewTitle.Name = "_lblPreviewTitle";
        this._lblPreviewTitle.Size = new System.Drawing.Size(55, 13);
        this._lblPreviewTitle.TabIndex = 13;
        this._lblPreviewTitle.Text = "Preview:";
            
        // 
        // lblPreview
        // 
        this._lblPreview.BackColor = System.Drawing.Color.LightGray;
        this._lblPreview.BorderStyle = BorderStyle.FixedSingle;
        this._lblPreview.Font = new System.Drawing.Font("Consolas", 8.25F);
        this._lblPreview.Location = new System.Drawing.Point(20, 490);
        this._lblPreview.Name = "_lblPreview";
        this._lblPreview.Padding = new Padding(5);
        this._lblPreview.Size = new System.Drawing.Size(485, 100);
        this._lblPreview.TabIndex = 14;
        this._lblPreview.Text = "Preview will appear here...";
            
        // 
        // btnOK
        // 
        this._btnOk.Location = new System.Drawing.Point(350, 610);
        this._btnOk.Name = "_btnOk";
        this._btnOk.Size = new System.Drawing.Size(75, 30);
        this._btnOk.TabIndex = 15;
        this._btnOk.Text = "OK";
        this._btnOk.UseVisualStyleBackColor = true;
        this._btnOk.Click += this.btnOK_Click;
            
        // 
        // btnCancel
        // 
        this._btnCancel.Location = new System.Drawing.Point(435, 610);
        this._btnCancel.Name = "_btnCancel";
        this._btnCancel.Size = new System.Drawing.Size(75, 30);
        this._btnCancel.TabIndex = 16;
        this._btnCancel.Text = "Cancel";
        this._btnCancel.UseVisualStyleBackColor = true;
        this._btnCancel.Click += this.btnCancel_Click;
            
        // 
        // BlockProcessingConfigForm
        // 
        this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
        this.AutoScaleMode = AutoScaleMode.Font;
        this.ClientSize = new System.Drawing.Size(530, 660);
        this.Controls.Add(this._lblTitle);
        this.Controls.Add(this._lblPrefix);
        this.Controls.Add(this._numericPrefix);
        this.Controls.Add(this._lblInstructions);
        this.Controls.Add(this._lblFindReplacePairs);
        this.Controls.Add(this._dataGridViewPairs);
        this.Controls.Add(this._btnAddPair);
        this.Controls.Add(this._btnRemovePair);
        this.Controls.Add(this._chkApplyContentReplace);
        this.Controls.Add(this._lblContentFindReplacePairs);
        this.Controls.Add(this._dataGridViewContentPairs);
        this.Controls.Add(this._btnAddContentPair);
        this.Controls.Add(this._btnRemoveContentPair);
        this.Controls.Add(this._lblPreviewTitle);
        this.Controls.Add(this._lblPreview);
        this.Controls.Add(this._btnOk);
        this.Controls.Add(this._btnCancel);
        this.FormBorderStyle = FormBorderStyle.FixedDialog;
        this.MaximizeBox = false;
        this.MinimizeBox = false;
        this.Name = "BlockProcessingConfigForm";
        this.StartPosition = FormStartPosition.CenterParent;
        this.Text = "Block Processing Configuration";
        ((System.ComponentModel.ISupportInitialize)(this._numericPrefix)).EndInit();
        ((System.ComponentModel.ISupportInitialize)(this._dataGridViewPairs)).EndInit();
        ((System.ComponentModel.ISupportInitialize)(this._dataGridViewContentPairs)).EndInit();
        this.ResumeLayout(false);
        this.PerformLayout();
    }

    private Label _lblTitle;
    private Label _lblPrefix;
    private NumericUpDown _numericPrefix;
    private Label _lblInstructions;
    private Label _lblFindReplacePairs;
    private DataGridView _dataGridViewPairs;
    private Button _btnAddPair;
    private Button _btnRemovePair;
    private CheckBox _chkApplyContentReplace;
    private Label _lblContentFindReplacePairs;
    private DataGridView _dataGridViewContentPairs;
    private Button _btnAddContentPair;
    private Button _btnRemoveContentPair;
    private Label _lblPreviewTitle;
    private Label _lblPreview;
    private Button _btnOk;
    private Button _btnCancel;
}
