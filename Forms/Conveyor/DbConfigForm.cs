#nullable enable
using System;
using System.Windows.Forms;
using OpennessCopy.Models;

namespace OpennessCopy.Forms.Conveyor
{
    public partial class DbConfigForm : Form
    {
        private readonly Func<PlcBlockInfo?>? _blockSelector;
        private PlcBlockInfo? _selectedBlock;
        private readonly string _description;

        public DbConfig Result { get; private set; }

        public DbConfigForm(DbConfig config, Func<PlcBlockInfo?>? blockSelector, string defaultName)
        {
            InitializeComponent();
            _blockSelector = blockSelector;
            _description = config.Description;

            _txtName.Text = string.IsNullOrWhiteSpace(config.ManualName) ? defaultName : config.ManualName;
            _chkAppend.Checked = config.AppendIfExists;
            _selectedBlock = config.SelectedBlock;
            Result = config;

            if (config.Mode == DbConfigMode.UseExisting)
            {
                _rdoExisting.Checked = true;
            }
            else
            {
                _rdoGenerate.Checked = true;
            }

            UpdateModeUi();
            UpdateSelectedLabel();
            _lblDescription.Text = string.IsNullOrWhiteSpace(_description)
                ? "No description provided."
                : _description;
        }

        private void OnModeChanged(object? sender, EventArgs e)
        {
            UpdateModeUi();
        }

        private void UpdateModeUi()
        {
            var useExisting = _rdoExisting.Checked;
            _btnSelectDb.Enabled = useExisting && _blockSelector != null;
            _lblSelected.Enabled = useExisting;
            _txtName.Enabled = !useExisting;
            _chkAppend.Enabled = !useExisting;
        }

        private void OnSelectDb(object? sender, EventArgs e)
        {
            if (_blockSelector == null)
            {
                return;
            }

            var picked = _blockSelector();
            if (picked != null)
            {
                _selectedBlock = picked;
                _rdoExisting.Checked = true;
                UpdateSelectedLabel();
            }
        }

        private void UpdateSelectedLabel()
        {
            _lblSelected.Text = _selectedBlock != null
                ? $"{_selectedBlock.Name} (#{_selectedBlock.BlockNumber})"
                : "No DB selected";
        }

        private void OnOk(object? sender, EventArgs e)
        {
            if (_rdoExisting.Checked && _selectedBlock == null)
            {
                MessageBox.Show("Select a DB or switch to generate mode.", "DB Required",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!_rdoExisting.Checked && string.IsNullOrWhiteSpace(_txtName.Text))
            {
                MessageBox.Show("Provide a DB name.", "Name Required",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            Result = new DbConfig
            {
                Mode = _rdoExisting.Checked ? DbConfigMode.UseExisting : DbConfigMode.GenerateOrAppend,
                ManualName = _txtName.Text.Trim(),
                AppendIfExists = _chkAppend.Checked,
                SelectedBlock = _selectedBlock,
                IsNameOverridden = true,
                Description = _description
            };

            DialogResult = DialogResult.OK;
            Close();
        }

        private void OnCancel(object? sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }
    }
}
