using System;
using System.Windows.Forms;

namespace ShutdownServerApp
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            // Als ApplicationConfiguration.Initialize() fouten geeft, vervang dan met:
            // Application.EnableVisualStyles();
            // Application.SetCompatibleTextRenderingDefault(false);
            ApplicationConfiguration.Initialize();
            Application.Run(new MainForm());
        }
    }
}
