using System.Text.Json;
using Cake.Core.IO;
using Cake.DotNetOutdated.GitLab;
using Cake.Testing;

namespace Cake.DotNetOutdated.Tests.GitLab;

public sealed class GitLabCodeQualityReportWriterTests
{
    private static GitLabCodeQualityIssue Issue(
        string description = "Newtonsoft.Json 12.0.1 can be updated to 13.0.3 (major update).",
        GitLabCodeQualitySeverity severity = GitLabCodeQualitySeverity.Major,
        string path = "src/App/App.csproj",
        int line = 12)
    {
        return new GitLabCodeQualityIssue
        {
            Description = description,
            CheckName = "outdated-package",
            Fingerprint = "abc123",
            Severity = severity,
            Path = path,
            Line = line,
        };
    }

    private static byte[] ReadAllBytes(IFileSystem fileSystem, string path)
    {
        using var stream = fileSystem.GetFile(path).OpenRead();
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }

    [Fact]
    public void Should_Serialize_No_Issues_As_An_Empty_Array()
    {
        Assert.Equal("[]", GitLabCodeQualityReportWriter.Serialize(Array.Empty<GitLabCodeQualityIssue>()));
    }

    [Fact]
    public void Should_Produce_The_Documented_Shape()
    {
        var json = GitLabCodeQualityReportWriter.Serialize(new[] { Issue() });

        using var document = JsonDocument.Parse(json);
        Assert.Equal(JsonValueKind.Array, document.RootElement.ValueKind);

        var item = Assert.Single(document.RootElement.EnumerateArray());
        Assert.Equal(
            new[] { "check_name", "description", "fingerprint", "location", "severity" },
            item.EnumerateObject().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal));
        Assert.Equal("outdated-package", item.GetProperty("check_name").GetString());
        Assert.Equal("Newtonsoft.Json 12.0.1 can be updated to 13.0.3 (major update).", item.GetProperty("description").GetString());
        Assert.Equal("abc123", item.GetProperty("fingerprint").GetString());
        Assert.Equal("major", item.GetProperty("severity").GetString());

        var location = item.GetProperty("location");
        Assert.Equal(new[] { "lines", "path" }, location.EnumerateObject().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal));
        Assert.Equal("src/App/App.csproj", location.GetProperty("path").GetString());
        Assert.Equal(12, location.GetProperty("lines").GetProperty("begin").GetInt32());
    }

    [Theory]
    [InlineData(GitLabCodeQualitySeverity.Info, "info")]
    [InlineData(GitLabCodeQualitySeverity.Minor, "minor")]
    [InlineData(GitLabCodeQualitySeverity.Major, "major")]
    [InlineData(GitLabCodeQualitySeverity.Critical, "critical")]
    [InlineData(GitLabCodeQualitySeverity.Blocker, "blocker")]
    public void Should_Write_Severity_In_Lower_Case(GitLabCodeQualitySeverity severity, string expected)
    {
        var json = GitLabCodeQualityReportWriter.Serialize(new[] { Issue(severity: severity) });

        using var document = JsonDocument.Parse(json);
        Assert.Equal(expected, document.RootElement[0].GetProperty("severity").GetString());
    }

    [Fact]
    public void Should_Not_Escape_Quotes_And_Angle_Brackets()
    {
        var json = GitLabCodeQualityReportWriter.Serialize(new[] { Issue(description: "Uses 'x' <y> & z") });

        Assert.Contains("Uses 'x' <y> & z", json);
    }

    [Fact]
    public void Should_Write_Utf8_Without_A_Byte_Order_Mark()
    {
        var environment = FakeEnvironment.CreateUnixEnvironment();
        var fileSystem = new FakeFileSystem(environment);

        new GitLabCodeQualityReportWriter(fileSystem, environment).Write(new[] { Issue() }, "report.json");

        var bytes = ReadAllBytes(fileSystem, "/Working/report.json");
        Assert.Equal((byte)'[', bytes[0]);
    }

    [Fact]
    public void Should_Write_An_Empty_Array_When_There_Are_No_Issues()
    {
        var environment = FakeEnvironment.CreateUnixEnvironment();
        var fileSystem = new FakeFileSystem(environment);

        new GitLabCodeQualityReportWriter(fileSystem, environment).Write(Array.Empty<GitLabCodeQualityIssue>(), "report.json");

        Assert.Equal("[]", fileSystem.ReadAllText("/Working/report.json"));
    }

    [Fact]
    public void Should_Create_The_Output_Directory()
    {
        var environment = FakeEnvironment.CreateUnixEnvironment();
        var fileSystem = new FakeFileSystem(environment);

        new GitLabCodeQualityReportWriter(fileSystem, environment).Write(new[] { Issue() }, "out/gitlab/report.json");

        Assert.True(fileSystem.Exist(new FilePath("/Working/out/gitlab/report.json")));
    }

    [Fact]
    public void Should_Overwrite_An_Existing_File()
    {
        var environment = FakeEnvironment.CreateUnixEnvironment();
        var fileSystem = new FakeFileSystem(environment);
        fileSystem.CreateFile("/Working/report.json").SetContent(new string('x', 500));

        new GitLabCodeQualityReportWriter(fileSystem, environment).Write(Array.Empty<GitLabCodeQualityIssue>(), "report.json");

        Assert.Equal("[]", fileSystem.ReadAllText("/Working/report.json"));
    }

    [Fact]
    public void Should_Throw_If_Issues_Are_Null()
    {
        var environment = FakeEnvironment.CreateUnixEnvironment();
        var writer = new GitLabCodeQualityReportWriter(new FakeFileSystem(environment), environment);

        Assertions.IsArgumentNullException(Record.Exception(() => writer.Write(null, "report.json")), "issues");
    }

    [Fact]
    public void Should_Throw_If_The_Output_File_Is_Null()
    {
        var environment = FakeEnvironment.CreateUnixEnvironment();
        var writer = new GitLabCodeQualityReportWriter(new FakeFileSystem(environment), environment);

        Assertions.IsArgumentNullException(Record.Exception(() => writer.Write(Array.Empty<GitLabCodeQualityIssue>(), null)), "outputFile");
    }
}
