using Cake.DotNetOutdated.GitLab;
using Cake.Testing;

namespace Cake.DotNetOutdated.Tests.GitLab;

public sealed class DependencyLocatorTests
{
    private const string Project = "/Working/src/App/App.csproj";

    private const string SdkProject = """
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <TargetFramework>net8.0</TargetFramework>
          </PropertyGroup>
          <ItemGroup>
            <PackageReference Include="Newtonsoft.Json" Version="12.0.1" />
            <PackageReference Update="Serilog" Version="3.0.0" />
            <PackageReference Include="Versioned" Version="$(VersionedVersion)" />
          </ItemGroup>
        </Project>
        """;

    private const string CentralPackages = """
        <Project>
          <PropertyGroup>
            <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
          </PropertyGroup>
          <ItemGroup>
            <PackageVersion Include="Newtonsoft.Json" Version="12.0.1" />
            <GlobalPackageReference Include="Analyzer.Package" Version="1.0.0" />
          </ItemGroup>
        </Project>
        """;

    private static (DependencyLocator Locator, FakeFileSystem FileSystem) Create()
    {
        var environment = FakeEnvironment.CreateUnixEnvironment();
        var fileSystem = new FakeFileSystem(environment);
        return (new DependencyLocator(fileSystem), fileSystem);
    }

    [Fact]
    public void Should_Find_A_Package_Reference_In_The_Project_File()
    {
        var (locator, fileSystem) = Create();
        fileSystem.CreateFile(Project).SetContent(SdkProject);

        var location = locator.Locate(Project, "Newtonsoft.Json");

        Assert.Equal(Project, location.File.FullPath);
        Assert.Equal(6, location.Line);
    }

    [Fact]
    public void Should_Match_Package_Ids_Case_Insensitively()
    {
        var (locator, fileSystem) = Create();
        fileSystem.CreateFile(Project).SetContent(SdkProject);

        Assert.Equal(6, locator.Locate(Project, "newtonsoft.JSON").Line);
    }

    [Fact]
    public void Should_Match_An_Update_Attribute()
    {
        var (locator, fileSystem) = Create();
        fileSystem.CreateFile(Project).SetContent(SdkProject);

        Assert.Equal(7, locator.Locate(Project, "Serilog").Line);
    }

    [Fact]
    public void Should_Find_A_Package_Whose_Version_Is_A_Property()
    {
        var (locator, fileSystem) = Create();
        fileSystem.CreateFile(Project).SetContent(SdkProject);

        Assert.Equal(8, locator.Locate(Project, "Versioned").Line);
    }

    [Fact]
    public void Should_Find_A_Package_Reference_In_A_Project_With_An_Xml_Namespace()
    {
        var (locator, fileSystem) = Create();
        fileSystem.CreateFile(Project).SetContent("""
            <?xml version="1.0" encoding="utf-8"?>
            <Project ToolsVersion="15.0" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
              <ItemGroup>
                <PackageReference Include="Newtonsoft.Json">
                  <Version>12.0.1</Version>
                </PackageReference>
              </ItemGroup>
            </Project>
            """);

        Assert.Equal(4, locator.Locate(Project, "Newtonsoft.Json").Line);
    }

    [Fact]
    public void Should_Prefer_The_Central_Package_Version_Over_The_Project_File()
    {
        var (locator, fileSystem) = Create();
        fileSystem.CreateFile(Project).SetContent(SdkProject);
        fileSystem.CreateFile("/Working/Directory.Packages.props").SetContent(CentralPackages);

        var location = locator.Locate(Project, "Newtonsoft.Json");

        Assert.Equal("/Working/Directory.Packages.props", location.File.FullPath);
        Assert.Equal(6, location.Line);
    }

    [Fact]
    public void Should_Find_A_Global_Package_Reference()
    {
        var (locator, fileSystem) = Create();
        fileSystem.CreateFile(Project).SetContent(SdkProject);
        fileSystem.CreateFile("/Working/Directory.Packages.props").SetContent(CentralPackages);

        var location = locator.Locate(Project, "Analyzer.Package");

        Assert.Equal("/Working/Directory.Packages.props", location.File.FullPath);
        Assert.Equal(7, location.Line);
    }

    [Fact]
    public void Should_Only_Consider_The_Nearest_Central_Packages_File()
    {
        var (locator, fileSystem) = Create();
        fileSystem.CreateFile(Project).SetContent(SdkProject);
        fileSystem.CreateFile("/Working/Directory.Packages.props").SetContent(CentralPackages);
        fileSystem.CreateFile("/Working/src/Directory.Packages.props").SetContent("<Project />");

        var location = locator.Locate(Project, "Newtonsoft.Json");

        // The nearest file does not declare the package, so the lookup continues with the project file.
        Assert.Equal(Project, location.File.FullPath);
        Assert.Equal(6, location.Line);
    }

