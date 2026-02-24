using System.ComponentModel;
using System.Windows.Forms;

namespace OpennessCopy.Forms.Conveyor
{
    partial class BlockSelectionForm
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
            this._lblTitle = new System.Windows.Forms.Label();
            this._treeBlocks = new System.Windows.Forms.TreeView();
            this._chkDb = new System.Windows.Forms.CheckBox();
            this._chkInstanceDb = new System.Windows.Forms.CheckBox();
            this._chkFb = new System.Windows.Forms.CheckBox();
            this._chkFc = new System.Windows.Forms.CheckBox();
            this._lblStatus = new System.Windows.Forms.Label();
            this._btnOk = new System.Windows.Forms.Button();
            this._btnCancel = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // _lblTitle
            // 
            this._lblTitle.AutoSize = true;
            this._lblTitle.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Bold);
            this._lblTitle.Location = new System.Drawing.Point(12, 9);
            this._lblTitle.Name = "_lblTitle";
            this._lblTitle.Size = new System.Drawing.Size(97, 17);
            this._lblTitle.TabIndex = 0;
            this._lblTitle.Text = "Select Block";
            // 
            // _treeBlocks
            // 
            this._treeBlocks.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this._treeBlocks.HideSelection = false;
            this._treeBlocks.Location = new System.Drawing.Point(15, 64);
            this._treeBlocks.Name = "_treeBlocks";
            this._treeBlocks.Size = new System.Drawing.Size(760, 339);
            this._treeBlocks.TabIndex = 5;
            this._treeBlocks.AfterSelect += new System.Windows.Forms.TreeViewEventHandler(this.OnTreeAfterSelect);
            this._treeBlocks.DoubleClick += new System.EventHandler(this.OnTreeNodeDoubleClick);
            // 
            // _chkDb
            // 
            this._chkDb.AutoSize = true;
            this._chkDb.Location = new System.Drawing.Point(15, 36);
            this._chkDb.Name = "_chkDb";
            this._chkDb.Size = new System.Drawing.Size(41, 17);
            this._chkDb.TabIndex = 1;
            this._chkDb.Text = "DB";
            this._chkDb.UseVisualStyleBackColor = true;
            this._chkDb.CheckedChanged += new System.EventHandler(this.OnFilterChanged);
            // 
            // _chkInstanceDb
            // 
            this._chkInstanceDb.AutoSize = true;
            this._chkInstanceDb.Location = new System.Drawing.Point(63, 36);
            this._chkInstanceDb.Name = "_chkInstanceDb";
            this._chkInstanceDb.Size = new System.Drawing.Size(90, 17);
            this._chkInstanceDb.TabIndex = 2;
            this._chkInstanceDb.Text = "Instance DBs";
            this._chkInstanceDb.UseVisualStyleBackColor = true;
            this._chkInstanceDb.CheckedChanged += new System.EventHandler(this.OnFilterChanged);
            // 
            // _chkFb
            // 
            this._chkFb.AutoSize = true;
            this._chkFb.Location = new System.Drawing.Point(162, 36);
            this._chkFb.Name = "_chkFb";
            this._chkFb.Size = new System.Drawing.Size(39, 17);
            this._chkFb.TabIndex = 3;
            this._chkFb.Text = "FB";
            this._chkFb.UseVisualStyleBackColor = true;
            this._chkFb.CheckedChanged += new System.EventHandler(this.OnFilterChanged);
            // 
            // _chkFc
            // 
            this._chkFc.AutoSize = true;
            this._chkFc.Location = new System.Drawing.Point(208, 36);
            this._chkFc.Name = "_chkFc";
            this._chkFc.Size = new System.Drawing.Size(39, 17);
            this._chkFc.TabIndex = 4;
            this._chkFc.Text = "FC";
            this._chkFc.UseVisualStyleBackColor = true;
            this._chkFc.CheckedChanged += new System.EventHandler(this.OnFilterChanged);
            // 
            // _lblStatus
            // 
            this._lblStatus.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this._lblStatus.Location = new System.Drawing.Point(12, 413);
            this._lblStatus.Name = "_lblStatus";
            this._lblStatus.Size = new System.Drawing.Size(580, 23);
            this._lblStatus.TabIndex = 6;
            this._lblStatus.Text = "Showing 0 block(s)";
            // 
            // _btnOk
            // 
            this._btnOk.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this._btnOk.Enabled = false;
            this._btnOk.Location = new System.Drawing.Point(616, 409);
            this._btnOk.Name = "_btnOk";
            this._btnOk.Size = new System.Drawing.Size(75, 27);
            this._btnOk.TabIndex = 7;
            this._btnOk.Text = "OK";
            this._btnOk.UseVisualStyleBackColor = true;
            this._btnOk.Click += new System.EventHandler(this.OnOkClicked);
            // 
            // _btnCancel
            // 
            this._btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this._btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this._btnCancel.Location = new System.Drawing.Point(700, 409);
            this._btnCancel.Name = "_btnCancel";
            this._btnCancel.Size = new System.Drawing.Size(75, 27);
            this._btnCancel.TabIndex = 8;
            this._btnCancel.Text = "Cancel";
            this._btnCancel.UseVisualStyleBackColor = true;
            this._btnCancel.Click += new System.EventHandler(this.OnCancelClicked);
            // 
            // BlockSelectionForm
            // 
            this.AcceptButton = this._btnOk;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this._btnCancel;
            this.ClientSize = new System.Drawing.Size(787, 444);
            this.Controls.Add(this._btnCancel);
            this.Controls.Add(this._btnOk);
            this.Controls.Add(this._lblStatus);
            this.Controls.Add(this._chkFc);
            this.Controls.Add(this._chkFb);
            this.Controls.Add(this._chkInstanceDb);
            this.Controls.Add(this._chkDb);
            this.Controls.Add(this._treeBlocks);
            this.Controls.Add(this._lblTitle);
            this.MinimizeBox = false;
            this.MinimumSize = new System.Drawing.Size(680, 300);
            this.Name = "BlockSelectionForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Block Selection";
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion

        private Label _lblTitle;
        private System.Windows.Forms.TreeView _treeBlocks;
        private CheckBox _chkDb;
        private CheckBox _chkInstanceDb;
        private CheckBox _chkFb;
        private CheckBox _chkFc;
        private System.Windows.Forms.Label _lblStatus;
        private System.Windows.Forms.Button _btnOk;
        private System.Windows.Forms.Button _btnCancel;
    }
}
