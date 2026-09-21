using Cake.Core.IO;
using Cake.DotNetOutdated.GitLab;
using Cake.Testing;

namespace Cake.DotNetOutdated.Tests.Fixtures;

internal sealed class GitLabRunnerFixture : DotNetOutdatedFixture<DotNetOutdatedReportSettings>
{
    public FilePath OutputFile { get; set; } = "gl-code-quality-report.json";

    public GitLabCodeQualitySettings GitLabSettings { get; set; } = new GitLabCodeQualitySettings();

    public FakeLog Log { get; } = new FakeLog();

    protected override void RunTool()
    {
        new DotNetOutdatedGitLabCodeQualityRunner(FileSystem, Environment, ProcessRunner, Tools, Log)
            .Run(Path, OutputFile, Settings, GitLabSettings);
    }
}
