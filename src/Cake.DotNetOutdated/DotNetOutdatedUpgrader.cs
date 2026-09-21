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
