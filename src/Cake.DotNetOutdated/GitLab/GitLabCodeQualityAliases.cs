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
