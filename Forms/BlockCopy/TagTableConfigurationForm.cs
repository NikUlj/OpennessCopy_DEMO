using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using OpennessCopy.Models;
using OpennessCopy.Utils;

namespace OpennessCopy.Forms.BlockCopy;

public class TagTableConfigurationForm : Form
{
    // Windows API for managing TextBox scroll position
    [DllImport("user32.dll")]
    private static extern int GetScrollPos(IntPtr hWnd, int nBar);

    [DllImport("user32.dll")]
    private static extern int SetScrollPos(IntPtr hWnd, int nBar, int nPos, bool bRedraw);

    [DllImport("user32.dll")]
    private static extern bool PostMessageA(IntPtr hWnd, int nBar, int wParam, int lParam);

    private const int SbVert = 1;
    private const int WmVertScroll = 0x115;
    private const int SbThumbPosition = 4;

    public List<FindReplacePair> NameReplacements { get; private set; } = new List<FindReplacePair>();
    public List<TagAddressReplacePair> AddressReplacements { get; private set; } = new List<TagAddressReplacePair>();

    private readonly List<TagExample> _sampleTags;
    private readonly List<int> _availableDigitLengths;

    public TagTableConfigurationForm(string tableName, int tagCount, List<TagExample> sampleTags = null)
    {
        InitializeComponent();
        _lblTableInfo.Text = $"Table: {tableName} ({tagCount} tags)";
        _sampleTags = sampleTags ?? new List<TagExample>();
        _availableDigitLengths = TagAddressAnalyzer.GetAvailableDigitLengths(_sampleTags);
        InitializeDropdownOptions();
        UpdatePreview();
    }

    private void InitializeDropdownOptions()
    {
        // Initialize Length dropdown with available options
        var lengthColumn = (DataGridViewComboBoxColumn)_dataGridViewAddressPairs.Columns[2];
        lengthColumn.Items.Clear();
        foreach (int length in _availableDigitLengths)
        {
            lengthColumn.Items.Add(length.ToString());
        }
        // Don't set a default value - let cells be empty until user makes selection
        lengthColumn.DefaultCellStyle.NullValue = "";
        
        // Same for position column
        var positionColumn = (DataGridViewComboBoxColumn)_dataGridViewAddressPairs.Columns[3];
        positionColumn.DefaultCellStyle.NullValue = "";
    }

    private void PopulatePositionDropdown(DataGridViewCell lengthCell, DataGridViewCell positionCell)
    {
        if (lengthCell.Value != null && int.TryParse(lengthCell.Value.ToString(), out int selectedLength))
        {
            var validPositions = TagAddressAnalyzer.GetValidPositionsForLength(selectedLength);
            var comboBoxCell = (DataGridViewComboBoxCell)positionCell;
            
            comboBoxCell.Items.Clear();
            foreach (int position in validPositions)
            {
                comboBoxCell.Items.Add(position.ToString());
            }
            
            // Set default value if no current value or current value is invalid
            bool hasValidCurrentValue = positionCell.Value != null && 
                                      int.TryParse(positionCell.Value.ToString(), out int currentPosition) && 
                                      validPositions.Contains(currentPosition);
            
            if (!hasValidCurrentValue)
            {
                positionCell.Value = validPositions.Count > 0 ? validPositions[0].ToString() : "";
            }
        }
        else
        {
            // Clear position options if no valid length selected
            var comboBoxCell = (DataGridViewComboBoxCell)positionCell;
            comboBoxCell.Items.Clear();
            positionCell.Value = "";
        }
    }
    
    public void LoadConfiguration(List<FindReplacePair> existingNameReplacements, List<TagAddressReplacePair> existingAddressReplacements)
    {
        // Clear existing rows (except new row)
        _dataGridViewNamePairs.Rows.Clear();
        _dataGridViewAddressPairs.Rows.Clear();
        
        // Add existing name replacements to grid
        foreach (var pair in existingNameReplacements)
        {
            _dataGridViewNamePairs.Rows.Add(pair.FindString, pair.ReplaceString);
        }
        
        // Add existing address replacements to grid
        foreach (var pair in existingAddressReplacements)
        {
            int rowIndex = _dataGridViewAddressPairs.Rows.Add(pair.FindString, pair.ReplaceString, null, null);
            var row = _dataGridViewAddressPairs.Rows[rowIndex];
            
            // Initialize ComboBox cells for the loaded row
            InitializeComboBoxCellsForRow(row);
            
            // Set the values after initializing the ComboBox items
            row.Cells[2].Value = pair.LengthFilter.ToString();
            PopulatePositionDropdown(row.Cells[2], row.Cells[3]);
            row.Cells[3].Value = pair.DigitPosition.ToString();
        }
        
        // Update internal lists and preview
        UpdateNameReplacementsFromGrid();
        UpdateAddressReplacementsFromGrid();
        UpdatePreview();
    }

