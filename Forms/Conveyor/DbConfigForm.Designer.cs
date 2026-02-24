using System.ComponentModel;
using System.Windows.Forms;

namespace OpennessCopy.Forms.Conveyor
{
    partial class DbConfigForm
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
            this._lblMode = new System.Windows.Forms.Label();
            this._rdoGenerate = new System.Windows.Forms.RadioButton();
            this._rdoExisting = new System.Windows.Forms.RadioButton();
            this._lblName = new System.Windows.Forms.Label();
            this._txtName = new System.Windows.Forms.TextBox();
            this._chkAppend = new System.Windows.Forms.CheckBox();
            this._btnSelectDb = new System.Windows.Forms.Button();
            this._lblSelected = new System.Windows.Forms.Label();
            this._btnOk = new System.Windows.Forms.Button();
            this._btnCancel = new System.Windows.Forms.Button();
            this._lblDescription = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // _lblMode
            // 
            this._lblMode.AutoSize = true;
            this._lblMode.Location = new System.Drawing.Point(12, 9);
            this._lblMode.Name = "_lblMode";
            this._lblMode.Size = new System.Drawing.Size(67, 13);
            this._lblMode.TabIndex = 0;
            this._lblMode.Text = "DB selection";
            // 
            // _rdoGenerate
            // 
            this._rdoGenerate.AutoSize = true;
            this._rdoGenerate.Location = new System.Drawing.Point(15, 27);
            this._rdoGenerate.Name = "_rdoGenerate";
            this._rdoGenerate.Size = new System.Drawing.Size(159, 17);
            this._rdoGenerate.TabIndex = 1;
            this._rdoGenerate.TabStop = true;
            this._rdoGenerate.Text = "Generate / append by name";
            this._rdoGenerate.UseVisualStyleBackColor = true;
            this._rdoGenerate.CheckedChanged += new System.EventHandler(this.OnModeChanged);
            // 
            // _rdoExisting
            // 
            this._rdoExisting.AutoSize = true;
            this._rdoExisting.Location = new System.Drawing.Point(15, 110);
            this._rdoExisting.Name = "_rdoExisting";
            this._rdoExisting.Size = new System.Drawing.Size(100, 17);
            this._rdoExisting.TabIndex = 4;
            this._rdoExisting.TabStop = true;
            this._rdoExisting.Text = "Use existing DB";
            this._rdoExisting.UseVisualStyleBackColor = true;
            this._rdoExisting.CheckedChanged += new System.EventHandler(this.OnModeChanged);
            // 
            // _lblName
            // 
            this._lblName.AutoSize = true;
            this._lblName.Location = new System.Drawing.Point(32, 52);
            this._lblName.Name = "_lblName";
            this._lblName.Size = new System.Drawing.Size(81, 13);
            this._lblName.TabIndex = 2;
            this._lblName.Text = "DB name (gen):";
            // 
            // _txtName
            // 
            this._txtName.Location = new System.Drawing.Point(125, 49);
            this._txtName.Name = "_txtName";
            this._txtName.Size = new System.Drawing.Size(260, 20);
            this._txtName.TabIndex = 2;
            // 
            // _chkAppend
            // 
            this._chkAppend.AutoSize = true;
            this._chkAppend.Location = new System.Drawing.Point(35, 75);
            this._chkAppend.Name = "_chkAppend";
            this._chkAppend.Size = new System.Drawing.Size(268, 17);
            this._chkAppend.TabIndex = 3;
            this._chkAppend.Text = "If DB exists, append variables instead of generating";
            this._chkAppend.UseVisualStyleBackColor = true;
            // 
            // _btnSelectDb
            // 
            this._btnSelectDb.Location = new System.Drawing.Point(35, 133);
            this._btnSelectDb.Name = "_btnSelectDb";
            this._btnSelectDb.Size = new System.Drawing.Size(110, 23);
            this._btnSelectDb.TabIndex = 5;
            this._btnSelectDb.Text = "Select DB...";
            this._btnSelectDb.UseVisualStyleBackColor = true;
            this._btnSelectDb.Click += new System.EventHandler(this.OnSelectDb);
            // 
            // _lblSelected
            // 
            this._lblSelected.AutoEllipsis = true;
            this._lblSelected.Location = new System.Drawing.Point(151, 137);
            this._lblSelected.Name = "_lblSelected";
            this._lblSelected.Size = new System.Drawing.Size(234, 15);
            this._lblSelected.TabIndex = 6;
            this._lblSelected.Text = "No DB selected";
            // 
            // _btnOk
            // 
            this._btnOk.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this._btnOk.Location = new System.Drawing.Point(229, 233);
            this._btnOk.Name = "_btnOk";
            this._btnOk.Size = new System.Drawing.Size(75, 27);
            this._btnOk.TabIndex = 7;
            this._btnOk.Text = "OK";
            this._btnOk.UseVisualStyleBackColor = true;
            this._btnOk.Click += new System.EventHandler(this.OnOk);
            // 
            // _btnCancel
            // 
            this._btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this._btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this._btnCancel.Location = new System.Drawing.Point(310, 233);
            this._btnCancel.Name = "_btnCancel";
            this._btnCancel.Size = new System.Drawing.Size(75, 27);
            this._btnCancel.TabIndex = 8;
            this._btnCancel.Text = "Cancel";
            this._btnCancel.UseVisualStyleBackColor = true;
            this._btnCancel.Click += new System.EventHandler(this.OnCancel);
            // 
            // _lblDescription
            // 
            this._lblDescription.AutoEllipsis = true;
            this._lblDescription.Location = new System.Drawing.Point(15, 165);
            this._lblDescription.Name = "_lblDescription";
            this._lblDescription.Size = new System.Drawing.Size(366, 32);
            this._lblDescription.TabIndex = 9;
            this._lblDescription.Text = "No description provided.";
            // 
            // DbConfigForm
            // 
            this.AcceptButton = this._btnOk;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this._btnCancel;
            this.ClientSize = new System.Drawing.Size(397, 272);
            this.Controls.Add(this._lblDescription);
            this.Controls.Add(this._btnCancel);
            this.Controls.Add(this._btnOk);
            this.Controls.Add(this._lblSelected);
            this.Controls.Add(this._btnSelectDb);
            this.Controls.Add(this._chkAppend);
            this.Controls.Add(this._txtName);
            this.Controls.Add(this._lblName);
            this.Controls.Add(this._rdoExisting);
            this.Controls.Add(this._rdoGenerate);
            this.Controls.Add(this._lblMode);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "DbConfigForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "DB Configuration";
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion

        private Label _lblMode;
        private RadioButton _rdoGenerate;
        private RadioButton _rdoExisting;
        private Label _lblName;
        private TextBox _txtName;
        private CheckBox _chkAppend;
        private Button _btnSelectDb;
        private Label _lblSelected;
        private System.Windows.Forms.Button _btnOk;
        private System.Windows.Forms.Button _btnCancel;
        private System.Windows.Forms.Label _lblDescription;
    }
}
