using System;
using System.Windows.Forms;
using OpennessCopy.Forms;
using OpennessCopy.Utils;

namespace OpennessCopy;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        try
        {
            if (args.Length > 0 && args[0].ToLower().Contains("-debug"))
                Logger.DebugTxtOn =  true;
            else
                Logger.DebugTxtOn = false;
            Logger.ClearDebugFile();
            
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Fatal error during startup: {ex.Message}", "Startup Error", 
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}