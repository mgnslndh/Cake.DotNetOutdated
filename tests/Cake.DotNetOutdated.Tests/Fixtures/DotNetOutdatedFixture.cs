using Cake.Core.IO;
using Cake.Core.Tooling;
using Cake.Testing.Fixtures;

namespace Cake.DotNetOutdated.Tests.Fixtures;

internal abstract class DotNetOutdatedFixture<TSettings> : ToolFixture<TSettings>
    where TSettings : ToolSettings, new()
{
    protected DotNetOutdatedFixture()
        : base("dotnet.exe")
    {
        ProcessRunner.Process.SetStandardOutput(new string[] { });
    }

    public string Path { get; set; }
}
