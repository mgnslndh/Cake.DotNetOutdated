using Cake.Core.Diagnostics;
using Cake.DotNetOutdated.GitLab;
using Cake.DotNetOutdated.Report;
using Cake.Testing;

namespace Cake.DotNetOutdated.Tests.GitLab;

public sealed class DotNetOutdatedGitLabConverterTests
{
    private const string AppProjectPath = "/Working/src/App/App.csproj";

    private const string AppProject = """
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <TargetFramework>net8.0</TargetFramework>
          </PropertyGroup>
          <ItemGroup>
            <PackageReference Include="Newtonsoft.Json" Version="12.0.1" />
            <PackageReference Include="Serilog" Version="3.0.0" />
          </ItemGroup>
        </Project>
        """;

    private const string CentralPackages = """
        <Project>
          <PropertyGroup>
            <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
          </PropertyGroup>
          <ItemGroup>
            <PackageVersion Include="Newtonsoft.Json" Version="12.0.1" />
          </ItemGroup>
        </Project>
        """;

    private sealed class Context
    {
        public Context()
        {
            Environment = FakeEnvironment.CreateUnixEnvironment();
            FileSystem = new FakeFileSystem(Environment);
            Log = new FakeLog();
        }

        public FakeEnvironment Environment { get; }

        public FakeFileSystem FileSystem { get; }

        public FakeLog Log { get; }

        public DotNetOutdatedGitLabConverter Converter => new DotNetOutdatedGitLabConverter(FileSystem, Environment, Log);

        public void Add(string path, string content) => FileSystem.CreateFile(path).SetContent(content);
    }

    private static DotNetOutdatedDependency Dep(string name, string resolved, string latest, DotNetOutdatedUpgradeSeverity severity)
    {
        return new DotNetOutdatedDependency { Name = name, ResolvedVersion = resolved, LatestVersion = latest, UpgradeSeverity = severity };
    }

    private static DotNetOutdatedProject Project(string filePath, params DotNetOutdatedDependency[] dependencies)
    {
        return new DotNetOutdatedProject
        {
            Name = Path.GetFileNameWithoutExtension(filePath),
            FilePath = filePath,
            TargetFrameworks = new[] { new DotNetOutdatedTargetFramework { Name = "net8.0", Dependencies = dependencies } },
        };
    }

    private static DotNetOutdatedReport Report(params DotNetOutdatedProject[] projects) => new DotNetOutdatedReport { Projects = projects };

    private static readonly DotNetOutdatedDependency Newtonsoft =
        Dep("Newtonsoft.Json", "12.0.1", "13.0.3", DotNetOutdatedUpgradeSeverity.Major);

    [Fact]
    public void Should_Return_No_Issues_For_An_Empty_Report()
    {
        var context = new Context();

        Assert.Empty(context.Converter.Convert(new DotNetOutdatedReport(), null));
    }

    [Fact]
    public void Should_Convert_A_Dependency_To_An_Issue_On_Its_Declaration_Line()
    {
        var context = new Context();
        context.Add(AppProjectPath, AppProject);

        var issues = context.Converter.Convert(Report(Project(AppProjectPath, Newtonsoft)), new GitLabCodeQualitySettings());

        var issue = Assert.Single(issues);
        Assert.Equal("src/App/App.csproj", issue.Path);
        Assert.Equal(6, issue.Line);
        Assert.Equal("outdated-package", issue.CheckName);
        Assert.Equal(GitLabCodeQualitySeverity.Major, issue.Severity);
        Assert.Equal("Newtonsoft.Json 12.0.1 can be updated to 13.0.3 (major update).", issue.Description);
        Assert.Matches("^[0-9a-f]{64}$", issue.Fingerprint);
    }

    [Fact]
    public void Should_Use_Default_Settings_When_None_Are_Given()
    {
        var context = new Context();
        context.Add(AppProjectPath, AppProject);

        var issues = context.Converter.Convert(Report(Project(AppProjectPath, Newtonsoft)), null);

        Assert.Single(issues);
    }

