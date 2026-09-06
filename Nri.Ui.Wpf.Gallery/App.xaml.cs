using System;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Threading;

namespace Nri.Ui.Wpf.Gallery
{
    public partial class App : Application
    {
        public App()
        {
            DispatcherUnhandledException += OnDispatcherUnhandledException;
        }

        private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            try
            {
                File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "gallery_startup_error.log"), e.Exception.ToString(), Encoding.UTF8);
            }
            catch { }
        }
    }
}
