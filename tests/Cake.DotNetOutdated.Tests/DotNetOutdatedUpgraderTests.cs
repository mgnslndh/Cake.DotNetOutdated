using Cake.DotNetOutdated.Tests.Fixtures;
using Cake.Testing;

namespace Cake.DotNetOutdated.Tests;

public sealed class DotNetOutdatedUpgraderTests
{
    private static string Args(Action<DotNetOutdatedUpgradeSettings> configure = null, string path = null)
    {
        var fixture = new UpgraderFixture { Path = path };
        configure?.Invoke(fixture.Settings);
        return fixture.Run().Args;
    }

    [Fact]
    public void Should_Throw_If_Settings_Are_Null()
    {
        var fixture = new UpgraderFixture { Settings = null };

        var result = Record.Exception(() => fixture.Run());

        Assertions.IsArgumentNullException(result, "settings");
    }

    [Fact]
    public void Should_Always_Upgrade_Automatically_Using_An_Attached_Value()
    {
        Assert.Equal("outdated \"./src\" --upgrade:Auto", Args(path: "./src"));
    }

    [Fact]
    public void Should_Add_No_Restore()
    {
        Assert.Equal("outdated --upgrade:Auto --no-restore", Args(s => s.NoRestore = true));
    }

    [Fact]
    public void Should_Emit_Shared_Options_Before_Upgrade_Options()
    {
        var args = Args(
            s =>
            {
                s.NoRestore = true;
                s.MaximumVersion = "8.0";
                s.Include.Add("Serilog");
            },
            "./src");

        Assert.Equal(
            "outdated \"./src\" --maximum-version \"8.0\" --include \"Serilog\" --upgrade:Auto --no-restore",
            args);
    }

    [Fact]
    public void Should_Throw_If_Process_Has_A_Non_Zero_Exit_Code()
    {
        var fixture = new UpgraderFixture();
        fixture.GivenProcessExitsWithCode(3);

        var result = Record.Exception(() => fixture.Run());

        Assertions.IsCakeException(result, "dotnet-outdated: Process returned an error (exit code 3).");
    }
}
