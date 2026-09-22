using System;
using Cake.Core;
using Cake.Core.Annotations;

namespace Cake.DotNetOutdated
{
    /// <summary>
    /// Contains functionality for running the dotnet-outdated tool.
    /// </summary>
    public static partial class DotNetOutdatedAliases
    {
        /// <summary>
        /// Reports outdated NuGet packages of a solution, project or directory using dotnet-outdated.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="path">The solution, project or directory to analyze; the current directory if empty.</param>
        /// <example>
        /// <code>
        /// DotNetOutdated(".");
        /// </code>
        /// </example>
        [CakeMethodAlias]
        [CakeAliasCategory("DotNetOutdated")]
        [CakeNamespaceImport("Cake.DotNetOutdated")]
        public static void DotNetOutdated(this ICakeContext context, string path)
        {
            context.DotNetOutdated(path, null);
        }

        /// <summary>
        /// Reports outdated NuGet packages of a solution, project or directory using dotnet-outdated
        /// and the specified settings.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="path">The solution, project or directory to analyze; the current directory if empty.</param>
        /// <param name="settings">The settings.</param>
        /// <example>
        /// <code>
        /// DotNetOutdated(".", new DotNetOutdatedReportSettings
        /// {
        ///     OutputFile = "artifacts/outdated.json",
        ///     OutputFormat = DotNetOutdatedOutputFormat.Json,
        ///     IncludeUpToDate = false
        /// });
        /// </code>
        /// </example>
        [CakeMethodAlias]
        [CakeAliasCategory("DotNetOutdated")]
        [CakeNamespaceImport("Cake.DotNetOutdated")]
        public static void DotNetOutdated(this ICakeContext context, string path, DotNetOutdatedReportSettings settings)
        {
            ArgumentNullException.ThrowIfNull(context);

            settings ??= new DotNetOutdatedReportSettings();

            var reporter = new DotNetOutdatedReporter(context.FileSystem, context.Environment, context.ProcessRunner, context.Tools);
            reporter.Report(path, settings);
        }
    }
}
