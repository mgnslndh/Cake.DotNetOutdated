using System;
using Cake.Core;
using Cake.Core.Annotations;

namespace Cake.DotNetOutdated
{
    public static partial class DotNetOutdatedAliases
    {
        /// <summary>
        /// Upgrades outdated NuGet packages of a solution, project or directory using dotnet-outdated.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="path">The solution, project or directory to upgrade; the current directory if empty.</param>
        /// <example>
        /// <code>
        /// DotNetOutdatedUpgrade("./src/App.sln");
        /// </code>
        /// </example>
        [CakeMethodAlias]
        [CakeAliasCategory("DotNetOutdated")]
        [CakeNamespaceImport("Cake.DotNetOutdated")]
        public static void DotNetOutdatedUpgrade(this ICakeContext context, string path)
        {
            context.DotNetOutdatedUpgrade(path, null);
        }

        /// <summary>
        /// Upgrades outdated NuGet packages of a solution, project or directory using dotnet-outdated
        /// and the specified settings.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="path">The solution, project or directory to upgrade; the current directory if empty.</param>
        /// <param name="settings">The settings.</param>
        /// <example>
        /// <code>
        /// DotNetOutdatedUpgrade("./src/App.sln", new DotNetOutdatedUpgradeSettings
        /// {
        ///     VersionLock = DotNetOutdatedVersionLock.Major,
        ///     NoRestore = true
        /// });
        /// </code>
        /// </example>
        [CakeMethodAlias]
        [CakeAliasCategory("DotNetOutdated")]
        [CakeNamespaceImport("Cake.DotNetOutdated")]
        public static void DotNetOutdatedUpgrade(this ICakeContext context, string path, DotNetOutdatedUpgradeSettings settings)
        {
            ArgumentNullException.ThrowIfNull(context);

            settings ??= new DotNetOutdatedUpgradeSettings();

            var upgrader = new DotNetOutdatedUpgrader(context.FileSystem, context.Environment, context.ProcessRunner, context.Tools);
            upgrader.Upgrade(path, settings);
        }
    }
}
