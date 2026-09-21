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
        /// Use <see cref="DotNetOutdatedUpgradeSeverity.Patch"/>, <see cref="DotNetOutdatedUpgradeSeverity.Minor"/> or
        /// <see cref="DotNetOutdatedUpgradeSeverity.Major"/>; <see cref="DotNetOutdatedUpgradeSeverity.Unknown"/> orders above
        /// <see cref="DotNetOutdatedUpgradeSeverity.Major"/>, so setting it reports only findings whose severity is unknown.
        /// </summary>
        public DotNetOutdatedUpgradeSeverity MinimumUpgradeSeverity { get; set; } = DotNetOutdatedUpgradeSeverity.Patch;
    }
}
