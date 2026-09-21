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
            // The caller's handler is asked once per exit code; its answer for code 2 decides the final check below.
            var callerAcceptedTwo = false;
            settings.HandleExitCode = code =>
            {
                var callerAccepted = callersHandler?.Invoke(code);
                if (code == 2)
                {
                    callerAcceptedTwo = callerAccepted ?? false;
                }

                return (callerAccepted ?? code == 0) || (failOnUpdates && code == 2);
            };
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
                // Cleanup must never fail the build or replace the exception that is already propagating.
                try
                {
                    var file = _fileSystem.GetFile(temporaryReport);
                    if (file.Exists)
                    {
                        file.Delete();
                    }
                }
                catch (Exception exception)
                {
                    _log.Debug("Could not delete the temporary report {0}: {1}", temporaryReport.FullPath, exception.Message);
                }
            }

            if (failOnUpdates && exitCode == 2 && !callerAcceptedTwo)
            {
                throw new CakeException(2, "dotnet-outdated: Process returned an error (exit code 2).");
            }
        }
    }
}