    private void dataGridViewNamePairs_CellValueChanged(object sender, DataGridViewCellEventArgs e)
    {
        UpdateNameReplacementsFromGrid();
        UpdatePreview();
    }

    private void dataGridViewNamePairs_CurrentCellDirtyStateChanged(object sender, EventArgs e)
    {
        if (_dataGridViewNamePairs.IsCurrentCellDirty)
        {
            _dataGridViewNamePairs.CommitEdit(DataGridViewDataErrorContexts.Commit);
        }
    }

    private void btnAddNamePair_Click(object sender, EventArgs e)
    {
        _dataGridViewNamePairs.Rows.Add("", "");
        UpdateNameReplacementsFromGrid();
        UpdatePreview();
    }

    private void btnRemoveNamePair_Click(object sender, EventArgs e)
    {
        if (_dataGridViewNamePairs.SelectedRows.Count > 0)
        {
            int selectedIndex = _dataGridViewNamePairs.SelectedRows[0].Index;
            if (selectedIndex < _dataGridViewNamePairs.Rows.Count - 1) // Don't remove the new row
            {
                _dataGridViewNamePairs.Rows.RemoveAt(selectedIndex);
                UpdateNameReplacementsFromGrid();
                UpdatePreview();
            }
        }
    }

    private void dataGridViewAddressPairs_CellValueChanged(object sender, DataGridViewCellEventArgs e)
    {
        // Handle Length column changes (column index 2)
        if (e.ColumnIndex == 2 && e.RowIndex >= 0)
        {
            var row = _dataGridViewAddressPairs.Rows[e.RowIndex];
            if (!row.IsNewRow)
            {
                PopulatePositionDropdown(row.Cells[2], row.Cells[3]);
            }
        }
        
        UpdateAddressReplacementsFromGrid();
        UpdatePreview();
    }

    private void dataGridViewAddressPairs_CurrentCellDirtyStateChanged(object sender, EventArgs e)
    {
        if (_dataGridViewAddressPairs.IsCurrentCellDirty)
        {
            _dataGridViewAddressPairs.CommitEdit(DataGridViewDataErrorContexts.Commit);
        }
    }

    private void dataGridViewAddressPairs_RowsAdded(object sender, DataGridViewRowsAddedEventArgs e)
    {
        // Initialize ComboBox cells for any new rows that get added automatically
        for (int i = e.RowIndex; i < e.RowIndex + e.RowCount; i++)
        {
            if (i < _dataGridViewAddressPairs.Rows.Count)
            {
                var row = _dataGridViewAddressPairs.Rows[i];
                if (!row.IsNewRow)
                {
                    InitializeComboBoxCellsForRow(row);
                }
            }
        }
    }

    private void dataGridViewAddressPairs_CellBeginEdit(object sender, DataGridViewCellCancelEventArgs e)
    {
        // Prevent editing Position column (index 3) if Length column (index 2) is empty
        if (e.ColumnIndex == 3 && e.RowIndex >= 0)
        {
            var row = _dataGridViewAddressPairs.Rows[e.RowIndex];
            var lengthCell = row.Cells[2];
            
            if (lengthCell.Value == null || string.IsNullOrEmpty(lengthCell.Value.ToString()))
            {
                MessageBox.Show("Please select a Length first before selecting a Position.", 
                    "Selection Order", MessageBoxButtons.OK, MessageBoxIcon.Information);
                e.Cancel = true; // Cancel the edit operation
            }
        }
    }

