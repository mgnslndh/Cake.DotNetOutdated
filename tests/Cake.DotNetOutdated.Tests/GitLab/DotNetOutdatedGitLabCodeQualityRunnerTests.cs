using System.Text.Json;
using Cake.Core.IO;
using Cake.DotNetOutdated.Tests.Fixtures;
using Cake.Testing;

namespace Cake.DotNetOutdated.Tests.GitLab;

public sealed class DotNetOutdatedGitLabCodeQualityRunnerTests
{
    private const string ReportPath = "/Working/gl-code-quality-report.json";
    private const string TemporaryReportPath = "/Working/gl-code-quality-report.json.outdated.json";
    private const string ProjectPath = "/Working/src/App/App.csproj";

    private const string Project = """
        <Project Sdk="Microsoft.NET.Sdk">
          <ItemGroup>
            <PackageReference Include="Newtonsoft.Json" Version="12.0.1" />
          </ItemGroup>
        </Project>
        """;

    private const string OutdatedJson = """
        {
          "Projects": [
            {
              "Name": "App",
              "FilePath": "/Working/src/App/App.csproj",
              "TargetFrameworks": [
                {
                  "Name": "net8.0",
                  "Dependencies": [
                    { "Name": "Newtonsoft.Json", "ResolvedVersion": "12.0.1", "LatestVersion": "13.0.3", "UpgradeSeverity": "Major" }
                  ]
                }
              ]
            }
          ]
        }
        """;

    private static GitLabRunnerFixture CreateFixture(bool toolReportsUpdates = true)
    {
        var fixture = new GitLabRunnerFixture { Path = "./src" };
        fixture.FileSystem.CreateFile(ProjectPath).SetContent(Project);
        if (toolReportsUpdates)
        {
            // The fake process cannot write files: simulate dotnet-outdated writing its JSON report.
            fixture.Settings.PostAction = _ => fixture.FileSystem.CreateFile(TemporaryReportPath).SetContent(OutdatedJson);
        }

        return fixture;
    }

    [Fact]
    public void Should_Run_The_Tool_With_Json_Output_To_A_Temporary_File()
    {
        var fixture = CreateFixture();

        var result = fixture.Run();

        Assert.Equal(
            "outdated \"./src\" --output \"/Working/gl-code-quality-report.json.outdated.json\" --output-format json",
            result.Args);
    }

    [Fact]
    public void Should_Keep_The_Callers_Options()
    {
        var fixture = CreateFixture();
        fixture.Settings.Include.Add("Newtonsoft");
        fixture.Settings.VersionLock = DotNetOutdatedVersionLock.Major;

        var result = fixture.Run();

        Assert.Equal(
            "outdated \"./src\" --version-lock Major --include \"Newtonsoft\" --output \"/Working/gl-code-quality-report.json.outdated.json\" --output-format json",
            result.Args);
    }

    [Fact]
    public void Should_Write_A_GitLab_Report_With_The_Located_Line()
    {
        var fixture = CreateFixture();

        fixture.Run();

        using var document = JsonDocument.Parse(fixture.FileSystem.ReadAllText(ReportPath));
        var issue = Assert.Single(document.RootElement.EnumerateArray());
        Assert.Equal("outdated-package", issue.GetProperty("check_name").GetString());
        Assert.Equal("major", issue.GetProperty("severity").GetString());
        Assert.Equal("src/App/App.csproj", issue.GetProperty("location").GetProperty("path").GetString());
        Assert.Equal(3, issue.GetProperty("location").GetProperty("lines").GetProperty("begin").GetInt32());
    }

    [Fact]
    public void Should_Write_An_Empty_Report_When_Nothing_Is_Outdated()
    {
        var fixture = CreateFixture(toolReportsUpdates: false);

        fixture.Run();

        Assert.Equal("[]", fixture.FileSystem.ReadAllText(ReportPath));
    }

