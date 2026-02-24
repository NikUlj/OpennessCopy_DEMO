using System.ComponentModel;
using System.Windows.Forms;

namespace OpennessCopy.Forms.Conveyor
{
    partial class ConveyorConfigForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }

            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this._lblHeader = new System.Windows.Forms.Label();
            this._lblPrefix = new System.Windows.Forms.Label();
            this._txtPrefix = new System.Windows.Forms.TextBox();
            this._lblStartBlock = new System.Windows.Forms.Label();
            this._numStartBlock = new System.Windows.Forms.NumericUpDown();
            this._lblTrsCount = new System.Windows.Forms.Label();
            this._numTrsCount = new System.Windows.Forms.NumericUpDown();
            this._lblTsegCount = new System.Windows.Forms.Label();
            this._numTsegCount = new System.Windows.Forms.NumericUpDown();
            this._btnGenerate = new System.Windows.Forms.Button();
            this._gridTsegs = new System.Windows.Forms.DataGridView();
            this._gridTrs = new System.Windows.Forms.DataGridView();
            this._treePreview = new System.Windows.Forms.TreeView();
            this._lblPreview = new System.Windows.Forms.Label();
            this._btnOk = new System.Windows.Forms.Button();
            this._btnCancel = new System.Windows.Forms.Button();
            this._chkUseExistingDb = new System.Windows.Forms.CheckBox();
            this._btnSelectDb = new System.Windows.Forms.Button();
            this._lblSelectedDb = new System.Windows.Forms.Label();
            this._lblAddressInstructions = new System.Windows.Forms.Label();
            this._grpAddressConfig = new System.Windows.Forms.GroupBox();
            this._btnConfigDb = new System.Windows.Forms.Button();
            this._lstDbConfigs = new System.Windows.Forms.ListBox();
            this._lblValidation = new System.Windows.Forms.Label();
            this._lblDbDescription = new System.Windows.Forms.Label();
            ((System.ComponentModel.ISupportInitialize)(this._numStartBlock)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this._numTrsCount)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this._numTsegCount)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this._gridTsegs)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this._gridTrs)).BeginInit();
            this._grpAddressConfig.SuspendLayout();
            this.SuspendLayout();
            // 
            // _lblHeader
            // 
            this._lblHeader.AutoSize = true;
            this._lblHeader.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Bold);
            this._lblHeader.Location = new System.Drawing.Point(12, 9);
            this._lblHeader.Name = "_lblHeader";
            this._lblHeader.Size = new System.Drawing.Size(178, 17);
            this._lblHeader.TabIndex = 0;
            this._lblHeader.Text = "Conveyor Configuration";
            // 
            // _lblPrefix
            // 
            this._lblPrefix.AutoSize = true;
            this._lblPrefix.Location = new System.Drawing.Point(14, 42);
            this._lblPrefix.Name = "_lblPrefix";
            this._lblPrefix.Size = new System.Drawing.Size(64, 13);
            this._lblPrefix.TabIndex = 1;
            this._lblPrefix.Text = "Name Prefix";
            // 
            // _txtPrefix
            // 
            this._txtPrefix.Location = new System.Drawing.Point(85, 39);
            this._txtPrefix.Name = "_txtPrefix";
            this._txtPrefix.Size = new System.Drawing.Size(90, 20);
            this._txtPrefix.TabIndex = 2;
            this._txtPrefix.TextChanged += new System.EventHandler(this.OnTextChanged);
            // 
            // _lblStartBlock
            // 
            this._lblStartBlock.AutoSize = true;
            this._lblStartBlock.Location = new System.Drawing.Point(191, 42);
            this._lblStartBlock.Name = "_lblStartBlock";
            this._lblStartBlock.Size = new System.Drawing.Size(79, 13);
            this._lblStartBlock.TabIndex = 3;
            this._lblStartBlock.Text = "Start Block No.";
            // 
            // _numStartBlock
            // 
            this._numStartBlock.Location = new System.Drawing.Point(274, 39);
            this._numStartBlock.Maximum = new decimal(new int[] { 99999, 0, 0, 0 });
            this._numStartBlock.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
            this._numStartBlock.Name = "_numStartBlock";
            this._numStartBlock.Size = new System.Drawing.Size(80, 20);
            this._numStartBlock.TabIndex = 4;
            this._numStartBlock.Value = new decimal(new int[] { 1, 0, 0, 0 });
            this._numStartBlock.ValueChanged += new System.EventHandler(this.OnNumericValueChanged);
            // 
            // _lblTrsCount
            // 
            this._lblTrsCount.AutoSize = true;
            this._lblTrsCount.Location = new System.Drawing.Point(370, 42);
            this._lblTrsCount.Name = "_lblTrsCount";
            this._lblTrsCount.Size = new System.Drawing.Size(60, 13);
            this._lblTrsCount.TabIndex = 5;
            this._lblTrsCount.Text = "TRS Count";
            // 
            // _numTrsCount
            // 
            this._numTrsCount.Location = new System.Drawing.Point(435, 39);
            this._numTrsCount.Maximum = new decimal(new int[] { 200, 0, 0, 0 });
            this._numTrsCount.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
            this._numTrsCount.Name = "_numTrsCount";
            this._numTrsCount.Size = new System.Drawing.Size(70, 20);
            this._numTrsCount.TabIndex = 6;
            this._numTrsCount.Value = new decimal(new int[] { 1, 0, 0, 0 });
            this._numTrsCount.ValueChanged += new System.EventHandler(this.OnNumericValueChanged);
            // 
            // _lblTsegCount
            // 
            this._lblTsegCount.AutoSize = true;
            this._lblTsegCount.Location = new System.Drawing.Point(521, 42);
            this._lblTsegCount.Name = "_lblTsegCount";
            this._lblTsegCount.Size = new System.Drawing.Size(67, 13);
            this._lblTsegCount.TabIndex = 7;
            this._lblTsegCount.Text = "TSEG Count";
            // 
            // _numTsegCount
            // 
            this._numTsegCount.Location = new System.Drawing.Point(595, 39);
            this._numTsegCount.Maximum = new decimal(new int[] { 50, 0, 0, 0 });
            this._numTsegCount.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
            this._numTsegCount.Name = "_numTsegCount";
            this._numTsegCount.Size = new System.Drawing.Size(70, 20);
            this._numTsegCount.TabIndex = 8;
            this._numTsegCount.Value = new decimal(new int[] { 1, 0, 0, 0 });
            this._numTsegCount.ValueChanged += new System.EventHandler(this.OnNumericValueChanged);
            // 
            // _btnGenerate
            // 
            this._btnGenerate.Location = new System.Drawing.Point(804, 35);
            this._btnGenerate.Name = "_btnGenerate";
            this._btnGenerate.Size = new System.Drawing.Size(140, 27);
            this._btnGenerate.TabIndex = 9;
            this._btnGenerate.Text = "Generate Structure";
            this._btnGenerate.UseVisualStyleBackColor = true;
            this._btnGenerate.Click += new System.EventHandler(this.OnGenerateClicked);
            // 
            // _gridTsegs
            // 
            this._gridTsegs.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this._gridTsegs.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this._gridTsegs.Location = new System.Drawing.Point(17, 95);
            this._gridTsegs.Name = "_gridTsegs";
            this._gridTsegs.Size = new System.Drawing.Size(366, 130);
            this._gridTsegs.TabIndex = 10;
            this._gridTsegs.CellBeginEdit += new System.Windows.Forms.DataGridViewCellCancelEventHandler(this.OnTsegCellBeginEdit);
            this._gridTsegs.CellEndEdit += new System.Windows.Forms.DataGridViewCellEventHandler(this.OnTsegCellEndEdit);
            // 
            // _gridTrs
            // 
            this._gridTrs.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this._gridTrs.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this._gridTrs.Location = new System.Drawing.Point(17, 246);
            this._gridTrs.Name = "_gridTrs";
            this._gridTrs.Size = new System.Drawing.Size(366, 335);
            this._gridTrs.TabIndex = 11;
            this._gridTrs.CellEndEdit += new System.Windows.Forms.DataGridViewCellEventHandler(this.OnTrsCellEndEdit);
            // 
            // _treePreview
            // 
            this._treePreview.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) | System.Windows.Forms.AnchorStyles.Right)));
            this._treePreview.Location = new System.Drawing.Point(389, 95);
            this._treePreview.Name = "_treePreview";
            this._treePreview.Size = new System.Drawing.Size(219, 486);
            this._treePreview.TabIndex = 12;
            // 
            // _lblPreview
            // 
            this._lblPreview.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this._lblPreview.AutoSize = true;
            this._lblPreview.Location = new System.Drawing.Point(389, 79);
            this._lblPreview.Name = "_lblPreview";
            this._lblPreview.Size = new System.Drawing.Size(91, 13);
            this._lblPreview.TabIndex = 13;
            this._lblPreview.Text = "Structure Preview";
            // 
            // _btnOk
            // 
            this._btnOk.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this._btnOk.Location = new System.Drawing.Point(859, 595);
            this._btnOk.Name = "_btnOk";
            this._btnOk.Size = new System.Drawing.Size(75, 28);
            this._btnOk.TabIndex = 15;
            this._btnOk.Text = "OK";
            this._btnOk.UseVisualStyleBackColor = true;
            this._btnOk.Click += new System.EventHandler(this.OnOkClicked);
            // 
            // _btnCancel
            // 
            this._btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this._btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this._btnCancel.Location = new System.Drawing.Point(940, 595);
            this._btnCancel.Name = "_btnCancel";
            this._btnCancel.Size = new System.Drawing.Size(75, 28);
            this._btnCancel.TabIndex = 16;
            this._btnCancel.Text = "Cancel";
            this._btnCancel.UseVisualStyleBackColor = true;
            this._btnCancel.Click += new System.EventHandler(this.OnCancelClicked);
            // 
            // _chkUseExistingDb
            // 
            this._chkUseExistingDb.AutoSize = true;
            this._chkUseExistingDb.Location = new System.Drawing.Point(6, 75);
            this._chkUseExistingDb.Name = "_chkUseExistingDb";
            this._chkUseExistingDb.Size = new System.Drawing.Size(15, 14);
            this._chkUseExistingDb.TabIndex = 17;
            this._chkUseExistingDb.UseVisualStyleBackColor = true;
            this._chkUseExistingDb.Visible = false;
            // 
            // _btnSelectDb
            // 
            this._btnSelectDb.Enabled = false;
            this._btnSelectDb.Location = new System.Drawing.Point(27, 69);
            this._btnSelectDb.Name = "_btnSelectDb";
            this._btnSelectDb.Size = new System.Drawing.Size(110, 24);
            this._btnSelectDb.TabIndex = 18;
            this._btnSelectDb.Text = "Select DB...";
            this._btnSelectDb.UseVisualStyleBackColor = true;
            this._btnSelectDb.Visible = false;
            // 
            // _lblSelectedDb
            // 
            this._lblSelectedDb.AutoEllipsis = true;
            this._lblSelectedDb.Location = new System.Drawing.Point(143, 75);
            this._lblSelectedDb.Name = "_lblSelectedDb";
            this._lblSelectedDb.Size = new System.Drawing.Size(252, 18);
            this._lblSelectedDb.TabIndex = 19;
            this._lblSelectedDb.Text = "No DB selected";
            this._lblSelectedDb.Visible = false;
            // 
            // _lblAddressInstructions
            // 
            this._lblAddressInstructions.AutoSize = true;
            this._lblAddressInstructions.ForeColor = System.Drawing.Color.Gray;
            this._lblAddressInstructions.Location = new System.Drawing.Point(6, 16);
            this._lblAddressInstructions.MaximumSize = new System.Drawing.Size(388, 0);
            this._lblAddressInstructions.Name = "_lblAddressInstructions";
            this._lblAddressInstructions.Size = new System.Drawing.Size(143, 13);
            this._lblAddressInstructions.TabIndex = 0;
            this._lblAddressInstructions.Text = "Configure DB settings below.";
            // 
            // _lblDbDescription
            // 
            this._lblDbDescription.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this._lblDbDescription.Location = new System.Drawing.Point(9, 156);
            this._lblDbDescription.Name = "_lblDbDescription";
            this._lblDbDescription.Size = new System.Drawing.Size(386, 18);
            this._lblDbDescription.TabIndex = 3;
            this._lblDbDescription.Text = "Select a DB to view its description.";
            // 
            // _grpAddressConfig
            // 
            this._grpAddressConfig.Controls.Add(this._lblDbDescription);
            this._grpAddressConfig.Controls.Add(this._btnConfigDb);
            this._grpAddressConfig.Controls.Add(this._lstDbConfigs);
            this._grpAddressConfig.Controls.Add(this._lblAddressInstructions);
            this._grpAddressConfig.Location = new System.Drawing.Point(614, 95);
            this._grpAddressConfig.Name = "_grpAddressConfig";
            this._grpAddressConfig.Size = new System.Drawing.Size(401, 180);
            this._grpAddressConfig.TabIndex = 22;
            this._grpAddressConfig.TabStop = false;
            this._grpAddressConfig.Text = "DB Configuration";
            // 
            // _btnConfigDb
            // 
            this._btnConfigDb.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this._btnConfigDb.Location = new System.Drawing.Point(312, 58);
            this._btnConfigDb.Name = "_btnConfigDb";
            this._btnConfigDb.Size = new System.Drawing.Size(83, 27);
            this._btnConfigDb.TabIndex = 2;
            this._btnConfigDb.Text = "Configure...";
            this._btnConfigDb.UseVisualStyleBackColor = true;
            this._btnConfigDb.Click += new System.EventHandler(this.OnConfigureDbClicked);
            // 
            // _lstDbConfigs
            // 
            this._lstDbConfigs.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this._lstDbConfigs.FormattingEnabled = true;
            this._lstDbConfigs.Location = new System.Drawing.Point(9, 58);
            this._lstDbConfigs.Name = "_lstDbConfigs";
            this._lstDbConfigs.Size = new System.Drawing.Size(297, 95);
            this._lstDbConfigs.TabIndex = 1;
            // 
            // _lblValidation
            // 
            this._lblValidation.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this._lblValidation.ForeColor = System.Drawing.Color.Firebrick;
            this._lblValidation.Location = new System.Drawing.Point(14, 595);
            this._lblValidation.Name = "_lblValidation";
            this._lblValidation.Size = new System.Drawing.Size(819, 18);
            this._lblValidation.TabIndex = 14;
            this._lblValidation.Text = " ";
            // 
            // ConveyorConfigForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.SystemColors.Control;
            this.ClientSize = new System.Drawing.Size(1030, 629);
            this.Controls.Add(this._grpAddressConfig);
            this.Controls.Add(this._btnCancel);
            this.Controls.Add(this._btnOk);
            this.Controls.Add(this._lblValidation);
            this.Controls.Add(this._lblPreview);
            this.Controls.Add(this._treePreview);
            this.Controls.Add(this._gridTrs);
            this.Controls.Add(this._gridTsegs);
            this.Controls.Add(this._btnGenerate);
            this.Controls.Add(this._numTsegCount);
            this.Controls.Add(this._lblTsegCount);
            this.Controls.Add(this._numTrsCount);
            this.Controls.Add(this._lblTrsCount);
            this.Controls.Add(this._numStartBlock);
            this.Controls.Add(this._lblStartBlock);
            this.Controls.Add(this._txtPrefix);
            this.Controls.Add(this._lblPrefix);
            this.Controls.Add(this._lblHeader);
            this.Location = new System.Drawing.Point(15, 15);
            this.MinimizeBox = false;
            this.MinimumSize = new System.Drawing.Size(840, 600);
            this.Name = "ConveyorConfigForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            ((System.ComponentModel.ISupportInitialize)(this._numStartBlock)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this._numTrsCount)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this._numTsegCount)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this._gridTsegs)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this._gridTrs)).EndInit();
            this._grpAddressConfig.ResumeLayout(false);
            this._grpAddressConfig.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        private System.Windows.Forms.Label _lblValidation;

        private System.Windows.Forms.GroupBox _grpAddressConfig;
        private System.Windows.Forms.Label _lblAddressInstructions;

        #endregion

        private Label _lblHeader;
        private System.Windows.Forms.Label _lblPrefix;
        private System.Windows.Forms.TextBox _txtPrefix;
        private Label _lblStartBlock;
        private NumericUpDown _numStartBlock;
        private Label _lblTrsCount;
        private NumericUpDown _numTrsCount;
        private Label _lblTsegCount;
        private NumericUpDown _numTsegCount;
        private System.Windows.Forms.Button _btnGenerate;
        private System.Windows.Forms.DataGridView _gridTsegs;
        private System.Windows.Forms.DataGridView _gridTrs;
        private System.Windows.Forms.TreeView _treePreview;
        private System.Windows.Forms.Label _lblPreview;
        private System.Windows.Forms.Button _btnOk;
        private System.Windows.Forms.Button _btnCancel;
        private System.Windows.Forms.CheckBox _chkUseExistingDb;
        private System.Windows.Forms.Button _btnSelectDb;
        private System.Windows.Forms.Label _lblSelectedDb;
        private System.Windows.Forms.ListBox _lstDbConfigs;
        private System.Windows.Forms.Button _btnConfigDb;
        private System.Windows.Forms.Label _lblDbDescription;
    }
}
