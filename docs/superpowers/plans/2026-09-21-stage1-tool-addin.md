# Cake.DotNetOutdated Stage 1: Tool Add-in Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A Cake add-in that runs the `dotnet-outdated` tool in report and upgrade modes, and reads its JSON report into a typed model.

**Architecture:** A `DotNetOutdatedTool<TSettings>` base (derives from `Cake.Core`'s `Tool<T>`, runs `dotnet outdated`) builds the shared arguments. Two runners (`DotNetOutdatedReporter`, `DotNetOutdatedUpgrader`) add command-specific arguments. Cake aliases are split into one partial file per command. `Cake.DotNetOutdated.Report` holds the typed JSON model and reader that Stage 2 (GitLab) builds on.

**Tech Stack:** C# / .NET (`net8.0;net9.0;net10.0`), `Cake.Core` 6.0.0, `System.Text.Json`, xUnit v3 (`xunit.v3` 4.0.1) on Microsoft Testing Platform, `Cake.Testing` 6.0.0.

**Spec:** `docs/superpowers/specs/2026-09-21-cake-dotnetoutdated-design.md` (Sections 1 and 2). Stage 2 plan: `docs/superpowers/plans/2026-09-21-stage2-gitlab-code-quality.md`.

## Global Constraints

- Package id `Cake.DotNetOutdated`; one assembly; namespaces `Cake.DotNetOutdated` (runners, settings, aliases), `Cake.DotNetOutdated.Report` (JSON model/reader), `Cake.DotNetOutdated.GitLab` (Stage 2 only).
- `Cake.DotNetOutdated` and `Cake.DotNetOutdated.Report` must never reference `Cake.DotNetOutdated.GitLab` (enforced by an architecture test).
- Baseline `Cake.Core` **6.0.0**, referenced with `PrivateAssets="All"`. No reference to `Cake.Common`.
- Target frameworks `net8.0;net9.0;net10.0`.
- Package tag `cake-addin`; embedded `PackageIcon` (`icon.png`); XML documentation file generated and shipped; `[CakeAliasCategory]` on every alias.
- Do **not** derive from Cake's `DotNetTool<T>` (it drags `Verbosity`/`DiagnosticOutput`, which dotnet-outdated rejects). Derive from `Tool<T>`; the executable is `dotnet` and the first argument is `outdated`.
- Argument order: `outdated`, optional quoted path, shared options, command-specific options.
- With `Recursive`, a relative path is made absolute (against `settings.WorkingDirectory` or the Cake working directory) before it is passed. dotnet-outdated 4.8.1 fails with `MSB1009: Project file does not exist` on relative paths combined with `--recursive` (observed); absolute paths and non-recursive relative paths work.
- Exit codes are not remapped: exit code 2 with `FailOnUpdates` throws `CakeException`. Users opt out with `HandleExitCode = c => c is 0 or 2`.
- `--upgrade` is a single-or-no-value option: always emit `--upgrade:Auto` (attached value). `Prompt` is not exposed.
- Tests: xUnit v3 + `Cake.Testing` `ToolFixture`. `global.json` must select the Microsoft Testing Platform runner (required by the .NET 10 SDK's `dotnet test`).
- Shell notes for Windows: use the Bash tool for the commands below; do **not** call `python3` (Windows Store stub hangs).

## File Structure

```
global.json
Cake.DotNetOutdated.sln
src/Cake.DotNetOutdated/
  Cake.DotNetOutdated.csproj
  icon.png                              Cake Contrib icon (downloaded, Task 1)
  DotNetOutdatedEnums.cs                PreRelease / VersionLock / OutputFormat / CredentialLogLevel
  DotNetOutdatedSettings.cs             shared options
  DotNetOutdatedTool.cs                 base runner: locates `dotnet`, builds shared arguments
  DotNetOutdatedReportSettings.cs       report-only options
  DotNetOutdatedReporter.cs             report runner (+ output-file guarantees)
  DotNetOutdatedUpgradeSettings.cs      upgrade-only options
  DotNetOutdatedUpgrader.cs             upgrade runner
  DotNetOutdatedAliases.Report.cs       DotNetOutdated(...)
  DotNetOutdatedAliases.Upgrade.cs      DotNetOutdatedUpgrade(...)
  DotNetOutdatedAliases.ReadReport.cs   ReadDotNetOutdatedReport(...)
  Report/DotNetOutdatedUpgradeSeverity.cs
  Report/DotNetOutdatedReport.cs        Report / Project / TargetFramework / Dependency model
  Report/DotNetOutdatedReportReader.cs
tests/Cake.DotNetOutdated.Tests/
  Cake.DotNetOutdated.Tests.csproj
  Assertions.cs, FileSystemHelpers.cs
  Fixtures/…                            one fixture per runner
  *Tests.cs
```

---

### Task 1: Scaffolding, shared settings and base tool

**Files:**
- Create: `global.json`, `Cake.DotNetOutdated.sln`
- Create: `src/Cake.DotNetOutdated/Cake.DotNetOutdated.csproj`, `src/Cake.DotNetOutdated/icon.png`
- Create: `src/Cake.DotNetOutdated/DotNetOutdatedEnums.cs`, `DotNetOutdatedSettings.cs`, `DotNetOutdatedTool.cs`
- Create: `tests/Cake.DotNetOutdated.Tests/Cake.DotNetOutdated.Tests.csproj`, `Assertions.cs`, `Fixtures/DotNetOutdatedFixture.cs`, `Fixtures/SharedOptionsFixture.cs`, `DotNetOutdatedToolTests.cs`

**Interfaces:**
- Produces: `DotNetOutdatedSettings : ToolSettings` (all shared options below), enums `DotNetOutdatedPreRelease`, `DotNetOutdatedVersionLock`, `DotNetOutdatedOutputFormat`, `DotNetOutdatedCredentialLogLevel`; `abstract class DotNetOutdatedTool<TSettings> : Tool<TSettings> where TSettings : DotNetOutdatedSettings` with `protected ProcessArgumentBuilder CreateArgumentBuilder(string path, TSettings settings)`; test helpers `Assertions.IsCakeException(Exception, string)` and `Assertions.IsArgumentNullException(Exception, string)`; test base `DotNetOutdatedFixture<TSettings>` (property `Path`).

- [ ] **Step 1: Create a feature branch and commit the design docs**

```bash
git switch -c feature/stage1-tool-addin
git add docs
git commit -m "docs: add design spec and implementation plans"
```

- [ ] **Step 2: Scaffold the solution and projects**

**File:** `global.json`

```json
{
  "test": {
    "runner": "Microsoft.Testing.Platform"
  }
}
```

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
    <Description>Cake add-in for the dotnet-outdated tool: report or upgrade outdated NuGet packages and read the JSON report.</Description>
    <PackageTags>cake;cake-addin;cake-build;dotnet-outdated;nuget;outdated</PackageTags>
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
    <None Include="icon.png" Pack="true" PackagePath="\" />
    <None Include="..\..\README.md" Pack="true" PackagePath="\" />
  </ItemGroup>

</Project>
```

**File:** `tests/Cake.DotNetOutdated.Tests/Cake.DotNetOutdated.Tests.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFrameworks>net8.0;net9.0;net10.0</TargetFrameworks>
    <OutputType>Exe</OutputType>
    <UseMicrosoftTestingPlatformRunner>true</UseMicrosoftTestingPlatformRunner>
    <TestingPlatformDotnetTestSupport>true</TestingPlatformDotnetTestSupport>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="xunit.v3" Version="4.0.1" />
    <PackageReference Include="Cake.Testing" Version="6.0.0" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\Cake.DotNetOutdated\Cake.DotNetOutdated.csproj" />
  </ItemGroup>

  <ItemGroup>
    <Using Include="Xunit" />
  </ItemGroup>

</Project>
```

```bash
dotnet new sln --name Cake.DotNetOutdated --format sln
dotnet sln add src/Cake.DotNetOutdated/Cake.DotNetOutdated.csproj tests/Cake.DotNetOutdated.Tests/Cake.DotNetOutdated.Tests.csproj
curl -sSL -o src/Cake.DotNetOutdated/icon.png https://raw.githubusercontent.com/cake-contrib/graphics/master/png/cake-contrib-medium.png
```

Expected: the sln contains both projects; `icon.png` is a PNG (check with `file src/Cake.DotNetOutdated/icon.png`, expect "PNG image data").

- [ ] **Step 3: Write the test helpers and the failing tests**

**File:** `tests/Cake.DotNetOutdated.Tests/Assertions.cs`

```csharp
using Cake.Core;

namespace Cake.DotNetOutdated.Tests;

internal static class Assertions
{
    public static void IsCakeException(Exception exception, string expectedMessage)
    {
        var cakeException = Assert.IsType<CakeException>(exception);
        Assert.Equal(expectedMessage, cakeException.Message);
    }

    public static void IsArgumentNullException(Exception exception, string expectedParameterName)
    {
        var argumentNullException = Assert.IsType<ArgumentNullException>(exception);
        Assert.Equal(expectedParameterName, argumentNullException.ParamName);
    }
}
```

**File:** `tests/Cake.DotNetOutdated.Tests/Fixtures/DotNetOutdatedFixture.cs`

```csharp
using Cake.Core.IO;
using Cake.Core.Tooling;
using Cake.Testing.Fixtures;

namespace Cake.DotNetOutdated.Tests.Fixtures;

internal abstract class DotNetOutdatedFixture<TSettings> : ToolFixture<TSettings>
    where TSettings : ToolSettings, new()
{
    protected DotNetOutdatedFixture()
        : base("dotnet.exe")
    {
        ProcessRunner.Process.SetStandardOutput(new string[] { });
    }

    public string Path { get; set; }
}
```

**File:** `tests/Cake.DotNetOutdated.Tests/Fixtures/SharedOptionsFixture.cs`

```csharp
using Cake.Core;
using Cake.Core.IO;
using Cake.Core.Tooling;

namespace Cake.DotNetOutdated.Tests.Fixtures;

/// <summary>Test-only runner that exposes the shared argument building of the base tool.</summary>
internal sealed class SharedOptionsRunner : DotNetOutdatedTool<DotNetOutdatedSettings>
{
    public SharedOptionsRunner(IFileSystem fileSystem, ICakeEnvironment environment, IProcessRunner processRunner, IToolLocator tools)
        : base(fileSystem, environment, processRunner, tools)
    {
    }

    public void Execute(string path, DotNetOutdatedSettings settings)
    {
        Run(settings, CreateArgumentBuilder(path, settings));
    }
}

internal sealed class SharedOptionsFixture : DotNetOutdatedFixture<DotNetOutdatedSettings>
{
    protected override void RunTool()
    {
        new SharedOptionsRunner(FileSystem, Environment, ProcessRunner, Tools).Execute(Path, Settings);
    }
}
```

**File:** `tests/Cake.DotNetOutdated.Tests/DotNetOutdatedToolTests.cs`

```csharp
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
```

- [ ] **Step 4: Run the tests to verify they fail**

Run: `dotnet build Cake.DotNetOutdated.sln`
Expected: FAIL to compile, errors like `The type or namespace name 'DotNetOutdatedSettings' could not be found`.

- [ ] **Step 5: Write the enums, shared settings and base tool**

**File:** `src/Cake.DotNetOutdated/DotNetOutdatedEnums.cs`

```csharp
namespace Cake.DotNetOutdated
{
    /// <summary>
    /// Specifies whether pre-release package versions are considered (<c>--pre-release</c>).
    /// </summary>
    public enum DotNetOutdatedPreRelease
    {
        /// <summary>Consider pre-release versions only when the current version is a pre-release (tool default).</summary>
        Auto,

        /// <summary>Always look for pre-release versions.</summary>
        Always,

        /// <summary>Never look for pre-release versions.</summary>
        Never,
    }

    /// <summary>
    /// Specifies whether a package is locked to its current major or minor version (<c>--version-lock</c>).
    /// </summary>
    public enum DotNetOutdatedVersionLock
    {
        /// <summary>Do not lock the version (tool default).</summary>
        None,

        /// <summary>Lock to the current major version.</summary>
        Major,

        /// <summary>Lock to the current minor version.</summary>
        Minor,
    }

    /// <summary>
    /// Specifies the format of the generated report file (<c>--output-format</c>).
    /// </summary>
    public enum DotNetOutdatedOutputFormat
    {
        /// <summary>JSON (tool default).</summary>
        Json,

        /// <summary>Comma separated values.</summary>
        Csv,

        /// <summary>Markdown.</summary>
        Markdown,
    }

    /// <summary>
    /// Specifies the minimum log level of the NuGet credential service (<c>--nuget-cred-log-level</c>).
    /// </summary>
    public enum DotNetOutdatedCredentialLogLevel
    {
        /// <summary>Debug.</summary>
        Debug,

        /// <summary>Verbose.</summary>
        Verbose,

        /// <summary>Information.</summary>
        Information,

        /// <summary>Minimal.</summary>
        Minimal,

        /// <summary>Warning (tool default).</summary>
        Warning,

        /// <summary>Error.</summary>
        Error,
    }
}
```

**File:** `src/Cake.DotNetOutdated/DotNetOutdatedSettings.cs`

```csharp
using System.Collections.Generic;
using Cake.Core.Tooling;

namespace Cake.DotNetOutdated
{
    /// <summary>
    /// Contains the settings shared by all dotnet-outdated commands.
    /// </summary>
    public class DotNetOutdatedSettings : ToolSettings
    {
        /// <summary>
        /// Gets or sets a value indicating whether auto-referenced packages are included (<c>--include-auto-references</c>).
        /// </summary>
        public bool IncludeAutoReferences { get; set; }

        /// <summary>
        /// Gets or sets whether pre-release versions are considered (<c>--pre-release</c>).
        /// </summary>
        public DotNetOutdatedPreRelease? PreRelease { get; set; }

        /// <summary>
        /// Gets or sets a label that pre-release versions must start with, for example <c>rc.1</c> (<c>--pre-release-label</c>).
        /// </summary>
        public string PreReleaseLabel { get; set; }

        /// <summary>
        /// Gets or sets whether packages are locked to their current major or minor version (<c>--version-lock</c>).
        /// </summary>
        public DotNetOutdatedVersionLock? VersionLock { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether transitive dependencies are detected (<c>--transitive</c>).
        /// </summary>
        public bool Transitive { get; set; }

        /// <summary>
        /// Gets or sets how many levels deep transitive dependencies are analyzed (<c>--transitive-depth</c>).
        /// </summary>
        public int? TransitiveDepth { get; set; }

        /// <summary>
        /// Gets or sets the minimum age in days of a package version to be considered (<c>--older-than</c>).
        /// </summary>
        public int? OlderThan { get; set; }

        /// <summary>
        /// Gets or sets the inclusive maximum version to upgrade to, for example <c>8.0</c> (<c>--maximum-version</c>).
        /// </summary>
        public string MaximumVersion { get; set; }

        /// <summary>
        /// Gets or sets the package name filters to include; a package matches if its name contains any of them (<c>--include</c>).
        /// </summary>
        public ICollection<string> Include { get; set; } = new List<string>();

        /// <summary>
        /// Gets or sets the package name filters to exclude; a package is excluded if its name contains any of them (<c>--exclude</c>).
        /// </summary>
        public ICollection<string> Exclude { get; set; } = new List<string>();

        /// <summary>
        /// Gets or sets a value indicating whether projects are searched for recursively (<c>--recursive</c>).
        /// </summary>
        public bool Recursive { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether loose file-based apps are included in a recursive search (<c>--include-file-based-apps</c>).
        /// </summary>
        public bool IncludeFileBasedApps { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether package source failures are treated as warnings (<c>--ignore-failed-sources</c>).
        /// </summary>
        public bool IgnoreFailedSources { get; set; }

        /// <summary>
        /// Gets or sets the minimum log level of the NuGet credential service (<c>--nuget-cred-log-level</c>).
        /// </summary>
        public DotNetOutdatedCredentialLogLevel? NuGetCredentialLogLevel { get; set; }

        /// <summary>
        /// Gets or sets the runtime identifier used during restore (<c>--runtime</c>).
        /// </summary>
        public string Runtime { get; set; }

        /// <summary>
        /// Gets or sets the idle timeout in seconds to wait for output from dotnet before assuming it has hung (<c>--idle-timeout</c>).
        /// </summary>
        public int? IdleTimeout { get; set; }
    }
}
```

**File:** `src/Cake.DotNetOutdated/DotNetOutdatedTool.cs`

```csharp
using System;
using System.Collections.Generic;
using System.Globalization;
using Cake.Core;
using Cake.Core.IO;
using Cake.Core.Tooling;

namespace Cake.DotNetOutdated
{
    /// <summary>
    /// Base class for the dotnet-outdated commands. Runs <c>dotnet outdated</c>, which works for both
    /// global and local (manifest) tool installations.
    /// </summary>
    /// <typeparam name="TSettings">The settings type.</typeparam>
    public abstract class DotNetOutdatedTool<TSettings> : Tool<TSettings>
        where TSettings : DotNetOutdatedSettings
    {
        private readonly ICakeEnvironment _environment;

        /// <summary>
        /// Initializes a new instance of the <see cref="DotNetOutdatedTool{TSettings}" /> class.
        /// </summary>
        /// <param name="fileSystem">The file system.</param>
        /// <param name="environment">The environment.</param>
        /// <param name="processRunner">The process runner.</param>
        /// <param name="tools">The tool locator.</param>
        protected DotNetOutdatedTool(
            IFileSystem fileSystem,
            ICakeEnvironment environment,
            IProcessRunner processRunner,
            IToolLocator tools)
            : base(fileSystem, environment, processRunner, tools)
        {
            _environment = environment;
        }

        /// <summary>
        /// Gets the name of the tool.
        /// </summary>
        /// <returns>The name of the tool.</returns>
        protected override string GetToolName()
        {
            return "dotnet-outdated";
        }

        /// <summary>
        /// Gets the possible names of the tool executable.
        /// </summary>
        /// <returns>The tool executable names.</returns>
        protected override IEnumerable<string> GetToolExecutableNames()
        {
            return new[] { "dotnet", "dotnet.exe" };
        }

        /// <summary>
        /// Creates a <see cref="ProcessArgumentBuilder"/> containing the <c>outdated</c> command,
        /// the optional path and the options shared by all commands.
        /// </summary>
        /// <param name="path">The solution, project or directory to analyze; the current directory if empty.</param>
        /// <param name="settings">The settings.</param>
        /// <returns>The argument builder.</returns>
        protected ProcessArgumentBuilder CreateArgumentBuilder(string path, TSettings settings)
        {
            ArgumentNullException.ThrowIfNull(settings);

            var builder = new ProcessArgumentBuilder();
            builder.Append("outdated");

            var resolvedPath = ResolvePath(path, settings);
            if (!string.IsNullOrWhiteSpace(resolvedPath))
            {
                builder.AppendQuoted(resolvedPath);
            }

            if (settings.IncludeAutoReferences)
            {
                builder.Append("--include-auto-references");
            }

            if (settings.PreRelease.HasValue)
            {
                builder.Append("--pre-release");
                builder.Append(settings.PreRelease.Value.ToString());
            }

            if (!string.IsNullOrWhiteSpace(settings.PreReleaseLabel))
            {
                builder.Append("--pre-release-label");
                builder.AppendQuoted(settings.PreReleaseLabel);
            }

            if (settings.VersionLock.HasValue)
            {
                builder.Append("--version-lock");
                builder.Append(settings.VersionLock.Value.ToString());
            }

            if (settings.Transitive)
            {
                builder.Append("--transitive");
            }

            if (settings.TransitiveDepth.HasValue)
            {
                builder.Append("--transitive-depth");
                builder.Append(settings.TransitiveDepth.Value.ToString(CultureInfo.InvariantCulture));
            }

            if (settings.OlderThan.HasValue)
            {
                builder.Append("--older-than");
                builder.Append(settings.OlderThan.Value.ToString(CultureInfo.InvariantCulture));
            }

            if (!string.IsNullOrWhiteSpace(settings.MaximumVersion))
            {
                builder.Append("--maximum-version");
                builder.AppendQuoted(settings.MaximumVersion);
            }

            AppendFilters(builder, "--include", settings.Include);
            AppendFilters(builder, "--exclude", settings.Exclude);

            if (settings.Recursive)
            {
                builder.Append("--recursive");
            }

            if (settings.IncludeFileBasedApps)
            {
                builder.Append("--include-file-based-apps");
            }

            if (settings.IgnoreFailedSources)
            {
                builder.Append("--ignore-failed-sources");
            }

            if (settings.NuGetCredentialLogLevel.HasValue)
            {
                builder.Append("--nuget-cred-log-level");
                builder.Append(settings.NuGetCredentialLogLevel.Value.ToString().ToLowerInvariant());
            }

            if (!string.IsNullOrWhiteSpace(settings.Runtime))
            {
                builder.Append("--runtime");
                builder.AppendQuoted(settings.Runtime);
            }

            if (settings.IdleTimeout.HasValue)
            {
                builder.Append("--idle-timeout");
                builder.Append(settings.IdleTimeout.Value.ToString(CultureInfo.InvariantCulture));
            }

            return builder;
        }

        private string ResolvePath(string path, TSettings settings)
        {
            // dotnet-outdated 4.8.1 fails ("Project file does not exist") to load the projects it discovers when a
            // relative path is combined with --recursive. An absolute path works, so hand it one, resolved against the
            // directory the process is started in.
            if (!settings.Recursive || string.IsNullOrWhiteSpace(path))
            {
                return path;
            }

            var workingDirectory = (settings.WorkingDirectory ?? _environment.WorkingDirectory).MakeAbsolute(_environment);
            return new DirectoryPath(path).MakeAbsolute(workingDirectory).FullPath;
        }

        private static void AppendFilters(ProcessArgumentBuilder builder, string name, ICollection<string> filters)
        {
            if (filters == null)
            {
                return;
            }

            foreach (var filter in filters)
            {
                if (string.IsNullOrWhiteSpace(filter))
                {
                    continue;
                }

                builder.Append(name);
                builder.AppendQuoted(filter);
            }
        }
    }
}
```

- [ ] **Step 6: Run the tests to verify they pass**

Run: `dotnet test Cake.DotNetOutdated.sln`
Expected: PASS on `net8.0`, `net9.0` and `net10.0` (`Test run summary: Passed!`, 0 failed).

- [ ] **Step 7: Commit**

```bash
git add global.json Cake.DotNetOutdated.sln src tests
git commit -m "feat: add solution scaffolding, shared settings and base tool"
```

---

### Task 2: Report command

**Files:**
- Create: `src/Cake.DotNetOutdated/DotNetOutdatedReportSettings.cs`, `DotNetOutdatedReporter.cs`, `DotNetOutdatedAliases.Report.cs`
- Create: `tests/Cake.DotNetOutdated.Tests/FileSystemHelpers.cs`, `Fixtures/ReporterFixture.cs`, `DotNetOutdatedReporterTests.cs`

**Interfaces:**
- Consumes: `DotNetOutdatedTool<TSettings>.CreateArgumentBuilder(string path, TSettings settings)`, `DotNetOutdatedFixture<TSettings>`, `Assertions` (Task 1).
- Produces: `DotNetOutdatedReportSettings : DotNetOutdatedSettings` with `FilePath OutputFile`, `DotNetOutdatedOutputFormat? OutputFormat`, `bool IncludeUpToDate`, `bool FailOnUpdates`; `DotNetOutdatedReporter(IFileSystem, ICakeEnvironment, IProcessRunner, IToolLocator)` with `void Report(string path, DotNetOutdatedReportSettings settings)`; aliases `DotNetOutdated(this ICakeContext, string path[, DotNetOutdatedReportSettings settings])`; test helpers `FileSystemHelpers.ReadAllText(this IFileSystem, string path)` and `FileSystemHelpers.WriteAllBytes(this IFileSystem, string path, byte[] content)`.
- Behavior guarantees (spec Section 2): the runner deletes an existing `OutputFile` before running and creates its directory; after a run with exit code 0 and JSON output, if the tool wrote no file, it writes `{"Projects": []}`.

- [ ] **Step 1: Write the failing tests**

**File:** `tests/Cake.DotNetOutdated.Tests/FileSystemHelpers.cs`

```csharp
using System.Text;
using Cake.Core.IO;
using Cake.Testing;

namespace Cake.DotNetOutdated.Tests;

internal static class FileSystemHelpers
{
    public static string ReadAllText(this IFileSystem fileSystem, string path)
    {
        using var stream = fileSystem.GetFile(path).OpenRead();
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }

    public static void WriteAllBytes(this IFileSystem fileSystem, string path, byte[] content)
    {
        fileSystem.GetDirectory(new FilePath(path).GetDirectory()).Create();
        using var stream = fileSystem.GetFile(path).Open(FileMode.Create, FileAccess.Write, FileShare.None);
        stream.Write(content, 0, content.Length);
    }
}
```

**File:** `tests/Cake.DotNetOutdated.Tests/Fixtures/ReporterFixture.cs`

```csharp
namespace Cake.DotNetOutdated.Tests.Fixtures;

internal sealed class ReporterFixture : DotNetOutdatedFixture<DotNetOutdatedReportSettings>
{
    protected override void RunTool()
    {
        new DotNetOutdatedReporter(FileSystem, Environment, ProcessRunner, Tools).Report(Path, Settings);
    }
}
```

**File:** `tests/Cake.DotNetOutdated.Tests/DotNetOutdatedReporterTests.cs`

```csharp
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
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet build Cake.DotNetOutdated.sln`
Expected: FAIL to compile (`DotNetOutdatedReportSettings` / `DotNetOutdatedReporter` do not exist).

- [ ] **Step 3: Write the report settings, runner and aliases**

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
    }
}
```

**File:** `src/Cake.DotNetOutdated/DotNetOutdatedReporter.cs`

```csharp
using System;
using System.IO;
using System.Text;
using Cake.Core;
using Cake.Core.IO;
using Cake.Core.Tooling;

namespace Cake.DotNetOutdated
{
    /// <summary>
    /// The dotnet-outdated report runner: lists outdated packages and optionally saves a report.
    /// </summary>
    public sealed class DotNetOutdatedReporter : DotNetOutdatedTool<DotNetOutdatedReportSettings>
    {
        private const string EmptyJsonReport = "{\n  \"Projects\": []\n}";

        private readonly IFileSystem _fileSystem;
        private readonly ICakeEnvironment _environment;

        /// <summary>
        /// Initializes a new instance of the <see cref="DotNetOutdatedReporter" /> class.
        /// </summary>
        /// <param name="fileSystem">The file system.</param>
        /// <param name="environment">The environment.</param>
        /// <param name="processRunner">The process runner.</param>
        /// <param name="tools">The tool locator.</param>
        public DotNetOutdatedReporter(
            IFileSystem fileSystem,
            ICakeEnvironment environment,
            IProcessRunner processRunner,
            IToolLocator tools)
            : base(fileSystem, environment, processRunner, tools)
        {
            _fileSystem = fileSystem;
            _environment = environment;
        }

        /// <summary>
        /// Reports outdated packages.
        /// </summary>
        /// <remarks>
        /// When an output file is configured, an existing file is deleted first (so a stale report cannot be misread)
        /// and its directory is created. dotnet-outdated writes no file when nothing is outdated, so after a successful
        /// run with JSON output an empty report is written in that case.
        /// </remarks>
        /// <param name="path">The solution, project or directory to analyze; the current directory if empty.</param>
        /// <param name="settings">The settings.</param>
        public void Report(string path, DotNetOutdatedReportSettings settings)
        {
            ArgumentNullException.ThrowIfNull(settings);

            var outputFile = settings.OutputFile?.MakeAbsolute(_environment);
            if (outputFile != null)
            {
                PrepareOutputFile(outputFile);
            }

            Run(settings, GetArguments(path, settings, outputFile), null, process => AfterRun(process, settings, outputFile));
        }

        private ProcessArgumentBuilder GetArguments(string path, DotNetOutdatedReportSettings settings, FilePath outputFile)
        {
            var builder = CreateArgumentBuilder(path, settings);

            if (settings.IncludeUpToDate)
            {
                builder.Append("--include-up-to-date");
            }

            if (settings.FailOnUpdates)
            {
                builder.Append("--fail-on-updates");
            }

            if (outputFile != null)
            {
                builder.Append("--output");
                builder.AppendQuoted(outputFile.FullPath);
            }

            if (settings.OutputFormat.HasValue)
            {
                builder.Append("--output-format");
                builder.Append(settings.OutputFormat.Value.ToString().ToLowerInvariant());
            }

            return builder;
        }

        private void PrepareOutputFile(FilePath outputFile)
        {
            var file = _fileSystem.GetFile(outputFile);
            if (file.Exists)
            {
                file.Delete();
            }

            var directory = _fileSystem.GetDirectory(outputFile.GetDirectory());
            if (!directory.Exists)
            {
                directory.Create();
            }
        }

        private void AfterRun(IProcess process, DotNetOutdatedReportSettings settings, FilePath outputFile)
        {
            settings.PostAction?.Invoke(process);

            if (outputFile == null || process.GetExitCode() != 0)
            {
                return;
            }

            if ((settings.OutputFormat ?? DotNetOutdatedOutputFormat.Json) != DotNetOutdatedOutputFormat.Json)
            {
                return;
            }

            var file = _fileSystem.GetFile(outputFile);
            if (file.Exists)
            {
                return;
            }

            using var stream = file.Open(FileMode.Create, FileAccess.Write, FileShare.None);
            using var writer = new StreamWriter(stream, new UTF8Encoding(false));
            writer.Write(EmptyJsonReport);
        }
    }
}
```

**File:** `src/Cake.DotNetOutdated/DotNetOutdatedAliases.Report.cs`

```csharp
using System;
using Cake.Core;
using Cake.Core.Annotations;

namespace Cake.DotNetOutdated
{
    /// <summary>
    /// Contains functionality for running the dotnet-outdated tool.
    /// </summary>
    public static partial class DotNetOutdatedAliases
    {
        /// <summary>
        /// Reports outdated NuGet packages of a solution, project or directory using dotnet-outdated.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="path">The solution, project or directory to analyze; the current directory if empty.</param>
        /// <example>
        /// <code>
        /// DotNetOutdated(".");
        /// </code>
        /// </example>
        [CakeMethodAlias]
        [CakeAliasCategory("DotNetOutdated")]
        [CakeNamespaceImport("Cake.DotNetOutdated")]
        public static void DotNetOutdated(this ICakeContext context, string path)
        {
            context.DotNetOutdated(path, null);
        }

        /// <summary>
        /// Reports outdated NuGet packages of a solution, project or directory using dotnet-outdated
        /// and the specified settings.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="path">The solution, project or directory to analyze; the current directory if empty.</param>
        /// <param name="settings">The settings.</param>
        /// <example>
        /// <code>
        /// DotNetOutdated(".", new DotNetOutdatedReportSettings
        /// {
        ///     OutputFile = "artifacts/outdated.json",
        ///     OutputFormat = DotNetOutdatedOutputFormat.Json,
        ///     IncludeUpToDate = false
        /// });
        /// </code>
        /// </example>
        [CakeMethodAlias]
        [CakeAliasCategory("DotNetOutdated")]
        [CakeNamespaceImport("Cake.DotNetOutdated")]
        public static void DotNetOutdated(this ICakeContext context, string path, DotNetOutdatedReportSettings settings)
        {
            ArgumentNullException.ThrowIfNull(context);

            settings ??= new DotNetOutdatedReportSettings();

            var reporter = new DotNetOutdatedReporter(context.FileSystem, context.Environment, context.ProcessRunner, context.Tools);
            reporter.Report(path, settings);
        }
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test Cake.DotNetOutdated.sln`
Expected: PASS on all three target frameworks.

- [ ] **Step 5: Commit**

```bash
git add src tests
git commit -m "feat: add dotnet-outdated report command"
```

---

### Task 3: Upgrade command

**Files:**
- Create: `src/Cake.DotNetOutdated/DotNetOutdatedUpgradeSettings.cs`, `DotNetOutdatedUpgrader.cs`, `DotNetOutdatedAliases.Upgrade.cs`
- Create: `tests/Cake.DotNetOutdated.Tests/Fixtures/UpgraderFixture.cs`, `DotNetOutdatedUpgraderTests.cs`

**Interfaces:**
- Consumes: `DotNetOutdatedTool<TSettings>.CreateArgumentBuilder`, `DotNetOutdatedFixture<TSettings>`, `Assertions` (Task 1).
- Produces: `DotNetOutdatedUpgradeSettings : DotNetOutdatedSettings` with `bool NoRestore`; `DotNetOutdatedUpgrader` with `void Upgrade(string path, DotNetOutdatedUpgradeSettings settings)`; aliases `DotNetOutdatedUpgrade(this ICakeContext, string path[, DotNetOutdatedUpgradeSettings settings])`.

- [ ] **Step 1: Write the failing tests**

**File:** `tests/Cake.DotNetOutdated.Tests/Fixtures/UpgraderFixture.cs`

```csharp
namespace Cake.DotNetOutdated.Tests.Fixtures;

internal sealed class UpgraderFixture : DotNetOutdatedFixture<DotNetOutdatedUpgradeSettings>
{
    protected override void RunTool()
    {
        new DotNetOutdatedUpgrader(FileSystem, Environment, ProcessRunner, Tools).Upgrade(Path, Settings);
    }
}
```

**File:** `tests/Cake.DotNetOutdated.Tests/DotNetOutdatedUpgraderTests.cs`

```csharp
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
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet build Cake.DotNetOutdated.sln`
Expected: FAIL to compile (`DotNetOutdatedUpgradeSettings` / `DotNetOutdatedUpgrader` do not exist).

- [ ] **Step 3: Write the upgrade settings, runner and aliases**

**File:** `src/Cake.DotNetOutdated/DotNetOutdatedUpgradeSettings.cs`

```csharp
namespace Cake.DotNetOutdated
{
    /// <summary>
    /// Contains the settings used by the upgrade command (<c>dotnet outdated --upgrade</c>).
    /// </summary>
    public class DotNetOutdatedUpgradeSettings : DotNetOutdatedSettings
    {
        /// <summary>
        /// Gets or sets a value indicating whether packages are upgraded without a restore preview
        /// and compatibility check (<c>--no-restore</c>).
        /// </summary>
        public bool NoRestore { get; set; }
    }
}
```

**File:** `src/Cake.DotNetOutdated/DotNetOutdatedUpgrader.cs`

```csharp
using System;
using Cake.Core;
using Cake.Core.IO;
using Cake.Core.Tooling;

namespace Cake.DotNetOutdated
{
    /// <summary>
    /// The dotnet-outdated upgrade runner: upgrades outdated packages automatically.
    /// </summary>
    public sealed class DotNetOutdatedUpgrader : DotNetOutdatedTool<DotNetOutdatedUpgradeSettings>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DotNetOutdatedUpgrader" /> class.
        /// </summary>
        /// <param name="fileSystem">The file system.</param>
        /// <param name="environment">The environment.</param>
        /// <param name="processRunner">The process runner.</param>
        /// <param name="tools">The tool locator.</param>
        public DotNetOutdatedUpgrader(
            IFileSystem fileSystem,
            ICakeEnvironment environment,
            IProcessRunner processRunner,
            IToolLocator tools)
            : base(fileSystem, environment, processRunner, tools)
        {
        }

        /// <summary>
        /// Upgrades outdated packages.
        /// </summary>
        /// <param name="path">The solution, project or directory to analyze; the current directory if empty.</param>
        /// <param name="settings">The settings.</param>
        public void Upgrade(string path, DotNetOutdatedUpgradeSettings settings)
        {
            ArgumentNullException.ThrowIfNull(settings);

            Run(settings, GetArguments(path, settings));
        }

        private ProcessArgumentBuilder GetArguments(string path, DotNetOutdatedUpgradeSettings settings)
        {
            var builder = CreateArgumentBuilder(path, settings);

            // --upgrade takes an optional value; it must be attached with ':' or the next token is read as the path.
            // Only Auto is supported: Prompt is interactive and would hang a build.
            builder.Append("--upgrade:Auto");

            if (settings.NoRestore)
            {
                builder.Append("--no-restore");
            }

            return builder;
        }
    }
}
```

**File:** `src/Cake.DotNetOutdated/DotNetOutdatedAliases.Upgrade.cs`

```csharp
using System;
using Cake.Core;
using Cake.Core.Annotations;

namespace Cake.DotNetOutdated
{
    public static partial class DotNetOutdatedAliases
    {
        /// <summary>
        /// Upgrades outdated NuGet packages of a solution, project or directory using dotnet-outdated.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="path">The solution, project or directory to upgrade; the current directory if empty.</param>
        /// <example>
        /// <code>
        /// DotNetOutdatedUpgrade("./src/App.sln");
        /// </code>
        /// </example>
        [CakeMethodAlias]
        [CakeAliasCategory("DotNetOutdated")]
        [CakeNamespaceImport("Cake.DotNetOutdated")]
        public static void DotNetOutdatedUpgrade(this ICakeContext context, string path)
        {
            context.DotNetOutdatedUpgrade(path, null);
        }

        /// <summary>
        /// Upgrades outdated NuGet packages of a solution, project or directory using dotnet-outdated
        /// and the specified settings.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="path">The solution, project or directory to upgrade; the current directory if empty.</param>
        /// <param name="settings">The settings.</param>
        /// <example>
        /// <code>
        /// DotNetOutdatedUpgrade("./src/App.sln", new DotNetOutdatedUpgradeSettings
        /// {
        ///     VersionLock = DotNetOutdatedVersionLock.Major,
        ///     NoRestore = true
        /// });
        /// </code>
        /// </example>
        [CakeMethodAlias]
        [CakeAliasCategory("DotNetOutdated")]
        [CakeNamespaceImport("Cake.DotNetOutdated")]
        public static void DotNetOutdatedUpgrade(this ICakeContext context, string path, DotNetOutdatedUpgradeSettings settings)
        {
            ArgumentNullException.ThrowIfNull(context);

            settings ??= new DotNetOutdatedUpgradeSettings();

            var upgrader = new DotNetOutdatedUpgrader(context.FileSystem, context.Environment, context.ProcessRunner, context.Tools);
            upgrader.Upgrade(path, settings);
        }
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test Cake.DotNetOutdated.sln`
Expected: PASS on all three target frameworks.

- [ ] **Step 5: Commit**

```bash
git add src tests
git commit -m "feat: add dotnet-outdated upgrade command"
```

---

### Task 4: Report model and reader

**Files:**
- Create: `src/Cake.DotNetOutdated/Report/DotNetOutdatedUpgradeSeverity.cs`, `Report/DotNetOutdatedReport.cs`, `Report/DotNetOutdatedReportReader.cs`, `DotNetOutdatedAliases.ReadReport.cs`
- Create: `tests/Cake.DotNetOutdated.Tests/DotNetOutdatedReportReaderTests.cs`

**Interfaces:**
- Consumes: `FileSystemHelpers.WriteAllBytes` (Task 2), `Assertions` (Task 1).
- Produces (namespace `Cake.DotNetOutdated.Report`, used by Stage 2): enum `DotNetOutdatedUpgradeSeverity { None, Patch, Minor, Major, Unknown }`; `DotNetOutdatedReport { IReadOnlyList<DotNetOutdatedProject> Projects }`; `DotNetOutdatedProject { string Name, string FilePath, IReadOnlyList<DotNetOutdatedTargetFramework> TargetFrameworks }`; `DotNetOutdatedTargetFramework { string Name, IReadOnlyList<DotNetOutdatedDependency> Dependencies }`; `DotNetOutdatedDependency { string Name, string ResolvedVersion, string LatestVersion, DotNetOutdatedUpgradeSeverity UpgradeSeverity }` (all `init` properties); `DotNetOutdatedReportReader(IFileSystem, ICakeEnvironment)` with `DotNetOutdatedReport Read(FilePath)` and `static DotNetOutdatedReport Parse(string json)`; alias `ReadDotNetOutdatedReport(this ICakeContext, FilePath path)`.

- [ ] **Step 1: Write the failing tests**

**File:** `tests/Cake.DotNetOutdated.Tests/DotNetOutdatedReportReaderTests.cs`

```csharp
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
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet build Cake.DotNetOutdated.sln`
Expected: FAIL to compile (`Cake.DotNetOutdated.Report` does not exist).

- [ ] **Step 3: Write the model, reader and alias**

**File:** `src/Cake.DotNetOutdated/Report/DotNetOutdatedUpgradeSeverity.cs`

```csharp
namespace Cake.DotNetOutdated.Report
{
    /// <summary>
    /// The severity of an available upgrade, as reported by dotnet-outdated.
    /// </summary>
    public enum DotNetOutdatedUpgradeSeverity
    {
        /// <summary>No upgrade is available.</summary>
        None,

        /// <summary>A patch upgrade is available.</summary>
        Patch,

        /// <summary>A minor upgrade is available.</summary>
        Minor,

        /// <summary>A major upgrade is available.</summary>
        Major,

        /// <summary>The severity could not be determined.</summary>
        Unknown,
    }
}
```

**File:** `src/Cake.DotNetOutdated/Report/DotNetOutdatedReport.cs`

```csharp
using System;
using System.Collections.Generic;

namespace Cake.DotNetOutdated.Report
{
    /// <summary>
    /// The JSON report written by dotnet-outdated (<c>--output-format json</c>).
    /// </summary>
    public sealed class DotNetOutdatedReport
    {
        /// <summary>
        /// Gets the analyzed projects that have outdated dependencies.
        /// </summary>
        public IReadOnlyList<DotNetOutdatedProject> Projects { get; init; } = Array.Empty<DotNetOutdatedProject>();
    }

    /// <summary>
    /// A project in a <see cref="DotNetOutdatedReport"/>.
    /// </summary>
    public sealed class DotNetOutdatedProject
    {
        /// <summary>
        /// Gets the project name.
        /// </summary>
        public string Name { get; init; }

        /// <summary>
        /// Gets the full path of the project file.
        /// </summary>
        public string FilePath { get; init; }

        /// <summary>
        /// Gets the analyzed target frameworks.
        /// </summary>
        public IReadOnlyList<DotNetOutdatedTargetFramework> TargetFrameworks { get; init; } = Array.Empty<DotNetOutdatedTargetFramework>();
    }

    /// <summary>
    /// A target framework of a <see cref="DotNetOutdatedProject"/>.
    /// </summary>
    public sealed class DotNetOutdatedTargetFramework
    {
        /// <summary>
        /// Gets the target framework name, for example <c>net8.0</c>.
        /// </summary>
        public string Name { get; init; }

        /// <summary>
        /// Gets the dependencies of the target framework.
        /// </summary>
        public IReadOnlyList<DotNetOutdatedDependency> Dependencies { get; init; } = Array.Empty<DotNetOutdatedDependency>();
    }

    /// <summary>
    /// A NuGet dependency of a <see cref="DotNetOutdatedTargetFramework"/>.
    /// </summary>
    public sealed class DotNetOutdatedDependency
    {
        /// <summary>
        /// Gets the package id.
        /// </summary>
        public string Name { get; init; }

        /// <summary>
        /// Gets the currently resolved version.
        /// </summary>
        public string ResolvedVersion { get; init; }

        /// <summary>
        /// Gets the latest available version, or <c>null</c> if it could not be determined.
        /// </summary>
        public string LatestVersion { get; init; }

        /// <summary>
        /// Gets the severity of the available upgrade.
        /// </summary>
        public DotNetOutdatedUpgradeSeverity UpgradeSeverity { get; init; }
    }
}
```

**File:** `src/Cake.DotNetOutdated/Report/DotNetOutdatedReportReader.cs`

```csharp
using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Cake.Core;
using Cake.Core.IO;

namespace Cake.DotNetOutdated.Report
{
    /// <summary>
    /// Reads the JSON report written by dotnet-outdated.
    /// </summary>
    public sealed class DotNetOutdatedReportReader
    {
        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new UpgradeSeverityConverter() },
        };

        private readonly IFileSystem _fileSystem;
        private readonly ICakeEnvironment _environment;

        /// <summary>
        /// Initializes a new instance of the <see cref="DotNetOutdatedReportReader" /> class.
        /// </summary>
        /// <param name="fileSystem">The file system.</param>
        /// <param name="environment">The environment.</param>
        public DotNetOutdatedReportReader(IFileSystem fileSystem, ICakeEnvironment environment)
        {
            ArgumentNullException.ThrowIfNull(fileSystem);
            ArgumentNullException.ThrowIfNull(environment);

            _fileSystem = fileSystem;
            _environment = environment;
        }

        /// <summary>
        /// Reads a report file.
        /// </summary>
        /// <param name="path">The report file; relative paths are resolved against the working directory.</param>
        /// <returns>The report.</returns>
        public DotNetOutdatedReport Read(FilePath path)
        {
            ArgumentNullException.ThrowIfNull(path);

            var file = _fileSystem.GetFile(path.MakeAbsolute(_environment));
            if (!file.Exists)
            {
                throw new FileNotFoundException(
                    $"The dotnet-outdated report '{file.Path.FullPath}' could not be found.",
                    file.Path.FullPath);
            }

            using var stream = file.OpenRead();
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            return Parse(reader.ReadToEnd());
        }

        /// <summary>
        /// Parses report JSON.
        /// </summary>
        /// <param name="json">The JSON text.</param>
        /// <returns>The report.</returns>
        public static DotNetOutdatedReport Parse(string json)
        {
            ArgumentNullException.ThrowIfNull(json);

            try
            {
                return JsonSerializer.Deserialize<DotNetOutdatedReport>(json, Options) ?? new DotNetOutdatedReport();
            }
            catch (JsonException exception)
            {
                throw new CakeException("The dotnet-outdated report is not valid JSON: " + exception.Message, exception);
            }
        }

        private sealed class UpgradeSeverityConverter : JsonConverter<DotNetOutdatedUpgradeSeverity>
        {
            public override DotNetOutdatedUpgradeSeverity Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                if (reader.TokenType == JsonTokenType.String
                    && Enum.TryParse(reader.GetString(), true, out DotNetOutdatedUpgradeSeverity value)
                    && Enum.IsDefined(value))
                {
                    return value;
                }

                if (reader.TokenType is JsonTokenType.StartObject or JsonTokenType.StartArray)
                {
                    reader.Skip();
                }

                return DotNetOutdatedUpgradeSeverity.Unknown;
            }

            public override void Write(Utf8JsonWriter writer, DotNetOutdatedUpgradeSeverity value, JsonSerializerOptions options)
            {
                writer.WriteStringValue(value.ToString());
            }
        }
    }
}
```

**File:** `src/Cake.DotNetOutdated/DotNetOutdatedAliases.ReadReport.cs`

```csharp
using System;
using Cake.Core;
using Cake.Core.Annotations;
using Cake.Core.IO;
using Cake.DotNetOutdated.Report;

namespace Cake.DotNetOutdated
{
    public static partial class DotNetOutdatedAliases
    {
        /// <summary>
        /// Reads a JSON report written by dotnet-outdated.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="path">The report file.</param>
        /// <returns>The report.</returns>
        /// <example>
        /// <code>
        /// DotNetOutdated(".", new DotNetOutdatedReportSettings { OutputFile = "outdated.json" });
        /// var report = ReadDotNetOutdatedReport("outdated.json");
        /// var majorUpdates = report.Projects
        ///     .SelectMany(p => p.TargetFrameworks)
        ///     .SelectMany(f => f.Dependencies)
        ///     .Count(d => d.UpgradeSeverity == DotNetOutdatedUpgradeSeverity.Major);
        /// </code>
        /// </example>
        [CakeMethodAlias]
        [CakeAliasCategory("DotNetOutdated Report")]
        [CakeNamespaceImport("Cake.DotNetOutdated.Report")]
        public static DotNetOutdatedReport ReadDotNetOutdatedReport(this ICakeContext context, FilePath path)
        {
            ArgumentNullException.ThrowIfNull(context);

            return new DotNetOutdatedReportReader(context.FileSystem, context.Environment).Read(path);
        }
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test Cake.DotNetOutdated.sln`
Expected: PASS on all three target frameworks.

- [ ] **Step 5: Commit**

```bash
git add src tests
git commit -m "feat: add typed dotnet-outdated JSON report model and reader"
```

---

### Task 5: Architecture guard, README, package and end-to-end verification

**Files:**
- Create: `tests/Cake.DotNetOutdated.Tests/ArchitectureTests.cs`
- Modify: `README.md`

**Interfaces:**
- Consumes: the whole add-in.
- Produces: a guard that fails when `Cake.DotNetOutdated` or `Cake.DotNetOutdated.Report` source references the `GitLab` namespace (spec: "nothing depends on `.GitLab`"); a documented, verified package.

- [ ] **Step 1: Write the architecture test**

**File:** `tests/Cake.DotNetOutdated.Tests/ArchitectureTests.cs`

```csharp
namespace Cake.DotNetOutdated.Tests;

public sealed class ArchitectureTests
{
    private static DirectoryInfo FindSourceDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "Cake.DotNetOutdated.sln")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return new DirectoryInfo(Path.Combine(directory.FullName, "src", "Cake.DotNetOutdated"));
    }

    [Fact]
    public void Core_And_Report_Namespaces_Must_Not_Reference_GitLab()
    {
        var source = FindSourceDirectory();
        var gitLabDirectory = Path.Combine(source.FullName, "GitLab") + Path.DirectorySeparatorChar;

        var offenders = source
            .EnumerateFiles("*.cs", SearchOption.AllDirectories)
            .Where(file => !file.FullName.StartsWith(gitLabDirectory, StringComparison.OrdinalIgnoreCase))
            .Where(file => !file.FullName.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar))
            .Where(file => File.ReadAllText(file.FullName).Contains("Cake.DotNetOutdated.GitLab"))
            .Select(file => Path.GetRelativePath(source.FullName, file.FullName))
            .ToList();

        Assert.True(offenders.Count == 0, "These files reference the GitLab namespace: " + string.Join(", ", offenders));
    }

    [Fact]
    public void The_Source_Directory_Must_Contain_The_Add_In_Sources()
    {
        var source = FindSourceDirectory();

        Assert.True(source.EnumerateFiles("DotNetOutdatedTool.cs", SearchOption.TopDirectoryOnly).Any());
    }
}
```

- [ ] **Step 2: Run the tests**

Run: `dotnet test Cake.DotNetOutdated.sln`
Expected: PASS (no GitLab code exists yet, so the guard passes; the second test proves the guard is not vacuous by locating the sources).

- [ ] **Step 3: Write the README**

Replace the contents of `README.md` with:

````markdown
# Cake.DotNetOutdated

A [Cake](https://cakebuild.net) add-in for [dotnet-outdated](https://github.com/dotnet-outdated/dotnet-outdated):
report or upgrade outdated NuGet packages from your build script, and read the JSON report as a typed model.

## Prerequisites

`dotnet-outdated` must be installed, globally or as a local tool (`dotnet tool install dotnet-outdated-tool`).
The add-in runs `dotnet outdated`, so both installation styles work.

## Installation

```csharp
#addin nuget:?package=Cake.DotNetOutdated
```

Targets Cake 6.0.0 and later (`net8.0`, `net9.0`, `net10.0`).

## Report outdated packages

```csharp
Task("Outdated").Does(() =>
{
    DotNetOutdated(".", new DotNetOutdatedReportSettings
    {
        OutputFile = "artifacts/outdated.json",   // optional
        OutputFormat = DotNetOutdatedOutputFormat.Json,
        IncludeUpToDate = false,
        Include = { "Serilog" },                  // filters are repeatable
    });
});
```

Options shared by report and upgrade (`DotNetOutdatedSettings`): `IncludeAutoReferences`, `PreRelease`,
`PreReleaseLabel`, `VersionLock`, `Transitive`, `TransitiveDepth`, `OlderThan`, `MaximumVersion`, `Include`,
`Exclude`, `Recursive`, `IncludeFileBasedApps`, `IgnoreFailedSources`, `NuGetCredentialLogLevel`, `Runtime`,
`IdleTimeout`. Report-only: `OutputFile`, `OutputFormat`, `IncludeUpToDate`, `FailOnUpdates`.

With `Recursive = true` a relative path (for example `"."`) is passed to the tool as an absolute path, because
dotnet-outdated 4.8.1 cannot load the projects it discovers when a relative path is combined with `--recursive`.

When `OutputFile` is set, an existing file is deleted before the run and its directory is created. dotnet-outdated
writes **no** file when nothing is outdated, so after a successful JSON run the add-in writes `{"Projects": []}`
in that case: the file always exists afterwards.

### Failing the build on updates

```csharp
DotNetOutdated(".", new DotNetOutdatedReportSettings { FailOnUpdates = true });
```

Exit codes are not remapped: with `FailOnUpdates` the tool exits with code 2 when updates exist and Cake throws.
To get the report without failing, accept the exit code:

```csharp
DotNetOutdated(".", new DotNetOutdatedReportSettings
{
    FailOnUpdates = true,
    HandleExitCode = code => code is 0 or 2,
});
```

## Upgrade packages

```csharp
DotNetOutdatedUpgrade("./src/App.sln", new DotNetOutdatedUpgradeSettings
{
    VersionLock = DotNetOutdatedVersionLock.Major,
    NoRestore = true,
});
```

Upgrades always run non-interactively (`--upgrade:Auto`); `--upgrade:Prompt` is not supported.

## Read the JSON report

```csharp
DotNetOutdated(".", new DotNetOutdatedReportSettings { OutputFile = "outdated.json" });

var report = ReadDotNetOutdatedReport("outdated.json");
var major = report.Projects
    .SelectMany(p => p.TargetFrameworks)
    .SelectMany(f => f.Dependencies)
    .Where(d => d.UpgradeSeverity == DotNetOutdatedUpgradeSeverity.Major);
```

Note: the JSON does not distinguish transitive from direct dependencies.

## License

MIT
````

- [ ] **Step 4: Pack and verify the package contents**

```bash
dotnet pack src/Cake.DotNetOutdated/Cake.DotNetOutdated.csproj -c Release -o artifacts
```

Expected: `artifacts/Cake.DotNetOutdated.0.1.0.nupkg` and `.snupkg`. Check the layout (a nupkg is a zip):

```bash
unzip -l artifacts/Cake.DotNetOutdated.0.1.0.nupkg
```

Expected entries: `lib/net8.0/Cake.DotNetOutdated.dll` and `.xml`, the same for `net9.0` and `net10.0`, `icon.png`, `README.md`. In `Cake.DotNetOutdated.nuspec` (extract with `unzip -p artifacts/*.nupkg Cake.DotNetOutdated.nuspec`): tag `cake-addin` is present and `Cake.Core` is **not** listed as a dependency (`PrivateAssets="All"`).

- [ ] **Step 5: End-to-end verification with the real Cake runner and the real tool**

This confirms what unit tests cannot: Cake's alias code generation accepts the aliases, `--upgrade:Auto`/`--output-format` are accepted by the real tool, and the "no file when nothing is outdated" behaviour. It needs network access to nuget.org. Use a scratch directory outside the repository.

```bash
E2E=$(mktemp -d) && cd "$E2E"
dotnet new tool-manifest
dotnet tool install Cake.Tool --version 6.0.0
dotnet tool install dotnet-outdated-tool
mkdir -p app && dotnet new console -o app --force >/dev/null
dotnet add app package Newtonsoft.Json --version 12.0.1
dotnet new console -o clean --force >/dev/null   # a project with no NuGet dependencies
```

Create `build.cake` (replace `<REPO>` with the absolute path of the repository, using forward slashes):

```csharp
#addin nuget:file:///<REPO>/artifacts/?package=Cake.DotNetOutdated&version=0.1.0

Task("Outdated").Does(() =>
{
    // 1. outdated project: report file written by the tool
    DotNetOutdated("app", new DotNetOutdatedReportSettings { OutputFile = "out/outdated.json", OutputFormat = DotNetOutdatedOutputFormat.Json });
    var report = ReadDotNetOutdatedReport("out/outdated.json");
    Information("Projects with outdated dependencies: {0}", report.Projects.Count);
    if (report.Projects.Count != 1) throw new Exception("expected one outdated project");

    // 2. nothing outdated: the add-in must still leave a readable JSON file
    DotNetOutdated("clean", new DotNetOutdatedReportSettings { OutputFile = "out/clean.json" });
    if (ReadDotNetOutdatedReport("out/clean.json").Projects.Count != 0) throw new Exception("expected an empty report");

    // 3. fail-on-updates gate + handled exit code
    DotNetOutdated("app", new DotNetOutdatedReportSettings { FailOnUpdates = true, HandleExitCode = c => c is 0 or 2 });
});

Task("Upgrade").Does(() =>
{
    DotNetOutdatedUpgrade("app", new DotNetOutdatedUpgradeSettings { VersionLock = DotNetOutdatedVersionLock.Major });
});

RunTarget(Argument("target", "Outdated"));
```

```bash
dotnet cake --target=Outdated
dotnet cake --target=Upgrade && grep Newtonsoft app/app.csproj
```

Expected: the `Outdated` target completes; `Projects with outdated dependencies: 1` is logged; no exceptions. The `Upgrade` target succeeds and `app.csproj` now references a `Newtonsoft.Json` 12.x version newer than 12.0.1 (version lock Major keeps it on 12.x).

If any check fails, record the exact tool output and fix the corresponding runner (argument spelling or casing of enum values are the most likely causes), add a unit test for the fix, and re-run this step.

Already verified while writing this plan (Cake.Tool 6.0.0, dotnet-outdated 4.8.1, `net10.0`): the `Outdated` target succeeds (aliases generate, report is written and read back, an empty report is produced for a project with nothing outdated); running `dotnet outdated clean -o x.json` directly writes **no** file and exits 0; `dotnet outdated app -f` exits 2; the `Upgrade` target upgrades `Newtonsoft.Json` 12.0.1 to 12.0.3. Expect the same results when you repeat it.

- [ ] **Step 6: Commit**

```bash
git add README.md tests
git commit -m "docs: add README and architecture guard"
```
