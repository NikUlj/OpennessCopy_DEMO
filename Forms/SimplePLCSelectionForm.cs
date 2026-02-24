using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using OpennessCopy.Models;
using OpennessCopy.Utils;

#nullable enable

namespace OpennessCopy.Forms;

public partial class SimplePLCSelectionForm : Form
{
    private List<PLCInfo> _availablePlcs = new();

    public PLCInfo? SelectedPlc { get; private set; }

    public SimplePLCSelectionForm(List<PLCInfo> availablePlcs)
    {
        InitializeComponent();
        LoadAvailablePlcs(availablePlcs);
    }

    private void LoadAvailablePlcs(List<PLCInfo> availablePlcs)
    {
        _availablePlcs = availablePlcs ?? new List<PLCInfo>();

        if (_availablePlcs.Count == 0)
        {
            Logger.LogError("No PLCs found in the project.");
            DialogResult = DialogResult.Cancel;
            Close();
            return;
        }

        var grouped = _availablePlcs
            .GroupBy(plc => $"{plc.ProjectName} ({plc.TiaInstanceId})")
            .ToList();

        PopulateTreeView(grouped);

        if (_availablePlcs.Count == 1)
        {
            var firstInstance = _treeViewPlcs.Nodes.Cast<TreeNode>().FirstOrDefault();
            if (firstInstance != null && firstInstance.Nodes.Count > 0)
            {
                _treeViewPlcs.SelectedNode = firstInstance.Nodes[0];
            }

            _lblStatus.Text = "Only one PLC found. Automatically selected.";
        }
        else
        {
            _lblStatus.Text = $"Found {_availablePlcs.Count} PLCs. Select one to continue.";
        }
    }

    private void PopulateTreeView(IEnumerable<IGrouping<string, PLCInfo>> plcsByInstance)
    {
        _treeViewPlcs.Nodes.Clear();

        foreach (var instanceGroup in plcsByInstance)
        {
            var instanceNode = new TreeNode($"TIA Portal - {instanceGroup.Key}")
            {
                Tag = null
            };

            foreach (var plcInfo in instanceGroup)
            {
                var plcNode = new TreeNode($"{plcInfo.DeviceName} - {plcInfo.Name}")
                {
                    Tag = plcInfo
                };
                instanceNode.Nodes.Add(plcNode);
            }

            if (instanceNode.Nodes.Count > 0)
            {
                _treeViewPlcs.Nodes.Add(instanceNode);
                instanceNode.Expand();
            }
        }
    }

    private void OnOkClicked(object? sender, EventArgs e)
    {
        if (_treeViewPlcs.SelectedNode?.Tag is not PLCInfo selected)
        {
            MessageBox.Show("Please select a PLC (not a TIA Portal instance).", "Selection Required",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (selected.IsArchive)
        {
            MessageBox.Show("Archived PLCs are read-only. Please select a PLC from a live TIA Portal project.",
                "Invalid Selection", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        SelectedPlc = selected;
        DialogResult = DialogResult.OK;
        Close();
    }

    private void OnCancelClicked(object? sender, EventArgs e)
    {
        DialogResult = DialogResult.Cancel;
        Close();
    }

    private void OnTreeViewDoubleClick(object? sender, EventArgs e)
    {
        if (_treeViewPlcs.SelectedNode?.Tag is PLCInfo)
        {
            OnOkClicked(sender, e);
        }
    }
}
