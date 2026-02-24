#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using OpennessCopy.Models;
using OpennessCopy.Services.CodeBuilders;

namespace OpennessCopy.Forms.Conveyor
{
    public partial class ConveyorConfigForm : Form
    {
        private readonly BindingList<TsegRow> _tsegRows = new();
        private readonly BindingList<TrsRow> _trsRows = new();
        private readonly BindingList<DbConfig> _dbConfigs = new();
        private bool _suppressEvents;
        private string _editingTsegOldName = string.Empty;
        private PlcBlockInfo? _selectedDb;

        private static readonly Regex PrefixPattern = new(@"^[1-9]\d*\.\d{2}$");

        public ConveyorConfig? Result { get; private set; }

        public ConveyorConfigForm()
        {
            InitializeComponent();
            InitializeGrids();
            InitializeDefaults();
            InitializeDbConfigs();
        }

        private void InitializeDbConfigs()
        {
            _dbConfigs.Clear();
            BindingList<DbConfig> seeded = new();
            // Removed for demo build
            // var seeded = DbConfigRegistry.BuildInitialConfigs(_txtPrefix.Text.Trim());
            foreach (var cfg in seeded)
            {
                _dbConfigs.Add(cfg);
            }

            _lstDbConfigs.DisplayMember = nameof(DbConfig.DisplayName);
            _lstDbConfigs.DataSource = _dbConfigs;
            _lstDbConfigs.SelectedIndexChanged += (_, _) => UpdateDbDescription();
            UpdateDbDescription();
        }

        private void InitializeDefaults()
        {
            _txtPrefix.Text = "5.01";
            _numStartBlock.Value = 4000;
            _numTrsCount.Value = 8;
            _numTsegCount.Value = 2;

            GenerateStructure();
        }

        private void OnUseExistingDbChanged(object? sender, EventArgs e)
        {
            _btnSelectDb.Enabled = _chkUseExistingDb.Checked;
            if (!_chkUseExistingDb.Checked)
            {
                _selectedDb = null;
                _lblSelectedDb.Text = "No DB selected";
            }
        }

        private void OnSelectDbClicked(object? sender, EventArgs e)
        {
            SetValidationMessage(string.Empty);
            RequestBlockSelection?.Invoke();
        }

        // Hook provided by caller to trigger block selection dialog
        public Action? RequestBlockSelection { get; set; }

