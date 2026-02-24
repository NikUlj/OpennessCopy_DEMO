using System;
using System.Drawing;
using System.Windows.Forms;

namespace OpennessCopy.Forms.HardwareCopy;

/// <summary>
/// Loading form with progress bar for device data extraction
/// Shows real-time progress: "Extracting devices: X / Y"
/// </summary>
public class DeviceLoadingForm : Form
{
    private ProgressBar _progressBar;
    private Label _labelStatus;
    private readonly string _customTitle;

    public DeviceLoadingForm(string title = null)
    {
        _customTitle = title;
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        this._progressBar = new ProgressBar();
        this._labelStatus = new Label();
        this.SuspendLayout();

        // Form properties
        this.Text = !string.IsNullOrWhiteSpace(_customTitle) ? _customTitle : "Loading Device Data";
        this.Size = new Size(400, 120);
        this.StartPosition = FormStartPosition.CenterParent;
        this.FormBorderStyle = FormBorderStyle.FixedDialog;
        this.MinimizeBox = false;
        this.MaximizeBox = false;
        this.ControlBox = false;

        // Status label
        this._labelStatus.Text = "Preparing to extract devices...";
        this._labelStatus.Location = new Point(12, 20);
        this._labelStatus.Size = new Size(360, 20);
        this._labelStatus.TextAlign = ContentAlignment.MiddleCenter;
        this._labelStatus.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

        // Progress bar
        this._progressBar.Location = new Point(12, 50);
        this._progressBar.Size = new Size(360, 23);
        this._progressBar.Style = ProgressBarStyle.Continuous;
        this._progressBar.Minimum = 0;
        this._progressBar.Maximum = 100;
        this._progressBar.Value = 0;
        this._progressBar.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

        // Add controls to form
        this.Controls.Add(this._labelStatus);
        this.Controls.Add(this._progressBar);

        this.ResumeLayout(false);
    }

    /// <summary>
    /// Updates progress with current and total device counts
    /// </summary>
    public void UpdateProgress(int current, int total)
    {
        if (InvokeRequired)
        {
            Invoke(new Action<int, int>(UpdateProgress), current, total);
            return;
        }

        _labelStatus.Text = $"Extracting devices: {current} / {total}";

        if (total > 0)
        {
            int percentage = (int)((double)current / total * 100);
            _progressBar.Value = Math.Min(100, Math.Max(0, percentage));
        }
    }

    /// <summary>
    /// Centers the loading form relative to the parent form
    /// </summary>
    public void CenterToParent(Form parent)
    {
        if (parent != null)
        {
            int x = parent.Left + (parent.Width - this.Width) / 2;
            int y = parent.Top + (parent.Height - this.Height) / 2;
            this.Location = new Point(x, y);
        }
    }
}
