using Avalonia.Controls;
using Avalonia.Platform;
using NLog;

namespace QrCodeBuilder.Views;

/// <summary>
/// Custom-Chrome nach Avalonia-12-Konvention (Kroste-Standard, Referenz: Amtsschimmel):
/// BorderOnly (NICHT None — sonst fehlen die nativen Resize-Griffe) und Client-Area
/// bis in die Dekoration ausgedehnt. Ohne ExtendClientArea liegt die OS-Caption-
/// Hit-Test-Zone über der eigenen Titelleiste und schluckt Klicks und Drag!
/// </summary>
public class ChromeWindow : Window
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();

    private static readonly Uri IconUri = new("avares://QrCodeBuilder/Assets/qrcodebuilder.png");

    protected ChromeWindow()
    {
        WindowDecorations = WindowDecorations.BorderOnly;
        ExtendClientAreaToDecorationsHint = true;
        ExtendClientAreaTitleBarHeightHint = -1;
        CanResize = true;

        // Ohne Icon bleibt das Fenster lauffähig — nur die Taskleiste zeigt dann das Standardsymbol.
        try
        {
            if (AssetLoader.Exists(IconUri))
            {
                Icon = new WindowIcon(AssetLoader.Open(IconUri));
            }
        }
        catch (Exception ex)
        {
            Log.Warn(ex, "Fenster-Icon konnte nicht geladen werden.");
        }
    }
}
