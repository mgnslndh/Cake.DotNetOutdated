namespace Cake.DotNetOutdated.Tests.Fixtures;

internal sealed class UpgraderFixture : DotNetOutdatedFixture<DotNetOutdatedUpgradeSettings>
{
    protected override void RunTool()
    {
        new DotNetOutdatedUpgrader(FileSystem, Environment, ProcessRunner, Tools).Upgrade(Path, Settings);
    }
}