        private void OnConfigureDbClicked(object? sender, EventArgs e)
        {
            if (_lstDbConfigs.SelectedItem is not DbConfig dbConfig)
            {
                MessageBox.Show("Select a DB entry first.", "No selection", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var defaultName = string.IsNullOrWhiteSpace(dbConfig.ManualName)
                ? $"DB_{_txtPrefix.Text.Trim().Replace('.', '_')}"
                : dbConfig.ManualName;

            Func<PlcBlockInfo?>? selector = null;
            if (RequestBlockSelection != null)
            {
                selector = () =>
                {
                    PlcBlockInfo? picked = null;
                    RequestBlockSelection.Invoke();
                    picked = _selectedDb;
                    return picked;
                };
            }

            using var dialog = new DbConfigForm(dbConfig, selector, defaultName);
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                var updated = dialog.Result;
                dbConfig.Mode = updated.Mode;
                dbConfig.ManualName = updated.ManualName;
                dbConfig.AppendIfExists = updated.AppendIfExists;
                dbConfig.SelectedBlock = updated.SelectedBlock;
                dbConfig.IsNameOverridden = updated.IsNameOverridden;
                dbConfig.DisplayName = updated.Mode == DbConfigMode.UseExisting
                    ? (updated.SelectedBlock?.Name ?? dbConfig.DisplayName)
                    : updated.ManualName;
                dbConfig.Description = updated.Description;
                _lstDbConfigs.Refresh();
                UpdateDbDescription();
            }
        }

        public void SetSelectedDb(PlcBlockInfo? blockInfo)
        {
            _selectedDb = blockInfo;
            if (blockInfo == null)
            {
                _lblSelectedDb.Text = "No DB selected";
                return;
            }

            _lblSelectedDb.Text = $"{blockInfo.Name} (#{blockInfo.BlockNumber})";
        }

        private void InitializeGrids()
        {
            _gridTsegs.AutoGenerateColumns = false;
            _gridTsegs.AllowUserToAddRows = false;
            _gridTsegs.AllowUserToDeleteRows = false;
            _gridTsegs.MultiSelect = false;
            _gridTsegs.SelectionMode = DataGridViewSelectionMode.FullRowSelect;

            var tsegNameColumn = new DataGridViewTextBoxColumn
            {
                HeaderText = "TSEG Name",
                DataPropertyName = nameof(TsegRow.Name),
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
            };

            var tsegCountColumn = new DataGridViewTextBoxColumn
            {
                HeaderText = "TRS Count",
                DataPropertyName = nameof(TsegRow.TrsCount),
                Width = 90,
                ReadOnly = true
            };

            _gridTsegs.Columns.AddRange(tsegNameColumn, tsegCountColumn);
            _gridTsegs.DataSource = _tsegRows;

            _gridTrs.AutoGenerateColumns = false;
            _gridTrs.AllowUserToAddRows = false;
            _gridTrs.AllowUserToDeleteRows = false;
            _gridTrs.SelectionMode = DataGridViewSelectionMode.FullRowSelect;

            var activeColumn = new DataGridViewCheckBoxColumn
            {
                HeaderText = "Active",
                DataPropertyName = nameof(TrsRow.Active),
                Width = 60
            };

            var trsNameColumn = new DataGridViewTextBoxColumn
            {
                HeaderText = "TRS Name",
                DataPropertyName = nameof(TrsRow.Name),
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
            };

            var tsegColumn = new DataGridViewComboBoxColumn
            {
                HeaderText = "TSEG",
                DataPropertyName = nameof(TrsRow.TsegName),
                DisplayMember = nameof(TsegRow.Name),
                ValueMember = nameof(TsegRow.Name),
                ValueType = typeof(string),
                Width = 140,
                FlatStyle = FlatStyle.Flat
            };

            _gridTrs.Columns.AddRange(activeColumn, trsNameColumn, tsegColumn);
            _gridTrs.DataSource = _trsRows;
            _gridTrs.CellValueChanged += OnTrsCellValueChanged;
            _gridTrs.CurrentCellDirtyStateChanged += (_, _) =>
            {
                if (_gridTrs.IsCurrentCellDirty)
                {
                    _gridTrs.CommitEdit(DataGridViewDataErrorContexts.Commit);
                }
            };

            _gridTrs.DataError += (_, _) => { };
            _gridTsegs.DataError += (_, _) => { };
        }

        private void OnGenerateClicked(object? sender, EventArgs e)
        {
            GenerateStructure();
        }

        private void GenerateStructure()
        {
            var prefix = _txtPrefix.Text.Trim();
            var trsCount = (int)_numTrsCount.Value;
            var tsegCount = (int)_numTsegCount.Value;
            SetValidationMessage(string.Empty);

            if (trsCount < 1)
            {
                trsCount = 1;
                _numTrsCount.Value = trsCount;
            }

            if (tsegCount < 1)
            {
                tsegCount = 1;
                _numTsegCount.Value = tsegCount;
            }

            if (tsegCount > trsCount)
            {
                tsegCount = trsCount;
                _numTsegCount.Value = tsegCount;
            }

            var basePerGroup = trsCount / tsegCount;
            var remainder = trsCount % tsegCount;

            _suppressEvents = true;
            _tsegRows.Clear();
            _trsRows.Clear();

            var tsegNames = new List<string>();
            var tsegAssignments = new List<string>();
            var trsIndex = 1;
            SetDefaultDbName(prefix);

            for (int i = 0; i < tsegCount; i++)
            {
                var countForGroup = basePerGroup + (i < remainder ? 1 : 0);
                if (countForGroup == 0)
                {
                    continue;
                }

                var firstTrsName = BuildTrsName(prefix, trsIndex);
                tsegNames.Add(firstTrsName);

                for (int j = 0; j < countForGroup; j++)
                {
                    tsegAssignments.Add(firstTrsName);
                    trsIndex++;
                }
            }

            for (int i = 0; i < tsegNames.Count; i++)
            {
                _tsegRows.Add(new TsegRow
                {
                    Name = tsegNames[i],
                    TrsCount = 0
                });
            }

            for (int i = 0; i < trsCount; i++)
            {
                var name = BuildTrsName(prefix, i + 1);
                var tsegName = i < tsegAssignments.Count ? tsegAssignments[i] : tsegNames.LastOrDefault() ?? string.Empty;

                _trsRows.Add(new TrsRow
                {
                    Active = true,
                    Name = name,
                    TsegName = tsegName
                });
            }

            RefreshTsegCombo();
            RecalculateTsegCounts();
            _suppressEvents = false;
            RefreshTreePreview();
        }

        private static string BuildTrsName(string prefix, int index)
        {
            return $"{prefix}.{index:D2}";
        }

        private void OnTsegCellBeginEdit(object? sender, DataGridViewCellCancelEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex != 0)
            {
                return;
            }

            _editingTsegOldName = _tsegRows[e.RowIndex].Name;
        }

