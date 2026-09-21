using System.Text;
using System.Text.RegularExpressions;

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

    /// <summary>
    /// Determines whether C# source code refers to the GitLab namespace, qualified or relative. Comments and string
    /// literals are ignored.
    /// </summary>
    internal static bool ReferencesGitLab(string source)
    {
        var code = StripCommentsAndStrings(source);

        return code.Contains("Cake.DotNetOutdated.GitLab", StringComparison.Ordinal)
            || Regex.IsMatch(code, @"(?<![\w.])GitLab\s*\.")
            || Regex.IsMatch(code, @"\busing\s+GitLab\b");
    }

    private static string StripCommentsAndStrings(string source)
    {
        var result = new StringBuilder(source.Length);
        var i = 0;

        while (i < source.Length)
        {
            var c = source[i];
            var next = i + 1 < source.Length ? source[i + 1] : '\0';

            if (c == '/' && next == '/')
            {
                while (i < source.Length && source[i] != '\n')
                {
                    i++;
                }
            }
            else if (c == '/' && next == '*')
            {
                var end = source.IndexOf("*/", i + 2, StringComparison.Ordinal);
                i = end < 0 ? source.Length : end + 2;
                result.Append(' ');
            }
            else if (c == '"')
            {
                var quotes = 1;
                while (i + quotes < source.Length && source[i + quotes] == '"')
                {
                    quotes++;
                }

                if (quotes >= 3)
                {
                    // Raw string literal: ends at the same number of quotes.
                    var end = source.IndexOf(new string('"', quotes), i + quotes, StringComparison.Ordinal);
                    i = end < 0 ? source.Length : end + quotes;
                }
                else if (quotes == 2)
                {
                    // Empty string.
                    i += 2;
                }
                else
                {
                    var verbatim = i > 0 && (source[i - 1] == '@' || (i > 1 && source[i - 1] == '$' && source[i - 2] == '@'));
                    i = SkipString(source, i + 1, verbatim);
                }

                result.Append("\"\"");
            }
            else if (c == '\'')
            {
                i++;
                while (i < source.Length && source[i] != '\'' && source[i] != '\n')
                {
                    i += source[i] == '\\' ? 2 : 1;
                }

                i++;
                result.Append("''");
            }
            else
            {
                result.Append(c);
                i++;
            }
        }

        return result.ToString();
    }

    private static int SkipString(string source, int index, bool verbatim)
    {
        while (index < source.Length)
        {
            var c = source[index];

            if (verbatim)
            {
                if (c == '"')
                {
                    if (index + 1 < source.Length && source[index + 1] == '"')
                    {
                        index += 2;
                        continue;
                    }

                    return index + 1;
                }
            }
            else if (c == '\\')
            {
                index += 2;
                continue;
            }
            else if (c == '"' || c == '\n')
            {
                return index + 1;
            }

            index++;
        }

        return index;
    }

    [Fact]
    public void Core_And_Report_Namespaces_Must_Not_Reference_GitLab()
    {
        var source = FindSourceDirectory();
        var gitLabDirectory = Path.Combine(source.FullName, "GitLab") + Path.DirectorySeparatorChar;

        var offenders = source
            .EnumerateFiles("*.cs", SearchOption.AllDirectories)
            .Where(file => !file.FullName.StartsWith(gitLabDirectory, StringComparison.OrdinalIgnoreCase))
            .Where(file => !IsBuildOutput(Path.GetRelativePath(source.FullName, file.FullName)))
            .Where(file => ReferencesGitLab(File.ReadAllText(file.FullName)))
            .Select(file => Path.GetRelativePath(source.FullName, file.FullName))
            .ToList();

        Assert.True(offenders.Count == 0, "These files reference the GitLab namespace: " + string.Join(", ", offenders));
    }

    private static bool IsBuildOutput(string relativePath)
    {
        return relativePath
            .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Contains("obj", StringComparer.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("Cake.DotNetOutdated.GitLab.Foo x;")]
    [InlineData("    GitLab.GitLabCodeQualityIssue issue;")]
    [InlineData("using GitLab;")]
    [InlineData("var x = new Cake.DotNetOutdated.GitLab.Foo(); // trailing comment")]
    [InlineData("/* comment */ GitLab.Foo x;")]
    public void ReferencesGitLab_Should_Flag_Code_That_Uses_The_GitLab_Namespace(string source)
    {
        Assert.True(ReferencesGitLab(source));
    }

    [Theory]
    [InlineData("// GitLab Code Quality")]
    [InlineData("    /// <summary>for GitLab.</summary>")]
    [InlineData("/* GitLab.Foo\n   using GitLab; */ int x;")]
    [InlineData("MyGitLabThing thing;")]
    [InlineData("var text = \"GitLab.Foo\";")]
    [InlineData("var x = 1; // GitLab.Foo")]
    public void ReferencesGitLab_Should_Not_Flag_Comments_Strings_Or_Other_Identifiers(string source)
    {
        Assert.False(ReferencesGitLab(source));
    }

    [Fact]
    public void The_Source_Directory_Must_Contain_The_Add_In_Sources()
    {
        var source = FindSourceDirectory();

        Assert.True(source.EnumerateFiles("DotNetOutdatedTool.cs", SearchOption.TopDirectoryOnly).Any());
    }
}