    [Theory]
    [InlineData(DotNetOutdatedUpgradeSeverity.Major, GitLabCodeQualitySeverity.Major)]
    [InlineData(DotNetOutdatedUpgradeSeverity.Minor, GitLabCodeQualitySeverity.Minor)]
    [InlineData(DotNetOutdatedUpgradeSeverity.Patch, GitLabCodeQualitySeverity.Info)]
    [InlineData(DotNetOutdatedUpgradeSeverity.Unknown, GitLabCodeQualitySeverity.Info)]
    public void Should_Map_Severities_By_Default(DotNetOutdatedUpgradeSeverity upgrade, GitLabCodeQualitySeverity expected)
    {
        var context = new Context();
        context.Add(AppProjectPath, AppProject);

        var issues = context.Converter.Convert(Report(Project(AppProjectPath, Dep("Serilog", "3.0.0", "4.0.0", upgrade))), null);

        Assert.Equal(expected, Assert.Single(issues).Severity);
    }

    [Fact]
    public void Should_Use_Overridden_Severities()
    {
        var context = new Context();
        context.Add(AppProjectPath, AppProject);
        var settings = new GitLabCodeQualitySettings
        {
            MajorSeverity = GitLabCodeQualitySeverity.Critical,
            MinorSeverity = GitLabCodeQualitySeverity.Blocker,
            PatchSeverity = GitLabCodeQualitySeverity.Minor,
            UnknownSeverity = GitLabCodeQualitySeverity.Major,
        };
        var report = Report(Project(
            AppProjectPath,
            Dep("Alpha", "1.0.0", "2.0.0", DotNetOutdatedUpgradeSeverity.Major),
            Dep("Bravo", "1.0.0", "1.1.0", DotNetOutdatedUpgradeSeverity.Minor),
            Dep("Charlie", "1.0.0", "1.0.1", DotNetOutdatedUpgradeSeverity.Patch),
            Dep("Delta", "1.0.0", null, DotNetOutdatedUpgradeSeverity.Unknown)));

        var issues = context.Converter.Convert(report, settings);

        GitLabCodeQualitySeverity SeverityOf(string package) => issues.Single(i => i.Description.Contains(package)).Severity;
        Assert.Equal(GitLabCodeQualitySeverity.Critical, SeverityOf("Alpha"));
        Assert.Equal(GitLabCodeQualitySeverity.Blocker, SeverityOf("Bravo"));
        Assert.Equal(GitLabCodeQualitySeverity.Minor, SeverityOf("Charlie"));
        Assert.Equal(GitLabCodeQualitySeverity.Major, SeverityOf("Delta"));
    }

    [Fact]
    public void Should_Never_Report_Up_To_Date_Dependencies()
    {
        var context = new Context();
        context.Add(AppProjectPath, AppProject);
        var report = Report(Project(AppProjectPath, Dep("Serilog", "3.0.0", "3.0.0", DotNetOutdatedUpgradeSeverity.None)));

        Assert.Empty(context.Converter.Convert(report, null));
    }

    [Theory]
    [InlineData(DotNetOutdatedUpgradeSeverity.Patch, 4)]
    [InlineData(DotNetOutdatedUpgradeSeverity.Minor, 3)]
    [InlineData(DotNetOutdatedUpgradeSeverity.Major, 2)]
    public void Should_Skip_Dependencies_Below_The_Minimum_Severity_But_Keep_Unknown_Ones(DotNetOutdatedUpgradeSeverity minimum, int expectedCount)
    {
        var context = new Context();
        context.Add(AppProjectPath, AppProject);
        var report = Report(Project(
            AppProjectPath,
            Dep("Patch", "1.0.0", "1.0.1", DotNetOutdatedUpgradeSeverity.Patch),
            Dep("Minor", "1.0.0", "1.1.0", DotNetOutdatedUpgradeSeverity.Minor),
            Dep("Major", "1.0.0", "2.0.0", DotNetOutdatedUpgradeSeverity.Major),
            Dep("Unknown", "1.0.0", null, DotNetOutdatedUpgradeSeverity.Unknown)));

        var issues = context.Converter.Convert(report, new GitLabCodeQualitySettings { MinimumUpgradeSeverity = minimum });

        Assert.Equal(expectedCount, issues.Count);
        Assert.Contains(issues, i => i.Description.StartsWith("Could not determine the latest version of Unknown", StringComparison.Ordinal));
    }

    [Fact]
    public void Should_Describe_A_Dependency_Without_A_Known_Latest_Version()
    {
        var context = new Context();
        context.Add(AppProjectPath, AppProject);
        var report = Report(Project(AppProjectPath, Dep("Serilog", "3.0.0", null, DotNetOutdatedUpgradeSeverity.Unknown)));

        var issue = Assert.Single(context.Converter.Convert(report, null));

        Assert.Equal("Could not determine the latest version of Serilog (resolved version 3.0.0).", issue.Description);
    }

