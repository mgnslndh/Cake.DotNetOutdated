using Cake.Core;
using Cake.Core.IO;
using Cake.Core.Tooling;

namespace Cake.DotNetOutdated.Tests.Fixtures;

/// <summary>Test-only runner that exposes the shared argument building of the base tool.</summary>
internal sealed class SharedOptionsRunner : DotNetOutdatedTool<DotNetOutdatedSettings>
{
    public SharedOptionsRunner(IFileSystem fileSystem, ICakeEnvironment environment, IProcessRunner processRunner, IToolLocator tools)
        : base(fileSystem, environment, processRunner, tools)
    {
    }

    public void Execute(string path, DotNetOutdatedSettings settings)
    {
        Run(settings, CreateArgumentBuilder(path, settings));
    }
}

internal sealed class SharedOptionsFixture : DotNetOutdatedFixture<DotNetOutdatedSettings>
{
    protected override void RunTool()
    {
        new SharedOptionsRunner(FileSystem, Environment, ProcessRunner, Tools).Execute(Path, Settings);
    }
}
