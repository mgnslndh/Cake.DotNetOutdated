using Cake.DotNetOutdated.Tests.Fixtures;
using Cake.Testing;

namespace Cake.DotNetOutdated.Tests;

public sealed class DotNetOutdatedToolTests
{
    private static string Args(Action<DotNetOutdatedSettings> configure = null, string path = null)
    {
        var fixture = new SharedOptionsFixture { Path = path };
        configure?.Invoke(fixture.Settings);
        return fixture.Run().Args;
    }

    [Fact]
    public void Should_Start_With_The_Outdated_Command_And_Omit_Path_When_Not_Specified()
    {
        Assert.Equal("outdated", Args());
    }

    [Fact]
    public void Should_Quote_The_Path_When_Specified()
    {
        Assert.Equal("outdated \"./src/App.sln\"", Args(path: "./src/App.sln"));
    }

    [Fact]
    public void Should_Add_Include_Auto_References()
    {
        Assert.Equal("outdated --include-auto-references", Args(s => s.IncludeAutoReferences = true));
    }

    [Theory]
    [InlineData(DotNetOutdatedPreRelease.Auto, "outdated --pre-release Auto")]
    [InlineData(DotNetOutdatedPreRelease.Always, "outdated --pre-release Always")]
    [InlineData(DotNetOutdatedPreRelease.Never, "outdated --pre-release Never")]
    public void Should_Add_Pre_Release(DotNetOutdatedPreRelease value, string expected)
    {
        Assert.Equal(expected, Args(s => s.PreRelease = value));
    }

    [Fact]
    public void Should_Add_Pre_Release_Label()
    {
        Assert.Equal("outdated --pre-release-label \"rc\"", Args(s => s.PreReleaseLabel = "rc"));
    }

    [Theory]
    [InlineData(DotNetOutdatedVersionLock.None, "outdated --version-lock None")]
    [InlineData(DotNetOutdatedVersionLock.Major, "outdated --version-lock Major")]
    [InlineData(DotNetOutdatedVersionLock.Minor, "outdated --version-lock Minor")]
    public void Should_Add_Version_Lock(DotNetOutdatedVersionLock value, string expected)
    {
        Assert.Equal(expected, Args(s => s.VersionLock = value));
    }

    [Fact]
    public void Should_Add_Transitive()
    {
        Assert.Equal("outdated --transitive", Args(s => s.Transitive = true));
    }

    [Fact]
    public void Should_Add_Transitive_Depth()
    {
        Assert.Equal("outdated --transitive-depth 2", Args(s => s.TransitiveDepth = 2));
    }

    [Fact]
    public void Should_Add_Older_Than()
    {
        Assert.Equal("outdated --older-than 7", Args(s => s.OlderThan = 7));
    }

    [Fact]
    public void Should_Add_Maximum_Version()
    {
        Assert.Equal("outdated --maximum-version \"8.0\"", Args(s => s.MaximumVersion = "8.0"));
    }

    [Fact]
    public void Should_Repeat_Include_For_Each_Filter()
    {
        Assert.Equal("outdated --include \"Newtonsoft\" --include \"Serilog\"", Args(s =>
        {
            s.Include.Add("Newtonsoft");
            s.Include.Add("Serilog");
        }));
    }

    [Fact]
    public void Should_Repeat_Exclude_For_Each_Filter()
    {
        Assert.Equal("outdated --exclude \"Microsoft\" --exclude \"System\"", Args(s =>
        {
            s.Exclude.Add("Microsoft");
            s.Exclude.Add("System");
        }));
    }

    [Fact]
    public void Should_Skip_Blank_Filters()
    {
        Assert.Equal("outdated", Args(s =>
        {
            s.Include.Add(" ");
            s.Exclude.Add(null);
        }));
    }

    [Fact]
    public void Should_Add_Recursive()
    {
        Assert.Equal("outdated --recursive", Args(s => s.Recursive = true));
    }

    [Theory]
    [InlineData("./src", "outdated \"/Working/src\" --recursive")]
    [InlineData(".", "outdated \"/Working\" --recursive")]
    [InlineData("src/App.sln", "outdated \"/Working/src/App.sln\" --recursive")]
    [InlineData("/abs/path", "outdated \"/abs/path\" --recursive")]
    public void Should_Make_The_Path_Absolute_When_Recursive(string path, string expected)
    {
        // dotnet-outdated 4.8.1 cannot load discovered projects when a relative path is combined with --recursive.
        Assert.Equal(expected, Args(s => s.Recursive = true, path));
    }

    [Fact]
    public void Should_Resolve_A_Recursive_Path_Against_The_Working_Directory_Setting()
    {
        Assert.Equal(
            "outdated \"/Working/sub/src\" --recursive",
            Args(s =>
            {
                s.Recursive = true;
                s.WorkingDirectory = "/Working/sub";
            }, "src"));
    }

    [Fact]
    public void Should_Not_Invent_A_Path_When_Recursive_Without_A_Path()
    {
        Assert.Equal("outdated --recursive", Args(s => s.Recursive = true));
    }

    [Fact]
    public void Should_Leave_The_Path_Unchanged_When_Not_Recursive()
    {
        Assert.Equal("outdated \"./src\"", Args(path: "./src"));
    }