        private void OnTsegCellEndEdit(object? sender, DataGridViewCellEventArgs e)
        {
            if (_suppressEvents || e.RowIndex < 0)
            {
                return;
            }

            if (e.ColumnIndex == 0)
            {
                var newName = _tsegRows[e.RowIndex].Name?.Trim() ?? string.Empty;
                _tsegRows[e.RowIndex].Name = newName;
                if (!string.IsNullOrEmpty(_editingTsegOldName) && !string.Equals(newName, _editingTsegOldName, StringComparison.Ordinal))
                {
                    foreach (var trs in _trsRows.Where(t => t.TsegName == _editingTsegOldName))
                    {
                        trs.TsegName = newName;
                    }
                }

                _editingTsegOldName = string.Empty;
                RefreshTsegCombo();
                RecalculateTsegCounts();
                RefreshTreePreview();
            }
        }

        private void OnTrsCellEndEdit(object? sender, DataGridViewCellEventArgs e)
        {
            if (_suppressEvents || e.RowIndex < 0)
            {
                return;
            }

            if (e.ColumnIndex == 1)
            {
                _trsRows[e.RowIndex].Name = _trsRows[e.RowIndex].Name?.Trim() ?? string.Empty;
            }

            if (e.ColumnIndex == 2)
            {
                _trsRows[e.RowIndex].TsegName = _trsRows[e.RowIndex].TsegName?.Trim() ?? string.Empty;
            }

            RecalculateTsegCounts();
            RefreshTreePreview();
        }

        private void OnTrsCellValueChanged(object? sender, DataGridViewCellEventArgs e)
        {
            if (_suppressEvents || e.RowIndex < 0)
            {
                return;
            }

            if (e.ColumnIndex == 2)
            {
                var cellValue = _gridTrs.Rows[e.RowIndex].Cells[e.ColumnIndex].Value;
                _trsRows[e.RowIndex].TsegName = cellValue?.ToString() ?? string.Empty;
            }

            RecalculateTsegCounts();
            RefreshTreePreview();
        }

        private void RefreshTsegCombo()
        {
            if (_gridTrs.Columns.OfType<DataGridViewComboBoxColumn>().FirstOrDefault() is not { } tsegColumn)
            {
                return;
            }

            tsegColumn.DataSource = null;
            tsegColumn.DisplayMember = nameof(TsegRow.Name);
            tsegColumn.ValueMember = nameof(TsegRow.Name);
            tsegColumn.DataSource = _tsegRows;
        }

        private void RecalculateTsegCounts()
        {
            var counts = _trsRows
                .GroupBy(t => t.TsegName)
                .ToDictionary(g => g.Key, g => g.Count());

            foreach (var tseg in _tsegRows)
            {
                counts.TryGetValue(tseg.Name, out int value);
                tseg.TrsCount = value;
            }

            _gridTsegs.Refresh();
        }

        private void RefreshTreePreview()
        {
            if (_suppressEvents)
            {
                return;
            }

            _treePreview.BeginUpdate();
            _treePreview.Nodes.Clear();

            var prefix = _txtPrefix.Text.Trim();
            var tsubNode = new TreeNode(string.IsNullOrWhiteSpace(prefix) ? "TSUB" : $"TSUB {prefix}");

            foreach (var tseg in _tsegRows.OrderBy(t => t.Name))
            {
                var trsForTseg = _trsRows
                    .Where(t => t.TsegName == tseg.Name)
                    .OrderBy(t => t.Name)
                    .ToList();

                if (trsForTseg.Count == 0)
                {
                    continue;
                }

                var activeCount = trsForTseg.Count(t => t.Active);
                var tsegLabel = activeCount == 0
                    ? $"{tseg.Name} (inactive)"
                    : $"{tseg.Name} ({activeCount}/{trsForTseg.Count} active)";

                var tsegNode = new TreeNode(tsegLabel);

                foreach (var trs in trsForTseg)
                {
                    var trsLabel = trs.Active ? trs.Name : $"{trs.Name} (inactive)";
                    tsegNode.Nodes.Add(new TreeNode(trsLabel));
                }

                tsubNode.Nodes.Add(tsegNode);
            }

            _treePreview.Nodes.Add(tsubNode);
            tsubNode.ExpandAll();
            _treePreview.EndUpdate();
        }

        private void OnOkClicked(object? sender, EventArgs e)
        {
            if (!TryBuildConfig(out var config, out var message))
            {
                SetValidationMessage(message);
                return;
            }

            Result = config;
            DialogResult = DialogResult.OK;
            Close();
        }

        private void SetValidationMessage(string message)
        {
            _lblValidation.Text = message;
        }

        private void SetDefaultDbName(string prefix)
        {
            // Removed for demo build
            //DbConfigRegistry.UpdateNamesForPrefix(_dbConfigs.ToList(), prefix);

            _lstDbConfigs.Refresh();
            UpdateDbDescription();
        }

        private void OnCancelClicked(object? sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }

