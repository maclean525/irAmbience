using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Threading;

namespace iRacingAmbience
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Mutex mt = null;
            try
            {
                mt = Mutex.OpenExisting("irAmbience");
            }
            catch (WaitHandleCannotBeOpenedException)
            {
            }
            if (mt != null)
            {
                mt.Close();
                MessageBox.Show("irAmbience already running");
                Application.Exit();
            }
            else
            {
                mt = new Mutex(true, "irAmbience");

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new MainForm());

                GC.KeepAlive(mt);
                mt.ReleaseMutex();
            }

        }
    }
}
