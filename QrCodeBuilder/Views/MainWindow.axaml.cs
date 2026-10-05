// WICHTIG (Avalonia 12): KEIN manuelles InitializeComponent() definieren —
// der NameGenerator emittiert es zusammen mit den x:Name-Feldern selbst.
using Avalonia.Interactivity;
using Microsoft.Extensions.DependencyInjection;
using NLog;
using QrCodeBuilder.Services;
using QrCodeBuilder.ViewModels;

namespace QrCodeBuilder.Views;

public partial class MainWindow : ChromeWindow
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();

    public MainWindow()
    {
        InitializeComponent();
    }

    private async void OnOpenSettings(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (App.Services?.GetRequiredService<SettingsWindowViewModel>() is not { } viewModel)
            {
                return;
            }

            Log.Info("Nutzeraktion: Einstellungen öffnen.");
            await new SettingsWindow(viewModel).ShowDialog(this);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Einstellungen-Fenster konnte nicht geöffnet werden.");
        }
    }

    private async void OnOpenAbout(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (App.Services?.GetRequiredService<UpdateService>() is not { } updateService)
            {
                return;
            }

            Log.Info("Nutzeraktion: Über-Fenster öffnen.");
            await new AboutWindow(updateService).ShowDialog(this);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Über-Fenster konnte nicht geöffnet werden.");
        }
    }
}