    [Fact]
    public void Should_Delete_The_Temporary_Report()
    {
        var fixture = CreateFixture();

        fixture.Run();

        Assert.False(fixture.FileSystem.Exist(new FilePath(TemporaryReportPath)));
    }

    [Fact]
    public void Should_Not_Modify_The_Callers_Settings()
    {
        var fixture = CreateFixture();
        var postAction = fixture.Settings.PostAction;

        fixture.Run();

        Assert.Null(fixture.Settings.OutputFile);
        Assert.Null(fixture.Settings.OutputFormat);
        Assert.Null(fixture.Settings.HandleExitCode);
        Assert.Same(postAction, fixture.Settings.PostAction);
    }

    [Fact]
    public void Should_Use_The_Configured_Repository_Root_And_Severities()
    {
        var fixture = CreateFixture();
        fixture.GitLabSettings = new Cake.DotNetOutdated.GitLab.GitLabCodeQualitySettings
        {
            RepositoryRoot = "src",
            MajorSeverity = Cake.DotNetOutdated.GitLab.GitLabCodeQualitySeverity.Blocker,
        };

        fixture.Run();

        using var document = JsonDocument.Parse(fixture.FileSystem.ReadAllText(ReportPath));
        var issue = document.RootElement[0];
        Assert.Equal("blocker", issue.GetProperty("severity").GetString());
        Assert.Equal("App/App.csproj", issue.GetProperty("location").GetProperty("path").GetString());
    }

    [Fact]
    public void Should_Write_The_Report_And_Then_Throw_On_Exit_Code_2_When_Fail_On_Updates_Is_Set()
    {
        var fixture = CreateFixture();
        fixture.Settings.FailOnUpdates = true;
        fixture.GivenProcessExitsWithCode(2);

        var result = Record.Exception(() => fixture.Run());

        Assertions.IsCakeException(result, "dotnet-outdated: Process returned an error (exit code 2).");
        Assert.Equal(2, ((Cake.Core.CakeException)result).ExitCode);
        Assert.Single(JsonDocument.Parse(fixture.FileSystem.ReadAllText(ReportPath)).RootElement.EnumerateArray());
        Assert.False(fixture.FileSystem.Exist(new FilePath(TemporaryReportPath)));
    }

    [Fact]
    public void Should_Not_Throw_On_Exit_Code_2_When_The_Caller_Handles_It()
    {
        var fixture = CreateFixture();
        fixture.Settings.FailOnUpdates = true;
        fixture.Settings.HandleExitCode = code => code is 0 or 2;
        fixture.GivenProcessExitsWithCode(2);

        Assert.Null(Record.Exception(() => fixture.Run()));
        Assert.True(fixture.FileSystem.Exist(new FilePath(ReportPath)));
    }

    [Fact]
    public void Should_Ask_The_Caller_Once_About_Exit_Code_2()
    {
        var calls = 0;
        var fixture = CreateFixture();
        fixture.Settings.FailOnUpdates = true;
        fixture.Settings.HandleExitCode = code =>
        {
            calls++;
            return code is 0 or 2;
        };
        fixture.GivenProcessExitsWithCode(2);

        Assert.Null(Record.Exception(() => fixture.Run()));
        Assert.Equal(1, calls);
    }

    [Fact]
    public void Should_Not_Fail_When_The_Temporary_Report_Cannot_Be_Deleted()
    {
        var fixture = CreateFixture();
        fixture.DecorateFileSystem = fileSystem => new FaultyFileSystem(fileSystem) { FailOnDelete = { TemporaryReportPath } };

        Assert.Null(Record.Exception(() => fixture.Run()));
        Assert.Single(JsonDocument.Parse(fixture.FileSystem.ReadAllText(ReportPath)).RootElement.EnumerateArray());
        Assert.Contains(
            fixture.Log.Entries,
            entry => entry.Verbosity == Cake.Core.Diagnostics.Verbosity.Diagnostic
                && entry.Message.Contains("Could not delete the temporary report", StringComparison.Ordinal));
    }

