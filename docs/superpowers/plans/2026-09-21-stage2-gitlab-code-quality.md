# Cake.DotNetOutdated Stage 2: GitLab Code Quality Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Convert a dotnet-outdated JSON report into a GitLab Code Quality report, so outdated dependencies appear as merge request findings, with composable aliases and a one-shot alias.

**Architecture:** A tool-agnostic core (`GitLabCodeQualityIssue`, `GitLabCodeQualityReportWriter`) plus an outdated-specific converter that turns a `DotNetOutdatedReport` into issues. A line locator finds the real declaration line (`Directory.Packages.props`, project file, `Directory.Build.*`, else line 1). A one-shot runner composes report run, conversion and write. Everything lives in `Cake.DotNetOutdated.GitLab` and depends only on `Cake.DotNetOutdated` and `Cake.DotNetOutdated.Report`, never the reverse.

**Tech Stack:** C# / .NET (`net8.0;net9.0;net10.0`), `Cake.Core` 6.0.0, `System.Text.Json`, `System.Xml.Linq`, xUnit v3 + `Cake.Testing`.

**Spec:** `docs/superpowers/specs/2026-09-21-cake-dotnetoutdated-design.md` (Section 3). Prerequisite: Stage 1 (`docs/superpowers/plans/2026-09-21-stage1-tool-addin.md`) is complete and merged.

## Global Constraints

- One package `Cake.DotNetOutdated`, one assembly; all Stage 2 code in namespace `Cake.DotNetOutdated.GitLab` (folder `src/Cake.DotNetOutdated/GitLab/`). `Cake.DotNetOutdated` and `Cake.DotNetOutdated.Report` must never reference `Cake.DotNetOutdated.GitLab` (the architecture test from Stage 1 enforces this).
- No new package dependencies: `System.Text.Json` and `System.Xml.Linq` are part of the BCL for `net8.0`+. `Cake.Core` stays `PrivateAssets="All"`.
- GitLab report format: JSON array of `{ description, check_name, fingerprint, severity, location: { path, lines: { begin } } }`; `severity` is one of `info|minor|major|critical|blocker`; `location.path` is repo-relative, forward slashes, no leading `./`; no byte order mark; identical fingerprints collapse into one finding; an empty report is `[]` and must always be written.
- `check_name` is `outdated-package`.
- Fingerprint: SHA-256 (lowercase hex) of `check_name | declaring-file repo-relative path | package name lowercased`, joined with `|`. Versions and the line number are **not** part of it.
- One finding per package per *declaring file*; multi-target duplicates are grouped, taking the highest severity (`Major` > `Minor` > `Patch` > `Unknown`). Under Central Package Management the declaring file is `Directory.Packages.props`, so several projects collapse into one finding.
- Default severity mapping: `Major` to `major`, `Minor` to `minor`, `Patch` and `Unknown` to `info`; `None` is never reported. Overridable in `GitLabCodeQualitySettings`, which also has `MinimumUpgradeSeverity` (default `Patch`; `Unknown` findings are always reported) and `RepositoryRoot` (default: the Cake working directory).
- Line lookup order: (1) `PackageVersion`/`GlobalPackageReference` in the nearest `Directory.Packages.props`; (2) `PackageReference` in the project file; (3) `PackageReference`/`PackageVersion` in `Directory.Build.props`/`.targets` walking up; (4) line 1 of the project file. Package ids match case-insensitively on `Include` or `Update`; elements match by local name.
- A declaration outside the repository root falls back to the project file; if that is outside too, the finding is skipped with a warning.
- Real dotnet-outdated output: `Project.FilePath` is an absolute, OS-native path (`C:\...` on Windows). Convert to repo-relative with forward slashes.
- The one-shot alias forces JSON output to a temporary file next to the report, never mutates the caller's settings, and with `FailOnUpdates` accepts exit code 2, writes the report, then throws `CakeException` (exit code 2), so GitLab still receives the artifact under `artifacts: when: always`.
- Tests: xUnit v3 + `Cake.Testing`. Shell notes for Windows: use the Bash tool; do **not** call `python3`.

## File Structure

```
src/Cake.DotNetOutdated/
  Cake.DotNetOutdated.csproj                      (replace: InternalsVisibleTo, description, tags)
  DotNetOutdatedReportSettings.cs                 (replace: adds internal Clone())
  GitLab/GitLabCodeQualitySeverity.cs
  GitLab/GitLabCodeQualityIssue.cs
  GitLab/GitLabCodeQualityReportWriter.cs
  GitLab/GitLabCodeQualitySettings.cs
  GitLab/DependencyLocator.cs                     internal: finds the declaration line
  GitLab/DotNetOutdatedGitLabConverter.cs         report → issues
  GitLab/DotNetOutdatedGitLabCodeQualityRunner.cs internal: run tool + convert + write
  GitLab/GitLabCodeQualityAliases.cs
tests/Cake.DotNetOutdated.Tests/
  GitLab/GitLabCodeQualityReportWriterTests.cs
  GitLab/DependencyLocatorTests.cs
  GitLab/DotNetOutdatedGitLabConverterTests.cs
  GitLab/DotNetOutdatedGitLabCodeQualityRunnerTests.cs
  Fixtures/GitLabRunnerFixture.cs
```

---

### Task 1: GitLab core: severity, issue and report writer

**Files:**
- Replace: `src/Cake.DotNetOutdated/Cake.DotNetOutdated.csproj`
- Create: `src/Cake.DotNetOutdated/GitLab/GitLabCodeQualitySeverity.cs`, `GitLab/GitLabCodeQualityIssue.cs`, `GitLab/GitLabCodeQualityReportWriter.cs`
- Create: `tests/Cake.DotNetOutdated.Tests/GitLab/GitLabCodeQualityReportWriterTests.cs`

**Interfaces:**
- Consumes: `FileSystemHelpers`, `Assertions` (Stage 1 test helpers).
- Produces: `enum GitLabCodeQualitySeverity { Info, Minor, Major, Critical, Blocker }`; `sealed class GitLabCodeQualityIssue { string Description, string CheckName, string Fingerprint, GitLabCodeQualitySeverity Severity, string Path, int Line }` (all `init`); `sealed class GitLabCodeQualityReportWriter(IFileSystem, ICakeEnvironment)` with `void Write(IEnumerable<GitLabCodeQualityIssue> issues, FilePath outputFile)` and `static string Serialize(IEnumerable<GitLabCodeQualityIssue> issues)`. The csproj gains `InternalsVisibleTo` for `Cake.DotNetOutdated.Tests` (later tasks test internal classes).

- [ ] **Step 1: Create a feature branch**

```bash
git switch main
git pull --ff-only
git switch -c feature/stage2-gitlab-code-quality
```

Expected: on a new branch that contains the Stage 1 code (`ls src/Cake.DotNetOutdated/DotNetOutdatedTool.cs` succeeds).

- [ ] **Step 2: Write the failing tests**

**File:** `tests/Cake.DotNetOutdated.Tests/GitLab/GitLabCodeQualityReportWriterTests.cs`

```csharp
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
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet build Cake.DotNetOutdated.sln`
Expected: FAIL to compile (`Cake.DotNetOutdated.GitLab` does not exist).

- [ ] **Step 4: Write the csproj, severity, issue and writer**

**File:** `src/Cake.DotNetOutdated/Cake.DotNetOutdated.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFrameworks>net8.0;net9.0;net10.0</TargetFrameworks>
    <AssemblyName>Cake.DotNetOutdated</AssemblyName>
    <RootNamespace>Cake.DotNetOutdated</RootNamespace>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
    <VersionPrefix>0.1.0</VersionPrefix>
  </PropertyGroup>

  <PropertyGroup Label="Package">
    <PackageId>Cake.DotNetOutdated</PackageId>
    <Authors>Magnus Lindhe</Authors>
    <Description>Cake add-in for the dotnet-outdated tool: report or upgrade outdated NuGet packages, read the JSON report, and turn it into a GitLab Code Quality report.</Description>
    <PackageTags>cake;cake-addin;cake-build;dotnet-outdated;nuget;outdated;gitlab;code-quality</PackageTags>
    <PackageLicenseExpression>MIT</PackageLicenseExpression>
    <PackageProjectUrl>https://github.com/mgnslndh/Cake.DotNetOutdated</PackageProjectUrl>
    <RepositoryUrl>https://github.com/mgnslndh/Cake.DotNetOutdated.git</RepositoryUrl>
    <RepositoryType>git</RepositoryType>
    <PackageIcon>icon.png</PackageIcon>
    <PackageReadmeFile>README.md</PackageReadmeFile>
    <IncludeSymbols>true</IncludeSymbols>
    <SymbolPackageFormat>snupkg</SymbolPackageFormat>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Cake.Core" Version="6.0.0" PrivateAssets="All" />
  </ItemGroup>

  <ItemGroup>
    <InternalsVisibleTo Include="Cake.DotNetOutdated.Tests" />
  </ItemGroup>

  <ItemGroup>
    <None Include="icon.png" Pack="true" PackagePath="\" />
    <None Include="..\..\README.md" Pack="true" PackagePath="\" />
  </ItemGroup>

</Project>
```

