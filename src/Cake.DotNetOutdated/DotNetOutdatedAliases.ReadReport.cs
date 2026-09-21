using System;
using Cake.Core;
using Cake.Core.Annotations;
using Cake.Core.IO;
using Cake.DotNetOutdated.Report;

namespace Cake.DotNetOutdated
{
    public static partial class DotNetOutdatedAliases
    {
        /// <summary>
        /// Reads a JSON report written by dotnet-outdated.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="path">The report file.</param>
        /// <returns>The report.</returns>
        /// <example>
        /// <code>
        /// DotNetOutdated(".", new DotNetOutdatedReportSettings { OutputFile = "outdated.json" });
        /// var report = ReadDotNetOutdatedReport("outdated.json");
        /// var majorUpdates = report.Projects
        ///     .SelectMany(p => p.TargetFrameworks)
        ///     .SelectMany(f => f.Dependencies)
        ///     .Count(d => d.UpgradeSeverity == DotNetOutdatedUpgradeSeverity.Major);
        /// </code>
        /// </example>
        [CakeMethodAlias]
        [CakeAliasCategory("DotNetOutdated Report")]
        [CakeNamespaceImport("Cake.DotNetOutdated.Report")]
        public static DotNetOutdatedReport ReadDotNetOutdatedReport(this ICakeContext context, FilePath path)
        {
            ArgumentNullException.ThrowIfNull(context);

            return new DotNetOutdatedReportReader(context.FileSystem, context.Environment).Read(path);
        }
    }
}