    [Fact]
    public void Should_Still_Throw_The_Exit_Code_Error_When_The_Temporary_Report_Cannot_Be_Deleted()
    {
        var fixture = CreateFixture();
        fixture.Settings.FailOnUpdates = true;
        fixture.DecorateFileSystem = fileSystem => new FaultyFileSystem(fileSystem) { FailOnDelete = { TemporaryReportPath } };
        fixture.GivenProcessExitsWithCode(2);

        var result = Record.Exception(() => fixture.Run());

        Assertions.IsCakeException(result, "dotnet-outdated: Process returned an error (exit code 2).");
        Assert.Equal(2, ((Cake.Core.CakeException)result).ExitCode);
        Assert.True(fixture.FileSystem.Exist(new FilePath(ReportPath)));
    }

    [Fact]
    public void Should_Not_Mask_The_Primary_Exception_When_The_Temporary_Report_Cannot_Be_Deleted()
    {
        var fixture = CreateFixture();
        fixture.DecorateFileSystem = fileSystem => new FaultyFileSystem(fileSystem)
        {
            FailOnDelete = { TemporaryReportPath },
            FailOnRead = { TemporaryReportPath },
        };

        var result = Record.Exception(() => fixture.Run());

        Assert.IsType<IOException>(result);
        Assert.Contains("Could not read", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Should_Not_Accept_Exit_Code_2_When_Fail_On_Updates_Is_Not_Set()
    {
        var fixture = CreateFixture();
        fixture.GivenProcessExitsWithCode(2);

        var result = Record.Exception(() => fixture.Run());

        Assertions.IsCakeException(result, "dotnet-outdated: Process returned an error (exit code 2).");
        Assert.False(fixture.FileSystem.Exist(new FilePath(ReportPath)));
    }

    [Fact]
    public void Should_Throw_And_Write_No_Report_When_The_Tool_Fails()
    {
        var fixture = CreateFixture(toolReportsUpdates: false);
        fixture.GivenProcessExitsWithCode(1);

        var result = Record.Exception(() => fixture.Run());

        Assertions.IsCakeException(result, "dotnet-outdated: Process returned an error (exit code 1).");
        Assert.False(fixture.FileSystem.Exist(new FilePath(ReportPath)));
    }

    [Fact]
    public void Should_Explain_When_A_Handled_Failure_Produced_No_Report()
    {
        var fixture = CreateFixture(toolReportsUpdates: false);
        fixture.Settings.HandleExitCode = _ => true;
        fixture.GivenProcessExitsWithCode(1);

        var result = Record.Exception(() => fixture.Run());

        Assertions.IsCakeException(
            result,
            "dotnet-outdated did not produce a report (exit code 1); no GitLab Code Quality report was written.");
        Assert.False(fixture.FileSystem.Exist(new FilePath(ReportPath)));
    }

    [Fact]
    public void Should_Invoke_The_Callers_Post_Action()
    {
        var invoked = false;
        var fixture = CreateFixture(toolReportsUpdates: false);
        fixture.Settings.PostAction = _ => invoked = true;

        fixture.Run();

        Assert.True(invoked);
    }

    [Fact]
    public void Should_Use_Default_Report_Settings_When_None_Are_Given()
    {
        var fixture = CreateFixture(toolReportsUpdates: false);
        fixture.Settings = null;

        var result = fixture.Run();

        Assert.Equal(
            "outdated \"./src\" --output \"/Working/gl-code-quality-report.json.outdated.json\" --output-format json",
            result.Args);
        Assert.Equal("[]", fixture.FileSystem.ReadAllText(ReportPath));
    }

    [Fact]
    public void Should_Throw_If_The_Output_File_Is_Null()
    {
        var fixture = CreateFixture();
        fixture.OutputFile = null;

        Assertions.IsArgumentNullException(Record.Exception(() => fixture.Run()), "outputFile");
    }
}