**File:** `src/Cake.DotNetOutdated/GitLab/GitLabCodeQualitySeverity.cs`

```csharp
namespace Cake.DotNetOutdated.GitLab
{
    /// <summary>
    /// The severity of a GitLab Code Quality finding.
    /// </summary>
    public enum GitLabCodeQualitySeverity
    {
        /// <summary>Informational (<c>info</c>).</summary>
        Info,

        /// <summary>Minor (<c>minor</c>).</summary>
        Minor,

        /// <summary>Major (<c>major</c>).</summary>
        Major,

        /// <summary>Critical (<c>critical</c>).</summary>
        Critical,

        /// <summary>Blocker (<c>blocker</c>).</summary>
        Blocker,
    }
}
```

**File:** `src/Cake.DotNetOutdated/GitLab/GitLabCodeQualityIssue.cs`

```csharp
namespace Cake.DotNetOutdated.GitLab
{
    /// <summary>
    /// A single finding in a GitLab Code Quality report.
    /// </summary>
    public sealed class GitLabCodeQualityIssue
    {
        /// <summary>
        /// Gets the human-readable description of the finding.
        /// </summary>
        public string Description { get; init; }

        /// <summary>
        /// Gets the name of the check (rule) that produced the finding.
        /// </summary>
        public string CheckName { get; init; }

        /// <summary>
        /// Gets the fingerprint that uniquely identifies the finding. GitLab shows findings with
        /// identical fingerprints as one entry and compares reports by fingerprint.
        /// </summary>
        public string Fingerprint { get; init; }

        /// <summary>
        /// Gets the severity.
        /// </summary>
        public GitLabCodeQualitySeverity Severity { get; init; }

        /// <summary>
        /// Gets the repository-relative path of the file, using forward slashes and no leading <c>./</c>.
        /// </summary>
        public string Path { get; init; }

        /// <summary>
        /// Gets the 1-based line the finding begins on.
        /// </summary>
        public int Line { get; init; }
    }
}
```

**File:** `src/Cake.DotNetOutdated/GitLab/GitLabCodeQualityReportWriter.cs`

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Cake.Core;
using Cake.Core.IO;

namespace Cake.DotNetOutdated.GitLab
{
    /// <summary>
    /// Writes GitLab Code Quality reports.
    /// </summary>
    public sealed class GitLabCodeQualityReportWriter
    {
        private readonly IFileSystem _fileSystem;
        private readonly ICakeEnvironment _environment;

        /// <summary>
        /// Initializes a new instance of the <see cref="GitLabCodeQualityReportWriter" /> class.
        /// </summary>
        /// <param name="fileSystem">The file system.</param>
        /// <param name="environment">The environment.</param>
        public GitLabCodeQualityReportWriter(IFileSystem fileSystem, ICakeEnvironment environment)
        {
            ArgumentNullException.ThrowIfNull(fileSystem);
            ArgumentNullException.ThrowIfNull(environment);

            _fileSystem = fileSystem;
            _environment = environment;
        }

        /// <summary>
        /// Writes a report file (UTF-8 without a byte order mark). An empty list writes <c>[]</c>.
        /// </summary>
        /// <param name="issues">The issues.</param>
        /// <param name="outputFile">The file to write; relative paths are resolved against the working directory.</param>
        public void Write(IEnumerable<GitLabCodeQualityIssue> issues, FilePath outputFile)
        {
            ArgumentNullException.ThrowIfNull(issues);
            ArgumentNullException.ThrowIfNull(outputFile);

            var json = Serialize(issues);
            var path = outputFile.MakeAbsolute(_environment);

            var directory = _fileSystem.GetDirectory(path.GetDirectory());
            if (!directory.Exists)
            {
                directory.Create();
            }

            var bytes = new UTF8Encoding(false).GetBytes(json);
            using var stream = _fileSystem.GetFile(path).Open(FileMode.Create, FileAccess.Write, FileShare.None);
            stream.Write(bytes, 0, bytes.Length);
        }

        /// <summary>
        /// Serializes issues to the GitLab Code Quality JSON format.
        /// </summary>
        /// <param name="issues">The issues.</param>
        /// <returns>The JSON text.</returns>
        public static string Serialize(IEnumerable<GitLabCodeQualityIssue> issues)
        {
            ArgumentNullException.ThrowIfNull(issues);

            using var stream = new MemoryStream();
            var options = new JsonWriterOptions
            {
                Indented = true,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            };

            using (var writer = new Utf8JsonWriter(stream, options))
            {
                writer.WriteStartArray();
                foreach (var issue in issues)
                {
                    writer.WriteStartObject();
                    writer.WriteString("description", issue.Description);
                    writer.WriteString("check_name", issue.CheckName);
                    writer.WriteString("fingerprint", issue.Fingerprint);
                    writer.WriteString("severity", ToJson(issue.Severity));
                    writer.WriteStartObject("location");
                    writer.WriteString("path", issue.Path);
                    writer.WriteStartObject("lines");
                    writer.WriteNumber("begin", issue.Line);
                    writer.WriteEndObject();
                    writer.WriteEndObject();
                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
            }

            return Encoding.UTF8.GetString(stream.ToArray());
        }

        private static string ToJson(GitLabCodeQualitySeverity severity)
        {
            return severity.ToString().ToLowerInvariant();
        }
    }
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test Cake.DotNetOutdated.sln`
Expected: PASS on all three target frameworks (Stage 1 tests still green).

- [ ] **Step 6: Commit**

```bash
git add src tests
git commit -m "feat: add GitLab Code Quality report model and writer"
```

---

### Task 2: Dependency line locator

**Files:**
- Create: `src/Cake.DotNetOutdated/GitLab/DependencyLocator.cs`
- Create: `tests/Cake.DotNetOutdated.Tests/GitLab/DependencyLocatorTests.cs`

**Interfaces:**
- Produces (internal): `sealed record DependencyLocation(FilePath File, int Line)`; `sealed class DependencyLocator(IFileSystem fileSystem)` with `DependencyLocation Locate(FilePath projectFile, string packageName)`. `projectFile` must be absolute. It never throws for missing or malformed files: it falls back to `(projectFile, 1)`. Parsed documents are cached per locator instance.

- [ ] **Step 1: Write the failing tests**

**File:** `tests/Cake.DotNetOutdated.Tests/GitLab/DependencyLocatorTests.cs`

```csharp
using Cake.DotNetOutdated.GitLab;
using Cake.Testing;

namespace Cake.DotNetOutdated.Tests.GitLab;

public sealed class DependencyLocatorTests
{
    private const string Project = "/Working/src/App/App.csproj";

    private const string SdkProject = """
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <TargetFramework>net8.0</TargetFramework>
          </PropertyGroup>
          <ItemGroup>
            <PackageReference Include="Newtonsoft.Json" Version="12.0.1" />
            <PackageReference Update="Serilog" Version="3.0.0" />
            <PackageReference Include="Versioned" Version="$(VersionedVersion)" />
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
            <GlobalPackageReference Include="Analyzer.Package" Version="1.0.0" />
          </ItemGroup>
        </Project>
        """;

    private static (DependencyLocator Locator, FakeFileSystem FileSystem) Create()
    {
        var environment = FakeEnvironment.CreateUnixEnvironment();
        var fileSystem = new FakeFileSystem(environment);
        return (new DependencyLocator(fileSystem), fileSystem);
    }

