using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Trinkwasser.App.Services;
using Trinkwasser.Core.About;
using Trinkwasser.Core.Localization;

namespace Trinkwasser.App.ViewModels;

/// <summary>Eintrag der Lizenzliste: Name, Lizenz und Urheber, Link zur Quelle.</summary>
/// <param name="label">Bei Tönen der Name in Sip (wechselt mit der Sprache); dahinter steht der Originaltitel.</param>
public sealed partial class CreditItem(Localizer loc, Credit credit, Func<string>? label = null) : ObservableObject
{
    public string Name => label?.Invoke() ?? credit.Name;

    public string Details => loc.InfoCredit(credit.License, credit.Author)
        + (label is null ? string.Empty : " · " + credit.Name);

    public string OpenName => loc.InfoOpenLink(Name);

    [RelayCommand]
    private Task Open() => AppInfo.OpenAsync(credit.Url);

    public void RefreshTexts() => OnPropertyChanged(string.Empty);
}
