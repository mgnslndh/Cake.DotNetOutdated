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