    [Fact]
    public void Should_Find_A_Package_Reference_In_The_Project_File()
    {
        var (locator, fileSystem) = Create();
        fileSystem.CreateFile(Project).SetContent(SdkProject);

        var location = locator.Locate(Project, "Newtonsoft.Json");

        Assert.Equal(Project, location.File.FullPath);
        Assert.Equal(6, location.Line);
    }

    [Fact]
    public void Should_Match_Package_Ids_Case_Insensitively()
    {
        var (locator, fileSystem) = Create();
        fileSystem.CreateFile(Project).SetContent(SdkProject);

        Assert.Equal(6, locator.Locate(Project, "newtonsoft.JSON").Line);
    }

    [Fact]
    public void Should_Match_An_Update_Attribute()
    {
        var (locator, fileSystem) = Create();
        fileSystem.CreateFile(Project).SetContent(SdkProject);

        Assert.Equal(7, locator.Locate(Project, "Serilog").Line);
    }

    [Fact]
    public void Should_Find_A_Package_Whose_Version_Is_A_Property()
    {
        var (locator, fileSystem) = Create();
        fileSystem.CreateFile(Project).SetContent(SdkProject);

        Assert.Equal(8, locator.Locate(Project, "Versioned").Line);
    }

    [Fact]
    public void Should_Find_A_Package_Reference_In_A_Project_With_An_Xml_Namespace()
    {
        var (locator, fileSystem) = Create();
        fileSystem.CreateFile(Project).SetContent("""
            <?xml version="1.0" encoding="utf-8"?>
            <Project ToolsVersion="15.0" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
              <ItemGroup>
                <PackageReference Include="Newtonsoft.Json">
                  <Version>12.0.1</Version>
                </PackageReference>
              </ItemGroup>
            </Project>
            """);

        Assert.Equal(4, locator.Locate(Project, "Newtonsoft.Json").Line);
    }

    [Fact]
    public void Should_Prefer_The_Central_Package_Version_Over_The_Project_File()
    {
        var (locator, fileSystem) = Create();
        fileSystem.CreateFile(Project).SetContent(SdkProject);
        fileSystem.CreateFile("/Working/Directory.Packages.props").SetContent(CentralPackages);

        var location = locator.Locate(Project, "Newtonsoft.Json");

        Assert.Equal("/Working/Directory.Packages.props", location.File.FullPath);
        Assert.Equal(6, location.Line);
    }

    [Fact]
    public void Should_Find_A_Global_Package_Reference()
    {
        var (locator, fileSystem) = Create();
        fileSystem.CreateFile(Project).SetContent(SdkProject);
        fileSystem.CreateFile("/Working/Directory.Packages.props").SetContent(CentralPackages);

        var location = locator.Locate(Project, "Analyzer.Package");

        Assert.Equal("/Working/Directory.Packages.props", location.File.FullPath);
        Assert.Equal(7, location.Line);
    }

    [Fact]
    public void Should_Only_Consider_The_Nearest_Central_Packages_File()
    {
        var (locator, fileSystem) = Create();
        fileSystem.CreateFile(Project).SetContent(SdkProject);
        fileSystem.CreateFile("/Working/Directory.Packages.props").SetContent(CentralPackages);
        fileSystem.CreateFile("/Working/src/Directory.Packages.props").SetContent("<Project />");

        var location = locator.Locate(Project, "Newtonsoft.Json");

        // The nearest file does not declare the package, so the lookup continues with the project file.
        Assert.Equal(Project, location.File.FullPath);
        Assert.Equal(6, location.Line);
    }

    [Fact]
    public void Should_Fall_Back_To_The_Project_File_When_Central_Packages_Do_Not_Declare_The_Package()
    {
        var (locator, fileSystem) = Create();
        fileSystem.CreateFile(Project).SetContent(SdkProject);
        fileSystem.CreateFile("/Working/Directory.Packages.props").SetContent(CentralPackages);

        var location = locator.Locate(Project, "Serilog");

        Assert.Equal(Project, location.File.FullPath);
        Assert.Equal(7, location.Line);
    }

    [Fact]
    public void Should_Find_A_Package_Reference_In_Directory_Build_Props()
    {
        var (locator, fileSystem) = Create();
        fileSystem.CreateFile(Project).SetContent("<Project Sdk=\"Microsoft.NET.Sdk\" />");
        fileSystem.CreateFile("/Working/Directory.Build.props").SetContent("""
            <Project>
              <ItemGroup>
                <PackageReference Include="Shared.Analyzer" Version="1.0.0" />
              </ItemGroup>
            </Project>
            """);

        var location = locator.Locate(Project, "Shared.Analyzer");

        Assert.Equal("/Working/Directory.Build.props", location.File.FullPath);
        Assert.Equal(3, location.Line);
    }

    [Fact]
    public void Should_Find_A_Package_Reference_In_Directory_Build_Targets()
    {
        var (locator, fileSystem) = Create();
        fileSystem.CreateFile(Project).SetContent("<Project Sdk=\"Microsoft.NET.Sdk\" />");
        fileSystem.CreateFile("/Working/src/Directory.Build.targets").SetContent("""
            <Project>
              <ItemGroup>
                <PackageReference Include="Shared.Targets" Version="1.0.0" />
              </ItemGroup>
            </Project>
            """);

        var location = locator.Locate(Project, "Shared.Targets");

        Assert.Equal("/Working/src/Directory.Build.targets", location.File.FullPath);
        Assert.Equal(3, location.Line);
    }

    [Fact]
    public void Should_Fall_Back_To_Line_One_Of_The_Project_When_The_Package_Is_Not_Declared()
    {
        var (locator, fileSystem) = Create();
        fileSystem.CreateFile(Project).SetContent(SdkProject);

        var location = locator.Locate(Project, "Transitive.Package");

        Assert.Equal(Project, location.File.FullPath);
        Assert.Equal(1, location.Line);
    }

    [Fact]
    public void Should_Fall_Back_To_Line_One_When_The_Project_File_Does_Not_Exist()
    {
        var (locator, _) = Create();

        var location = locator.Locate(Project, "Anything");

        Assert.Equal(Project, location.File.FullPath);
        Assert.Equal(1, location.Line);
    }

    [Fact]
    public void Should_Ignore_Files_That_Are_Not_Valid_Xml()
    {
        var (locator, fileSystem) = Create();
        fileSystem.CreateFile(Project).SetContent("<Project><unclosed>");
        fileSystem.CreateFile("/Working/Directory.Packages.props").SetContent("not xml at all");

        var location = locator.Locate(Project, "Newtonsoft.Json");

        Assert.Equal(Project, location.File.FullPath);
        Assert.Equal(1, location.Line);
    }

    [Fact]
    public void Should_Terminate_At_The_File_System_Root()
    {
        var (locator, fileSystem) = Create();
        fileSystem.CreateFile("/App.csproj").SetContent("<Project />");

        var location = locator.Locate("/App.csproj", "Anything");

        Assert.Equal("/App.csproj", location.File.FullPath);
        Assert.Equal(1, location.Line);
    }

    [Fact]
    public void Should_Reuse_Parsed_Documents_Between_Lookups()
    {
        var (locator, fileSystem) = Create();
        fileSystem.CreateFile(Project).SetContent(SdkProject);

        Assert.Equal(6, locator.Locate(Project, "Newtonsoft.Json").Line);

        // A cached document is not re-read: changing the file afterwards does not change the answer.
        fileSystem.CreateFile(Project).SetContent("<Project />");
        Assert.Equal(6, locator.Locate(Project, "Newtonsoft.Json").Line);
    }

    [Fact]
    public void Should_Throw_If_The_Project_File_Is_Null()
    {
        var (locator, _) = Create();

        Assertions.IsArgumentNullException(Record.Exception(() => locator.Locate(null, "X")), "projectFile");
    }

