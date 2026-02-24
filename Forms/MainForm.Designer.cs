using System.ComponentModel;
using System.Windows.Forms;

namespace OpennessCopy.Forms;

partial class MainForm
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
        this._cmbWorkflowType = new System.Windows.Forms.ComboBox();
        this._lblTiaVersion = new System.Windows.Forms.Label();
        this._cmbTiaVersion = new System.Windows.Forms.ComboBox();
        this._btnStart = new System.Windows.Forms.Button();
        this._btnCancel = new System.Windows.Forms.Button();
        this._lblTitle = new System.Windows.Forms.Label();
        this._lblStatus = new System.Windows.Forms.Label();
        this._progressBar = new System.Windows.Forms.ProgressBar();
        this._logPanel = new System.Windows.Forms.RichTextBox();
        this.SuspendLayout();
        // 
        // _cmbWorkflowType
        // 
        this._cmbWorkflowType.Anchor = System.Windows.Forms.AnchorStyles.Top;
        this._cmbWorkflowType.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
        this._cmbWorkflowType.Items.AddRange(new object[] { "PLC Block Copy", "Hardware Copy", "Conveyor Generator" });
        this._cmbWorkflowType.Location = new System.Drawing.Point(250, 73);
        this._cmbWorkflowType.Name = "_cmbWorkflowType";
        this._cmbWorkflowType.Size = new System.Drawing.Size(200, 21);
        this._cmbWorkflowType.TabIndex = 1;
        this._cmbWorkflowType.SelectedIndex = 0;
        // 
        // _lblTiaVersion
        // 
        this._lblTiaVersion.Anchor = System.Windows.Forms.AnchorStyles.Top;
        this._lblTiaVersion.Location = new System.Drawing.Point(250, 100);
        this._lblTiaVersion.Name = "_lblTiaVersion";
        this._lblTiaVersion.Size = new System.Drawing.Size(200, 15);
        this._lblTiaVersion.TabIndex = 8;
        this._lblTiaVersion.Text = "TIA API Version";
        this._lblTiaVersion.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
        // 
        // _cmbTiaVersion
        // 
        this._cmbTiaVersion.Anchor = System.Windows.Forms.AnchorStyles.Top;
        this._cmbTiaVersion.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
        this._cmbTiaVersion.Items.AddRange(new object[] { "TIA Portal V18", "TIA Portal V20" });
        this._cmbTiaVersion.Location = new System.Drawing.Point(250, 118);
        this._cmbTiaVersion.Name = "_cmbTiaVersion";
        this._cmbTiaVersion.Size = new System.Drawing.Size(200, 21);
        this._cmbTiaVersion.TabIndex = 2;
        this._cmbTiaVersion.SelectedIndex = 0;
        // 
        // _btnStart
        // 
        this._btnStart.Anchor = System.Windows.Forms.AnchorStyles.Top;
        this._btnStart.Location = new System.Drawing.Point(250, 150);
        this._btnStart.Name = "_btnStart";
        this._btnStart.Size = new System.Drawing.Size(200, 30);
        this._btnStart.TabIndex = 3;
        this._btnStart.Text = "Start";
        this._btnStart.UseVisualStyleBackColor = true;
        this._btnStart.Click += new System.EventHandler(this.btnStart_Click);
        // 
        // _btnCancel
        // 
        this._btnCancel.Anchor = System.Windows.Forms.AnchorStyles.Top;
        this._btnCancel.Location = new System.Drawing.Point(250, 150);
        this._btnCancel.Name = "_btnCancel";
        this._btnCancel.Size = new System.Drawing.Size(200, 30);
        this._btnCancel.TabIndex = 3;
        this._btnCancel.Text = "Cancel";
        this._btnCancel.UseVisualStyleBackColor = true;
        this._btnCancel.Visible = false;
        this._btnCancel.Click += new System.EventHandler(this.btnCancel_Click);
        // 
        // _lblTitle
        // 
        this._lblTitle.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
        this._lblTitle.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
        this._lblTitle.Location = new System.Drawing.Point(0, 30);
        this._lblTitle.Name = "_lblTitle";
        this._lblTitle.Size = new System.Drawing.Size(700, 20);
        this._lblTitle.TabIndex = 0;
        this._lblTitle.Text = "Openness Multitool";
        this._lblTitle.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
        // 
        // _lblStatus
        // 
        this._lblStatus.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
        this._lblStatus.Location = new System.Drawing.Point(0, 194);
        this._lblStatus.Name = "_lblStatus";
        this._lblStatus.Size = new System.Drawing.Size(700, 13);
        this._lblStatus.TabIndex = 3;
        this._lblStatus.Text = "Ready";
        this._lblStatus.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
        // 
        // _progressBar
        // 
        this._progressBar.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
        this._progressBar.Location = new System.Drawing.Point(10, 225);
        this._progressBar.Name = "_progressBar";
        this._progressBar.Size = new System.Drawing.Size(680, 20);
        this._progressBar.Style = System.Windows.Forms.ProgressBarStyle.Continuous;
        this._progressBar.TabIndex = 4;
        this._progressBar.Visible = false;
        // 
        // _logPanel
        // 
        this._logPanel.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
        this._logPanel.BackColor = System.Drawing.Color.Black;
        this._logPanel.Font = new System.Drawing.Font("Consolas", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
        this._logPanel.ForeColor = System.Drawing.Color.White;
        this._logPanel.Location = new System.Drawing.Point(10, 250);
        this._logPanel.Name = "_logPanel";
        this._logPanel.ReadOnly = true;
        this._logPanel.ScrollBars = System.Windows.Forms.RichTextBoxScrollBars.Vertical;
        this._logPanel.Size = new System.Drawing.Size(680, 160);
        this._logPanel.TabIndex = 4;
        this._logPanel.Text = "";
        this._logPanel.WordWrap = false;
        // 
        // MainForm
        // 
        this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
        this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
        this.ClientSize = new System.Drawing.Size(700, 420);
        this.Controls.Add(this._lblTitle);
        this.Controls.Add(this._cmbWorkflowType);
        this.Controls.Add(this._lblTiaVersion);
        this.Controls.Add(this._cmbTiaVersion);
        this.Controls.Add(this._btnStart);
        this.Controls.Add(this._btnCancel);
        this.Controls.Add(this._lblStatus);
        this.Controls.Add(this._progressBar);
        this.Controls.Add(this._logPanel);
        this.MinimumSize = new System.Drawing.Size(700, 420);
        this.Name = "MainForm";
        this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
        this.Text = "Openness Multitool";
        this.ResumeLayout(false);
    }
    
    private ComboBox _cmbWorkflowType;
    private Button _btnStart;
    private Button _btnCancel;
    private Label _lblTitle;
    private System.Windows.Forms.Label _lblStatus;
    private ProgressBar _progressBar;
    private RichTextBox _logPanel;
    private Label _lblTiaVersion;
    private ComboBox _cmbTiaVersion;

    #endregion
}