        private bool TryBuildConfig(out ConveyorConfig? config, out string message)
        {
            config = null;

            var prefix = _txtPrefix.Text.Trim();
            if (!PrefixPattern.IsMatch(prefix))
            {
                message = "Name prefix must match x.xx with no leading zeros (e.g., 5.01).";
                return false;
            }

            if (_numStartBlock.Value < 1)
            {
                message = "Start block number must be 1 or greater.";
                return false;
            }

            var namePattern = new Regex("^" + Regex.Escape(prefix) + @"\.\d{2,}$");

            var tsegNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (var tseg in _tsegRows)
            {
                var name = tseg.Name?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(name))
                {
                    message = "TSEG names cannot be empty.";
                    return false;
                }

                if (!namePattern.IsMatch(name))
                {
                    message = $"TSEG name '{name}' must start with {prefix} and end with .xx.";
                    return false;
                }

                if (!tsegNames.Add(name))
                {
                    message = $"Duplicate TSEG name detected: {name}";
                    return false;
                }
            }

            var trsNames = new HashSet<string>(StringComparer.Ordinal);
            var anyActive = false;

            foreach (var trs in _trsRows)
            {
                var name = trs.Name?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(name))
                {
                    message = "TRS names cannot be empty.";
                    return false;
                }

                if (!namePattern.IsMatch(name))
                {
                    message = $"TRS name '{name}' must start with {prefix} and end with .xx.";
                    return false;
                }

                if (!trsNames.Add(name))
                {
                    message = $"Duplicate TRS name detected: {name}";
                    return false;
                }

                if (string.IsNullOrWhiteSpace(trs.TsegName))
                {
                    message = $"TRS '{name}' must be assigned to a TSEG.";
                    return false;
                }

                if (!tsegNames.Contains(trs.TsegName))
                {
                    message = $"TRS '{name}' references unknown TSEG '{trs.TsegName}'.";
                    return false;
                }

                if (trs.Active)
                {
                    anyActive = true;
                }
            }

            if (!anyActive)
            {
                message = "At least one TRS must remain active.";
                return false;
            }

            var tsegLookup = _tsegRows.ToDictionary(t => t.Name, t => new TsegConfig
            {
                Name = t.Name
            }, StringComparer.Ordinal);

            foreach (var trs in _trsRows)
            {
                if (!tsegLookup.TryGetValue(trs.TsegName, out var tsegConfig))
                {
                    continue;
                }

                tsegConfig.TrsList.Add(new TrsConfig
                {
                    Name = trs.Name.Trim(),
                    Active = trs.Active
                });
            }

            config = new ConveyorConfig
            {
                NamePrefix = prefix,
                StartBlockNumber = (int)_numStartBlock.Value,
                DbConfigs = _dbConfigs.ToDictionary(d => d.Key, d => d),
                TsegGroups = tsegLookup.Values.Where(t => t.TrsList.Count > 0).ToList()
            };

            if (config.TsegGroups.Count == 0)
            {
                message = "No TSEGs have TRS assigned. Please assign TRS to at least one TSEG.";
                config = null;
                return false;
            }

            message = string.Empty;
            return true;
        }

        private void OnNumericValueChanged(object? sender, EventArgs e)
        {
            SetValidationMessage(string.Empty);
        }

        private void OnTextChanged(object? sender, EventArgs e)
        {
            SetValidationMessage(string.Empty);
            RefreshTreePreview();
            if (ReferenceEquals(sender, _txtPrefix))
            {
                SetDefaultDbName(_txtPrefix.Text.Trim());
            }
        }

        private void OnTrsCurrentCellDirtyStateChanged(object? sender, EventArgs e)
        {
            if (_gridTrs.IsCurrentCellDirty)
            {
                _gridTrs.CommitEdit(DataGridViewDataErrorContexts.Commit);
            }
        }

        private void UpdateDbDescription()
        {
            if (_lstDbConfigs.SelectedItem is DbConfig config && !string.IsNullOrWhiteSpace(config.Description))
            {
                _lblDbDescription.Text = config.Description;
            }
            else
            {
                _lblDbDescription.Text = "Select a DB to view its description.";
            }
        }

        private sealed class TsegRow : INotifyPropertyChanged
        {
            private string _name = string.Empty;
            private int _trsCount;

            public string Name
            {
                get => _name;
                set
                {
                    if (_name == value)
                    {
                        return;
                    }

                    _name = value;
                    OnPropertyChanged(nameof(Name));
                }
            }

            public int TrsCount
            {
                get => _trsCount;
                set
                {
                    if (_trsCount == value)
                    {
                        return;
                    }

                    _trsCount = value;
                    OnPropertyChanged(nameof(TrsCount));
                }
            }

            public event PropertyChangedEventHandler? PropertyChanged;

            private void OnPropertyChanged(string propertyName)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        private sealed class TrsRow
        {
            public bool Active { get; set; }

            public string Name { get; set; } = string.Empty;

            public string TsegName { get; set; } = string.Empty;
        }
    }
}
