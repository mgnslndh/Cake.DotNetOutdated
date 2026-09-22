using System.Text;
using Cake.Core.IO;
using Cake.Testing;

namespace Cake.DotNetOutdated.Tests;

internal static class FileSystemHelpers
{
    public static string ReadAllText(this IFileSystem fileSystem, string path)
    {
        using var stream = fileSystem.GetFile(path).OpenRead();
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }

    public static void WriteAllBytes(this IFileSystem fileSystem, string path, byte[] content)
    {
        fileSystem.GetDirectory(new FilePath(path).GetDirectory()).Create();
        using var stream = fileSystem.GetFile(path).Open(FileMode.Create, FileAccess.Write, FileShare.None);
        stream.Write(content, 0, content.Length);
    }
}
