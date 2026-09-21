namespace Cake.DotNetOutdated.Tests;

public sealed class ArchitectureTests
{
    private static DirectoryInfo FindSourceDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "Cake.DotNetOutdated.sln")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return new DirectoryInfo(Path.Combine(directory.FullName, "src", "Cake.DotNetOutdated"));
    }

    [Fact]
    public void Core_And_Report_Namespaces_Must_Not_Reference_GitLab()
    {
        var source = FindSourceDirectory();
        var gitLabDirectory = Path.Combine(source.FullName, "GitLab") + Path.DirectorySeparatorChar;

        var offenders = source
            .EnumerateFiles("*.cs", SearchOption.AllDirectories)
            .Where(file => !file.FullName.StartsWith(gitLabDirectory, StringComparison.OrdinalIgnoreCase))
            .Where(file => !file.FullName.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar))
            .Where(file => File.ReadAllText(file.FullName).Contains("Cake.DotNetOutdated.GitLab"))
            .Select(file => Path.GetRelativePath(source.FullName, file.FullName))
            .ToList();

        Assert.True(offenders.Count == 0, "These files reference the GitLab namespace: " + string.Join(", ", offenders));
    }

    [Fact]
    public void The_Source_Directory_Must_Contain_The_Add_In_Sources()
    {
        var source = FindSourceDirectory();

        Assert.True(source.EnumerateFiles("DotNetOutdatedTool.cs", SearchOption.TopDirectoryOnly).Any());
    }
}
