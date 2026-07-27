using System;
using System.Windows;
using RiftAssistant.Services;
using Velopack;

namespace RiftAssistant
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        [STAThread]
        private static void Main(string[] args)
        {
            // Velopack mümkün olduğunca erken çalışmalı.
            VelopackApp
                .Build()
                .Run();

            var app = new App();

            app.InitializeComponent();

            // UI açılışını bekletmeden arka planda güncelleme kontrolü yap.
            app.Startup += (_, _) =>
            {
                _ = UpdateService.CheckForUpdatesAsync();
            };

            app.Run();
        }
    }
}
