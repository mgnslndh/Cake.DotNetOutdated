# Cake.DotNetOutdated

[![NuGet](https://img.shields.io/nuget/v/Cake.DotNetOutdated.svg)](https://www.nuget.org/packages/Cake.DotNetOutdated)
[![NuGet Downloads](https://img.shields.io/nuget/dt/Cake.DotNetOutdated.svg)](https://www.nuget.org/packages/Cake.DotNetOutdated)
[![Build](https://github.com/mgnslndh/Cake.DotNetOutdated/actions/workflows/build.yml/badge.svg)](https://github.com/mgnslndh/Cake.DotNetOutdated/actions/workflows/build.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

A [Cake](https://cakebuild.net) add-in for [dotnet-outdated](https://github.com/dotnet-outdated/dotnet-outdated):
report or upgrade outdated NuGet packages from your build script, and read the JSON report as a typed model.

## Table of contents

- [Prerequisites](#prerequisites)
- [Installation](#installation)
- [Report outdated packages](#report-outdated-packages)
- [Upgrade packages](#upgrade-packages)
- [Read the JSON report](#read-the-json-report)
- [GitLab Code Quality report](#gitlab-code-quality-report)
- [License](#license)

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

If the tool fails, a report file left over from an earlier run at the output path is not removed, so delete it first when the
workspace is cached.

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

## License

MIT
