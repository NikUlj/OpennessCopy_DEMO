using System;
using System.Windows.Forms;

namespace OpennessCopy.Forms
{
    public class SimplePasswordForm : Form
    {
        public string Password { get; private set; }
        
        private TextBox _txtPassword;
        private Button _btnOk;
        private Button _btnCancel;
        private Label _lblPrompt;

        public SimplePasswordForm(string deviceName)
        {
            InitializeComponent(deviceName);
        }

        private void InitializeComponent(string deviceName)
        {
            Text = "Safety Password Required";
            Size = new System.Drawing.Size(350, 150);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            
            _lblPrompt = new Label
            {
                Text = $"Enter safety password for '{deviceName}':",
                Location = new System.Drawing.Point(12, 15),
                Size = new System.Drawing.Size(310, 23),
                TextAlign = System.Drawing.ContentAlignment.MiddleLeft
            };
            
            _txtPassword = new TextBox
            {
                Location = new System.Drawing.Point(12, 45),
                Size = new System.Drawing.Size(310, 23),
                UseSystemPasswordChar = true
            };
            
            _btnOk = new Button
            {
                Text = "OK",
                Location = new System.Drawing.Point(167, 75),
                Size = new System.Drawing.Size(75, 23),
                DialogResult = DialogResult.OK
            };
            
            _btnCancel = new Button
            {
                Text = "Cancel",
                Location = new System.Drawing.Point(247, 75),
                Size = new System.Drawing.Size(75, 23),
                DialogResult = DialogResult.Cancel
            };
            
            _btnOk.Click += BtnOK_Click;
            
            Controls.AddRange([_lblPrompt, _txtPassword, _btnOk, _btnCancel]);
            AcceptButton = _btnOk;
            CancelButton = _btnCancel;
        }

        private void BtnOK_Click(object sender, EventArgs e)
        {
            Password = _txtPassword.Text;
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            _txtPassword.Focus();
        }
    }
}