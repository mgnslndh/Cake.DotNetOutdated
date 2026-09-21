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
