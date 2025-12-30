using System;
using System.IO;
using System.Windows;
using Vexa.Models;
using Vexa.Services;
using Vexa.ViewModels;

namespace Vexa;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var window = new MainWindow();
        MainWindow = window;
        window.Show();

        var autosavePath = GetAutosavePath();
        if (File.Exists(autosavePath))
        {
            var result = MessageBox.Show("Recover autosaved session?", "Recovery", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    var session = new JsonSessionService().Load(autosavePath);
                    window.ViewModel.LoadRecoveredSession(session, autosavePath);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to recover autosave: {ex.Message}", "Recovery", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }
    }

    private static string GetAutosavePath()
    {
        var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Vexa");
        Directory.CreateDirectory(folder);
        return Path.Combine(folder, "autosave.json");
    }
}
