using System;
using Cake.Core;
using Cake.Core.Diagnostics;
using Cake.Core.IO;
using Cake.Core.Tooling;
using Cake.DotNetOutdated.Report;

namespace Cake.DotNetOutdated.GitLab
{
    /// <summary>
    /// Runs dotnet-outdated, converts its JSON report and writes a GitLab Code Quality report.
    /// </summary>
    internal sealed class DotNetOutdatedGitLabCodeQualityRunner
    {
        private readonly IFileSystem _fileSystem;
        private readonly ICakeEnvironment _environment;
        private readonly IProcessRunner _processRunner;
        private readonly IToolLocator _tools;
        private readonly ICakeLog _log;

        public DotNetOutdatedGitLabCodeQualityRunner(
            IFileSystem fileSystem,
            ICakeEnvironment environment,
            IProcessRunner processRunner,
            IToolLocator tools,
            ICakeLog log)
        {
            _fileSystem = fileSystem;
            _environment = environment;
            _processRunner = processRunner;
            _tools = tools;
            _log = log;
        }

        public void Run(string path, FilePath outputFile, DotNetOutdatedReportSettings reportSettings, GitLabCodeQualitySettings gitLabSettings)
        {
            ArgumentNullException.ThrowIfNull(outputFile);

            reportSettings ??= new DotNetOutdatedReportSettings();

            var output = outputFile.MakeAbsolute(_environment);
            var temporaryReport = new FilePath(output.FullPath + ".outdated.json");

            var callersHandler = reportSettings.HandleExitCode;
            var failOnUpdates = reportSettings.FailOnUpdates;
            var exitCode = 0;

            var settings = reportSettings.Clone();
            settings.OutputFile = temporaryReport;
            settings.OutputFormat = DotNetOutdatedOutputFormat.Json;

            // Exit code 2 (updates found) must not abort before the report is written; it is re-thrown afterwards.
            settings.HandleExitCode = code => (callersHandler?.Invoke(code) ?? code == 0) || (failOnUpdates && code == 2);
            settings.PostAction = process =>
            {
                exitCode = process.GetExitCode();
                reportSettings.PostAction?.Invoke(process);
            };

            try
            {
                new DotNetOutdatedReporter(_fileSystem, _environment, _processRunner, _tools).Report(path, settings);

                if (!_fileSystem.Exist(temporaryReport))
                {
                    throw new CakeException(
                        $"dotnet-outdated did not produce a report (exit code {exitCode}); no GitLab Code Quality report was written.");
                }

                var report = new DotNetOutdatedReportReader(_fileSystem, _environment).Read(temporaryReport);
                var issues = new DotNetOutdatedGitLabConverter(_fileSystem, _environment, _log).Convert(report, gitLabSettings);
                new GitLabCodeQualityReportWriter(_fileSystem, _environment).Write(issues, output);
            }
            finally
            {
                var file = _fileSystem.GetFile(temporaryReport);
                if (file.Exists)
                {
                    file.Delete();
                }
            }

            if (failOnUpdates && exitCode == 2 && !(callersHandler?.Invoke(2) ?? false))
            {
                throw new CakeException(2, "dotnet-outdated: Process returned an error (exit code 2).");
            }
        }
    }
}
