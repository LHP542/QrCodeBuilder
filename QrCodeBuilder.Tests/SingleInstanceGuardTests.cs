using FluentAssertions;
using QrCodeBuilder.Services;

namespace QrCodeBuilder.Tests;

public class SingleInstanceGuardTests
{
    [Fact]
    public async Task Zweitstart_wird_abgewiesen_und_weckt_die_erste_Instanz()
    {
        var pipe = "QrCodeBuilder.Test." + Guid.NewGuid().ToString("N");
        var aktiviert = new TaskCompletionSource();

        using var erste = new SingleInstanceGuard(pipe);
        erste.ActivationRequested += () => aktiviert.TrySetResult();
        erste.TryClaim().Should().BeTrue();

        using var zweite = new SingleInstanceGuard(pipe);
        zweite.TryClaim().Should().BeFalse();
        zweite.NotifyPrimary().Should().BeTrue();

        var fertig = await Task.WhenAny(aktiviert.Task, Task.Delay(5000, TestContext.Current.CancellationToken));
        fertig.Should().Be(aktiviert.Task, "die erste Instanz muss das Aktivierungssignal bekommen");
    }
}