    [Fact]
    public void Should_Fall_Back_To_The_Project_File_When_Central_Packages_Do_Not_Declare_The_Package()
    {
        var (locator, fileSystem) = Create();
        fileSystem.CreateFile(Project).SetContent(SdkProject);
        fileSystem.CreateFile("/Working/Directory.Packages.props").SetContent(CentralPackages);

        var location = locator.Locate(Project, "Serilog");

        Assert.Equal(Project, location.File.FullPath);
        Assert.Equal(7, location.Line);
    }

    [Fact]
    public void Should_Find_A_Package_Reference_In_Directory_Build_Props()
    {
        var (locator, fileSystem) = Create();
        fileSystem.CreateFile(Project).SetContent("<Project Sdk=\"Microsoft.NET.Sdk\" />");
        fileSystem.CreateFile("/Working/Directory.Build.props").SetContent("""
            <Project>
              <ItemGroup>
                <PackageReference Include="Shared.Analyzer" Version="1.0.0" />
              </ItemGroup>
            </Project>
            """);

        var location = locator.Locate(Project, "Shared.Analyzer");

        Assert.Equal("/Working/Directory.Build.props", location.File.FullPath);
        Assert.Equal(3, location.Line);
    }

    [Fact]
    public void Should_Find_A_Package_Reference_In_Directory_Build_Targets()
    {
        var (locator, fileSystem) = Create();
        fileSystem.CreateFile(Project).SetContent("<Project Sdk=\"Microsoft.NET.Sdk\" />");
        fileSystem.CreateFile("/Working/src/Directory.Build.targets").SetContent("""
            <Project>
              <ItemGroup>
                <PackageReference Include="Shared.Targets" Version="1.0.0" />
              </ItemGroup>
            </Project>
            """);

        var location = locator.Locate(Project, "Shared.Targets");

        Assert.Equal("/Working/src/Directory.Build.targets", location.File.FullPath);
        Assert.Equal(3, location.Line);
    }

    [Fact]
    public void Should_Fall_Back_To_Line_One_Of_The_Project_When_The_Package_Is_Not_Declared()
    {
        var (locator, fileSystem) = Create();
        fileSystem.CreateFile(Project).SetContent(SdkProject);

        var location = locator.Locate(Project, "Transitive.Package");

        Assert.Equal(Project, location.File.FullPath);
        Assert.Equal(1, location.Line);
    }

    [Fact]
    public void Should_Fall_Back_To_Line_One_When_The_Project_File_Does_Not_Exist()
    {
        var (locator, _) = Create();

        var location = locator.Locate(Project, "Anything");

        Assert.Equal(Project, location.File.FullPath);
        Assert.Equal(1, location.Line);
    }

    [Fact]
    public void Should_Ignore_Files_That_Are_Not_Valid_Xml()
    {
        var (locator, fileSystem) = Create();
        fileSystem.CreateFile(Project).SetContent("<Project><unclosed>");
        fileSystem.CreateFile("/Working/Directory.Packages.props").SetContent("not xml at all");

        var location = locator.Locate(Project, "Newtonsoft.Json");

        Assert.Equal(Project, location.File.FullPath);
        Assert.Equal(1, location.Line);
    }

    [Fact]
    public void Should_Terminate_At_The_File_System_Root()
    {
        var (locator, fileSystem) = Create();
        fileSystem.CreateFile("/App.csproj").SetContent("<Project />");

        var location = locator.Locate("/App.csproj", "Anything");

        Assert.Equal("/App.csproj", location.File.FullPath);
        Assert.Equal(1, location.Line);
    }

    [Fact]
    public void Should_Reuse_Parsed_Documents_Between_Lookups()
    {
        var (locator, fileSystem) = Create();
        fileSystem.CreateFile(Project).SetContent(SdkProject);

        Assert.Equal(6, locator.Locate(Project, "Newtonsoft.Json").Line);

        // A cached document is not re-read: changing the file afterwards does not change the answer.
        fileSystem.CreateFile(Project).SetContent("<Project />");
        Assert.Equal(6, locator.Locate(Project, "Newtonsoft.Json").Line);
    }

    [Fact]
    public void Should_Throw_If_The_Project_File_Is_Null()
    {
        var (locator, _) = Create();

        Assertions.IsArgumentNullException(Record.Exception(() => locator.Locate(null, "X")), "projectFile");
    }

    [Fact]
    public void Should_Throw_If_The_Package_Name_Is_Blank()
    {
        var (locator, _) = Create();

        Assert.ThrowsAny<ArgumentException>(() => locator.Locate(Project, " "));
    }
}