    [Fact]
    public void Should_Add_Include_File_Based_Apps()
    {
        Assert.Equal("outdated --include-file-based-apps", Args(s => s.IncludeFileBasedApps = true));
    }

    [Fact]
    public void Should_Add_Ignore_Failed_Sources()
    {
        Assert.Equal("outdated --ignore-failed-sources", Args(s => s.IgnoreFailedSources = true));
    }

    [Theory]
    [InlineData(DotNetOutdatedCredentialLogLevel.Debug, "outdated --nuget-cred-log-level debug")]
    [InlineData(DotNetOutdatedCredentialLogLevel.Verbose, "outdated --nuget-cred-log-level verbose")]
    [InlineData(DotNetOutdatedCredentialLogLevel.Information, "outdated --nuget-cred-log-level information")]
    [InlineData(DotNetOutdatedCredentialLogLevel.Minimal, "outdated --nuget-cred-log-level minimal")]
    [InlineData(DotNetOutdatedCredentialLogLevel.Warning, "outdated --nuget-cred-log-level warning")]
    [InlineData(DotNetOutdatedCredentialLogLevel.Error, "outdated --nuget-cred-log-level error")]
    public void Should_Add_NuGet_Credential_Log_Level(DotNetOutdatedCredentialLogLevel value, string expected)
    {
        Assert.Equal(expected, Args(s => s.NuGetCredentialLogLevel = value));
    }

    [Fact]
    public void Should_Add_Runtime()
    {
        Assert.Equal("outdated --runtime \"linux-x64\"", Args(s => s.Runtime = "linux-x64"));
    }

    [Fact]
    public void Should_Add_Idle_Timeout()
    {
        Assert.Equal("outdated --idle-timeout 300", Args(s => s.IdleTimeout = 300));
    }

    [Fact]
    public void Should_Emit_Options_In_A_Stable_Order()
    {
        var args = Args(
            s =>
            {
                s.IdleTimeout = 60;
                s.Runtime = "win-x64";
                s.NuGetCredentialLogLevel = DotNetOutdatedCredentialLogLevel.Error;
                s.IgnoreFailedSources = true;
                s.IncludeFileBasedApps = true;
                s.Recursive = true;
                s.Exclude.Add("b");
                s.Include.Add("a");
                s.MaximumVersion = "8.0";
                s.OlderThan = 7;
                s.TransitiveDepth = 2;
                s.Transitive = true;
                s.VersionLock = DotNetOutdatedVersionLock.Minor;
                s.PreReleaseLabel = "rc";
                s.PreRelease = DotNetOutdatedPreRelease.Always;
                s.IncludeAutoReferences = true;
            },
            "./src");

        Assert.Equal(
            "outdated \"/Working/src\" --include-auto-references --pre-release Always --pre-release-label \"rc\" " +
            "--version-lock Minor --transitive --transitive-depth 2 --older-than 7 --maximum-version \"8.0\" " +
            "--include \"a\" --exclude \"b\" --recursive --include-file-based-apps --ignore-failed-sources " +
            "--nuget-cred-log-level error --runtime \"win-x64\" --idle-timeout 60",
            args);
    }

    [Fact]
    public void Should_Throw_If_Executable_Could_Not_Be_Found()
    {
        var fixture = new SharedOptionsFixture();
        fixture.GivenDefaultToolDoNotExist();

        var result = Record.Exception(() => fixture.Run());

        Assertions.IsCakeException(result, "dotnet-outdated: Could not locate executable.");
    }

    [Fact]
    public void Should_Throw_If_Process_Was_Not_Started()
    {
        var fixture = new SharedOptionsFixture();
        fixture.GivenProcessCannotStart();

        var result = Record.Exception(() => fixture.Run());

        Assertions.IsCakeException(result, "dotnet-outdated: Process was not started.");
    }

    [Fact]
    public void Should_Throw_If_Process_Has_A_Non_Zero_Exit_Code()
    {
        var fixture = new SharedOptionsFixture();
        fixture.GivenProcessExitsWithCode(1);

        var result = Record.Exception(() => fixture.Run());

        Assertions.IsCakeException(result, "dotnet-outdated: Process returned an error (exit code 1).");
    }

    [Fact]
    public void Should_Not_Throw_If_Exit_Code_Is_Handled()
    {
        var fixture = new SharedOptionsFixture();
        fixture.Settings.HandleExitCode = code => code is 0 or 2;
        fixture.GivenProcessExitsWithCode(2);

        var result = Record.Exception(() => fixture.Run());

        Assert.Null(result);
    }

    [Fact]
    public void Should_Use_Explicit_Tool_Path()
    {
        var fixture = new SharedOptionsFixture();
        fixture.Settings.ToolPath = "/custom/dotnet.exe";
        fixture.GivenSettingsToolPathExist();

        var result = fixture.Run();

        Assert.Equal("/custom/dotnet.exe", result.Path.FullPath);
    }

    [Fact]
    public void Should_Use_Working_Directory_From_Settings()
    {
        var fixture = new SharedOptionsFixture();
        fixture.Settings.WorkingDirectory = "/Working/src";

        var result = fixture.Run();

        Assert.Equal("/Working/src", result.Process.WorkingDirectory.FullPath);
    }
}