    [Fact]
    public void Should_Group_Target_Frameworks_And_Keep_The_Highest_Severity()
    {
        var context = new Context();
        context.Add(AppProjectPath, AppProject);
        var project = new DotNetOutdatedProject
        {
            Name = "App",
            FilePath = AppProjectPath,
            TargetFrameworks = new[]
            {
                new DotNetOutdatedTargetFramework
                {
                    Name = "net8.0",
                    Dependencies = new[] { Dep("Newtonsoft.Json", "12.0.1", "12.0.3", DotNetOutdatedUpgradeSeverity.Patch) },
                },
                new DotNetOutdatedTargetFramework
                {
                    Name = "net9.0",
                    Dependencies = new[] { Dep("Newtonsoft.Json", "12.0.1", "13.0.3", DotNetOutdatedUpgradeSeverity.Major) },
                },
            },
        };

        var issue = Assert.Single(context.Converter.Convert(Report(project), null));

        Assert.Equal(GitLabCodeQualitySeverity.Major, issue.Severity);
        Assert.Equal("Newtonsoft.Json 12.0.1 can be updated to 13.0.3 (major update).", issue.Description);
    }

    [Fact]
    public void Should_Prefer_A_Known_Severity_Over_Unknown_When_Grouping()
    {
        var context = new Context();
        context.Add(AppProjectPath, AppProject);
        var project = new DotNetOutdatedProject
        {
            Name = "App",
            FilePath = AppProjectPath,
            TargetFrameworks = new[]
            {
                new DotNetOutdatedTargetFramework { Name = "net8.0", Dependencies = new[] { Dep("Serilog", "3.0.0", null, DotNetOutdatedUpgradeSeverity.Unknown) } },
                new DotNetOutdatedTargetFramework { Name = "net9.0", Dependencies = new[] { Dep("Serilog", "3.0.0", "3.0.1", DotNetOutdatedUpgradeSeverity.Patch) } },
            },
        };

        var issue = Assert.Single(context.Converter.Convert(Report(project), null));

        Assert.Equal("Serilog 3.0.0 can be updated to 3.0.1 (patch update).", issue.Description);
    }

    [Fact]
    public void Should_Collapse_Projects_That_Share_A_Central_Package_Declaration()
    {
        var context = new Context();
        context.Add("/Working/Directory.Packages.props", CentralPackages);
        context.Add("/Working/src/App/App.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\" />");
        context.Add("/Working/src/Lib/Lib.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\" />");
        var report = Report(
            Project("/Working/src/App/App.csproj", Dep("Newtonsoft.Json", "12.0.1", "12.0.3", DotNetOutdatedUpgradeSeverity.Patch)),
            Project("/Working/src/Lib/Lib.csproj", Newtonsoft));

        var issue = Assert.Single(context.Converter.Convert(report, null));

        Assert.Equal("Directory.Packages.props", issue.Path);
        Assert.Equal(6, issue.Line);
        Assert.Equal(GitLabCodeQualitySeverity.Major, issue.Severity);
    }

    [Fact]
    public void Should_Keep_Findings_Separate_When_Declared_In_Different_Files()
    {
        var context = new Context();
        context.Add("/Working/src/App/App.csproj", AppProject);
        context.Add("/Working/src/Lib/Lib.csproj", AppProject);
        var report = Report(
            Project("/Working/src/App/App.csproj", Newtonsoft),
            Project("/Working/src/Lib/Lib.csproj", Newtonsoft));

        var issues = context.Converter.Convert(report, null);

        Assert.Equal(new[] { "src/App/App.csproj", "src/Lib/Lib.csproj" }, issues.Select(i => i.Path));
        Assert.Equal(2, issues.Select(i => i.Fingerprint).Distinct().Count());
    }

    [Fact]
    public void Should_Compute_A_Fingerprint_That_Ignores_Versions_And_Line_Numbers()
    {
        var first = new Context();
        first.Add(AppProjectPath, AppProject);
        var second = new Context();
        second.Add(AppProjectPath, "<!-- a new leading comment moves everything down -->\n" + AppProject);

        var before = Assert.Single(first.Converter.Convert(Report(Project(AppProjectPath, Newtonsoft)), null));
        var after = Assert.Single(second.Converter.Convert(
            Report(Project(AppProjectPath, Dep("Newtonsoft.Json", "12.0.1", "13.0.4", DotNetOutdatedUpgradeSeverity.Major))),
            null));

        Assert.NotEqual(before.Line, after.Line);
        Assert.NotEqual(before.Description, after.Description);
        Assert.Equal(before.Fingerprint, after.Fingerprint);
    }

