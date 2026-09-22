using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Cake.Core;
using Cake.Core.IO;

namespace Cake.DotNetOutdated.GitLab
{
    /// <summary>
    /// Writes GitLab Code Quality reports.
    /// </summary>
    public sealed class GitLabCodeQualityReportWriter
    {
        private readonly IFileSystem _fileSystem;
        private readonly ICakeEnvironment _environment;

        /// <summary>
        /// Initializes a new instance of the <see cref="GitLabCodeQualityReportWriter" /> class.
        /// </summary>
        /// <param name="fileSystem">The file system.</param>
        /// <param name="environment">The environment.</param>
        public GitLabCodeQualityReportWriter(IFileSystem fileSystem, ICakeEnvironment environment)
        {
            ArgumentNullException.ThrowIfNull(fileSystem);
            ArgumentNullException.ThrowIfNull(environment);

            _fileSystem = fileSystem;
            _environment = environment;
        }

        /// <summary>
        /// Writes a report file (UTF-8 without a byte order mark). An empty list writes <c>[]</c>.
        /// </summary>
        /// <param name="issues">The issues.</param>
        /// <param name="outputFile">The file to write; relative paths are resolved against the working directory.</param>
        public void Write(IEnumerable<GitLabCodeQualityIssue> issues, FilePath outputFile)
        {
            ArgumentNullException.ThrowIfNull(issues);
            ArgumentNullException.ThrowIfNull(outputFile);

            var json = Serialize(issues);
            var path = outputFile.MakeAbsolute(_environment);

            var directory = _fileSystem.GetDirectory(path.GetDirectory());
            if (!directory.Exists)
            {
                directory.Create();
            }

            var bytes = new UTF8Encoding(false).GetBytes(json);
            using var stream = _fileSystem.GetFile(path).Open(FileMode.Create, FileAccess.Write, FileShare.None);
            stream.Write(bytes, 0, bytes.Length);
        }

        /// <summary>
        /// Serializes issues to the GitLab Code Quality JSON format.
        /// </summary>
        /// <param name="issues">The issues.</param>
        /// <returns>The JSON text.</returns>
        public static string Serialize(IEnumerable<GitLabCodeQualityIssue> issues)
        {
            ArgumentNullException.ThrowIfNull(issues);

            using var stream = new MemoryStream();
            var options = new JsonWriterOptions
            {
                Indented = true,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            };

            using (var writer = new Utf8JsonWriter(stream, options))
            {
                writer.WriteStartArray();
                foreach (var issue in issues)
                {
                    writer.WriteStartObject();
                    writer.WriteString("description", issue.Description);
                    writer.WriteString("check_name", issue.CheckName);
                    writer.WriteString("fingerprint", issue.Fingerprint);
                    writer.WriteString("severity", ToJson(issue.Severity));
                    writer.WriteStartObject("location");
                    writer.WriteString("path", issue.Path);
                    writer.WriteStartObject("lines");
                    writer.WriteNumber("begin", issue.Line);
                    writer.WriteEndObject();
                    writer.WriteEndObject();
                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
            }

            return Encoding.UTF8.GetString(stream.ToArray());
        }

        private static string ToJson(GitLabCodeQualitySeverity severity)
        {
            return severity.ToString().ToLowerInvariant();
        }
    }
}
