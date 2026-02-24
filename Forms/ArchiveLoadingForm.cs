using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace OpennessCopy.Forms;

/// <summary>
/// Non-modal progress form shown while loading TIA Portal file (archive or project)
/// </summary>
public class ArchiveLoadingForm : Form
{
    private Label _labelStatus;
    private ProgressBar _progressBar;

    public ArchiveLoadingForm(string archivePath)
    {
        InitializeComponent();
        _labelStatus.Text = $"Loading file: {Path.GetFileName(archivePath)}...";
    }

    private void InitializeComponent()
    {
        this._labelStatus = new Label();
        this._progressBar = new ProgressBar();
        this.SuspendLayout();

        // Form properties
        this.Text = "Loading File";
        this.Size = new Size(400, 120);
        this.StartPosition = FormStartPosition.CenterParent;
        this.MinimizeBox = false;
        this.MaximizeBox = false;
        this.FormBorderStyle = FormBorderStyle.FixedDialog;
        this.ControlBox = false; // No close button - must be closed programmatically

        // Status label
        this._labelStatus.Text = "Loading file...";
        this._labelStatus.Location = new Point(12, 15);
        this._labelStatus.Size = new Size(360, 20);
        this._labelStatus.TextAlign = ContentAlignment.MiddleLeft;

        // Progress bar (indeterminate/marquee style)
        this._progressBar.Location = new Point(12, 45);
        this._progressBar.Size = new Size(360, 23);
        this._progressBar.Style = ProgressBarStyle.Marquee;
        this._progressBar.MarqueeAnimationSpeed = 30;

        // Add controls to form
        this.Controls.Add(this._labelStatus);
        this.Controls.Add(this._progressBar);

        this.ResumeLayout(false);
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
