using System.ComponentModel;
using System.Windows.Forms;

namespace OpennessCopy.Forms;

partial class SimplePLCSelectionForm
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
        this._treeViewPlcs = new System.Windows.Forms.TreeView();
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
        this._lblTitle.Size = new System.Drawing.Size(150, 17);
        this._lblTitle.TabIndex = 0;
        this._lblTitle.Text = "Select Target PLC";
        // 
        // _treeViewPlcs
        // 
        this._treeViewPlcs.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
        this._treeViewPlcs.HideSelection = false;
        this._treeViewPlcs.Location = new System.Drawing.Point(15, 35);
        this._treeViewPlcs.Name = "_treeViewPlcs";
        this._treeViewPlcs.Size = new System.Drawing.Size(520, 300);
        this._treeViewPlcs.TabIndex = 1;
        this._treeViewPlcs.DoubleClick += new System.EventHandler(this.OnTreeViewDoubleClick);
        // 
        // _lblStatus
        // 
        this._lblStatus.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
        this._lblStatus.Location = new System.Drawing.Point(12, 342);
        this._lblStatus.Name = "_lblStatus";
        this._lblStatus.Size = new System.Drawing.Size(523, 18);
        this._lblStatus.TabIndex = 2;
        this._lblStatus.Text = "Loading PLCs...";
        // 
        // _btnOk
        // 
        this._btnOk.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
        this._btnOk.Location = new System.Drawing.Point(349, 370);
        this._btnOk.Name = "_btnOk";
        this._btnOk.Size = new System.Drawing.Size(90, 30);
        this._btnOk.TabIndex = 3;
        this._btnOk.Text = "OK";
        this._btnOk.UseVisualStyleBackColor = true;
        this._btnOk.Click += new System.EventHandler(this.OnOkClicked);
        // 
        // _btnCancel
        // 
        this._btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
        this._btnCancel.Location = new System.Drawing.Point(445, 370);
        this._btnCancel.Name = "_btnCancel";
        this._btnCancel.Size = new System.Drawing.Size(90, 30);
        this._btnCancel.TabIndex = 4;
        this._btnCancel.Text = "Cancel";
        this._btnCancel.UseVisualStyleBackColor = true;
        this._btnCancel.Click += new System.EventHandler(this.OnCancelClicked);
        // 
        // SimplePLCSelectionForm
        // 
        this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
        this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
        this.ClientSize = new System.Drawing.Size(550, 412);
        this.Controls.Add(this._btnCancel);
        this.Controls.Add(this._btnOk);
        this.Controls.Add(this._lblStatus);
        this.Controls.Add(this._treeViewPlcs);
        this.Controls.Add(this._lblTitle);
        this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
        this.MaximizeBox = false;
        this.MinimizeBox = false;
        this.Name = "SimplePLCSelectionForm";
        this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
        this.Text = "PLC Selection";
        this.ResumeLayout(false);
        this.PerformLayout();

    }

    #endregion

    private Label _lblTitle;
    private TreeView _treeViewPlcs;
    private Label _lblStatus;
    private Button _btnOk;
    private Button _btnCancel;
}
