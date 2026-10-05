using QrCodeBuilder.Localization;

namespace QrCodeBuilder.ViewModels;

/// <summary>
/// Auswahleintrag einer ComboBox mit übersetzter Beschriftung. Die Beschriftung ist ein
/// <see cref="LocalizedString"/> und folgt damit dem Sprachwechsel live — ein fertiger
/// String über ToString würde in einer bereits gefüllten ComboBox stehen bleiben.
/// </summary>
public abstract record LocalizedChoice(string Key)
{
    public LocalizedString Label => LocalizedString.Get(Key);
}

public sealed record Choice<T>(T Value, string Key) : LocalizedChoice(Key);
