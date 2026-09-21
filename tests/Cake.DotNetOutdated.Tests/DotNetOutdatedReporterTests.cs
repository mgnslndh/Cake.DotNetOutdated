using Cake.Core.IO;
using Cake.DotNetOutdated.Tests.Fixtures;
using Cake.Testing;

namespace Cake.DotNetOutdated.Tests;

public sealed class DotNetOutdatedReporterTests
{
    private const string EmptyReport = "{\n  \"Projects\": []\n}";

    private static string Args(Action<DotNetOutdatedReportSettings> configure = null, string path = null)
    {
        var fixture = new ReporterFixture { Path = path };
        configure?.Invoke(fixture.Settings);
        return fixture.Run().Args;
    }

    [Fact]
    public void Should_Throw_If_Settings_Are_Null()
    {
        var fixture = new ReporterFixture { Settings = null };

        var result = Record.Exception(() => fixture.Run());

        Assertions.IsArgumentNullException(result, "settings");
    }

    [Fact]
    public void Should_Run_Without_Options_By_Default()
    {
        Assert.Equal("outdated \"./src\"", Args(path: "./src"));
    }

    [Fact]
    public void Should_Add_Include_Up_To_Date()
    {
        Assert.Equal("outdated --include-up-to-date", Args(s => s.IncludeUpToDate = true));
    }

    [Fact]
    public void Should_Add_Fail_On_Updates()
    {
        Assert.Equal("outdated --fail-on-updates", Args(s => s.FailOnUpdates = true));
    }

    [Fact]
    public void Should_Pass_The_Output_File_As_An_Absolute_Path()
    {
        Assert.Equal("outdated --output \"/Working/out/outdated.json\"", Args(s => s.OutputFile = "out/outdated.json"));
    }

    [Theory]
    [InlineData(DotNetOutdatedOutputFormat.Json, "json")]
    [InlineData(DotNetOutdatedOutputFormat.Csv, "csv")]
    [InlineData(DotNetOutdatedOutputFormat.Markdown, "markdown")]
    public void Should_Add_Output_Format(DotNetOutdatedOutputFormat format, string expected)
    {
        Assert.Equal($"outdated --output-format {expected}", Args(s => s.OutputFormat = format));
    }

    [Fact]
    public void Should_Emit_Shared_Options_Before_Report_Options()
    {
        var args = Args(
            s =>
            {
                s.OutputFormat = DotNetOutdatedOutputFormat.Json;
                s.OutputFile = "out/outdated.json";
                s.FailOnUpdates = true;
                s.IncludeUpToDate = true;
                s.IncludeAutoReferences = true;
            },
            "./src");

        Assert.Equal(
            "outdated \"./src\" --include-auto-references --include-up-to-date --fail-on-updates " +
            "--output \"/Working/out/outdated.json\" --output-format json",
            args);
    }

    [Fact]
    public void Should_Delete_A_Stale_Output_File_Before_Running()
    {
        var fixture = new ReporterFixture();
        fixture.Settings.OutputFile = "outdated.json";
        fixture.Settings.OutputFormat = DotNetOutdatedOutputFormat.Csv;
        fixture.FileSystem.CreateFile("/Working/outdated.json").SetContent("stale");

        fixture.Run();

        Assert.False(fixture.FileSystem.Exist(new FilePath("/Working/outdated.json")));
    }

    [Fact]
    public void Should_Create_The_Output_Directory()
    {
        var fixture = new ReporterFixture();
        fixture.Settings.OutputFile = "out/reports/outdated.json";

        fixture.Run();

        Assert.True(fixture.FileSystem.Exist(new DirectoryPath("/Working/out/reports")));
    }

    [Fact]
    public void Should_Write_An_Empty_Json_Report_When_The_Tool_Wrote_Nothing()
    {
        var fixture = new ReporterFixture();
        fixture.Settings.OutputFile = "outdated.json";

        fixture.Run();

        Assert.Equal(EmptyReport, fixture.FileSystem.ReadAllText("/Working/outdated.json"));
    }

    [Fact]
    public void Should_Write_An_Empty_Json_Report_When_Format_Is_Explicitly_Json()
    {
        var fixture = new ReporterFixture();
        fixture.Settings.OutputFile = "outdated.json";
        fixture.Settings.OutputFormat = DotNetOutdatedOutputFormat.Json;

        fixture.Run();

        Assert.Equal(EmptyReport, fixture.FileSystem.ReadAllText("/Working/outdated.json"));
    }

    [Fact]
    public void Should_Not_Overwrite_A_Report_Written_By_The_Tool()
    {
        var fixture = new ReporterFixture();
        fixture.Settings.OutputFile = "outdated.json";
        fixture.Settings.PostAction = _ => fixture.FileSystem.CreateFile("/Working/outdated.json").SetContent("{\"Projects\":[{\"Name\":\"App\"}]}");

        fixture.Run();

        Assert.Equal("{\"Projects\":[{\"Name\":\"App\"}]}", fixture.FileSystem.ReadAllText("/Working/outdated.json"));
    }

    [Theory]
    [InlineData(DotNetOutdatedOutputFormat.Csv)]
    [InlineData(DotNetOutdatedOutputFormat.Markdown)]
    public void Should_Not_Write_An_Empty_Report_For_Non_Json_Formats(DotNetOutdatedOutputFormat format)
    {
        var fixture = new ReporterFixture();
        fixture.Settings.OutputFile = "outdated.txt";
        fixture.Settings.OutputFormat = format;

        fixture.Run();

        Assert.False(fixture.FileSystem.Exist(new FilePath("/Working/outdated.txt")));
    }

    [Fact]
    public void Should_Not_Write_An_Empty_Report_When_A_Non_Zero_Exit_Code_Is_Handled()
    {
        var fixture = new ReporterFixture();
        fixture.Settings.OutputFile = "outdated.json";
        fixture.Settings.HandleExitCode = _ => true;
        fixture.GivenProcessExitsWithCode(1);

        fixture.Run();

        Assert.False(fixture.FileSystem.Exist(new FilePath("/Working/outdated.json")));
    }

    [Fact]
    public void Should_Invoke_The_Users_Post_Action()
    {
        var invoked = false;
        var fixture = new ReporterFixture();
        fixture.Settings.PostAction = _ => invoked = true;

        fixture.Run();

        Assert.True(invoked);
    }

    [Fact]
    public void Should_Throw_On_Exit_Code_2_When_Fail_On_Updates_Is_Set()
    {
        var fixture = new ReporterFixture();
        fixture.Settings.FailOnUpdates = true;
        fixture.GivenProcessExitsWithCode(2);

        var result = Record.Exception(() => fixture.Run());

        Assertions.IsCakeException(result, "dotnet-outdated: Process returned an error (exit code 2).");
    }

    [Fact]
    public void Should_Not_Throw_On_Exit_Code_2_When_It_Is_Handled()
    {
        var fixture = new ReporterFixture();
        fixture.Settings.FailOnUpdates = true;
        fixture.Settings.HandleExitCode = code => code is 0 or 2;
        fixture.GivenProcessExitsWithCode(2);

        Assert.Null(Record.Exception(() => fixture.Run()));
    }
}
