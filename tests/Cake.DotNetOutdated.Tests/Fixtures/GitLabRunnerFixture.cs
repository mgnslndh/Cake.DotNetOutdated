using Cake.Core.IO;
using Cake.DotNetOutdated.GitLab;
using Cake.Testing;

namespace Cake.DotNetOutdated.Tests.Fixtures;

internal sealed class GitLabRunnerFixture : DotNetOutdatedFixture<DotNetOutdatedReportSettings>
{
    public FilePath OutputFile { get; set; } = "gl-code-quality-report.json";

    public GitLabCodeQualitySettings GitLabSettings { get; set; } = new GitLabCodeQualitySettings();

    public FakeLog Log { get; } = new FakeLog();

    /// <summary>
    /// Gets or sets a function that wraps the fake file system the runner uses, for example to make it fail.
    /// </summary>
    public Func<FakeFileSystem, IFileSystem> DecorateFileSystem { get; set; }

    protected override void RunTool()
    {
        IFileSystem fileSystem = DecorateFileSystem?.Invoke(FileSystem) ?? FileSystem;

        new DotNetOutdatedGitLabCodeQualityRunner(fileSystem, Environment, ProcessRunner, Tools, Log)
            .Run(Path, OutputFile, Settings, GitLabSettings);
    }
}
