#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows.Forms;
using OpennessCopy.Models;

namespace OpennessCopy.Forms.Conveyor
{
    public partial class BlockSelectionForm : Form
    {
        private readonly List<PlcBlockInfo> _allBlocks;
        private readonly HashSet<PlcBlockKind> _allowedKinds;
        private readonly Dictionary<PlcBlockKind, CheckBox> _kindCheckboxes;

        public PlcBlockInfo? SelectedBlock { get; private set; }

        public BlockSelectionForm(List<PlcBlockInfo> blocks, HashSet<PlcBlockKind>? allowedKinds = null)
        {
            _allBlocks = blocks ?? new List<PlcBlockInfo>();
            _allowedKinds = allowedKinds ?? new HashSet<PlcBlockKind>();
            InitializeComponent();
            _kindCheckboxes = new Dictionary<PlcBlockKind, CheckBox>
            {
                [PlcBlockKind.GlobalDb] = _chkDb,
                [PlcBlockKind.ArrayDb] = _chkDb,
                [PlcBlockKind.InstanceDb] = _chkInstanceDb,
                [PlcBlockKind.FB] = _chkFb,
                [PlcBlockKind.FC] = _chkFc
            };

            LoadFilters();
            ApplyFilter();
        }

        private void LoadFilters()
        {
            _chkDb.Checked = true;
            _chkInstanceDb.Checked = true;
            _chkFb.Checked = true;
            _chkFc.Checked = true;

            // Disable checkboxes for kinds not allowed by caller (leave checked to reflect active server-side filter)
            foreach (var kvp in _kindCheckboxes)
            {
                if (_allowedKinds.Count > 0 && !_allowedKinds.Contains(kvp.Key))
                {
                    kvp.Value.Enabled = false;
                }
            }
        }

        private void ApplyFilter()
        {
            // Preserve state
            var expandedPaths = new HashSet<string>(StringComparer.Ordinal);
            SaveExpandedState(_treeBlocks.Nodes, string.Empty, expandedPaths);
            var selectedPath = GetNodePath(_treeBlocks.SelectedNode);
            var topPath = GetNodePath(_treeBlocks.TopNode);

            _treeBlocks.BeginUpdate();
            _treeBlocks.Nodes.Clear();

            var filtered = _allBlocks.Where(block =>
            {
                bool allowed = block.Kind switch
                {
                    PlcBlockKind.InstanceDb => _chkInstanceDb.Checked,
                    PlcBlockKind.GlobalDb or PlcBlockKind.ArrayDb => _chkDb.Checked,
                    PlcBlockKind.FB => _chkFb.Checked,
                    PlcBlockKind.FC => _chkFc.Checked,
                    _ => false
                };

                if (_allowedKinds.Count > 0 && !_allowedKinds.Contains(block.Kind))
                {
                    return false;
                }

                return allowed;
            }).ToList();

            var root = new TreeNode("Blocks") { Tag = null };
            _treeBlocks.Nodes.Add(root);

            foreach (var block in filtered.OrderBy(b => b.GroupPath).ThenBy(b => b.Name))
            {
                var pathParts = string.IsNullOrEmpty(block.GroupPath)
                    ? Array.Empty<string>()
                    : block.GroupPath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);

                var parent = root;
                foreach (var part in pathParts)
                {
                    var existing = parent.Nodes.Cast<TreeNode>().FirstOrDefault(n => n.Tag == null && n.Text == part);
                    if (existing == null)
                    {
                        existing = new TreeNode(part) { Tag = null };
                        parent.Nodes.Add(existing);
                    }
                    parent = existing;
                }

                var label = $"{block.Name} (#{block.BlockNumber}, {block.Kind})";
                var node = new TreeNode(label) { Tag = block };
                parent.Nodes.Add(node);
            }

            root.Expand();
            _treeBlocks.EndUpdate();

            // Restore state
            RestoreExpandedState(_treeBlocks.Nodes, string.Empty, expandedPaths);
            RestoreSelection(selectedPath);
            RestoreTopNode(topPath);

            UpdateStatus();
            UpdateSelection();
        }

        private void SaveExpandedState(TreeNodeCollection nodes, string parentPath, HashSet<string> expandedPaths)
        {
            foreach (TreeNode node in nodes)
            {
                var path = BuildPath(parentPath, node.Text);
                if (node.IsExpanded)
                {
                    expandedPaths.Add(path);
                }
                SaveExpandedState(node.Nodes, path, expandedPaths);
            }
        }

        private void RestoreExpandedState(TreeNodeCollection nodes, string parentPath, HashSet<string> expandedPaths)
        {
            foreach (TreeNode node in nodes)
            {
                var path = BuildPath(parentPath, node.Text);
                if (expandedPaths.Contains(path))
                {
                    node.Expand();
                }
                RestoreExpandedState(node.Nodes, path, expandedPaths);
            }
        }

        private string GetNodePath(TreeNode? node)
        {
            if (node == null)
            {
                return string.Empty;
            }

            var parts = new Stack<string>();
            var current = node;
            while (current != null)
            {
                parts.Push(current.Text);
                current = current.Parent;
            }

            return string.Join("/", parts);
        }

        private void RestoreSelection(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            var node = FindNodeByPath(_treeBlocks.Nodes, path.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries), 0);
            if (node != null)
            {
                _treeBlocks.SelectedNode = node;
            }
        }

        private void RestoreTopNode(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            var node = FindNodeByPath(_treeBlocks.Nodes, path.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries), 0);
            if (node != null)
            {
                _treeBlocks.TopNode = node;
            }
        }

        private TreeNode? FindNodeByPath(TreeNodeCollection nodes, string[] parts, int index)
        {
            if (index >= parts.Length)
            {
                return null;
            }

            foreach (TreeNode node in nodes)
            {
                if (!string.Equals(node.Text, parts[index], StringComparison.Ordinal))
                {
                    continue;
                }

                if (index == parts.Length - 1)
                {
                    return node;
                }

                var found = FindNodeByPath(node.Nodes, parts, index + 1);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private static string BuildPath(string parent, string current)
        {
            if (string.IsNullOrEmpty(parent))
            {
                return current;
            }

            return $"{parent}/{current}";
        }

        private void UpdateStatus()
        {
            int count = 0;
            foreach (TreeNode node in _treeBlocks.Nodes)
            {
                count += CountLeafBlocks(node);
            }
            _lblStatus.Text = $"Showing {count} block(s)";
        }

        private int CountLeafBlocks(TreeNode node)
        {
            int total = 0;
            foreach (TreeNode child in node.Nodes)
            {
                if (child.Tag is PlcBlockInfo)
                {
                    total++;
                }
                total += CountLeafBlocks(child);
            }
            return total;
        }

        private void UpdateSelection()
        {
            SelectedBlock = _treeBlocks.SelectedNode?.Tag as PlcBlockInfo;
            _btnOk.Enabled = SelectedBlock != null;
        }

        private void OnFilterChanged(object? sender, EventArgs e)
        {
            ApplyFilter();
        }

        private void OnOkClicked(object? sender, EventArgs e)
        {
            if (SelectedBlock == null)
            {
                MessageBox.Show("Select a block first.", "Selection Required",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            DialogResult = DialogResult.OK;
            Close();
        }

        private void OnCancelClicked(object? sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }

        private void OnTreeAfterSelect(object? sender, TreeViewEventArgs e)
        {
            UpdateSelection();
        }

        private void OnTreeNodeDoubleClick(object? sender, EventArgs e)
        {
            if (SelectedBlock != null)
            {
                OnOkClicked(sender, e);
            }
        }
    }
}
