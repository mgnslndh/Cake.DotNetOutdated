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
