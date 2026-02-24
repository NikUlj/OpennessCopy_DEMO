using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using OpennessCopy.Models;
using OpennessCopy.Utils;
using Siemens.Engineering.HW;

namespace OpennessCopy.Forms.HardwareCopy;

public class ETModuleConfigurationForm : Form
{
    public List<TagAddressReplacePair> ETAddressReplacements { get; private set; } = new List<TagAddressReplacePair>();

    private readonly List<DeviceAddressInfo> _etModules;
    private readonly List<int> _availableDigitLengths;
    private readonly List<HardwareDeviceInfo> _selectedETDevices;
    // Dictionary structure: PLC Name -> Address Type -> Address -> Device.Module Name
    private readonly Dictionary<string, Dictionary<AddressIoType, Dictionary<int, string>>> _existingAddressesByPlc;
    private readonly string _selectedIoSystemPlcName; // PLC name from the selected IoSystem

    public ETModuleConfigurationForm(List<HardwareDeviceInfo> selectedETDevices,
        Dictionary<string, Dictionary<AddressIoType, Dictionary<int, string>>> existingAddressesByPlc = null,
        string selectedIoSystemPlcName = null)
    {
        InitializeComponent();

        // Store selected ET devices for preview grouping
        _selectedETDevices = selectedETDevices.Where(d => d.IsETDevice).ToList();
        _existingAddressesByPlc = existingAddressesByPlc ?? new Dictionary<string, Dictionary<AddressIoType, Dictionary<int, string>>>();
        _selectedIoSystemPlcName = selectedIoSystemPlcName;

        // Collect all ET modules from selected devices
        _etModules = new List<DeviceAddressInfo>();
        foreach (var device in _selectedETDevices)
        {
            foreach (var module in device.AddressModules)
            {
                _etModules.Add(module);
            }
        }

        _lblModuleInfo.Text = $"ET Modules: {_etModules.Count} modules found from {selectedETDevices.Count(d => d.IsETDevice)} devices";

        // Get available digit lengths from all start addresses
        var allAddresses = _etModules.SelectMany(m => m.AddressInfos.Select(a => a.StartAddress)).ToList();
        _availableDigitLengths = TagAddressAnalyzer.GetAvailableDigitLengthsFromIntegers(allAddresses);

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

    public void LoadConfiguration(List<TagAddressReplacePair> existingAddressReplacements)
    {
        // Clear existing rows (except new row)
        _dataGridViewAddressPairs.Rows.Clear();

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
        UpdateAddressReplacementsFromGrid();
        UpdatePreview();
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

    private void UpdateAddressReplacementsFromGrid()
    {
        ETAddressReplacements.Clear();
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
                    ETAddressReplacements.Add(new TagAddressReplacePair(findText, replaceText, position, length));
                }
            }
        }
    }

    /// <summary>
    /// Recursively saves the expanded state of nodes
    /// </summary>
    private void SaveNodeExpandedState(TreeNodeCollection nodes, string parentPath, HashSet<string> expandedPaths)
    {
        foreach (TreeNode node in nodes)
        {
            string nodePath = string.IsNullOrEmpty(parentPath) ? node.Text : $"{parentPath}\\{node.Text}";

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

    /// <summary>
    /// Recursively restores the expanded state of nodes
    /// </summary>
    private void RestoreNodeExpandedState(TreeNodeCollection nodes, string parentPath, HashSet<string> expandedPaths)
    {
        foreach (TreeNode node in nodes)
        {
            string nodePath = string.IsNullOrEmpty(parentPath) ? node.Text : $"{parentPath}\\{node.Text}";

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

    /// <summary>
    /// Finds the first visible node that is NOT a conflict
    /// Starting from the given node, searches down through visible nodes
    /// </summary>
    private TreeNode FindFirstNonConflictVisibleNode(TreeNode startNode)
    {
        // Calculate depth of start node
        int GetNodeDepth(TreeNode node)
        {
            int depth = 0;
            var temp = node;
            while (temp != null)
            {
                depth++;
                temp = temp.Parent;
            }
            return depth;
        }

        // If the start node is already non-conflict (depth <= 3), use it
        int startDepth = GetNodeDepth(startNode);
        if (startDepth <= 3)
        {
            return startNode;
        }

        // Start node is a conflict node, search siblings and descendants for first non-conflict
        // Start from the parent of the conflict and search forward
        TreeNode addressNode = startNode;
        while (GetNodeDepth(addressNode) > 3)
        {
            addressNode = addressNode.Parent;
        }

        // Now we're at the address level - but this might be the wrong address
        // We need to find the next non-conflict node after the current TopNode position
        // Search through all visible nodes starting from the conflict's parent

        // Get the next sibling or next node in tree order
        TreeNode nextNode = GetNextVisibleNode(startNode);
        while (nextNode != null)
        {
            int depth = GetNodeDepth(nextNode);
            if (depth <= 3)
            {
                return nextNode;
            }
            nextNode = GetNextVisibleNode(nextNode);
        }

        // If no non-conflict node found after, return the address node of the conflict
        return addressNode;
    }

    /// <summary>
    /// Gets the next visible node in tree order (like down arrow key navigation)
    /// </summary>
    private TreeNode GetNextVisibleNode(TreeNode node)
    {
        if (node == null) return null;

        // If it has children and is expanded, go to first child
        if (node.Nodes.Count > 0 && node.IsExpanded)
        {
            return node.Nodes[0];
        }

        // Try next sibling
        TreeNode current = node;
        while (current != null)
        {
            if (current.NextNode != null)
            {
                return current.NextNode;
            }
            // Go up to parent and try its next sibling
            current = current.Parent;
        }

        return null;
    }

    /// <summary>
    /// Finds a node by path hierarchy with fallback to parent nodes
    /// If the exact node doesn't exist, falls back to the closest parent that exists
    /// </summary>
    private TreeNode FindNodeByPathWithFallback(List<string> pathHierarchy)
    {
        if (pathHierarchy == null || pathHierarchy.Count == 0)
            return null;

        // Try to find the full path first, then progressively fall back to parents
        for (int depth = pathHierarchy.Count; depth > 0; depth--)
        {
            var pathToTry = pathHierarchy.Take(depth).ToList();
            var node = FindNodeByPath(pathToTry);
            if (node != null)
            {
                return node;
            }
        }

        return null;
    }

    /// <summary>
    /// Finds a node by following a path hierarchy (root to leaf)
    /// </summary>
    private TreeNode FindNodeByPath(List<string> path)
    {
        if (path == null || path.Count == 0)
            return null;

        TreeNodeCollection currentNodes = _treeViewPreview.Nodes;
        TreeNode currentNode = null;

        foreach (var nodeName in path)
        {
            currentNode = null;
            foreach (TreeNode node in currentNodes)
            {
                if (node.Text == nodeName)
                {
                    currentNode = node;
                    currentNodes = node.Nodes;
                    break;
                }
            }

            if (currentNode == null)
            {
                // Path not found
                return null;
            }
        }

        return currentNode;
    }

    private void UpdatePreview()
    {
        // Save current expanded state and scroll position before updating
        var expandedPaths = new HashSet<string>();
        SaveNodeExpandedState(_treeViewPreview.Nodes, "", expandedPaths);

        // Save the TopNode path hierarchy for semantic scroll position restoration
        // Save BOTH the exact TopNode path and a fallback non-conflict path
        List<string> topNodePathHierarchy = new List<string>();
        List<string> fallbackPathHierarchy = new List<string>();

        if (_treeViewPreview.TopNode != null)
        {
            // Save the exact TopNode path (even if it's a conflict)
            var node = _treeViewPreview.TopNode;
            while (node != null)
            {
                topNodePathHierarchy.Insert(0, node.Text);
                node = node.Parent;
            }

            // Also save a fallback path to the first non-conflict node
            TreeNode fallbackNode = FindFirstNonConflictVisibleNode(_treeViewPreview.TopNode);
            if (fallbackNode != null)
            {
                node = fallbackNode;
                while (node != null)
                {
                    fallbackPathHierarchy.Insert(0, node.Text);
                    node = node.Parent;
                }
            }
        }

        _treeViewPreview.BeginUpdate();
        _treeViewPreview.Nodes.Clear();

        if (_etModules.Count == 0)
        {
            var noModulesNode = new TreeNode("No ET modules detected.")
            {
                ImageKey = "warning",
                SelectedImageKey = "warning"
            };
            _treeViewPreview.Nodes.Add(noModulesNode);
        }
        else
        {
            // Group modules by device and show device ItemName
            foreach (var device in _selectedETDevices)
            {
                var deviceDisplayName = device.ItemName ?? device.Name;
                var deviceNode = new TreeNode($"{deviceDisplayName} (ET200SP-Station)")
                {
                    ImageKey = "folder",
                    SelectedImageKey = "folder",
                    Tag = device
                };

                foreach (var module in device.AddressModules)
                {
                    var moduleNode = new TreeNode(module.ModuleName)
                    {
                        ImageKey = "folder",
                        SelectedImageKey = "folder",
                        Tag = module
                    };

                    foreach (var addressInfo in module.AddressInfos)
                    {
                        var address = addressInfo.StartAddress;
                        int transformedAddress = address;

                        // Apply all transformations
                        foreach (var pair in ETAddressReplacements)
                        {
                            transformedAddress = ProcessAddressTransformation(transformedAddress, pair);
                        }

                        // Determine address type for display
                        string addressType = addressInfo.Type switch
                        {
                            AddressIoType.Input => "(I)",
                            AddressIoType.Output => "(Q)",
                            _ => $"({addressInfo.Type})"
                        };

                        // Build the address display text
                        var transformationText = transformedAddress != address
                            ? $"{address} -> {transformedAddress} {addressType}"
                            : $"{address} {addressType} (unchanged)";

                        // Check for conflicts for this specific address (uses selected IoSystem's PLC)
                        var hasConflicts = CheckAddressForConflicts(transformedAddress, addressInfo, _selectedIoSystemPlcName);
                        var addressNode = new TreeNode(transformationText)
                        {
                            ImageKey = hasConflicts ? "warning" : "success",
                            SelectedImageKey = hasConflicts ? "warning" : "success",
                            Tag = addressInfo
                        };

                        // Add conflict details as child nodes if there are conflicts
                        if (hasConflicts)
                        {
                            var conflicts = GetConflictsForAddress(transformedAddress, addressInfo, _selectedIoSystemPlcName);
                            foreach (var conflict in conflicts)
                            {
                                var conflictNode = new TreeNode(conflict)
                                {
                                    ImageKey = "error",
                                    SelectedImageKey = "error"
                                };
                                addressNode.Nodes.Add(conflictNode);
                            }
                        }

                        moduleNode.Nodes.Add(addressNode);
                    }

                    deviceNode.Nodes.Add(moduleNode);
                }

                _treeViewPreview.Nodes.Add(deviceNode);
            }
        }

        _lblPreviewTitle.Text = ETAddressReplacements.Count == 0
            ? "Preview:"
            : $"Preview ({ETAddressReplacements.Count} transformations):";

        // Restore expanded state BEFORE ending the update cycle
        if (expandedPaths.Count > 0)
        {
            RestoreNodeExpandedState(_treeViewPreview.Nodes, "", expandedPaths);
        }
        else
        {
            // Collapse all nodes by default for cleaner initial view (only if no previous state)
            _treeViewPreview.CollapseAll();
        }

        // End the single update cycle
        _treeViewPreview.EndUpdate();

        // Update status column to reflect current conflicts
        UpdateStatusColumn();

        // Restore scroll position AFTER everything is completely done
        // Try exact TopNode first, then fall back to non-conflict node
        TreeNode nodeToShow = null;

        // First priority: Try to find the exact TopNode (even if it's a conflict)
        if (topNodePathHierarchy.Count > 0)
        {
            nodeToShow = FindNodeByPathWithFallback(topNodePathHierarchy);
        }

        // Fallback: If exact node not found, use the non-conflict fallback
        if (nodeToShow == null && fallbackPathHierarchy.Count > 0)
        {
            nodeToShow = FindNodeByPathWithFallback(fallbackPathHierarchy);
        }

        // Set the TopNode if we found something
        if (nodeToShow != null)
        {
            try
            {
                _treeViewPreview.TopNode = nodeToShow;
            }
            catch
            {
                // Fallback - if TopNode setting fails, do nothing
            }
        }
    }

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
    /// Creates a simple bitmap icon with text and background color
    /// </summary>
    private System.Drawing.Bitmap CreateIconBitmap(string text, System.Drawing.Color color)
    {
        var bitmap = new System.Drawing.Bitmap(16, 16);
        using var graphics = System.Drawing.Graphics.FromImage(bitmap);
        graphics.Clear(System.Drawing.Color.Transparent);
        using var brush = new System.Drawing.SolidBrush(color);
        using var font = new System.Drawing.Font("Segoe UI", 8F, System.Drawing.FontStyle.Bold);
        var textSize = graphics.MeasureString(text, font);
        var x = (16 - textSize.Width) / 2;
        var y = (16 - textSize.Height) / 2;
        graphics.DrawString(text, font, brush, x, y);

        return bitmap;
    }
    
    /// <summary>
    /// Checks if a specific transformed address has any conflicts
    /// Uses PLC-scoped conflict checking - only checks against addresses from the same controlling PLC
    /// </summary>
    private bool CheckAddressForConflicts(int transformedAddress, AddressInfo addressInfo, string controllingPlcName)
    {
        // Calculate how many addresses this I/O occupies (length is in bits, convert to bytes)
        int addressCount = (addressInfo.Length + 7) / 8; // Convert bits to bytes (round up)

        // Check each address that this I/O occupies
        for (int offset = 0; offset < addressCount; offset++)
        {
            int address = transformedAddress + offset;

            // Check against existing project addresses for this specific PLC
            if (!string.IsNullOrWhiteSpace(controllingPlcName) &&
                _existingAddressesByPlc.TryGetValue(controllingPlcName, out var plcAddresses) &&
                plcAddresses.ContainsKey(addressInfo.Type) &&
                plcAddresses[addressInfo.Type].ContainsKey(address))
            {
                return true;
            }

            // Check against other selected devices (all will be connected to the selected IoSystem's PLC after copy)
            foreach (var device in _selectedETDevices)
            {
                foreach (var module in device.AddressModules)
                {
                    foreach (var otherAddressInfo in module.AddressInfos)
                    {
                        if (otherAddressInfo == addressInfo) continue; // Skip self

                        int otherTransformedAddress = otherAddressInfo.StartAddress;
                        foreach (var pair in ETAddressReplacements)
                        {
                            otherTransformedAddress = ProcessAddressTransformation(otherTransformedAddress, pair);
                        }

                        int otherAddressCount = (otherAddressInfo.Length + 7) / 8;
                        for (int otherOffset = 0; otherOffset < otherAddressCount; otherOffset++)
                        {
                            if (address == otherTransformedAddress + otherOffset &&
                                addressInfo.Type == otherAddressInfo.Type)
                            {
                                return true;
                            }
                        }
                    }
                }
            }
        }
        
        return false;
    }

    /// <summary>
    /// Gets detailed conflict descriptions for a specific address
    /// Uses PLC-scoped conflict checking - only checks against addresses from the same controlling PLC
    /// </summary>
    private List<string> GetConflictsForAddress(int transformedAddress, AddressInfo addressInfo, string controllingPlcName)
    {
        var conflicts = new List<string>();

        // Calculate how many addresses this I/O occupies (length is in bits, convert to bytes)
        int addressCount = (addressInfo.Length + 7) / 8; // Convert bits to bytes (round up)

        // Check each address that this I/O occupies
        for (int offset = 0; offset < addressCount; offset++)
        {
            int address = transformedAddress + offset;

            // Check against existing project addresses for this specific PLC
            if (!string.IsNullOrWhiteSpace(controllingPlcName) &&
                _existingAddressesByPlc.TryGetValue(controllingPlcName, out var plcAddresses) &&
                plcAddresses.ContainsKey(addressInfo.Type) &&
                plcAddresses[addressInfo.Type].ContainsKey(address))
            {
                var conflictingModule = plcAddresses[addressInfo.Type][address];
                conflicts.Add($"Address {address} conflicts with existing '{conflictingModule}'");
            }

            // Check against other selected devices connected to the SAME PLC
            foreach (var device in _selectedETDevices)
            {
                // Only check devices connected to the same PLC
                if (device.ControllingPlcName != controllingPlcName)
                {
                    continue;
                }

                var deviceDisplayName = device.ItemName ?? device.Name;
                foreach (var module in device.AddressModules)
                {
                    foreach (var otherAddressInfo in module.AddressInfos)
                    {
                        if (otherAddressInfo == addressInfo) continue; // Skip self

                        int otherTransformedAddress = otherAddressInfo.StartAddress;
                        foreach (var pair in ETAddressReplacements)
                        {
                            otherTransformedAddress = ProcessAddressTransformation(otherTransformedAddress, pair);
                        }

                        int otherAddressCount = (otherAddressInfo.Length + 7) / 8;
                        for (int otherOffset = 0; otherOffset < otherAddressCount; otherOffset++)
                        {
                            if (address == otherTransformedAddress + otherOffset &&
                                addressInfo.Type == otherAddressInfo.Type)
                            {
                                conflicts.Add($"Address {address} conflicts with {deviceDisplayName}.{module.ModuleName}");
                            }
                        }
                    }
                }
            }
        }

        return conflicts.Distinct().ToList();
    }

    /// <summary>
    /// Updates the status column in the DataGridView to show conflict icons for each transformation rule
    /// </summary>
    private void UpdateStatusColumn()
    {
        foreach (DataGridViewRow row in _dataGridViewAddressPairs.Rows)
        {
            if (row.IsNewRow) continue;

            string findText = row.Cells[0].Value?.ToString() ?? "";
            string replaceText = row.Cells[1].Value?.ToString() ?? "";
            string lengthText = row.Cells[2].Value?.ToString() ?? "";
            string positionText = row.Cells[3].Value?.ToString() ?? "";

            // Check if this transformation rule is valid and configured
            if (string.IsNullOrEmpty(findText))
            {
                row.Cells[4].Value = "➖"; // Gray dash for unconfigured
                row.Cells[4].Style.BackColor = System.Drawing.Color.LightGray;
                continue;
            }

            // Validate the transformation rule parameters
            if (!int.TryParse(positionText, out int position) || position <= 0 ||
                !int.TryParse(lengthText, out int length) || length <= 0)
            {
                row.Cells[4].Value = "❌"; // Red X for validation error
                row.Cells[4].Style.BackColor = System.Drawing.Color.LightPink;
                continue;
            }

            // Check if this rule actually makes any changes
            var (ruleHasConflicts, ruleChangesAddresses) = CheckTransformationRuleStatus(findText, replaceText, position, length);

            if (!ruleChangesAddresses)
            {
                // Rule doesn't match any addresses - no effect
                row.Cells[4].Value = "➖"; // Gray dash for no effect
                row.Cells[4].Style.BackColor = System.Drawing.Color.LightGray;
            }
            else if (ruleHasConflicts)
            {
                // Rule changes addresses but causes conflicts
                row.Cells[4].Value = "⚠️"; // Warning for conflicts
                row.Cells[4].Style.BackColor = System.Drawing.Color.LightYellow;
            }
            else
            {
                // Rule changes addresses with no conflicts
                row.Cells[4].Value = "✅"; // Success for valid transformations
                row.Cells[4].Style.BackColor = System.Drawing.Color.LightGreen;
            }
        }
    }

    /// <summary>
    /// Checks if a specific transformation rule causes conflicts and whether it makes any changes
    /// Returns (hasConflicts, makesChanges)
    /// </summary>
    private (bool hasConflicts, bool makesChanges) CheckTransformationRuleStatus(string findString, string replaceString, int position, int length)
    {
        var tempPair = new TagAddressReplacePair(findString, replaceString, position, length);

        bool hasConflicts = false;
        bool makesChanges = false;

        // Check all addresses that would be affected by this transformation rule
        foreach (var device in _selectedETDevices)
        {
            foreach (var module in device.AddressModules)
            {
                foreach (var addressInfo in module.AddressInfos)
                {
                    int originalAddress = addressInfo.StartAddress;
                    int transformedAddress = ProcessAddressTransformation(originalAddress, tempPair);

                    // If this rule would change the address, check for conflicts (uses selected IoSystem's PLC)
                    if (transformedAddress != originalAddress)
                    {
                        makesChanges = true;

                        if (CheckAddressForConflicts(transformedAddress, addressInfo, _selectedIoSystemPlcName))
                        {
                            hasConflicts = true;
                        }
                    }
                }
            }
        }

        return (hasConflicts, makesChanges);
    }

    /// <summary>
    /// Event handler for TreeView double-click - provides user feedback or additional functionality
    /// </summary>
    private void TreeViewPreview_NodeMouseDoubleClick(object sender, TreeNodeMouseClickEventArgs e)
    {
        // Optional: Add functionality for double-click on nodes
        // For now, just ensure the node is expanded/collapsed
        if (e.Node.Nodes.Count > 0)
        {
            if (e.Node.IsExpanded)
                e.Node.Collapse();
            else
                e.Node.Expand();
        }
    }

    /// <summary>
    /// Checks if there are any ET address conflicts that would prevent proceeding
    /// </summary>
    private bool HasAnyETConflicts()
    {
        foreach (var device in _selectedETDevices)
        {
            foreach (var module in device.AddressModules)
            {
                foreach (var addressInfo in module.AddressInfos)
                {
                    int transformedAddress = addressInfo.StartAddress;
                    foreach (var pair in ETAddressReplacements)
                    {
                        transformedAddress = ProcessAddressTransformation(transformedAddress, pair);
                    }

                    if (CheckAddressForConflicts(transformedAddress, addressInfo, _selectedIoSystemPlcName))
                    {
                        return true;
                    }
                }
            }
        }
        return false;
    }

    private void btnOK_Click(object sender, EventArgs e)
    {
        // Update pairs from grids before validation
        UpdateAddressReplacementsFromGrid();

        // Validate address replacement inputs
        foreach (var pair in ETAddressReplacements)
        {
            if (!string.IsNullOrEmpty(pair.FindString) && string.IsNullOrEmpty(pair.ReplaceString))
            {
                MessageBox.Show($"If you specify a 'Find' string ('{pair.FindString}'), you must also specify a 'Replace' string.",
                    "Invalid Configuration", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _dataGridViewAddressPairs.Focus();
                return;
            }

            if (pair.DigitPosition <= 0)
            {
                MessageBox.Show($"Digit Position must be greater than 0. Current value: {pair.DigitPosition}",
                    "Invalid Configuration", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _dataGridViewAddressPairs.Focus();
                return;
            }

            if (pair.LengthFilter <= 0)
            {
                MessageBox.Show($"Length Filter must be greater than 0. Current value: {pair.LengthFilter}",
                    "Invalid Configuration", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _dataGridViewAddressPairs.Focus();
                return;
            }
        }

        // Check for ET address conflicts
        if (HasAnyETConflicts())
        {
            MessageBox.Show("There are ET module address conflicts that must be resolved before proceeding. " +
                          "Please check the preview tree for detailed conflict information and adjust your transformations.",
                          "Address Conflicts Detected", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        DialogResult = DialogResult.OK;
        Close();
    }

    private void btnCancel_Click(object sender, EventArgs e)
    {
        DialogResult = DialogResult.Cancel;
        Close();
    }

    /// <summary>
    /// Event handler for Expand All button - expands all nodes in the TreeView
    /// </summary>
    private void BtnExpandAll_Click(object sender, EventArgs e)
    {
        // Save current scroll position before expanding
        var currentTopNode = _treeViewPreview.TopNode;

        _treeViewPreview.BeginUpdate();
        _treeViewPreview.ExpandAll();
        _treeViewPreview.EndUpdate();

        // Restore scroll position
        if (currentTopNode != null)
        {
            try
            {
                _treeViewPreview.TopNode = currentTopNode;
            }
            catch
            {
                // Fallback - if TopNode setting fails, do nothing
            }
        }
    }

    /// <summary>
    /// Event handler for Collapse All button - collapses all nodes in the TreeView
    /// </summary>
    private void BtnCollapseAll_Click(object sender, EventArgs e)
    {
        _treeViewPreview.CollapseAll();
    }

    private void InitializeComponent()
    {
        this._lblTitle = new Label();
        this._lblModuleInfo = new Label();
        this._grpAddressConfig = new GroupBox();
        this._lblAddressInstructions = new Label();
        this._lblAddressPairs = new Label();
        this._dataGridViewAddressPairs = new DataGridView();
        this._btnAddAddressPair = new Button();
        this._btnRemoveAddressPair = new Button();
        this._lblPreviewTitle = new Label();
        this._treeViewPreview = new TreeView();
        this._imageList = new ImageList();
        this._btnExpandAll = new Button();
        this._btnCollapseAll = new Button();
        this._btnOk = new Button();
        this._btnCancel = new Button();
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
        this._lblTitle.Size = new System.Drawing.Size(250, 17);
        this._lblTitle.TabIndex = 0;
        this._lblTitle.Text = "ET Module Address Configuration";

        //
        // lblModuleInfo
        //
        this._lblModuleInfo.AutoSize = true;
        this._lblModuleInfo.Location = new System.Drawing.Point(20, 50);
        this._lblModuleInfo.Name = "_lblModuleInfo";
        this._lblModuleInfo.Size = new System.Drawing.Size(120, 13);
        this._lblModuleInfo.TabIndex = 1;
        this._lblModuleInfo.Text = "Module Info";

        //
        // grpAddressConfig
        //
        this._grpAddressConfig.Controls.Add(this._lblAddressInstructions);
        this._grpAddressConfig.Controls.Add(this._lblAddressPairs);
        this._grpAddressConfig.Controls.Add(this._dataGridViewAddressPairs);
        this._grpAddressConfig.Controls.Add(this._btnAddAddressPair);
        this._grpAddressConfig.Controls.Add(this._btnRemoveAddressPair);
        this._grpAddressConfig.Location = new System.Drawing.Point(20, 80);
        this._grpAddressConfig.Name = "_grpAddressConfig";
        this._grpAddressConfig.Size = new System.Drawing.Size(480, 270);
        this._grpAddressConfig.TabIndex = 2;
        this._grpAddressConfig.TabStop = false;
        this._grpAddressConfig.Text = "ET Module Address Configuration";

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
        this._lblAddressInstructions.Text = "Optional: Configure address modifications for ET module start addresses. Position is counted right-to-left. Length Filter only processes addresses with specified digit count.";

        //
        // lblAddressPairs
        //
        this._lblAddressPairs.AutoSize = true;
        this._lblAddressPairs.Location = new System.Drawing.Point(15, 90);
        this._lblAddressPairs.Name = "_lblAddressPairs";
        this._lblAddressPairs.Size = new System.Drawing.Size(140, 13);
        this._lblAddressPairs.TabIndex = 1;
        this._lblAddressPairs.Text = "Address Replacements:";

        //
        // dataGridViewAddressPairs
        //
        this._dataGridViewAddressPairs.AllowUserToResizeRows = false;
        this._dataGridViewAddressPairs.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
        this._dataGridViewAddressPairs.Location = new System.Drawing.Point(15, 110);
        this._dataGridViewAddressPairs.Name = "_dataGridViewAddressPairs";
        this._dataGridViewAddressPairs.Size = new System.Drawing.Size(380, 140);
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
        positionColumn.FillWeight = 20; // Reduced weight to make room for status column
        positionColumn.DisplayStyle = DataGridViewComboBoxDisplayStyle.DropDownButton;
        positionColumn.DropDownWidth = 80;
        this._dataGridViewAddressPairs.Columns.Add(positionColumn);

        // Add Status column for conflict icons
        var statusColumn = new DataGridViewTextBoxColumn();
        statusColumn.Name = "statusColumn";
        statusColumn.HeaderText = "Status";
        statusColumn.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
        statusColumn.Width = 60;
        statusColumn.ReadOnly = true;
        statusColumn.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        statusColumn.DefaultCellStyle.Font = new System.Drawing.Font("Segoe UI", 9F);
        this._dataGridViewAddressPairs.Columns.Add(statusColumn);

        //
        // btnAddAddressPair
        //
        this._btnAddAddressPair.Location = new System.Drawing.Point(410, 110);
        this._btnAddAddressPair.Name = "_btnAddAddressPair";
        this._btnAddAddressPair.Size = new System.Drawing.Size(60, 25);
        this._btnAddAddressPair.TabIndex = 3;
        this._btnAddAddressPair.Text = "Add";
        this._btnAddAddressPair.UseVisualStyleBackColor = true;
        this._btnAddAddressPair.Click += this.btnAddAddressPair_Click;

        //
        // btnRemoveAddressPair
        //
        this._btnRemoveAddressPair.Location = new System.Drawing.Point(410, 140);
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
        this._lblPreviewTitle.Location = new System.Drawing.Point(520, 80);
        this._lblPreviewTitle.Name = "_lblPreviewTitle";
        this._lblPreviewTitle.Size = new System.Drawing.Size(55, 13);
        this._lblPreviewTitle.TabIndex = 3;
        this._lblPreviewTitle.Text = "Preview:";

        //
        // imageList
        //
        this._imageList.ColorDepth = ColorDepth.Depth32Bit;
        this._imageList.ImageSize = new System.Drawing.Size(16, 16);
        // Add basic icons using standard symbols
        this._imageList.Images.Add("folder", CreateIconBitmap("📁", System.Drawing.Color.Blue));
        this._imageList.Images.Add("success", CreateIconBitmap("✅", System.Drawing.Color.Green));
        this._imageList.Images.Add("warning", CreateIconBitmap("⚠️", System.Drawing.Color.Orange));
        this._imageList.Images.Add("error", CreateIconBitmap("❌", System.Drawing.Color.Red));

        //
        // treeViewPreview
        //
        this._treeViewPreview.Location = new System.Drawing.Point(520, 100);
        this._treeViewPreview.Name = "_treeViewPreview";
        this._treeViewPreview.Size = new System.Drawing.Size(480, 270);
        this._treeViewPreview.TabIndex = 4;
        this._treeViewPreview.ShowLines = true;
        this._treeViewPreview.ShowPlusMinus = true;
        this._treeViewPreview.FullRowSelect = false;  // Temporarily disable to test
        this._treeViewPreview.ImageList = this._imageList;
        this._treeViewPreview.Font = new System.Drawing.Font("Consolas", 8.25F);
        this._treeViewPreview.HideSelection = false;
        this._treeViewPreview.NodeMouseDoubleClick += this.TreeViewPreview_NodeMouseDoubleClick;

        //
        // btnExpandAll
        //
        this._btnExpandAll.Location = new System.Drawing.Point(520, 380);
        this._btnExpandAll.Name = "_btnExpandAll";
        this._btnExpandAll.Size = new System.Drawing.Size(80, 25);
        this._btnExpandAll.TabIndex = 5;
        this._btnExpandAll.Text = "Expand All";
        this._btnExpandAll.UseVisualStyleBackColor = true;
        this._btnExpandAll.Click += this.BtnExpandAll_Click;

        //
        // btnCollapseAll
        //
        this._btnCollapseAll.Location = new System.Drawing.Point(610, 380);
        this._btnCollapseAll.Name = "_btnCollapseAll";
        this._btnCollapseAll.Size = new System.Drawing.Size(80, 25);
        this._btnCollapseAll.TabIndex = 6;
        this._btnCollapseAll.Text = "Collapse All";
        this._btnCollapseAll.UseVisualStyleBackColor = true;
        this._btnCollapseAll.Click += this.BtnCollapseAll_Click;

        //
        // btnOK
        //
        this._btnOk.Location = new System.Drawing.Point(840, 390);
        this._btnOk.Name = "_btnOk";
        this._btnOk.Size = new System.Drawing.Size(75, 30);
        this._btnOk.TabIndex = 7;
        this._btnOk.Text = "OK";
        this._btnOk.UseVisualStyleBackColor = true;
        this._btnOk.Click += this.btnOK_Click;

        //
        // btnCancel
        //
        this._btnCancel.Location = new System.Drawing.Point(925, 390);
        this._btnCancel.Name = "_btnCancel";
        this._btnCancel.Size = new System.Drawing.Size(75, 30);
        this._btnCancel.TabIndex = 8;
        this._btnCancel.Text = "Cancel";
        this._btnCancel.UseVisualStyleBackColor = true;
        this._btnCancel.Click += this.btnCancel_Click;

        //
        // ETModuleConfigurationForm
        //
        this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
        this.AutoScaleMode = AutoScaleMode.Font;
        this.ClientSize = new System.Drawing.Size(1020, 440);
        this.Controls.Add(this._lblTitle);
        this.Controls.Add(this._lblModuleInfo);
        this.Controls.Add(this._grpAddressConfig);
        this.Controls.Add(this._lblPreviewTitle);
        this.Controls.Add(this._treeViewPreview);
        this.Controls.Add(this._btnExpandAll);
        this.Controls.Add(this._btnCollapseAll);
        this.Controls.Add(this._btnOk);
        this.Controls.Add(this._btnCancel);
        this.FormBorderStyle = FormBorderStyle.FixedDialog;
        this.MaximizeBox = false;
        this.MinimizeBox = false;
        this.Name = "ETModuleConfigurationForm";
        this.StartPosition = FormStartPosition.CenterParent;
        this.Text = "ET Module Address Configuration";
        this._grpAddressConfig.ResumeLayout(false);
        this._grpAddressConfig.PerformLayout();
        ((System.ComponentModel.ISupportInitialize)(this._dataGridViewAddressPairs)).EndInit();
        this.ResumeLayout(false);
        this.PerformLayout();
    }

    private Label _lblTitle;
    private Label _lblModuleInfo;
    private GroupBox _grpAddressConfig;
    private Label _lblAddressInstructions;
    private Label _lblAddressPairs;
    private DataGridView _dataGridViewAddressPairs;
    private Button _btnAddAddressPair;
    private Button _btnRemoveAddressPair;
    private Label _lblPreviewTitle;
    private TreeView _treeViewPreview;
    private ImageList _imageList;
    private Button _btnExpandAll;
    private Button _btnCollapseAll;
    private Button _btnOk;
    private Button _btnCancel;
}
