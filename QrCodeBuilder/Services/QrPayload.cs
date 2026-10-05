using System.Text;

namespace QrCodeBuilder.Services;

/// <summary>Verschlüsselung eines WLANs im WIFI:-Schema.</summary>
public enum WifiSecurity
{
    Wpa,
    Wep,
    None,
}

/// <summary>
/// Baut die Texte, die im QR-Code stehen. Für WLAN gilt das de-facto-Format
/// aus ZXing (<c>WIFI:T:WPA;S:name;P:passwort;;</c>), das Android und iOS mit der
/// Kamera-App direkt verstehen.
/// </summary>
public static class QrPayload
{
    public static string Wifi(string ssid, string? password, WifiSecurity security, bool hidden)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ssid);

        var payload = new StringBuilder("WIFI:");

        payload.Append("T:").Append(security switch
        {
            WifiSecurity.Wep => "WEP",
            WifiSecurity.None => "nopass",
            _ => "WPA",
        }).Append(';');

        payload.Append("S:").Append(Escape(ssid)).Append(';');

        if (security != WifiSecurity.None && !string.IsNullOrEmpty(password))
        {
            payload.Append("P:").Append(Escape(password)).Append(';');
        }

        if (hidden)
        {
            payload.Append("H:true;");
        }

        return payload.Append(';').ToString();
    }

    /// <summary>
    /// Die Zeichen <c>\ ; , : "</c> haben im WIFI-Schema eine Bedeutung und
    /// werden mit Backslash entwertet — sonst bricht ein Semikolon im Passwort
    /// das Feld vorzeitig ab und das Handy verbindet sich mit dem falschen Kennwort.
    /// </summary>
    public static string Escape(string value)
    {
        var escaped = new StringBuilder(value.Length + 8);

        foreach (var c in value)
        {
            if (c is '\\' or ';' or ',' or ':' or '"')
            {
                escaped.Append('\\');
            }

            escaped.Append(c);
        }

        return escaped.ToString();
    }
}
