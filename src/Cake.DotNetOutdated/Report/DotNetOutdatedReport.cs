using System;
using System.Collections.Generic;

namespace Cake.DotNetOutdated.Report
{
    /// <summary>
    /// The JSON report written by dotnet-outdated (<c>--output-format json</c>).
    /// </summary>
    public sealed class DotNetOutdatedReport
    {
        private readonly IReadOnlyList<DotNetOutdatedProject> _projects = Array.Empty<DotNetOutdatedProject>();

        /// <summary>
        /// Gets the analyzed projects that have outdated dependencies.
        /// </summary>
        public IReadOnlyList<DotNetOutdatedProject> Projects
        {
            get => _projects;
            init => _projects = value ?? Array.Empty<DotNetOutdatedProject>();
        }
    }

    /// <summary>
    /// A project in a <see cref="DotNetOutdatedReport"/>.
    /// </summary>
    public sealed class DotNetOutdatedProject
    {
        private readonly IReadOnlyList<DotNetOutdatedTargetFramework> _targetFrameworks = Array.Empty<DotNetOutdatedTargetFramework>();

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
        public IReadOnlyList<DotNetOutdatedTargetFramework> TargetFrameworks
        {
            get => _targetFrameworks;
            init => _targetFrameworks = value ?? Array.Empty<DotNetOutdatedTargetFramework>();
        }
    }

    /// <summary>
    /// A target framework of a <see cref="DotNetOutdatedProject"/>.
    /// </summary>
    public sealed class DotNetOutdatedTargetFramework
    {
        private readonly IReadOnlyList<DotNetOutdatedDependency> _dependencies = Array.Empty<DotNetOutdatedDependency>();

        /// <summary>
        /// Gets the target framework name, for example <c>net8.0</c>.
        /// </summary>
        public string Name { get; init; }

        /// <summary>
        /// Gets the dependencies of the target framework.
        /// </summary>
        public IReadOnlyList<DotNetOutdatedDependency> Dependencies
        {
            get => _dependencies;
            init => _dependencies = value ?? Array.Empty<DotNetOutdatedDependency>();
        }
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