    private void dataGridViewAddressPairs_CellClick(object sender, DataGridViewCellEventArgs e)
    {
        // Auto-open dropdown for ComboBox columns (Length and Position)
        if (e.RowIndex >= 0 && (e.ColumnIndex == 2 || e.ColumnIndex == 3))
        {
            var dataGridView = (DataGridView)sender;
            
            // For Position column, check if Length is selected first
            if (e.ColumnIndex == 3)
            {
                var lengthCell = dataGridView.Rows[e.RowIndex].Cells[2];
                if (lengthCell.Value == null || string.IsNullOrEmpty(lengthCell.Value.ToString()))
                {
                    MessageBox.Show("Please select a Length first before selecting a Position.", 
                        "Selection Order", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
            }
            
            // Begin edit mode to open the dropdown immediately
            dataGridView.BeginEdit(true);
            
            // If it's a ComboBox cell, send the dropdown command
            if (dataGridView.EditingControl is ComboBox comboBox)
            {
                comboBox.DroppedDown = true;
            }
        }
    }

    private void btnAddAddressPair_Click(object sender, EventArgs e)
    {
        int newRowIndex = _dataGridViewAddressPairs.Rows.Add("", "", null, null);
        var newRow = _dataGridViewAddressPairs.Rows[newRowIndex];
        
        // Initialize ComboBox cells for the new row
        InitializeComboBoxCellsForRow(newRow);
        
        UpdateAddressReplacementsFromGrid();
        UpdatePreview();
    }

    private void InitializeComboBoxCellsForRow(DataGridViewRow row)
    {
        // Initialize Length ComboBox cell
        var lengthCell = (DataGridViewComboBoxCell)row.Cells[2];
        lengthCell.Items.Clear();
        foreach (int length in _availableDigitLengths)
        {
            lengthCell.Items.Add(length.ToString());
        }
        lengthCell.Value = ""; // Explicitly set to empty
        
        // Initialize Position ComboBox cell (initially empty until Length is selected)
        var positionCell = (DataGridViewComboBoxCell)row.Cells[3];
        positionCell.Items.Clear();
        positionCell.Value = ""; // Explicitly set to empty
    }

    private void btnRemoveAddressPair_Click(object sender, EventArgs e)
    {
        if (_dataGridViewAddressPairs.SelectedRows.Count > 0)
        {
            int selectedIndex = _dataGridViewAddressPairs.SelectedRows[0].Index;
            if (selectedIndex < _dataGridViewAddressPairs.Rows.Count - 1) // Don't remove the new row
            {
                _dataGridViewAddressPairs.Rows.RemoveAt(selectedIndex);
                UpdateAddressReplacementsFromGrid();
                UpdatePreview();
            }
        }
    }

    private void UpdateNameReplacementsFromGrid()
    {
        NameReplacements.Clear();
        foreach (DataGridViewRow row in _dataGridViewNamePairs.Rows)
        {
            if (row.IsNewRow) continue;
            
            string findText = row.Cells[0].Value?.ToString() ?? "";
            string replaceText = row.Cells[1].Value?.ToString() ?? "";
            
            if (!string.IsNullOrEmpty(findText))
            {
                NameReplacements.Add(new FindReplacePair(findText, replaceText));
            }
        }
    }

    private void UpdateAddressReplacementsFromGrid()
    {
        AddressReplacements.Clear();
        foreach (DataGridViewRow row in _dataGridViewAddressPairs.Rows)
        {
            if (row.IsNewRow) continue;
            
            string findText = row.Cells[0].Value?.ToString() ?? "";
            string replaceText = row.Cells[1].Value?.ToString() ?? "";
            string lengthText = row.Cells[2].Value?.ToString() ?? "1";
            string positionText = row.Cells[3].Value?.ToString() ?? "1";
            
            if (!string.IsNullOrEmpty(findText))
            {
                if (int.TryParse(positionText, out int position) && position > 0 &&
                    int.TryParse(lengthText, out int length) && length > 0)
                {
                    AddressReplacements.Add(new TagAddressReplacePair(findText, replaceText, position, length));
                }
            }
        }
    }

    private void UpdatePreview()
    {
        // Save current scroll position
        int scrollPos = GetScrollPos(_lblPreview.Handle, SbVert);

        // Get examples to use in preview - either from real tag data or fallback defaults
        var exampleTags = GetPreviewExamples();

        string preview = "";

        // Header
        preview += "=== TAG TRANSFORMATION PREVIEW ===" + Environment.NewLine + Environment.NewLine;

        // Show examples section first
        preview += "EXAMPLES FROM TAG TABLE:" + Environment.NewLine;
        if (exampleTags.Count == 0)
        {
            preview += "  No examples available from tag table" + Environment.NewLine;
            preview += "  (Using default examples for preview)" + Environment.NewLine + Environment.NewLine;
        }
        else
        {
            foreach (var example in exampleTags)
            {
                preview += $"  {example.Name} -> {example.Address} ({example.DigitCount} digits)" + Environment.NewLine;
            }
            preview += Environment.NewLine;
        }

        // Process each example
        foreach (var example in exampleTags)
        {
            preview += $"=== EXAMPLE: {example.Name} ({example.DigitCount} digits) ===" + Environment.NewLine;

            // Name transformations
            preview += "NAME PROCESSING:" + Environment.NewLine;
            if (NameReplacements.Count == 0)
            {
                preview += $"  {example.Name} -> {example.Name}" + Environment.NewLine;
                preview += "  (No name changes configured)" + Environment.NewLine;
            }
            else
            {
                string currentName = example.Name;
                preview += $"  Original: {example.Name}" + Environment.NewLine;
                
                for (int i = 0; i < NameReplacements.Count; i++)
                {
                    var pair = NameReplacements[i];
                    string newName = currentName.Replace(pair.FindString, pair.ReplaceString);
                    preview += $"  Step {i + 1}: {currentName} -> {newName}" + Environment.NewLine;
                    preview += $"           Find: '{pair.FindString}' -> Replace: '{pair.ReplaceString}'" + Environment.NewLine;
                    currentName = newName;
                }
                preview += $"  Final Name: {currentName}" + Environment.NewLine;
            }

            preview += Environment.NewLine;

            // Address transformations
            preview += "ADDRESS PROCESSING:" + Environment.NewLine;
            if (AddressReplacements.Count == 0)
            {
                preview += $"  {example.Address} -> {example.Address}" + Environment.NewLine;
                preview += "  (No address changes configured)" + Environment.NewLine;
            }
            else
            {
                preview += $"  Original: {example.Address}" + Environment.NewLine;
                
                string currentAddress = example.Address;
                for (int i = 0; i < AddressReplacements.Count; i++)
                {
                    var pair = AddressReplacements[i];
                    string newAddress = ProcessExampleAddress(currentAddress, pair);
                    preview += $"  Step {i + 1}: {currentAddress} -> {newAddress}" + Environment.NewLine;
                    preview += $"           Find: '{pair.FindString}' -> Replace: '{pair.ReplaceString}'" + Environment.NewLine;
                    preview += $"           Position: {pair.DigitPosition} (right-to-left)" + Environment.NewLine;
                    preview += $"           Length Filter: {pair.LengthFilter} digit(s)" + Environment.NewLine;
                    
                    if (newAddress == currentAddress)
                    {
                        preview += $"           ⚠️  No change (criteria not met)" + Environment.NewLine;
                    }
                    currentAddress = newAddress;
                }
                preview += $"  Final Address: {currentAddress}" + Environment.NewLine;
            }

            preview += $"{Environment.NewLine}FINAL RESULT FOR {example.Name}:" + Environment.NewLine;
            preview += $"  Name: {(NameReplacements.Count > 0 ? ProcessFinalName(example.Name) : example.Name)}" + Environment.NewLine;
            preview += $"  Address: {(AddressReplacements.Count > 0 ? ProcessFinalAddress(example.Address) : example.Address)}" + Environment.NewLine + Environment.NewLine;
        }

        // Summary
        preview += "=== CONFIGURATION SUMMARY ===" + Environment.NewLine;
        preview += $"Name Replacements: {NameReplacements.Count}" + Environment.NewLine;
        preview += $"Address Replacements: {AddressReplacements.Count}" + Environment.NewLine;
        preview += $"Examples shown: {exampleTags.Count}";
        
        if (NameReplacements.Count == 0 && AddressReplacements.Count == 0)
        {
            preview += Environment.NewLine + Environment.NewLine + "Result: Tags will be copied with '_1' suffix" + Environment.NewLine;
            preview += "(No transformations configured)";
        }

        int totalReplacements = NameReplacements.Count + AddressReplacements.Count;
        _lblPreviewTitle.Text = totalReplacements == 0
            ? "Preview:"
            : $"Preview ({totalReplacements} total replacements):";

        _lblPreview.Text = preview;

        // Restore scroll position
        SetScrollPos(_lblPreview.Handle, SbVert, scrollPos, true);
        PostMessageA(_lblPreview.Handle, WmVertScroll, SbThumbPosition + 0x10000 * scrollPos, 0);
    }

    private List<TagExample> GetPreviewExamples()
    {
        // If we have sample tags from the actual table, ensure we get only one example per digit variation
        if (_sampleTags.Count > 0)
        {
            // Group by digit count to ensure only one example per variation
            var uniqueExamples = _sampleTags
                .GroupBy(tag => tag.DigitCount)
                .Select(group => group.First())
                .OrderBy(tag => tag.DigitCount)
                .ToList();
            return uniqueExamples;
        }

        // Fallback to examples matching available digit lengths if no real data available
        var examples = new List<TagExample>();
        foreach (int digitLength in _availableDigitLengths)
        {
            examples.Add(new TagExample 
            { 
                Name = $"Tag_{digitLength}Digit", 
                Address = GenerateExampleAddress(digitLength), 
                DigitCount = digitLength 
            });
        }
        
        return examples;
    }

    private string GenerateExampleAddress(int digitCount)
    {
        // Generate example addresses with the specified digit count
        string digits = new string('1', digitCount); // e.g., "11", "111", "1111"
        return $"%M{digits}.0";
    }

    private string ProcessFinalName(string name)
    {
        string currentName = name;
        foreach (var pair in NameReplacements)
        {
            currentName = currentName.Replace(pair.FindString, pair.ReplaceString);
        }
        return currentName;
    }

    private string ProcessFinalAddress(string address)
    {
        string currentAddress = address;
        foreach (var pair in AddressReplacements)
        {
            currentAddress = ProcessExampleAddress(currentAddress, pair);
        }
        return currentAddress;
    }

    private string ProcessExampleAddress(string address, TagAddressReplacePair pair)
    {
        // Use same algorithm as TagCopyService for preview accuracy
        if (address.Contains("%") && address.Contains("."))
        {
            int percentIndex = address.IndexOf('%');
            int dotIndex = address.IndexOf('.');
            
            if (dotIndex > percentIndex + 1)
            {
                // Find where the prefix ends and digits begin
                int digitStartIndex = percentIndex + 1;
                while (digitStartIndex < dotIndex && !char.IsDigit(address[digitStartIndex]))
                {
                    digitStartIndex++;
                }
                
                if (digitStartIndex < dotIndex)
                {
                    string prefix = address.Substring(0, digitStartIndex); // e.g., "%M", "%QW", "%MW"
                    string digits = address.Substring(digitStartIndex, dotIndex - digitStartIndex); // e.g., "127", "1234"
                    string suffix = address.Substring(dotIndex); // e.g., ".0", ".7"
                    
                    // Check length filter - only process addresses with specified digit count
                    if (digits.Length == pair.LengthFilter)
                    {
                        // Apply replacement to specific digit position (right-to-left counting)
                        if (pair.DigitPosition > 0 && pair.DigitPosition <= digits.Length && !string.IsNullOrEmpty(pair.FindString))
                        {
                            int findLength = pair.FindString.Length;
                            int rightmostIndex = digits.Length - pair.DigitPosition; // Where rightmost digit of find string should be
                            int leftmostIndex = rightmostIndex - findLength + 1; // Where leftmost digit of find string should be
                            
                            // Check if we have enough digits to the left for the find string
                            if (leftmostIndex >= 0 && rightmostIndex < digits.Length)
                            {
                                // Extract the substring to compare
                                string currentSubstring = digits.Substring(leftmostIndex, findLength);
                                
                                if (currentSubstring == pair.FindString)
                                {
                                    // Replace the multi-digit sequence
                                    string beforeReplacement = digits.Substring(0, leftmostIndex);
                                    string afterReplacement = digits.Substring(rightmostIndex + 1);
                                    string replacement = pair.ReplaceString ?? "";
                                    
                                    digits = beforeReplacement + replacement + afterReplacement;
                                }
                            }
                        }
                    }
                    
                    return prefix + digits + suffix;
                }
            }
        }
        
        return address; // Return unchanged if parsing fails
    }

    private void btnOK_Click(object sender, EventArgs e)
    {
        // Update pairs from grids before validation
        UpdateNameReplacementsFromGrid();
        UpdateAddressReplacementsFromGrid();
        
        // Validate name replacement inputs
        foreach (var pair in NameReplacements)
        {
            if (!string.IsNullOrEmpty(pair.FindString) && string.IsNullOrEmpty(pair.ReplaceString))
            {
                MessageBox.Show($"If you specify a 'Find' string ('{pair.FindString}'), you must also specify a 'Replace' string.", 
                    "Invalid Name Configuration", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _dataGridViewNamePairs.Focus();
                return;
            }
        }
        
        // Validate address replacement inputs
        foreach (var pair in AddressReplacements)
        {
            if (!string.IsNullOrEmpty(pair.FindString) && string.IsNullOrEmpty(pair.ReplaceString))
            {
                MessageBox.Show($"If you specify a 'Find' string ('{pair.FindString}'), you must also specify a 'Replace' string.", 
                    "Invalid Address Configuration", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _dataGridViewAddressPairs.Focus();
                return;
            }
            
            if (pair.DigitPosition <= 0)
            {
                MessageBox.Show($"Digit Position must be greater than 0. Current value: {pair.DigitPosition}", 
                    "Invalid Address Configuration", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _dataGridViewAddressPairs.Focus();
                return;
            }
            
            if (pair.LengthFilter <= 0)
            {
                MessageBox.Show($"Length Filter must be greater than 0. Current value: {pair.LengthFilter}", 
                    "Invalid Address Configuration", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _dataGridViewAddressPairs.Focus();
                return;
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
        this._lblTableInfo = new Label();
        this._grpNameConfig = new GroupBox();
        this._lblNameInstructions = new Label();
        this._lblNamePairs = new Label();
        this._dataGridViewNamePairs = new DataGridView();
        this._btnAddNamePair = new Button();
        this._btnRemoveNamePair = new Button();
        this._grpAddressConfig = new GroupBox();
        this._lblAddressInstructions = new Label();
        this._lblAddressPairs = new Label();
        this._dataGridViewAddressPairs = new DataGridView();
        this._btnAddAddressPair = new Button();
        this._btnRemoveAddressPair = new Button();
        this._lblPreviewTitle = new Label();
        this._lblPreview = new TextBox();
        this._btnOk = new Button();
        this._btnCancel = new Button();
        this._grpNameConfig.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)(this._dataGridViewNamePairs)).BeginInit();
        this._grpAddressConfig.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)(this._dataGridViewAddressPairs)).BeginInit();
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
        this._lblTitle.Text = "Tag Table Configuration";
            
        // 
        // lblTableInfo
        // 
        this._lblTableInfo.AutoSize = true;
        this._lblTableInfo.Location = new System.Drawing.Point(20, 50);
        this._lblTableInfo.Name = "_lblTableInfo";
        this._lblTableInfo.Size = new System.Drawing.Size(120, 13);
        this._lblTableInfo.TabIndex = 1;
        this._lblTableInfo.Text = "Table Info";
            
        // 
        // grpNameConfig
        // 
        this._grpNameConfig.Controls.Add(this._lblNameInstructions);
        this._grpNameConfig.Controls.Add(this._lblNamePairs);
        this._grpNameConfig.Controls.Add(this._dataGridViewNamePairs);
        this._grpNameConfig.Controls.Add(this._btnAddNamePair);
        this._grpNameConfig.Controls.Add(this._btnRemoveNamePair);
        this._grpNameConfig.Location = new System.Drawing.Point(20, 80);
        this._grpNameConfig.Name = "_grpNameConfig";
        this._grpNameConfig.Size = new System.Drawing.Size(420, 240);
        this._grpNameConfig.TabIndex = 2;
        this._grpNameConfig.TabStop = false;
        this._grpNameConfig.Text = "Tag Name Configuration";
            
        // 
        // lblNameInstructions
        // 
        this._lblNameInstructions.AutoSize = true;
        this._lblNameInstructions.ForeColor = System.Drawing.Color.Gray;
        this._lblNameInstructions.Location = new System.Drawing.Point(15, 25);
        this._lblNameInstructions.MaximumSize = new System.Drawing.Size(380, 0);
        this._lblNameInstructions.Name = "_lblNameInstructions";
        this._lblNameInstructions.Size = new System.Drawing.Size(360, 26);
        this._lblNameInstructions.TabIndex = 0;
        this._lblNameInstructions.Text = "Optional: Add multiple find and replace pairs for tag names. Replacements are applied in order.";
            
        // 
        // lblNamePairs
        // 
        this._lblNamePairs.AutoSize = true;
        this._lblNamePairs.Location = new System.Drawing.Point(15, 60);
        this._lblNamePairs.Name = "_lblNamePairs";
        this._lblNamePairs.Size = new System.Drawing.Size(110, 13);
        this._lblNamePairs.TabIndex = 1;
        this._lblNamePairs.Text = "Find/Replace Pairs:";
            
        // 
        // dataGridViewNamePairs
        // 
        this._dataGridViewNamePairs.AllowUserToResizeRows = false;
        this._dataGridViewNamePairs.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
        this._dataGridViewNamePairs.Location = new System.Drawing.Point(15, 80);
        this._dataGridViewNamePairs.Name = "_dataGridViewNamePairs";
        this._dataGridViewNamePairs.Size = new System.Drawing.Size(320, 140);
        this._dataGridViewNamePairs.TabIndex = 2;
        this._dataGridViewNamePairs.ScrollBars = ScrollBars.Vertical;
        this._dataGridViewNamePairs.CellValueChanged += this.dataGridViewNamePairs_CellValueChanged;
        this._dataGridViewNamePairs.CurrentCellDirtyStateChanged += this.dataGridViewNamePairs_CurrentCellDirtyStateChanged;
        
        // Add columns
        var findColumn = new DataGridViewTextBoxColumn();
        findColumn.Name = "findColumn";
        findColumn.HeaderText = "Find";
        findColumn.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
        findColumn.FillWeight = 50; // Equal weight
        this._dataGridViewNamePairs.Columns.Add(findColumn);
        
        var replaceColumn = new DataGridViewTextBoxColumn();
        replaceColumn.Name = "replaceColumn";
        replaceColumn.HeaderText = "Replace With";
        replaceColumn.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
        replaceColumn.FillWeight = 50; // Equal weight
        this._dataGridViewNamePairs.Columns.Add(replaceColumn);
            
        // 
        // btnAddNamePair
        // 
        this._btnAddNamePair.Location = new System.Drawing.Point(350, 80);
        this._btnAddNamePair.Name = "_btnAddNamePair";
        this._btnAddNamePair.Size = new System.Drawing.Size(60, 25);
        this._btnAddNamePair.TabIndex = 3;
        this._btnAddNamePair.Text = "Add";
        this._btnAddNamePair.UseVisualStyleBackColor = true;
        this._btnAddNamePair.Click += this.btnAddNamePair_Click;
            
        // 
        // btnRemoveNamePair
        // 
        this._btnRemoveNamePair.Location = new System.Drawing.Point(350, 110);
        this._btnRemoveNamePair.Name = "_btnRemoveNamePair";
        this._btnRemoveNamePair.Size = new System.Drawing.Size(60, 25);
        this._btnRemoveNamePair.TabIndex = 4;
        this._btnRemoveNamePair.Text = "Remove";
        this._btnRemoveNamePair.UseVisualStyleBackColor = true;
        this._btnRemoveNamePair.Click += this.btnRemoveNamePair_Click;
            
        // 
        // grpAddressConfig
        // 
        this._grpAddressConfig.Controls.Add(this._lblAddressInstructions);
        this._grpAddressConfig.Controls.Add(this._lblAddressPairs);
        this._grpAddressConfig.Controls.Add(this._dataGridViewAddressPairs);
        this._grpAddressConfig.Controls.Add(this._btnAddAddressPair);
        this._grpAddressConfig.Controls.Add(this._btnRemoveAddressPair);
        this._grpAddressConfig.Location = new System.Drawing.Point(20, 340);
        this._grpAddressConfig.Name = "_grpAddressConfig";
        this._grpAddressConfig.Size = new System.Drawing.Size(420, 240);
        this._grpAddressConfig.TabIndex = 3;
        this._grpAddressConfig.TabStop = false;
        this._grpAddressConfig.Text = "Tag Address Configuration";
            
        // 
        // lblAddressInstructions
        // 
        this._lblAddressInstructions.AutoSize = true;
        this._lblAddressInstructions.ForeColor = System.Drawing.Color.Gray;
        this._lblAddressInstructions.Location = new System.Drawing.Point(15, 25);
        this._lblAddressInstructions.MaximumSize = new System.Drawing.Size(380, 0);
        this._lblAddressInstructions.Name = "_lblAddressInstructions";
        this._lblAddressInstructions.Size = new System.Drawing.Size(360, 39);
        this._lblAddressInstructions.TabIndex = 0;
        this._lblAddressInstructions.Text = "Optional: Configure address modifications. Position is counted right-to-left from decimal. Length Filter only processes addresses with specified digit count.";
            
        // 
        // lblAddressPairs
        // 
        this._lblAddressPairs.AutoSize = true;
        this._lblAddressPairs.Location = new System.Drawing.Point(15, 60);
        this._lblAddressPairs.Name = "_lblAddressPairs";
        this._lblAddressPairs.Size = new System.Drawing.Size(140, 13);
        this._lblAddressPairs.TabIndex = 1;
        this._lblAddressPairs.Text = "Address Replacements:";
            
        // 
        // dataGridViewAddressPairs
        // 
        this._dataGridViewAddressPairs.AllowUserToResizeRows = false;
        this._dataGridViewAddressPairs.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
        this._dataGridViewAddressPairs.Location = new System.Drawing.Point(15, 80);
        this._dataGridViewAddressPairs.Name = "_dataGridViewAddressPairs";
        this._dataGridViewAddressPairs.Size = new System.Drawing.Size(320, 140);
        this._dataGridViewAddressPairs.TabIndex = 2;
        this._dataGridViewAddressPairs.ScrollBars = ScrollBars.Vertical;
        this._dataGridViewAddressPairs.CellValueChanged += this.dataGridViewAddressPairs_CellValueChanged;
        this._dataGridViewAddressPairs.CurrentCellDirtyStateChanged += this.dataGridViewAddressPairs_CurrentCellDirtyStateChanged;
        this._dataGridViewAddressPairs.RowsAdded += this.dataGridViewAddressPairs_RowsAdded;
        this._dataGridViewAddressPairs.CellBeginEdit += this.dataGridViewAddressPairs_CellBeginEdit;
        this._dataGridViewAddressPairs.CellClick += this.dataGridViewAddressPairs_CellClick;
        
        // Add columns
        var findAddressColumn = new DataGridViewTextBoxColumn();
        findAddressColumn.Name = "findAddressColumn";
        findAddressColumn.HeaderText = "Find";
        findAddressColumn.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
        findAddressColumn.FillWeight = 25; // Equal weight across 4 columns
        this._dataGridViewAddressPairs.Columns.Add(findAddressColumn);
        
        var replaceAddressColumn = new DataGridViewTextBoxColumn();
        replaceAddressColumn.Name = "replaceAddressColumn";
        replaceAddressColumn.HeaderText = "Replace";
        replaceAddressColumn.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
        replaceAddressColumn.FillWeight = 25; // Equal weight across 4 columns
        this._dataGridViewAddressPairs.Columns.Add(replaceAddressColumn);
        
        var lengthColumn = new DataGridViewComboBoxColumn();
        lengthColumn.Name = "lengthColumn";
        lengthColumn.HeaderText = "Length";
        lengthColumn.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
        lengthColumn.FillWeight = 25; // Equal weight across 4 columns
        lengthColumn.DisplayStyle = DataGridViewComboBoxDisplayStyle.DropDownButton;
        lengthColumn.DropDownWidth = 80;
        this._dataGridViewAddressPairs.Columns.Add(lengthColumn);
        
        var positionColumn = new DataGridViewComboBoxColumn();
        positionColumn.Name = "positionColumn";
        positionColumn.HeaderText = "Position";
        positionColumn.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
        positionColumn.FillWeight = 25; // Equal weight across 4 columns
        positionColumn.DisplayStyle = DataGridViewComboBoxDisplayStyle.DropDownButton;
        positionColumn.DropDownWidth = 80;
        this._dataGridViewAddressPairs.Columns.Add(positionColumn);
            
        // 
        // btnAddAddressPair
        // 
        this._btnAddAddressPair.Location = new System.Drawing.Point(350, 80);
        this._btnAddAddressPair.Name = "_btnAddAddressPair";
        this._btnAddAddressPair.Size = new System.Drawing.Size(60, 25);
        this._btnAddAddressPair.TabIndex = 3;
        this._btnAddAddressPair.Text = "Add";
        this._btnAddAddressPair.UseVisualStyleBackColor = true;
        this._btnAddAddressPair.Click += this.btnAddAddressPair_Click;
            
        // 
        // btnRemoveAddressPair
        // 
        this._btnRemoveAddressPair.Location = new System.Drawing.Point(350, 110);
        this._btnRemoveAddressPair.Name = "_btnRemoveAddressPair";
        this._btnRemoveAddressPair.Size = new System.Drawing.Size(60, 25);
        this._btnRemoveAddressPair.TabIndex = 4;
        this._btnRemoveAddressPair.Text = "Remove";
        this._btnRemoveAddressPair.UseVisualStyleBackColor = true;
        this._btnRemoveAddressPair.Click += this.btnRemoveAddressPair_Click;
            
        // 
        // lblPreviewTitle
        // 
        this._lblPreviewTitle.AutoSize = true;
        this._lblPreviewTitle.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold);
        this._lblPreviewTitle.Location = new System.Drawing.Point(460, 80);
        this._lblPreviewTitle.Name = "_lblPreviewTitle";
        this._lblPreviewTitle.Size = new System.Drawing.Size(55, 13);
        this._lblPreviewTitle.TabIndex = 4;
        this._lblPreviewTitle.Text = "Preview:";
            
        // 
        // lblPreview (now a TextBox for scroll-ability)
        // 
        this._lblPreview.BackColor = System.Drawing.Color.LightGray;
        this._lblPreview.BorderStyle = BorderStyle.FixedSingle;
        this._lblPreview.Font = new System.Drawing.Font("Consolas", 8.25F);
        this._lblPreview.Location = new System.Drawing.Point(460, 100);
        this._lblPreview.Multiline = true;
        this._lblPreview.Name = "_lblPreview";
        this._lblPreview.ReadOnly = true;
        this._lblPreview.ScrollBars = ScrollBars.Vertical;
        this._lblPreview.Size = new System.Drawing.Size(480, 500);
        this._lblPreview.TabIndex = 5;
        this._lblPreview.Text = "Preview will appear here...";
            
        // 
        // btnOK
        // 
        this._btnOk.Location = new System.Drawing.Point(780, 620);
        this._btnOk.Name = "_btnOk";
        this._btnOk.Size = new System.Drawing.Size(75, 30);
        this._btnOk.TabIndex = 6;
        this._btnOk.Text = "OK";
        this._btnOk.UseVisualStyleBackColor = true;
        this._btnOk.Click += this.btnOK_Click;
            
        // 
        // btnCancel
        // 
        this._btnCancel.Location = new System.Drawing.Point(865, 620);
        this._btnCancel.Name = "_btnCancel";
        this._btnCancel.Size = new System.Drawing.Size(75, 30);
        this._btnCancel.TabIndex = 7;
        this._btnCancel.Text = "Cancel";
        this._btnCancel.UseVisualStyleBackColor = true;
        this._btnCancel.Click += this.btnCancel_Click;
            
        // 
        // TagTableConfigurationForm
        // 
        this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
        this.AutoScaleMode = AutoScaleMode.Font;
        this.ClientSize = new System.Drawing.Size(960, 670);
        this.Controls.Add(this._lblTitle);
        this.Controls.Add(this._lblTableInfo);
        this.Controls.Add(this._grpNameConfig);
        this.Controls.Add(this._grpAddressConfig);
        this.Controls.Add(this._lblPreviewTitle);
        this.Controls.Add(this._lblPreview);
        this.Controls.Add(this._btnOk);
        this.Controls.Add(this._btnCancel);
        this.FormBorderStyle = FormBorderStyle.FixedDialog;
        this.MaximizeBox = false;
        this.MinimizeBox = false;
        this.Name = "TagTableConfigurationForm";
        this.StartPosition = FormStartPosition.CenterParent;
        this.Text = "Tag Table Configuration";
        this._grpNameConfig.ResumeLayout(false);
        this._grpNameConfig.PerformLayout();
        ((System.ComponentModel.ISupportInitialize)(this._dataGridViewNamePairs)).EndInit();
        this._grpAddressConfig.ResumeLayout(false);
        this._grpAddressConfig.PerformLayout();
        ((System.ComponentModel.ISupportInitialize)(this._dataGridViewAddressPairs)).EndInit();
        this.ResumeLayout(false);
        this.PerformLayout();
    }

    private Label _lblTitle;
    private Label _lblTableInfo;
    private GroupBox _grpNameConfig;
    private Label _lblNameInstructions;
    private Label _lblNamePairs;
    private DataGridView _dataGridViewNamePairs;
    private Button _btnAddNamePair;
    private Button _btnRemoveNamePair;
    private GroupBox _grpAddressConfig;
    private Label _lblAddressInstructions;
    private Label _lblAddressPairs;
    private DataGridView _dataGridViewAddressPairs;
    private Button _btnAddAddressPair;
    private Button _btnRemoveAddressPair;
    private Label _lblPreviewTitle;
    private TextBox _lblPreview;
    private Button _btnOk;
    private Button _btnCancel;
}
