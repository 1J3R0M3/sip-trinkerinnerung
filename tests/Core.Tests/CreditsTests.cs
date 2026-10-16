using Trinkwasser.Core.About;
using Trinkwasser.Core.Localization;
using Trinkwasser.Core.Sounds;

namespace Trinkwasser.Core.Tests;

public class CreditsTests
{
    private static IEnumerable<Credit> All => [Credits.App, .. Credits.Libraries, .. Credits.Sounds.Values];

    [Fact]
    public void EverySipSoundHasACredit() =>
        Assert.Equal(SoundCatalog.SipSounds.Order(), Credits.Sounds.Keys.Order());

    [Fact]
    public void AllSoundsAreCc0() =>
        Assert.All(Credits.Sounds.Values, c => Assert.Equal(Credits.Cc0, c.License));

    [Fact]
    public void EveryCreditHasAnHttpsLinkAndAnAuthor() =>
        Assert.All(All, c =>
        {
            Assert.StartsWith("https://", c.Url);
            Assert.False(string.IsNullOrWhiteSpace(c.Author));
        });

    [Theory]
    [InlineData("de")]
    [InlineData("en")]
    public void EveryLicenseHasAName(string language)
    {
        var loc = new Localizer(language, _ => "Sip");
        Assert.All(All.Select(c => c.License).Distinct(), id => Assert.NotEqual("license." + id, loc.LicenseName(id)));
    }
}
