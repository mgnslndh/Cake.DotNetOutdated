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
