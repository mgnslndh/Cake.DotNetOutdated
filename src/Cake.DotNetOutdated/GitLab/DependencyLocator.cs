using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using Cake.Core.IO;

namespace Cake.DotNetOutdated.GitLab
{
    /// <summary>
    /// The file and line where a package is declared.
    /// </summary>
    /// <param name="File">The declaring file.</param>
    /// <param name="Line">The 1-based line.</param>
    internal sealed record DependencyLocation(FilePath File, int Line);

    /// <summary>
    /// Finds the line on which a NuGet package is declared, so a finding can point at the exact dependency.
    /// </summary>
    /// <remarks>
    /// Lookup order: the nearest <c>Directory.Packages.props</c>, the project file, then
    /// <c>Directory.Build.props</c>/<c>.targets</c> walking up the tree, else line 1 of the project file.
    /// MSBuild property definitions are not followed: the element that declares the package is what is located.
    /// </remarks>
    internal sealed class DependencyLocator
    {
        private static readonly string[] CentralElements = { "PackageVersion", "GlobalPackageReference" };
        private static readonly string[] ReferenceElements = { "PackageReference" };
        private static readonly string[] BuildPropsElements = { "PackageReference", "PackageVersion" };

        private readonly IFileSystem _fileSystem;
        private readonly Dictionary<string, XDocument> _documents = new Dictionary<string, XDocument>(StringComparer.Ordinal);

        public DependencyLocator(IFileSystem fileSystem)
        {
            ArgumentNullException.ThrowIfNull(fileSystem);

            _fileSystem = fileSystem;
        }

        /// <summary>
        /// Locates the declaration of a package for a project.
        /// </summary>
        /// <param name="projectFile">The absolute path of the project file.</param>
        /// <param name="packageName">The package id.</param>
        /// <returns>The declaring file and line; line 1 of the project file if no declaration was found.</returns>
        public DependencyLocation Locate(FilePath projectFile, string packageName)
        {
            ArgumentNullException.ThrowIfNull(projectFile);
            ArgumentException.ThrowIfNullOrWhiteSpace(packageName);

            var directories = WalkUp(projectFile.GetDirectory()).ToList();

            // 1. Central package management: only the nearest Directory.Packages.props counts.
            foreach (var directory in directories)
            {
                var packagesProps = directory.CombineWithFilePath("Directory.Packages.props");
                if (!_fileSystem.Exist(packagesProps))
                {
                    continue;
                }

                if (TryFind(packagesProps, packageName, CentralElements, out var centralLine))
                {
                    return new DependencyLocation(packagesProps, centralLine);
                }

                break;
            }

            // 2. The project file itself.
            if (TryFind(projectFile, packageName, ReferenceElements, out var projectLine))
            {
                return new DependencyLocation(projectFile, projectLine);
            }

            // 3. Directory.Build.props / Directory.Build.targets, nearest first.
            foreach (var directory in directories)
            {
                foreach (var name in new[] { "Directory.Build.props", "Directory.Build.targets" })
                {
                    var buildFile = directory.CombineWithFilePath(name);
                    if (TryFind(buildFile, packageName, BuildPropsElements, out var buildLine))
                    {
                        return new DependencyLocation(buildFile, buildLine);
                    }
                }
            }

            // 4. Not declared anywhere we can see (transitive, non-SDK project, ...).
            return new DependencyLocation(projectFile, 1);
        }

        private static IEnumerable<DirectoryPath> WalkUp(DirectoryPath start)
        {
            var current = start;
            while (current != null)
            {
                yield return current;

                var parent = current.GetParent();
                if (parent == null || parent.FullPath == current.FullPath)
                {
                    yield break;
                }

                current = parent;
            }
        }

        private bool TryFind(FilePath file, string packageName, string[] elementNames, out int line)
        {
            line = 0;

            var document = Load(file);
            if (document?.Root == null)
            {
                return false;
            }

            foreach (var element in document.Descendants())
            {
                if (Array.IndexOf(elementNames, element.Name.LocalName) < 0)
                {
                    continue;
                }

                var id = (string)element.Attribute("Include") ?? (string)element.Attribute("Update");
                if (!string.Equals(id, packageName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var lineInfo = (IXmlLineInfo)element;
                line = lineInfo.HasLineInfo() ? lineInfo.LineNumber : 1;
                return true;
            }

            return false;
        }

        private XDocument Load(FilePath path)
        {
            if (_documents.TryGetValue(path.FullPath, out var cached))
            {
                return cached;
            }

            XDocument document = null;
            var file = _fileSystem.GetFile(path);
            if (file.Exists)
            {
                try
                {
                    using var stream = file.OpenRead();
                    document = XDocument.Load(stream, LoadOptions.SetLineInfo);
                }
                catch (XmlException)
                {
                    document = null;
                }
            }

            _documents[path.FullPath] = document;
            return document;
        }
    }
}
