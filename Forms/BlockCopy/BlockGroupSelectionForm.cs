using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using OpennessCopy.Models;
using OpennessCopy.Utils;

namespace OpennessCopy.Forms.BlockCopy;

public class BlockGroupSelectionForm : Form
{
    public string SelectedGroupPath { get; private set; }
    public string SelectedGroupId { get; private set; }
        
    private readonly BlockGroupSelectionData _blockGroupData;

    public BlockGroupSelectionForm(BlockGroupSelectionData blockGroupData)
    {
        _blockGroupData = blockGroupData;
        InitializeComponent();
        LoadBlockGroups();
    }

    private void LoadBlockGroups()
    {
        try
        {
            _treeViewGroups.Nodes.Clear();
                
            // Add root node for the PLC
            var rootNode = new TreeNode($"Block Groups - {_blockGroupData.PlcName}")
            {
                ImageIndex = 0,
                SelectedImageIndex = 0,
                Tag = null // Root has no associated group
            };
            _treeViewGroups.Nodes.Add(rootNode);
                
            // Add all user groups recursively using DTO data (sorted alphabetically)
            var sortedRootGroups = _blockGroupData.RootGroups.OrderBy(g => g.Name).ToList();
            foreach (var groupInfo in sortedRootGroups)
            {
                AddGroupInfoToTree(rootNode, groupInfo);
            }
                
            // Expand the root node
            rootNode.Expand();
                
            var totalGroups = CountAllGroups(_blockGroupData.RootGroups);
            _lblStatus.Text = $"Found {totalGroups} block groups. Select one to explore.";
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error loading block groups: {ex.Message}");
            DialogResult = DialogResult.Cancel;
            Close();
        }
    }


    private void treeViewGroups_AfterSelect(object sender, TreeViewEventArgs e)
    {
        var selectedNode = e.Node;
            
        if (selectedNode.Tag is BlockGroupInfo blockGroupInfo)
        {
            _lblStatus.Text = $"Selected: {blockGroupInfo.Path} (Contains {blockGroupInfo.BlockCount} blocks)";
            _btnOk.Enabled = true;
        }
        else
        {
            // Root node or invalid selection
            _lblStatus.Text = "Please select a block group.";
            _btnOk.Enabled = false;
        }
    }

    private void btnOK_Click(object sender, EventArgs e)
    {
        var selectedNode = _treeViewGroups.SelectedNode;
            
        if (selectedNode?.Tag is BlockGroupInfo blockGroupInfo)
        {
            SelectedGroupId = blockGroupInfo.GroupId;
            SelectedGroupPath = blockGroupInfo.Path;
            DialogResult = DialogResult.OK;
            Close();
        }
        else
        {
            MessageBox.Show("Please select a block group.", "Selection Required",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void btnCancel_Click(object sender, EventArgs e)
    {
        DialogResult = DialogResult.Cancel;
        Close();
    }

    private void BlockGroupSelectionForm_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter && _btnOk.Enabled)
        {
            btnOK_Click(sender, EventArgs.Empty);
        }
        else if (e.KeyCode == Keys.Escape)
        {
            btnCancel_Click(sender, EventArgs.Empty);
        }
    }

    private void InitializeComponent()
    {
        this._treeViewGroups = new TreeView();
        this._lblTitle = new Label();
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
        this._lblTitle.Text = "Select Block Group";
            
        // 
        // treeViewGroups
        // 
        this._treeViewGroups.HideSelection = false;
        this._treeViewGroups.Location = new System.Drawing.Point(20, 50);
        this._treeViewGroups.Name = "_treeViewGroups";
        this._treeViewGroups.Size = new System.Drawing.Size(460, 300);
        this._treeViewGroups.TabIndex = 1;
        this._treeViewGroups.AfterSelect += this.treeViewGroups_AfterSelect;
            
        // 
        // lblStatus
        // 
        this._lblStatus.AutoSize = true;
        this._lblStatus.Location = new System.Drawing.Point(20, 360);
        this._lblStatus.MaximumSize = new System.Drawing.Size(460, 0);
        this._lblStatus.Name = "_lblStatus";
        this._lblStatus.Size = new System.Drawing.Size(120, 13);
        this._lblStatus.TabIndex = 2;
        this._lblStatus.Text = "Loading block groups...";
            
        // 
        // btnOK
        // 
        this._btnOk.Enabled = false;
        this._btnOk.Location = new System.Drawing.Point(320, 390);
        this._btnOk.Name = "_btnOk";
        this._btnOk.Size = new System.Drawing.Size(75, 30);
        this._btnOk.TabIndex = 3;
        this._btnOk.Text = "OK";
        this._btnOk.UseVisualStyleBackColor = true;
        this._btnOk.Click += this.btnOK_Click;
            
        // 
        // btnCancel
        // 
        this._btnCancel.Location = new System.Drawing.Point(405, 390);
        this._btnCancel.Name = "_btnCancel";
        this._btnCancel.Size = new System.Drawing.Size(75, 30);
        this._btnCancel.TabIndex = 4;
        this._btnCancel.Text = "Cancel";
        this._btnCancel.UseVisualStyleBackColor = true;
        this._btnCancel.Click += this.btnCancel_Click;
            
        // 
        // BlockGroupSelectionForm
        // 
        this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
        this.AutoScaleMode = AutoScaleMode.Font;
        this.ClientSize = new System.Drawing.Size(500, 440);
        this.Controls.Add(this._lblTitle);
        this.Controls.Add(this._treeViewGroups);
        this.Controls.Add(this._lblStatus);
        this.Controls.Add(this._btnOk);
        this.Controls.Add(this._btnCancel);
        this.FormBorderStyle = FormBorderStyle.FixedDialog;
        this.MaximizeBox = false;
        this.MinimizeBox = false;
        this.Name = "BlockGroupSelectionForm";
        this.StartPosition = FormStartPosition.CenterParent;
        this.Text = "Select Block Group";
        this.KeyPreview = true;
        this.KeyDown += this.BlockGroupSelectionForm_KeyDown;
        this.ResumeLayout(false);
        this.PerformLayout();
    }
    
    private void AddGroupInfoToTree(TreeNode parentNode, BlockGroupInfo groupInfo)
    {
        var displayText = $"{groupInfo.Name} ({groupInfo.BlockCount} blocks, {groupInfo.SubGroupCount} subgroups)";
        var groupNode = new TreeNode(displayText)
        {
            ImageIndex = 1,
            SelectedImageIndex = 1,
            Tag = groupInfo // Store the DTO info
        };
        
        parentNode.Nodes.Add(groupNode);
        
        // Add subgroups recursively (sorted alphabetically)
        var sortedSubGroups = groupInfo.SubGroups.OrderBy(g => g.Name).ToList();
        foreach (var subGroupInfo in sortedSubGroups)
        {
            AddGroupInfoToTree(groupNode, subGroupInfo);
        }
    }
    
    private int CountAllGroups(List<BlockGroupInfo> groups)
    {
        int count = groups.Count;
        foreach (var group in groups)
        {
            count += CountAllGroups(group.SubGroups);
        }
        return count;
    }


    private TreeView _treeViewGroups;
    private Label _lblTitle;
    private Label _lblStatus;
    private Button _btnOk;
    private Button _btnCancel;
}