using Cake.Core.IO;

namespace Cake.DotNetOutdated
{
    /// <summary>
    /// Contains the settings used by the report command (<c>dotnet outdated</c>).
    /// </summary>
    public class DotNetOutdatedReportSettings : DotNetOutdatedSettings
    {
        /// <summary>
        /// Gets or sets the file to save the report to (<c>--output</c>). Relative paths are resolved against the Cake working directory.
        /// </summary>
        public FilePath OutputFile { get; set; }

        /// <summary>
        /// Gets or sets the format of the report file (<c>--output-format</c>).
        /// </summary>
        public DotNetOutdatedOutputFormat? OutputFormat { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether all dependencies are reported, including up-to-date ones (<c>--include-up-to-date</c>).
        /// </summary>
        public bool IncludeUpToDate { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the tool exits with code 2 when updates are found (<c>--fail-on-updates</c>).
        /// Cake treats any non-zero exit code as a failure unless <see cref="Cake.Core.Tooling.ToolSettings.HandleExitCode"/> accepts it.
        /// </summary>
        public bool FailOnUpdates { get; set; }
    }
}