    [Fact]
    public void Should_Compute_A_Fingerprint_That_Differs_Per_Package()
    {
        var context = new Context();
        context.Add(AppProjectPath, AppProject);
        var report = Report(Project(AppProjectPath, Newtonsoft, Dep("Serilog", "3.0.0", "4.0.0", DotNetOutdatedUpgradeSeverity.Major)));

        var issues = context.Converter.Convert(report, null);

        Assert.Equal(2, issues.Select(i => i.Fingerprint).Distinct().Count());
    }

    [Fact]
    public void Should_Compute_The_Same_Fingerprint_Regardless_Of_Package_Name_Casing()
    {
        var context = new Context();
        context.Add(AppProjectPath, AppProject);

        var upper = Assert.Single(context.Converter.Convert(Report(Project(AppProjectPath, Dep("Newtonsoft.Json", "12.0.1", "13.0.3", DotNetOutdatedUpgradeSeverity.Major))), null));
        var lower = Assert.Single(context.Converter.Convert(Report(Project(AppProjectPath, Dep("newtonsoft.json", "12.0.1", "13.0.3", DotNetOutdatedUpgradeSeverity.Major))), null));

        Assert.Equal(upper.Fingerprint, lower.Fingerprint);
    }

    [Fact]
    public void Should_Make_Paths_Relative_To_The_Configured_Repository_Root()
    {
        var context = new Context();
        context.Add(AppProjectPath, AppProject);

        var issues = context.Converter.Convert(
            Report(Project(AppProjectPath, Newtonsoft)),
            new GitLabCodeQualitySettings { RepositoryRoot = "src" });

        Assert.Equal("App/App.csproj", Assert.Single(issues).Path);
    }

    [Fact]
    public void Should_Fall_Back_To_The_Project_File_When_The_Declaration_Is_Outside_The_Repository()
    {
        var context = new Context();
        context.Add("/Working/Directory.Packages.props", CentralPackages);
        context.Add(AppProjectPath, "<Project Sdk=\"Microsoft.NET.Sdk\" />");

        var issues = context.Converter.Convert(
            Report(Project(AppProjectPath, Newtonsoft)),
            new GitLabCodeQualitySettings { RepositoryRoot = "/Working/src/App" });

        var issue = Assert.Single(issues);
        Assert.Equal("App.csproj", issue.Path);
        Assert.Equal(1, issue.Line);
    }

    [Fact]
    public void Should_Skip_And_Warn_When_The_Project_Is_Outside_The_Repository()
    {
        var context = new Context();
        context.Add(AppProjectPath, AppProject);

        var issues = context.Converter.Convert(
            Report(Project(AppProjectPath, Newtonsoft)),
            new GitLabCodeQualitySettings { RepositoryRoot = "/Elsewhere" });

        Assert.Empty(issues);
        Assert.Contains(context.Log.Entries, m => m.Level == LogLevel.Warning && m.Message.Contains("outside the repository root"));
    }

    [Fact]
    public void Should_Skip_Projects_Without_A_File_Path()
    {
        var context = new Context();
        var project = new DotNetOutdatedProject
        {
            Name = "Broken",
            FilePath = null,
            TargetFrameworks = new[] { new DotNetOutdatedTargetFramework { Name = "net8.0", Dependencies = new[] { Newtonsoft } } },
        };

        Assert.Empty(context.Converter.Convert(Report(project), null));
    }

    [Fact]
    public void Should_Order_Issues_By_Path_Then_Line()
    {
        var context = new Context();
        context.Add("/Working/src/B/B.csproj", AppProject);
        context.Add("/Working/src/A/A.csproj", AppProject);
        var report = Report(
            Project("/Working/src/B/B.csproj", Newtonsoft),
            Project("/Working/src/A/A.csproj", Dep("Serilog", "3.0.0", "4.0.0", DotNetOutdatedUpgradeSeverity.Major), Newtonsoft));

        var issues = context.Converter.Convert(report, null);

        Assert.Equal(
            new[] { ("src/A/A.csproj", 6), ("src/A/A.csproj", 7), ("src/B/B.csproj", 6) },
            issues.Select(i => (i.Path, i.Line)));
    }

    [Fact]
    public void Should_Throw_If_The_Report_Is_Null()
    {
        var context = new Context();

        Assertions.IsArgumentNullException(Record.Exception(() => context.Converter.Convert(null, null)), "report");
    }
}