    [Fact]
    public void Should_Throw_If_The_Package_Name_Is_Blank()
    {
        var (locator, _) = Create();

        Assert.ThrowsAny<ArgumentException>(() => locator.Locate(Project, " "));
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet build Cake.DotNetOutdated.sln`
Expected: FAIL to compile (`DependencyLocator` does not exist).

- [ ] **Step 3: Write the locator**

**File:** `src/Cake.DotNetOutdated/GitLab/DependencyLocator.cs`

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using Cake.Core.IO;

namespace Cake.DotNetOutdated.GitLab
{
    /// <summary>
    /// The file and line where a package is declared.
    /// </summary>
    /// <param name="File">The declaring file.</param>
    /// <param name="Line">The 1-based line.</param>
    internal sealed record DependencyLocation(FilePath File, int Line);

    /// <summary>
    /// Finds the line on which a NuGet package is declared, so a finding can point at the exact dependency.
    /// </summary>
    /// <remarks>
    /// Lookup order: the nearest <c>Directory.Packages.props</c>, the project file, then
    /// <c>Directory.Build.props</c>/<c>.targets</c> walking up the tree, else line 1 of the project file.
    /// MSBuild property definitions are not followed: the element that declares the package is what is located.
    /// </remarks>
    internal sealed class DependencyLocator
    {
        private static readonly string[] CentralElements = { "PackageVersion", "GlobalPackageReference" };
        private static readonly string[] ReferenceElements = { "PackageReference" };
        private static readonly string[] BuildPropsElements = { "PackageReference", "PackageVersion" };

        private readonly IFileSystem _fileSystem;
        private readonly Dictionary<string, XDocument> _documents = new Dictionary<string, XDocument>(StringComparer.Ordinal);

        public DependencyLocator(IFileSystem fileSystem)
        {
            ArgumentNullException.ThrowIfNull(fileSystem);

            _fileSystem = fileSystem;
        }

        /// <summary>
        /// Locates the declaration of a package for a project.
        /// </summary>
        /// <param name="projectFile">The absolute path of the project file.</param>
        /// <param name="packageName">The package id.</param>
        /// <returns>The declaring file and line; line 1 of the project file if no declaration was found.</returns>
        public DependencyLocation Locate(FilePath projectFile, string packageName)
        {
            ArgumentNullException.ThrowIfNull(projectFile);
            ArgumentException.ThrowIfNullOrWhiteSpace(packageName);

            var directories = WalkUp(projectFile.GetDirectory()).ToList();

            // 1. Central package management: only the nearest Directory.Packages.props counts.
            foreach (var directory in directories)
            {
                var packagesProps = directory.CombineWithFilePath("Directory.Packages.props");
                if (!_fileSystem.Exist(packagesProps))
                {
                    continue;
                }

                if (TryFind(packagesProps, packageName, CentralElements, out var centralLine))
                {
                    return new DependencyLocation(packagesProps, centralLine);
                }

                break;
            }

            // 2. The project file itself.
            if (TryFind(projectFile, packageName, ReferenceElements, out var projectLine))
            {
                return new DependencyLocation(projectFile, projectLine);
            }

            // 3. Directory.Build.props / Directory.Build.targets, nearest first.
            foreach (var directory in directories)
            {
                foreach (var name in new[] { "Directory.Build.props", "Directory.Build.targets" })
                {
                    var buildFile = directory.CombineWithFilePath(name);
                    if (TryFind(buildFile, packageName, BuildPropsElements, out var buildLine))
                    {
                        return new DependencyLocation(buildFile, buildLine);
                    }
                }
            }

            // 4. Not declared anywhere we can see (transitive, non-SDK project, ...).
            return new DependencyLocation(projectFile, 1);
        }

        private static IEnumerable<DirectoryPath> WalkUp(DirectoryPath start)
        {
            var current = start;
            while (current != null)
            {
                yield return current;

                var parent = current.GetParent();
                if (parent == null || parent.FullPath == current.FullPath)
                {
                    yield break;
                }

                current = parent;
            }
        }

        private bool TryFind(FilePath file, string packageName, string[] elementNames, out int line)
        {
            line = 0;

            var document = Load(file);
            if (document?.Root == null)
            {
                return false;
            }

            foreach (var element in document.Descendants())
            {
                if (Array.IndexOf(elementNames, element.Name.LocalName) < 0)
                {
                    continue;
                }

                var id = (string)element.Attribute("Include") ?? (string)element.Attribute("Update");
                if (!string.Equals(id, packageName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var lineInfo = (IXmlLineInfo)element;
                line = lineInfo.HasLineInfo() ? lineInfo.LineNumber : 1;
                return true;
            }

            return false;
        }

        private XDocument Load(FilePath path)
        {
            if (_documents.TryGetValue(path.FullPath, out var cached))
            {
                return cached;
            }

            XDocument document = null;
            var file = _fileSystem.GetFile(path);
            if (file.Exists)
            {
                try
                {
                    using var stream = file.OpenRead();
                    document = XDocument.Load(stream, LoadOptions.SetLineInfo);
                }
                catch (XmlException)
                {
                    document = null;
                }
            }

            _documents[path.FullPath] = document;
            return document;
        }
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test Cake.DotNetOutdated.sln`
Expected: PASS on all three target frameworks. If `Should_Terminate_At_The_File_System_Root` fails because `DirectoryPath.GetParent()` throws or loops at the root, fix `WalkUp` (for example catch the exception and stop), keeping the test.

- [ ] **Step 5: Commit**

```bash
git add src tests
git commit -m "feat: locate NuGet package declarations for GitLab findings"
```

---

### Task 3: Settings and outdated → GitLab converter

**Files:**
- Create: `src/Cake.DotNetOutdated/GitLab/GitLabCodeQualitySettings.cs`, `GitLab/DotNetOutdatedGitLabConverter.cs`
- Create: `tests/Cake.DotNetOutdated.Tests/GitLab/DotNetOutdatedGitLabConverterTests.cs`

**Interfaces:**
- Consumes: `DependencyLocator.Locate(FilePath, string)` → `DependencyLocation(FilePath File, int Line)` (Task 2); `GitLabCodeQualityIssue`, `GitLabCodeQualitySeverity` (Task 1); `DotNetOutdatedReport`, `DotNetOutdatedProject`, `DotNetOutdatedTargetFramework`, `DotNetOutdatedDependency`, `DotNetOutdatedUpgradeSeverity` (Stage 1).
- Produces: `class GitLabCodeQualitySettings { DirectoryPath RepositoryRoot; GitLabCodeQualitySeverity MajorSeverity, MinorSeverity, PatchSeverity, UnknownSeverity; DotNetOutdatedUpgradeSeverity MinimumUpgradeSeverity }`; `sealed class DotNetOutdatedGitLabConverter(IFileSystem, ICakeEnvironment, ICakeLog)` with `const string CheckName = "outdated-package"` and `IReadOnlyList<GitLabCodeQualityIssue> Convert(DotNetOutdatedReport report, GitLabCodeQualitySettings settings)` (`settings` may be null: defaults are used).

- [ ] **Step 1: Write the failing tests**

**File:** `tests/Cake.DotNetOutdated.Tests/GitLab/DotNetOutdatedGitLabConverterTests.cs`

```csharp
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
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet build Cake.DotNetOutdated.sln`
Expected: FAIL to compile (`GitLabCodeQualitySettings` / `DotNetOutdatedGitLabConverter` do not exist).

- [ ] **Step 3: Write the settings and the converter**

**File:** `src/Cake.DotNetOutdated/GitLab/GitLabCodeQualitySettings.cs`

```csharp
using Cake.Core.IO;
using Cake.DotNetOutdated.Report;

namespace Cake.DotNetOutdated.GitLab
{
    /// <summary>
    /// Contains the settings used when converting a dotnet-outdated report to a GitLab Code Quality report.
    /// </summary>
    public class GitLabCodeQualitySettings
    {
        /// <summary>
        /// Gets or sets the repository root that finding paths are made relative to.
        /// Defaults to the Cake working directory.
        /// </summary>
        public DirectoryPath RepositoryRoot { get; set; }

        /// <summary>
        /// Gets or sets the severity used for major upgrades. Defaults to <see cref="GitLabCodeQualitySeverity.Major"/>.
        /// </summary>
        public GitLabCodeQualitySeverity MajorSeverity { get; set; } = GitLabCodeQualitySeverity.Major;

        /// <summary>
        /// Gets or sets the severity used for minor upgrades. Defaults to <see cref="GitLabCodeQualitySeverity.Minor"/>.
        /// </summary>
        public GitLabCodeQualitySeverity MinorSeverity { get; set; } = GitLabCodeQualitySeverity.Minor;

        /// <summary>
        /// Gets or sets the severity used for patch upgrades. Defaults to <see cref="GitLabCodeQualitySeverity.Info"/>.
        /// </summary>
        public GitLabCodeQualitySeverity PatchSeverity { get; set; } = GitLabCodeQualitySeverity.Info;

        /// <summary>
        /// Gets or sets the severity used when the upgrade severity is unknown. Defaults to <see cref="GitLabCodeQualitySeverity.Info"/>.
        /// </summary>
        public GitLabCodeQualitySeverity UnknownSeverity { get; set; } = GitLabCodeQualitySeverity.Info;

        /// <summary>
        /// Gets or sets the lowest upgrade severity that is reported. Defaults to <see cref="DotNetOutdatedUpgradeSeverity.Patch"/>
        /// (everything). Findings whose severity is <see cref="DotNetOutdatedUpgradeSeverity.Unknown"/> are always reported.
        /// </summary>
        public DotNetOutdatedUpgradeSeverity MinimumUpgradeSeverity { get; set; } = DotNetOutdatedUpgradeSeverity.Patch;
    }
}
```

**File:** `src/Cake.DotNetOutdated/GitLab/DotNetOutdatedGitLabConverter.cs`

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Cake.Core;
using Cake.Core.Diagnostics;
using Cake.Core.IO;
using Cake.DotNetOutdated.Report;

namespace Cake.DotNetOutdated.GitLab
{
    /// <summary>
    /// Converts a dotnet-outdated report to GitLab Code Quality findings.
    /// </summary>
    /// <remarks>
    /// One finding is produced per package per declaring file. The fingerprint contains neither versions nor line
    /// numbers, so GitLab does not report new and fixed findings when a new package version is released.
    /// </remarks>
    public sealed class DotNetOutdatedGitLabConverter
    {
        /// <summary>
        /// The <c>check_name</c> of all findings.
        /// </summary>
        public const string CheckName = "outdated-package";

        private readonly IFileSystem _fileSystem;
        private readonly ICakeEnvironment _environment;
        private readonly ICakeLog _log;

        /// <summary>
        /// Initializes a new instance of the <see cref="DotNetOutdatedGitLabConverter" /> class.
        /// </summary>
        /// <param name="fileSystem">The file system.</param>
        /// <param name="environment">The environment.</param>
        /// <param name="log">The log.</param>
        public DotNetOutdatedGitLabConverter(IFileSystem fileSystem, ICakeEnvironment environment, ICakeLog log)
        {
            ArgumentNullException.ThrowIfNull(fileSystem);
            ArgumentNullException.ThrowIfNull(environment);
            ArgumentNullException.ThrowIfNull(log);

            _fileSystem = fileSystem;
            _environment = environment;
            _log = log;
        }

        /// <summary>
        /// Converts a report.
        /// </summary>
        /// <param name="report">The dotnet-outdated report.</param>
        /// <param name="settings">The settings; defaults are used if <c>null</c>.</param>
        /// <returns>The findings, ordered by path and line.</returns>
        public IReadOnlyList<GitLabCodeQualityIssue> Convert(DotNetOutdatedReport report, GitLabCodeQualitySettings settings)
        {
            ArgumentNullException.ThrowIfNull(report);

            settings ??= new GitLabCodeQualitySettings();

            var root = (settings.RepositoryRoot ?? _environment.WorkingDirectory).MakeAbsolute(_environment);
            var locator = new DependencyLocator(_fileSystem);
            var findings = new Dictionary<string, Finding>(StringComparer.Ordinal);

            foreach (var project in report.Projects)
            {
                if (string.IsNullOrWhiteSpace(project.FilePath))
                {
                    continue;
                }

                var projectFile = new FilePath(project.FilePath).MakeAbsolute(_environment);

                var strongestPerPackage = project.TargetFrameworks
                    .SelectMany(framework => framework.Dependencies)
                    .Where(dependency => IsReported(dependency, settings))
                    .GroupBy(dependency => dependency.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(group => group.OrderByDescending(dependency => Rank(dependency.UpgradeSeverity)).First());

                foreach (var dependency in strongestPerPackage)
                {
                    var location = locator.Locate(projectFile, dependency.Name);
                    var path = ToRepositoryPath(root, location.File);
                    var line = location.Line;

                    if (path == null)
                    {
                        // The declaration is outside the repository (for example a shared props file): point at the project instead.
                        path = ToRepositoryPath(root, projectFile);
                        line = 1;
                    }

                    if (path == null)
                    {
                        _log.Warning(
                            "Skipping {0} in {1}: the project is outside the repository root {2}.",
                            dependency.Name,
                            projectFile.FullPath,
                            root.FullPath);
                        continue;
                    }

                    var key = path + "|" + dependency.Name.ToLowerInvariant();
                    if (findings.TryGetValue(key, out var existing)
                        && Rank(existing.Dependency.UpgradeSeverity) >= Rank(dependency.UpgradeSeverity))
                    {
                        continue;
                    }

                    findings[key] = new Finding(path, line, dependency);
                }
            }

            return findings.Values
                .Select(finding => new GitLabCodeQualityIssue
                {
                    Description = Describe(finding.Dependency),
                    CheckName = CheckName,
                    Fingerprint = Fingerprint(finding.Path, finding.Dependency.Name),
                    Severity = MapSeverity(finding.Dependency.UpgradeSeverity, settings),
                    Path = finding.Path,
                    Line = Math.Max(1, finding.Line),
                })
                .OrderBy(issue => issue.Path, StringComparer.Ordinal)
                .ThenBy(issue => issue.Line)
                .ThenBy(issue => issue.Description, StringComparer.Ordinal)
                .ToList();
        }

        private static bool IsReported(DotNetOutdatedDependency dependency, GitLabCodeQualitySettings settings)
        {
            var severity = dependency.UpgradeSeverity;
            if (severity == DotNetOutdatedUpgradeSeverity.None)
            {
                return false;
            }

            return severity == DotNetOutdatedUpgradeSeverity.Unknown || severity >= settings.MinimumUpgradeSeverity;
        }

        private static int Rank(DotNetOutdatedUpgradeSeverity severity)
        {
            return severity switch
            {
                DotNetOutdatedUpgradeSeverity.Major => 4,
                DotNetOutdatedUpgradeSeverity.Minor => 3,
                DotNetOutdatedUpgradeSeverity.Patch => 2,
                DotNetOutdatedUpgradeSeverity.Unknown => 1,
                _ => 0,
            };
        }

        private static GitLabCodeQualitySeverity MapSeverity(DotNetOutdatedUpgradeSeverity severity, GitLabCodeQualitySettings settings)
        {
            return severity switch
            {
                DotNetOutdatedUpgradeSeverity.Major => settings.MajorSeverity,
                DotNetOutdatedUpgradeSeverity.Minor => settings.MinorSeverity,
                DotNetOutdatedUpgradeSeverity.Patch => settings.PatchSeverity,
                _ => settings.UnknownSeverity,
            };
        }

        private static string Describe(DotNetOutdatedDependency dependency)
        {
            var resolved = string.IsNullOrWhiteSpace(dependency.ResolvedVersion) ? "unknown" : dependency.ResolvedVersion;

            if (string.IsNullOrWhiteSpace(dependency.LatestVersion))
            {
                return $"Could not determine the latest version of {dependency.Name} (resolved version {resolved}).";
            }

            var kind = dependency.UpgradeSeverity switch
            {
                DotNetOutdatedUpgradeSeverity.Major => " (major update)",
                DotNetOutdatedUpgradeSeverity.Minor => " (minor update)",
                DotNetOutdatedUpgradeSeverity.Patch => " (patch update)",
                _ => string.Empty,
            };

            return $"{dependency.Name} {resolved} can be updated to {dependency.LatestVersion}{kind}.";
        }

        private static string Fingerprint(string path, string packageName)
        {
            var input = string.Join("|", CheckName, path, packageName.ToLowerInvariant());
            return System.Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input))).ToLowerInvariant();
        }

        private static string ToRepositoryPath(DirectoryPath root, FilePath file)
        {
            var relative = System.IO.Path.GetRelativePath(root.FullPath, file.FullPath).Replace('\\', '/');

            if (relative == ".." || relative.StartsWith("../", StringComparison.Ordinal) || System.IO.Path.IsPathRooted(relative))
            {
                return null;
            }

            return relative;
        }

        private sealed record Finding(string Path, int Line, DotNetOutdatedDependency Dependency);
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test Cake.DotNetOutdated.sln`
Expected: PASS on all three target frameworks.

- [ ] **Step 5: Commit**

```bash
git add src tests
git commit -m "feat: convert dotnet-outdated reports to GitLab Code Quality findings"
```

---

### Task 4: One-shot runner and aliases

**Files:**
- Replace: `src/Cake.DotNetOutdated/DotNetOutdatedReportSettings.cs` (adds `internal Clone()`)
- Create: `src/Cake.DotNetOutdated/GitLab/DotNetOutdatedGitLabCodeQualityRunner.cs`, `GitLab/GitLabCodeQualityAliases.cs`
- Create: `tests/Cake.DotNetOutdated.Tests/Fixtures/GitLabRunnerFixture.cs`, `tests/Cake.DotNetOutdated.Tests/GitLab/DotNetOutdatedGitLabCodeQualityRunnerTests.cs`

**Interfaces:**
- Consumes: `DotNetOutdatedReporter.Report(string, DotNetOutdatedReportSettings)`, `DotNetOutdatedReportReader.Read(FilePath)`, `DotNetOutdatedGitLabConverter.Convert(...)`, `GitLabCodeQualityReportWriter.Write(...)`, `DotNetOutdatedFixture<TSettings>`, `Assertions`, `FileSystemHelpers` (earlier tasks).
- Produces: `internal DotNetOutdatedReportSettings DotNetOutdatedReportSettings.Clone()` (shallow copy); `internal sealed class DotNetOutdatedGitLabCodeQualityRunner(IFileSystem, ICakeEnvironment, IProcessRunner, IToolLocator, ICakeLog)` with `void Run(string path, FilePath outputFile, DotNetOutdatedReportSettings reportSettings, GitLabCodeQualitySettings gitLabSettings)`; aliases (class `GitLabCodeQualityAliases`): `IReadOnlyList<GitLabCodeQualityIssue> ConvertToGitLabCodeQuality(this ICakeContext, DotNetOutdatedReport[, GitLabCodeQualitySettings])`, `void WriteGitLabCodeQualityReport(this ICakeContext, IEnumerable<GitLabCodeQualityIssue>, FilePath)`, `void DotNetOutdatedGitLabCodeQuality(this ICakeContext, string path, FilePath outputFile[, DotNetOutdatedReportSettings, GitLabCodeQualitySettings])`.
- Runner behavior: the temporary JSON report is `<outputFile>.outdated.json` (absolute), always deleted afterwards; the caller's settings are never modified; with `FailOnUpdates` and exit code 2 the report is written first, then a `CakeException` (exit code 2, message `dotnet-outdated: Process returned an error (exit code 2).`) is thrown unless the caller's `HandleExitCode` accepts 2.

- [ ] **Step 1: Write the failing tests**

**File:** `tests/Cake.DotNetOutdated.Tests/Fixtures/GitLabRunnerFixture.cs`

```csharp
using Cake.Core.IO;
using Cake.DotNetOutdated.GitLab;
using Cake.Testing;

namespace Cake.DotNetOutdated.Tests.Fixtures;

internal sealed class GitLabRunnerFixture : DotNetOutdatedFixture<DotNetOutdatedReportSettings>
{
    public FilePath OutputFile { get; set; } = "gl-code-quality-report.json";

    public GitLabCodeQualitySettings GitLabSettings { get; set; } = new GitLabCodeQualitySettings();

    public FakeLog Log { get; } = new FakeLog();

    protected override void RunTool()
    {
        new DotNetOutdatedGitLabCodeQualityRunner(FileSystem, Environment, ProcessRunner, Tools, Log)
            .Run(Path, OutputFile, Settings, GitLabSettings);
    }
}
```

**File:** `tests/Cake.DotNetOutdated.Tests/GitLab/DotNetOutdatedGitLabCodeQualityRunnerTests.cs`

```csharp
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
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet build Cake.DotNetOutdated.sln`
Expected: FAIL to compile (`DotNetOutdatedGitLabCodeQualityRunner` does not exist).

- [ ] **Step 3: Add `Clone()` to the report settings and write the runner and aliases**

**File:** `src/Cake.DotNetOutdated/DotNetOutdatedReportSettings.cs`

```csharp
using Cake.Core.IO;

namespace Cake.DotNetOutdated
{
    /// <summary>
    /// Contains the settings used by the report command (<c>dotnet outdated</c>).
    /// </summary>
    public class DotNetOutdatedReportSettings : DotNetOutdatedSettings
    {
        /// <summary>
        /// Gets or sets the file to save the report to (<c>--output</c>). Relative paths are resolved against the Cake working directory.
        /// </summary>
        public FilePath OutputFile { get; set; }

        /// <summary>
        /// Gets or sets the format of the report file (<c>--output-format</c>).
        /// </summary>
        public DotNetOutdatedOutputFormat? OutputFormat { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether all dependencies are reported, including up-to-date ones (<c>--include-up-to-date</c>).
        /// </summary>
        public bool IncludeUpToDate { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the tool exits with code 2 when updates are found (<c>--fail-on-updates</c>).
        /// Cake treats any non-zero exit code as a failure unless <see cref="Cake.Core.Tooling.ToolSettings.HandleExitCode"/> accepts it.
        /// </summary>
        public bool FailOnUpdates { get; set; }

        /// <summary>
        /// Creates a shallow copy, so callers' settings can be adjusted without being modified.
        /// </summary>
        /// <returns>The copy.</returns>
        internal DotNetOutdatedReportSettings Clone()
        {
            return (DotNetOutdatedReportSettings)MemberwiseClone();
        }
    }
}
```

**File:** `src/Cake.DotNetOutdated/GitLab/DotNetOutdatedGitLabCodeQualityRunner.cs`

```csharp
using System;
using Cake.Core;
using Cake.Core.Diagnostics;
using Cake.Core.IO;
using Cake.Core.Tooling;
using Cake.DotNetOutdated.Report;

namespace Cake.DotNetOutdated.GitLab
{
    /// <summary>
    /// Runs dotnet-outdated, converts its JSON report and writes a GitLab Code Quality report.
    /// </summary>
    internal sealed class DotNetOutdatedGitLabCodeQualityRunner
    {
        private readonly IFileSystem _fileSystem;
        private readonly ICakeEnvironment _environment;
        private readonly IProcessRunner _processRunner;
        private readonly IToolLocator _tools;
        private readonly ICakeLog _log;

        public DotNetOutdatedGitLabCodeQualityRunner(
            IFileSystem fileSystem,
            ICakeEnvironment environment,
            IProcessRunner processRunner,
            IToolLocator tools,
            ICakeLog log)
        {
            _fileSystem = fileSystem;
            _environment = environment;
            _processRunner = processRunner;
            _tools = tools;
            _log = log;
        }

        public void Run(string path, FilePath outputFile, DotNetOutdatedReportSettings reportSettings, GitLabCodeQualitySettings gitLabSettings)
        {
            ArgumentNullException.ThrowIfNull(outputFile);

            reportSettings ??= new DotNetOutdatedReportSettings();

            var output = outputFile.MakeAbsolute(_environment);
            var temporaryReport = new FilePath(output.FullPath + ".outdated.json");

            var callersHandler = reportSettings.HandleExitCode;
            var failOnUpdates = reportSettings.FailOnUpdates;
            var exitCode = 0;

            var settings = reportSettings.Clone();
            settings.OutputFile = temporaryReport;
            settings.OutputFormat = DotNetOutdatedOutputFormat.Json;

            // Exit code 2 (updates found) must not abort before the report is written; it is re-thrown afterwards.
            settings.HandleExitCode = code => (callersHandler?.Invoke(code) ?? code == 0) || (failOnUpdates && code == 2);
            settings.PostAction = process =>
            {
                exitCode = process.GetExitCode();
                reportSettings.PostAction?.Invoke(process);
            };

            try
            {
                new DotNetOutdatedReporter(_fileSystem, _environment, _processRunner, _tools).Report(path, settings);

                if (!_fileSystem.Exist(temporaryReport))
                {
                    throw new CakeException(
                        $"dotnet-outdated did not produce a report (exit code {exitCode}); no GitLab Code Quality report was written.");
                }

                var report = new DotNetOutdatedReportReader(_fileSystem, _environment).Read(temporaryReport);
                var issues = new DotNetOutdatedGitLabConverter(_fileSystem, _environment, _log).Convert(report, gitLabSettings);
                new GitLabCodeQualityReportWriter(_fileSystem, _environment).Write(issues, output);
            }
            finally
            {
                var file = _fileSystem.GetFile(temporaryReport);
                if (file.Exists)
                {
                    file.Delete();
                }
            }

            if (failOnUpdates && exitCode == 2 && !(callersHandler?.Invoke(2) ?? false))
            {
                throw new CakeException(2, "dotnet-outdated: Process returned an error (exit code 2).");
            }
        }
    }
}
```

**File:** `src/Cake.DotNetOutdated/GitLab/GitLabCodeQualityAliases.cs`

```csharp
using System;
using System.Collections.Generic;
using Cake.Core;
using Cake.Core.Annotations;
using Cake.Core.IO;
using Cake.DotNetOutdated.Report;

namespace Cake.DotNetOutdated.GitLab
{
    /// <summary>
    /// Contains functionality for creating GitLab Code Quality reports from dotnet-outdated reports.
    /// </summary>
    public static class GitLabCodeQualityAliases
    {
        /// <summary>
        /// Converts a dotnet-outdated report to GitLab Code Quality findings.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="report">The dotnet-outdated report.</param>
        /// <returns>The findings.</returns>
        /// <example>
        /// <code>
        /// var report = ReadDotNetOutdatedReport("outdated.json");
        /// var issues = ConvertToGitLabCodeQuality(report);
        /// WriteGitLabCodeQualityReport(issues, "gl-code-quality-report.json");
        /// </code>
        /// </example>
        [CakeMethodAlias]
        [CakeAliasCategory("GitLab Code Quality")]
        [CakeNamespaceImport("Cake.DotNetOutdated.GitLab")]
        [CakeNamespaceImport("Cake.DotNetOutdated.Report")]
        public static IReadOnlyList<GitLabCodeQualityIssue> ConvertToGitLabCodeQuality(this ICakeContext context, DotNetOutdatedReport report)
        {
            return context.ConvertToGitLabCodeQuality(report, null);
        }

        /// <summary>
        /// Converts a dotnet-outdated report to GitLab Code Quality findings using the specified settings.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="report">The dotnet-outdated report.</param>
        /// <param name="settings">The settings.</param>
        /// <returns>The findings.</returns>
        /// <example>
        /// <code>
        /// var issues = ConvertToGitLabCodeQuality(report, new GitLabCodeQualitySettings
        /// {
        ///     MajorSeverity = GitLabCodeQualitySeverity.Critical,
        ///     MinimumUpgradeSeverity = DotNetOutdatedUpgradeSeverity.Minor
        /// });
        /// </code>
        /// </example>
        [CakeMethodAlias]
        [CakeAliasCategory("GitLab Code Quality")]
        [CakeNamespaceImport("Cake.DotNetOutdated.GitLab")]
        [CakeNamespaceImport("Cake.DotNetOutdated.Report")]
        public static IReadOnlyList<GitLabCodeQualityIssue> ConvertToGitLabCodeQuality(
            this ICakeContext context,
            DotNetOutdatedReport report,
            GitLabCodeQualitySettings settings)
        {
            ArgumentNullException.ThrowIfNull(context);

            return new DotNetOutdatedGitLabConverter(context.FileSystem, context.Environment, context.Log).Convert(report, settings);
        }

        /// <summary>
        /// Writes GitLab Code Quality findings to a report file (UTF-8 without a byte order mark; <c>[]</c> when empty).
        /// Declare it in <c>.gitlab-ci.yml</c> with <c>artifacts:reports:codequality</c>.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="issues">The findings.</param>
        /// <param name="outputFile">The report file.</param>
        [CakeMethodAlias]
        [CakeAliasCategory("GitLab Code Quality")]
        [CakeNamespaceImport("Cake.DotNetOutdated.GitLab")]
        public static void WriteGitLabCodeQualityReport(this ICakeContext context, IEnumerable<GitLabCodeQualityIssue> issues, FilePath outputFile)
        {
            ArgumentNullException.ThrowIfNull(context);

            new GitLabCodeQualityReportWriter(context.FileSystem, context.Environment).Write(issues, outputFile);
        }

        /// <summary>
        /// Runs dotnet-outdated and writes the result as a GitLab Code Quality report.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="path">The solution, project or directory to analyze; the current directory if empty.</param>
        /// <param name="outputFile">The GitLab Code Quality report file.</param>
        /// <example>
        /// <code>
        /// DotNetOutdatedGitLabCodeQuality(".", "gl-code-quality-report.json");
        /// </code>
        /// </example>
        [CakeMethodAlias]
        [CakeAliasCategory("GitLab Code Quality")]
        [CakeNamespaceImport("Cake.DotNetOutdated")]
        [CakeNamespaceImport("Cake.DotNetOutdated.GitLab")]
        public static void DotNetOutdatedGitLabCodeQuality(this ICakeContext context, string path, FilePath outputFile)
        {
            context.DotNetOutdatedGitLabCodeQuality(path, outputFile, null, null);
        }

        /// <summary>
        /// Runs dotnet-outdated and writes the result as a GitLab Code Quality report, using the specified settings.
        /// </summary>
        /// <remarks>
        /// The tool always writes JSON to a temporary file next to the report (removed afterwards); the output settings of
        /// <paramref name="reportSettings"/> are ignored and the settings object is not modified. With
        /// <see cref="DotNetOutdatedReportSettings.FailOnUpdates"/> the report is written first and the build is then failed
        /// (exit code 2) unless <c>HandleExitCode</c> accepts it, so GitLab still receives the artifact.
        /// </remarks>
        /// <param name="context">The context.</param>
        /// <param name="path">The solution, project or directory to analyze; the current directory if empty.</param>
        /// <param name="outputFile">The GitLab Code Quality report file.</param>
        /// <param name="reportSettings">The dotnet-outdated settings.</param>
        /// <param name="gitLabSettings">The GitLab Code Quality settings.</param>
        /// <example>
        /// <code>
        /// DotNetOutdatedGitLabCodeQuality(
        ///     ".",
        ///     "gl-code-quality-report.json",
        ///     new DotNetOutdatedReportSettings { Recursive = true, FailOnUpdates = true },
        ///     new GitLabCodeQualitySettings { MinimumUpgradeSeverity = DotNetOutdatedUpgradeSeverity.Minor });
        /// </code>
        /// </example>
        [CakeMethodAlias]
        [CakeAliasCategory("GitLab Code Quality")]
        [CakeNamespaceImport("Cake.DotNetOutdated")]
        [CakeNamespaceImport("Cake.DotNetOutdated.GitLab")]
        [CakeNamespaceImport("Cake.DotNetOutdated.Report")]
        public static void DotNetOutdatedGitLabCodeQuality(
            this ICakeContext context,
            string path,
            FilePath outputFile,
            DotNetOutdatedReportSettings reportSettings,
            GitLabCodeQualitySettings gitLabSettings)
        {
            ArgumentNullException.ThrowIfNull(context);

            new DotNetOutdatedGitLabCodeQualityRunner(context.FileSystem, context.Environment, context.ProcessRunner, context.Tools, context.Log)
                .Run(path, outputFile, reportSettings, gitLabSettings);
        }
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test Cake.DotNetOutdated.sln`
Expected: PASS on all three target frameworks, including the Stage 1 architecture test (nothing outside `GitLab/` references the GitLab namespace).

- [ ] **Step 5: Commit**

```bash
git add src tests
git commit -m "feat: add one-shot GitLab Code Quality alias and runner"
```

---

### Task 5: README, packaging and end-to-end verification

**Files:**
- Modify: `README.md`

**Interfaces:**
- Consumes: the finished add-in.
- Produces: user documentation and a verified package.

- [ ] **Step 1: Document the feature**

In `README.md`, insert the following section immediately before the `## License` heading:

````markdown
## GitLab Code Quality report

Turn the report into a [GitLab Code Quality](https://docs.gitlab.com/ci/testing/code_quality/) report, so outdated
dependencies show up as findings in merge requests.

### One call

```csharp
Task("Outdated").Does(() =>
{
    DotNetOutdatedGitLabCodeQuality(".", "gl-code-quality-report.json");
});
```

With settings:

```csharp
DotNetOutdatedGitLabCodeQuality(
    ".",
    "gl-code-quality-report.json",
    new DotNetOutdatedReportSettings { Recursive = true, FailOnUpdates = true },
    new GitLabCodeQualitySettings { MinimumUpgradeSeverity = DotNetOutdatedUpgradeSeverity.Minor });
```

The tool always writes JSON to a temporary file next to the report, which is removed afterwards; your settings object is
not modified. With `FailOnUpdates` the report is written **first** and the build then fails (exit code 2) unless
`HandleExitCode` accepts it, so GitLab still receives the artifact.

### Step by step

```csharp
DotNetOutdated(".", new DotNetOutdatedReportSettings { OutputFile = "outdated.json" });

var report = ReadDotNetOutdatedReport("outdated.json");
var issues = ConvertToGitLabCodeQuality(report, new GitLabCodeQualitySettings
{
    MajorSeverity = GitLabCodeQualitySeverity.Critical,
});
WriteGitLabCodeQualityReport(issues, "gl-code-quality-report.json");
```

### `.gitlab-ci.yml`

```yaml
outdated:
  script:
    - dotnet tool restore
    - dotnet cake --target=Outdated
  artifacts:
    when: always
    reports:
      codequality: gl-code-quality-report.json
```

### How findings are built

- One finding per package per *declaring file*. Multi-target projects are grouped (highest severity wins). With Central
  Package Management the finding is on `Directory.Packages.props`, so several projects that use the same package produce a
  single finding.
- Findings point at the real line: `PackageVersion`/`GlobalPackageReference` in the nearest `Directory.Packages.props`, else
  the `PackageReference` in the project file, else `Directory.Build.props`/`.targets`, else line 1 of the project file
  (for example for transitive dependencies).
- Severity: `Major` to `major`, `Minor` to `minor`, `Patch` and `Unknown` to `info` (all configurable); up-to-date
  dependencies are never reported. `MinimumUpgradeSeverity` drops the lower levels; `Unknown` is always reported.
- The fingerprint is a hash of the check name, the file path and the package name. It does not contain versions or line
  numbers, so a new NuGet release does not show up as "1 fixed, 1 new" in the merge request.
- Paths are relative to `RepositoryRoot` (default: the Cake working directory). A declaration outside the repository falls
  back to the project file; a project outside the repository is skipped with a warning.
- Limitations: MSBuild property definitions are not followed (the element declaring the package is located instead), and
  non-SDK `packages.config` projects fall back to line 1.
````


- [ ] **Step 2: Run the full test suite and pack**

```bash
dotnet test Cake.DotNetOutdated.sln
dotnet pack src/Cake.DotNetOutdated/Cake.DotNetOutdated.csproj -c Release -o artifacts
```

Expected: all tests pass on `net8.0`, `net9.0`, `net10.0`; `artifacts/Cake.DotNetOutdated.0.1.0.nupkg` is created. As in Stage 1, `unzip -l` shows the three `lib/<tfm>/` folders with `.dll` and `.xml`, and the nuspec lists tag `cake-addin` and no `Cake.Core` dependency.

- [ ] **Step 3: End-to-end verification with the real Cake runner and the real tool**

Use scratch directories outside the repository (network access to nuget.org is required). This checks what unit tests cannot: Cake's alias generation for the new aliases (including the `IReadOnlyList<>` return type), real dotnet-outdated output (absolute, OS-native paths), recursion with a relative path, and the locator against real files.

Set up a sandbox with two projects that use Central Package Management:

```bash
E2E=$(mktemp -d) && cd "$E2E"
dotnet new tool-manifest
dotnet tool install Cake.Tool --version 6.0.0
dotnet tool install dotnet-outdated-tool
mkdir app cpm
for name in app cpm; do echo 'Console.WriteLine("hi");' > $name/Program.cs; done
cat > app/app.csproj <<'XML'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Newtonsoft.Json" />
  </ItemGroup>
</Project>
XML
cat > cpm/cpm.csproj <<'XML'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Serilog" />
  </ItemGroup>
</Project>
XML
cat > Directory.Packages.props <<'XML'
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
  </PropertyGroup>
  <ItemGroup>
    <PackageVersion Include="Newtonsoft.Json" Version="12.0.1" />
    <PackageVersion Include="Serilog" Version="2.10.0" />
  </ItemGroup>
</Project>
XML
```

Create `build.cake` (replace `<REPO>` with the absolute path of the repository, using forward slashes and a drive letter on Windows, for example `C:/Dev/...`):

```csharp
#addin nuget:file:///<REPO>/artifacts/?package=Cake.DotNetOutdated&version=0.1.0

Task("Outdated").Does(() =>
{
    // one call
    DotNetOutdatedGitLabCodeQuality(".", "out/one-shot.json", new DotNetOutdatedReportSettings { Recursive = true }, null);

    // step by step
    DotNetOutdated(".", new DotNetOutdatedReportSettings { Recursive = true, OutputFile = "out/outdated.json" });
    var issues = ConvertToGitLabCodeQuality(ReadDotNetOutdatedReport("out/outdated.json"));
    WriteGitLabCodeQualityReport(issues, "out/step-by-step.json");
    Information("Findings: {0}", issues.Count);
});

Task("Gate").Does(() =>
{
    DotNetOutdatedGitLabCodeQuality(".", "out/gate.json", new DotNetOutdatedReportSettings { Recursive = true, FailOnUpdates = true }, null);
});

RunTarget(Argument("target", "Outdated"));
```

```bash
dotnet cake --target=Outdated
cat out/one-shot.json
diff out/one-shot.json out/step-by-step.json && echo "one-shot equals step-by-step"
ls out
dotnet cake --target=Gate; echo "cake exit=$?"; ls out/gate.json
```

Expected:
- `Outdated` succeeds and logs `Findings: 2`.
- `out/one-shot.json` is a JSON array with two findings, both `check_name` `outdated-package`, `severity` `major`, a 64-character hex `fingerprint`, and `location.path` `Directory.Packages.props` (both packages are declared centrally) with `lines.begin` `6` (Newtonsoft.Json) and `7` (Serilog). Paths use forward slashes and have no `./` and no drive letter.
- `diff` reports no differences; `ls out` shows `one-shot.json`, `outdated.json`, `step-by-step.json` and **no** `*.outdated.json` temporary file next to the reports.
- `Gate` fails (`cake exit=1`, log contains `exit code 2`) **and** `out/gate.json` exists with the same two findings.

Also check the two other cases in separate sandboxes (each with the same tool manifest and a `build.cake` whose first line is the `#addin` line above):
1. **Nothing outdated:** a single project with no package references; run `DotNetOutdatedGitLabCodeQuality(".", "out/empty.json", new DotNetOutdatedReportSettings { Recursive = true }, null)`. Expected: `out/empty.json` contains exactly `[]`.
2. **No central package management:** a single project `plain/plain.csproj` with `<PackageReference Include="Serilog" Version="2.10.0" />` on line 7 and `<PackageReference Include="Newtonsoft.Json" Version="12.0.1" />` on line 8. Expected: `location.path` is `plain/plain.csproj` with `lines.begin` `7` and `8`.

Already verified while writing this plan (Cake.Tool 6.0.0, dotnet-outdated 4.8.1, Windows): all of the above, with `Findings: 2`, lines 6 and 7 on `Directory.Packages.props`, `[]` for the empty case, lines 7 and 8 on `plain/plain.csproj`, and the gate failing with exit code 2 after writing the report. Expect the same results when you repeat it.

If any check fails, capture the exact output, fix the corresponding class, add a unit test that reproduces it, and repeat this step. Typical suspects: path separators or drive letters in `location.path` (`DotNetOutdatedGitLabConverter.ToRepositoryPath`), alias namespace imports (`[CakeNamespaceImport]` on `GitLabCodeQualityAliases`), and line numbers (`DependencyLocator`).

- [ ] **Step 4: Commit**

```bash
git add README.md
git commit -m "docs: document GitLab Code Quality report generation"
```
