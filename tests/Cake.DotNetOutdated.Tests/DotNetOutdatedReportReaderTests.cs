using System.Text;
using Cake.Core;
using Cake.DotNetOutdated.Report;
using Cake.Testing;

namespace Cake.DotNetOutdated.Tests;

public sealed class DotNetOutdatedReportReaderTests
{
    private const string Sample = """
        {
          "Projects": [
            {
              "Name": "App",
              "FilePath": "/repo/src/App/App.csproj",
              "TargetFrameworks": [
                {
                  "Name": "net8.0",
                  "Dependencies": [
                    { "Name": "Newtonsoft.Json", "ResolvedVersion": "12.0.1", "LatestVersion": "13.0.3", "UpgradeSeverity": "Major" },
                    { "Name": "Serilog", "ResolvedVersion": "3.0.0", "LatestVersion": "3.0.1", "UpgradeSeverity": "Patch" }
                  ]
                }
              ]
            }
          ]
        }
        """;

    [Fact]
    public void Should_Parse_The_Tool_Output()
    {
        var report = DotNetOutdatedReportReader.Parse(Sample);

        var project = Assert.Single(report.Projects);
        Assert.Equal("App", project.Name);
        Assert.Equal("/repo/src/App/App.csproj", project.FilePath);

        var framework = Assert.Single(project.TargetFrameworks);
        Assert.Equal("net8.0", framework.Name);
        Assert.Equal(2, framework.Dependencies.Count);

        var first = framework.Dependencies[0];
        Assert.Equal("Newtonsoft.Json", first.Name);
        Assert.Equal("12.0.1", first.ResolvedVersion);
        Assert.Equal("13.0.3", first.LatestVersion);
        Assert.Equal(DotNetOutdatedUpgradeSeverity.Major, first.UpgradeSeverity);
        Assert.Equal(DotNetOutdatedUpgradeSeverity.Patch, framework.Dependencies[1].UpgradeSeverity);
    }

    [Fact]
    public void Should_Match_Property_Names_Case_Insensitively()
    {
        var report = DotNetOutdatedReportReader.Parse(
            "{\"projects\":[{\"name\":\"App\",\"filePath\":\"/a.csproj\",\"targetFrameworks\":[{\"name\":\"net8.0\",\"dependencies\":[{\"name\":\"X\",\"resolvedVersion\":\"1.0.0\",\"latestVersion\":\"2.0.0\",\"upgradeSeverity\":\"minor\"}]}]}]}");

        var dependency = report.Projects[0].TargetFrameworks[0].Dependencies[0];
        Assert.Equal("X", dependency.Name);
        Assert.Equal(DotNetOutdatedUpgradeSeverity.Minor, dependency.UpgradeSeverity);
    }

    [Fact]
    public void Should_Ignore_Unknown_Fields()
    {
        var report = DotNetOutdatedReportReader.Parse("{\"Projects\":[],\"Extra\":{\"a\":[1,2,3]}}");

        Assert.Empty(report.Projects);
    }

    [Theory]
    [InlineData("\"Bogus\"")]
    [InlineData("42")]
    [InlineData("null")]
    [InlineData("{\"a\":1}")]
    public void Should_Map_Unrecognized_Severities_To_Unknown(string severity)
    {
        var report = DotNetOutdatedReportReader.Parse(
            "{\"Projects\":[{\"Name\":\"App\",\"FilePath\":\"/a.csproj\",\"TargetFrameworks\":[{\"Name\":\"net8.0\",\"Dependencies\":[{\"Name\":\"X\",\"UpgradeSeverity\":" + severity + "}]}]}]}");

        Assert.Equal(DotNetOutdatedUpgradeSeverity.Unknown, report.Projects[0].TargetFrameworks[0].Dependencies[0].UpgradeSeverity);
    }

    [Fact]
    public void Should_Return_An_Empty_Report_For_An_Empty_Object()
    {
        Assert.Empty(DotNetOutdatedReportReader.Parse("{}").Projects);
    }

    [Fact]
    public void Should_Treat_A_Null_Projects_Collection_As_Empty()
    {
        var report = DotNetOutdatedReportReader.Parse("{\"Projects\":null}");

        Assert.NotNull(report.Projects);
        Assert.Empty(report.Projects);
    }

    [Fact]
    public void Should_Treat_Null_TargetFrameworks_As_Empty()
    {
        var report = DotNetOutdatedReportReader.Parse("{\"Projects\":[{\"Name\":\"App\",\"TargetFrameworks\":null}]}");

        var project = Assert.Single(report.Projects);
        Assert.NotNull(project.TargetFrameworks);
        Assert.Empty(project.TargetFrameworks);
    }

    [Fact]
    public void Should_Treat_Null_Dependencies_As_Empty()
    {
        var report = DotNetOutdatedReportReader.Parse("{\"Projects\":[{\"Name\":\"App\",\"TargetFrameworks\":[{\"Name\":\"net8.0\",\"Dependencies\":null}]}]}");

        var framework = Assert.Single(Assert.Single(report.Projects).TargetFrameworks);
        Assert.NotNull(framework.Dependencies);
        Assert.Empty(framework.Dependencies);
    }

    [Fact]
    public void Should_Parse_The_Empty_Report_Written_By_The_Report_Runner()
    {
        Assert.Empty(DotNetOutdatedReportReader.Parse("{\n  \"Projects\": []\n}").Projects);
    }

    [Fact]
    public void Should_Throw_A_Cake_Exception_For_Invalid_Json()
    {
        var result = Record.Exception(() => DotNetOutdatedReportReader.Parse("not json"));

        Assert.IsType<CakeException>(result);
        Assert.StartsWith("The dotnet-outdated report is not valid JSON:", result.Message);
    }

    [Fact]
    public void Should_Read_A_File_Relative_To_The_Working_Directory()
    {
        var environment = FakeEnvironment.CreateUnixEnvironment();
        var fileSystem = new FakeFileSystem(environment);
        fileSystem.CreateFile("/Working/outdated.json").SetContent(Sample);

        var report = new DotNetOutdatedReportReader(fileSystem, environment).Read("outdated.json");

        Assert.Single(report.Projects);
    }

    [Fact]
    public void Should_Read_A_File_With_A_Byte_Order_Mark()
    {
        var environment = FakeEnvironment.CreateUnixEnvironment();
        var fileSystem = new FakeFileSystem(environment);
        var content = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(Sample)).ToArray();
        fileSystem.WriteAllBytes("/Working/outdated.json", content);

        var report = new DotNetOutdatedReportReader(fileSystem, environment).Read("outdated.json");

        Assert.Single(report.Projects);
    }

    [Fact]
    public void Should_Throw_If_The_File_Does_Not_Exist()
    {
        var environment = FakeEnvironment.CreateUnixEnvironment();
        var fileSystem = new FakeFileSystem(environment);

        var result = Record.Exception(() => new DotNetOutdatedReportReader(fileSystem, environment).Read("missing.json"));

        var exception = Assert.IsType<FileNotFoundException>(result);
        Assert.Contains("/Working/missing.json", exception.Message);
    }

    [Fact]
    public void Should_Throw_If_The_Path_Is_Null()
    {
        var environment = FakeEnvironment.CreateUnixEnvironment();
        var reader = new DotNetOutdatedReportReader(new FakeFileSystem(environment), environment);

        var result = Record.Exception(() => reader.Read(null));

        Assertions.IsArgumentNullException(result, "path");
    }
}
