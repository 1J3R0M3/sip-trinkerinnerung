using Trinkwasser.Core.Notifications;

namespace Trinkwasser.Core.Tests;

public class ToastArgumentsTests
{
    [Theory]
    [InlineData(ToastAction.Open)]
    [InlineData(ToastAction.Drank)]
    [InlineData(ToastAction.Snooze)]
    [InlineData(ToastAction.TestDrank)]
    [InlineData(ToastAction.Achievements)]
    public void RoundTrips(ToastAction action) =>
        Assert.Equal(action, ToastArguments.Parse(new Dictionary<string, string>
        {
            [ToastArguments.ActionKey] = ToastArguments.ToArgument(action),
        }));

    [Fact]
    public void UnknownOrMissingMeansOpen()
    {
        Assert.Equal(ToastAction.Open, ToastArguments.Parse(new Dictionary<string, string>()));
        Assert.Equal(ToastAction.Open, ToastArguments.Parse(new Dictionary<string, string> { ["action"] = "x" }));
    }
}
