namespace Cake.DotNetOutdated.Tests.Fixtures;

internal sealed class ReporterFixture : DotNetOutdatedFixture<DotNetOutdatedReportSettings>
{
    protected override void RunTool()
    {
        new DotNetOutdatedReporter(FileSystem, Environment, ProcessRunner, Tools).Report(Path, Settings);
    }
}
