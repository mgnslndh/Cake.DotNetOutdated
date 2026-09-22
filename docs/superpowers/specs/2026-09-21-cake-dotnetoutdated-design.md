# Cake.DotNetOutdated — Design

Date: 2026-09-21
Status: Draft for review

## Goal

A Cake add-in that wraps the [`dotnet-outdated`](https://github.com/dotnet-outdated/dotnet-outdated) .NET tool (package `dotnet-outdated-tool`, 4.8.1 at time of writing), plus an independent feature that converts its JSON output into a [GitLab Code Quality](https://docs.gitlab.com/ci/testing/code_quality/) report so outdated dependencies show up as merge request findings.

The work is delivered in two stages, each with its own implementation plan:

1. **Stage 1 — Tool add-in:** runner, settings, aliases, and the typed JSON report model/reader (Sections 1–2).
2. **Stage 2 — GitLab Code Quality:** converter, line locator, writer, aliases (Section 3). Depends on Stage 1's report model only.

## Decisions

| Topic | Decision |
|---|---|
| Packaging | One repo, one NuGet package `Cake.DotNetOutdated`, layered namespaces (approach A). The `.GitLab` namespace depends on `.Report`; nothing depends on `.GitLab`. Enforced by an architecture test. Splitting into a second package later is mechanical. |
| Parse format | JSON. CSV has only the project *name* (no path) and is culture-dependent; Markdown is a human table with LaTeX colour markup. GitLab needs a repo-relative file path, which only JSON carries (`FilePath`). |
| GitLab API shape | Composable aliases plus a thin one-shot alias in the `.GitLab` namespace. **Not** an `OutputFormat.GitLabCodeQuality` value on the runner: it would couple the runner to GitLab, break the settings-mirror-CLI-flags convention, and need a hidden temp file. |
| Line numbers | Locate the real declaration line (see Section 3); fall back to line 1 of the project file. |
| Cake conventions | Baseline Cake 6.0.0; multi-target `net8.0;net9.0;net10.0`; `Cake.Core` with `PrivateAssets="All"`; package tag `cake-addin`; embedded `PackageIcon`; XML docs shipped in the package; `[CakeAliasCategory]` on aliases. |

## Research findings

**dotnet-outdated**
- One analysis command (plus an `mcp` subcommand that starts an MCP server; out of scope for a build add-in). Modes: report (default), upgrade (`-u`), CI gate (`-f`).
- `-u|--upgrade` is a single-or-no-value option: the value must be attached (`--upgrade:Auto`), never space-separated, or the next token is parsed as the positional path. `--include`/`--exclude` are repeatable.
- Executed as `dotnet outdated` (tool command name `dotnet-outdated`); works for global and local-manifest installs.
- Exit codes (from `Program.cs`): `0` ok, `1` validation error, `2` updates found and `-f` set, `3` upgrade failed.
- JSON output (from `JsonFormatter.cs`, not documented in the README): PascalCase `Projects[] → TargetFrameworks[] → Dependencies[]`; dependency fields `Name`, `ResolvedVersion`, `LatestVersion`, `UpgradeSeverity` (`None|Patch|Minor|Major|Unknown`); project fields `Name`, `FilePath`. `IsTransitive`/`IsAutoReferenced` are `[JsonIgnore]`d. No line numbers, and no indication whether a version is declared in the project or in `Directory.Packages.props`.
- `-o` output is written only when at least one project has outdated dependencies (`GenerateOutputFile` is called inside the "has outdated" branch). **Observed** against dotnet-outdated 4.8.1: on a project with nothing outdated the tool exits 0 and writes no file.
- `Project.FilePath` in the JSON is an absolute, OS-native path (e.g. `C:\repo\src\App\App.csproj` on Windows, `/repo/src/App/App.csproj` on Linux). The GitLab converter must make it repo-relative with forward slashes.
- **Tool bug (observed, 4.8.1):** `--recursive` combined with a *relative* path fails with `MSB1009: Project file does not exist` for every discovered project; an absolute path works, and a relative path without `--recursive` works. The tool layer therefore makes the path absolute when `Recursive` is set (against `settings.WorkingDirectory` or the Cake working directory).
- Verified end to end (Cake.Tool 6.0.0 + dotnet-outdated 4.8.1): `--fail-on-updates` exits 2 when updates exist; `--upgrade:Auto` with `--version-lock Major` upgrades within the current major version.
- Options: `--include-auto-references`, `-pre`, `-vl`, `-t`, `-td`, `-prl`, `-ot`, `-mv`, `-inc`, `-exc`, `-u`, `-n`, `-f`, `-o`, `-of`, `-utd`, `-r`, `-fba`, `-ifs`, `-ncll`, `-rt`, `-it|--idle-timeout` (seconds, undocumented in the README), positional path (optional; defaults to the current directory).

**GitLab Code Quality**
- JSON array of `{ description, check_name, fingerprint, severity, location: { path, lines: { begin } } }`.
- `severity`: `info|minor|major|critical|blocker`. Identical fingerprints collapse to one entry; GitLab also diffs MR vs. target-branch reports by fingerprint. `location.path` is repo-relative, no `./`. No BOM. Declared via `artifacts:reports:codequality`.

**Cake conventions** (from `cake-build/cake` `src/Cake.Common/Tools/DotNet` and the add-in best-practices page): per-command runner classes with matching settings classes, aliases split into partial files per command, `[CakeMethodAlias]` / `[CakeAliasCategory]` / `[CakeNamespaceImport]`.

## Solution layout

```
Cake.DotNetOutdated.sln
src/Cake.DotNetOutdated/
  DotNetOutdatedTool.cs            abstract base
  DotNetOutdatedSettings.cs        shared options
  Report/                          typed JSON model + reader
  GitLab/                          Stage 2
  DotNetOutdatedAliases.*.cs       one partial file per command
tests/Cake.DotNetOutdated.Tests/   xUnit v3 + Cake.Testing
```

## Section 1 — Tool layer (Stage 1)

- `DotNetOutdatedTool<TSettings> : Tool<TSettings>` from `Cake.Core`. It locates `dotnet` and prepends `outdated`. It deliberately does **not** derive from Cake's `DotNetTool<T>`: that forces settings to derive `DotNetSettings` (`Verbosity`, `DiagnosticOutput`), which dotnet-outdated rejects. Consequence: no `Cake.Common` reference at all.
- `DotNetOutdatedSettings : ToolSettings` — shared options: `IncludeAutoReferences`, `PreRelease` (Auto/Always/Never), `PreReleaseLabel`, `VersionLock` (None/Major/Minor), `Transitive`, `TransitiveDepth`, `OlderThan` (days), `MaximumVersion`, `Include`, `Exclude`, `Recursive`, `IncludeFileBasedApps`, `IgnoreFailedSources`, `NuGetCredentialLogLevel`, `Runtime`, `IdleTimeout`. Shared arguments are built by the base class; the path is optional and emitted first, then shared options, then command-specific options. With `Recursive`, a relative path is passed as an absolute one (see the tool bug in Research findings).
- Two commands, each with its own runner and settings class:
  - **Report:** `DotNetOutdatedReporter` / `DotNetOutdatedReportSettings` adds `OutputFile`, `OutputFormat` (Json/Csv/Markdown), `IncludeUpToDate`, `FailOnUpdates`.
  - **Upgrade:** `DotNetOutdatedUpgrader` / `DotNetOutdatedUpgradeSettings` adds `NoRestore`. Always emits `--upgrade:Auto`; `Prompt` is interactive and would hang CI, so it is not exposed.
- Aliases (category `DotNetOutdated`): `DotNetOutdated(path[, settings])`, `DotNetOutdatedUpgrade(path[, settings])`.
- Exit codes are not remapped. With `FailOnUpdates`, exit `2` throws `CakeException` (a working gate). To get the report without failing, users set `HandleExitCode = c => c is 0 or 2`; documented in the README.

## Section 2 — Report model and reader (Stage 1, `Cake.DotNetOutdated.Report`)

- Immutable types mirroring the JSON: `DotNetOutdatedReport { Projects }` → `DotNetOutdatedProject { Name, FilePath, TargetFrameworks }` → `DotNetOutdatedTargetFramework { Name, Dependencies }` → `DotNetOutdatedDependency { Name, ResolvedVersion, LatestVersion, UpgradeSeverity }`; enum `DotNetOutdatedUpgradeSeverity`. Versions are `string` (no `NuGet.Versioning` dependency).
- Reader uses `System.Text.Json`: case-insensitive property names, unknown fields ignored, BOM tolerated. Alias `ReadDotNetOutdatedReport(FilePath)` throws on a missing file.
- Report runner guarantees an output file for JSON: it deletes any existing `OutputFile` before running (no stale reports), and after a successful run writes `{"Projects":[]}` if the tool wrote nothing. CSV/Markdown are left as the tool produces them.
- Known limitation: the transitive flag is absent from the JSON, so `-t` results cannot be distinguished from direct dependencies; the line-locator fallback covers them.

## Section 3 — GitLab Code Quality (Stage 2, `Cake.DotNetOutdated.GitLab`)

**Core (tool-agnostic):** `GitLabCodeQualityIssue { Description, CheckName, Fingerprint, Severity, Path, Line }`; the writer emits a UTF-8 JSON array without BOM and writes `[]` for zero findings so the artifact always exists.

**Converter (report → issues):**
- One issue per package per *declaring file*; multi-target duplicates are grouped, taking the highest severity. Under Central Package Management the declaring file is `Directory.Packages.props`, so several projects flagging the same package collapse into one finding.
- `check_name`: `outdated-package`.
- Fingerprint: SHA-256 of `check_name | declaring-file relative path | package name`. Versions and line are excluded so new NuGet releases and line shifts don't produce new/fixed churn in MR diffs.
- Default severity mapping: `Major→major`, `Minor→minor`, `Patch→info`, `Unknown→info`, `None` skipped. Overridable via `GitLabCodeQualitySettings`, which also has a minimum-severity threshold and `RepositoryRoot` (default: Cake working directory).
- Paths are repo-relative with forward slashes, no `./`. A declaration outside the repo root falls back to the project file; if that is also outside, the finding is skipped with a warning.

**Line locator:** loads XML with line info through Cake's `IFileSystem`, caches parsed documents per run, matches package ids case-insensitively on `Include` or `Update`, matches elements by local name (old-style `xmlns` projects). Order:
1. `PackageVersion` / `GlobalPackageReference` in the nearest `Directory.Packages.props`.
2. `PackageReference` in the project file.
3. `Directory.Build.props` / `.targets`, walking up.
4. Line 1 of the project file (transitive, non-SDK, or not found).

`Version="$(Var)"` still resolves correctly because matching is by element, not by property definition. MSBuild property definitions are not chased.

**Aliases** (category `GitLab Code Quality`):
```csharp
var report = ReadDotNetOutdatedReport("outdated.json");
var issues = ConvertToGitLabCodeQuality(report, gitLabSettings);
WriteGitLabCodeQualityReport(issues, "gl-code-quality-report.json");

// one-shot
DotNetOutdatedGitLabCodeQuality(".", "gl-code-quality-report.json", reportSettings, gitLabSettings);
```
The one-shot forces JSON to a temp file. If `FailOnUpdates` is set it accepts exit code `2`, writes the report, then throws, so GitLab still receives the artifact with `artifacts: when: always`.

## Testing

- Unit tests (xUnit v3, `Cake.Testing` `ToolFixture` pattern): argument building per option for both runners; report reader; converter; locator (CPM, project file, `Directory.Build.props`, fallback, casing, namespaces); writer (no BOM, `[]`).
- Golden-file test of the emitted report against GitLab's documented shape.
- Architecture test: `Cake.DotNetOutdated` and `Cake.DotNetOutdated.Report` must not reference `Cake.DotNetOutdated.GitLab`.
- Real-tool verification: a documented end-to-end run (packed add-in, `Cake.Tool`, `dotnet-outdated-tool` in a scratch local-tool manifest) confirms alias generation, the JSON shape and the "no file when nothing outdated" behaviour. It needs network access, so it is a manual step in each plan rather than part of the automated suite.
- Tests run on all supported TFMs; Cake runner coverage (.NET Tool, Frosting, SDK) per the add-in best practices is verified in CI.

## Out of scope

- `-u:prompt` (interactive).
- CSV/Markdown parsing.
- Chasing MSBuild property definitions or `packages.config`.
- Any GitLab output other than the Code Quality report (e.g. MR comments).
- Publishing/CI pipeline for the add-in itself (separate task).

## Documentation

README covers installation (`#addin nuget:?package=Cake.DotNetOutdated`), both runners, the exit-code note, the composable and one-shot GitLab flows, and a `.gitlab-ci.yml` example using `artifacts: when: always` and `reports: codequality`.
