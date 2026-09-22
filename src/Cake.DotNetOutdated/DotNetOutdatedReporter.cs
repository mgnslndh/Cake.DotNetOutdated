using System;
using System.IO;
using System.Text;
using Cake.Core;
using Cake.Core.IO;
using Cake.Core.Tooling;

namespace Cake.DotNetOutdated
{
    /// <summary>
    /// The dotnet-outdated report runner: lists outdated packages and optionally saves a report.
    /// </summary>
    public sealed class DotNetOutdatedReporter : DotNetOutdatedTool<DotNetOutdatedReportSettings>
    {
        private const string EmptyJsonReport = "{\n  \"Projects\": []\n}";

        private readonly IFileSystem _fileSystem;
        private readonly ICakeEnvironment _environment;

        /// <summary>
        /// Initializes a new instance of the <see cref="DotNetOutdatedReporter" /> class.
        /// </summary>
        /// <param name="fileSystem">The file system.</param>
        /// <param name="environment">The environment.</param>
        /// <param name="processRunner">The process runner.</param>
        /// <param name="tools">The tool locator.</param>
        public DotNetOutdatedReporter(
            IFileSystem fileSystem,
            ICakeEnvironment environment,
            IProcessRunner processRunner,
            IToolLocator tools)
            : base(fileSystem, environment, processRunner, tools)
        {
            _fileSystem = fileSystem;
            _environment = environment;
        }

        /// <summary>
        /// Reports outdated packages.
        /// </summary>
        /// <remarks>
        /// When an output file is configured, an existing file is deleted first (so a stale report cannot be misread)
        /// and its directory is created. dotnet-outdated writes no file when nothing is outdated, so after a successful
        /// run with JSON output an empty report is written in that case.
        /// </remarks>
        /// <param name="path">The solution, project or directory to analyze; the current directory if empty.</param>
        /// <param name="settings">The settings.</param>
        public void Report(string path, DotNetOutdatedReportSettings settings)
        {
            ArgumentNullException.ThrowIfNull(settings);

            var outputFile = settings.OutputFile?.MakeAbsolute(_environment);
            if (outputFile != null)
            {
                PrepareOutputFile(outputFile);
            }

            Run(settings, GetArguments(path, settings, outputFile), null, process => AfterRun(process, settings, outputFile));
        }

        private ProcessArgumentBuilder GetArguments(string path, DotNetOutdatedReportSettings settings, FilePath outputFile)
        {
            var builder = CreateArgumentBuilder(path, settings);

            if (settings.IncludeUpToDate)
            {
                builder.Append("--include-up-to-date");
            }

            if (settings.FailOnUpdates)
            {
                builder.Append("--fail-on-updates");
            }

            if (outputFile != null)
            {
                builder.Append("--output");
                builder.AppendQuoted(outputFile.FullPath);
            }

            if (settings.OutputFormat.HasValue)
            {
                builder.Append("--output-format");
                builder.Append(settings.OutputFormat.Value.ToString().ToLowerInvariant());
            }

            return builder;
        }

        private void PrepareOutputFile(FilePath outputFile)
        {
            var file = _fileSystem.GetFile(outputFile);
            if (file.Exists)
            {
                file.Delete();
            }

            var directory = _fileSystem.GetDirectory(outputFile.GetDirectory());
            if (!directory.Exists)
            {
                directory.Create();
            }
        }

        private void AfterRun(IProcess process, DotNetOutdatedReportSettings settings, FilePath outputFile)
        {
            settings.PostAction?.Invoke(process);

            if (outputFile == null || process.GetExitCode() != 0)
            {
                return;
            }

            if ((settings.OutputFormat ?? DotNetOutdatedOutputFormat.Json) != DotNetOutdatedOutputFormat.Json)
            {
                return;
            }

            var file = _fileSystem.GetFile(outputFile);
            if (file.Exists)
            {
                return;
            }

            using var stream = file.Open(FileMode.Create, FileAccess.Write, FileShare.None);
            using var writer = new StreamWriter(stream, new UTF8Encoding(false));
            writer.Write(EmptyJsonReport);
        }
    }
}
