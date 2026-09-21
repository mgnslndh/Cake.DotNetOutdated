using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Cake.Core;
using Cake.Core.IO;

namespace Cake.DotNetOutdated.Report
{
    /// <summary>
    /// Reads the JSON report written by dotnet-outdated.
    /// </summary>
    public sealed class DotNetOutdatedReportReader
    {
        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new UpgradeSeverityConverter() },
        };

        private readonly IFileSystem _fileSystem;
        private readonly ICakeEnvironment _environment;

        /// <summary>
        /// Initializes a new instance of the <see cref="DotNetOutdatedReportReader" /> class.
        /// </summary>
        /// <param name="fileSystem">The file system.</param>
        /// <param name="environment">The environment.</param>
        public DotNetOutdatedReportReader(IFileSystem fileSystem, ICakeEnvironment environment)
        {
            ArgumentNullException.ThrowIfNull(fileSystem);
            ArgumentNullException.ThrowIfNull(environment);

            _fileSystem = fileSystem;
            _environment = environment;
        }

        /// <summary>
        /// Reads a report file.
        /// </summary>
        /// <param name="path">The report file; relative paths are resolved against the working directory.</param>
        /// <returns>The report.</returns>
        public DotNetOutdatedReport Read(FilePath path)
        {
            ArgumentNullException.ThrowIfNull(path);

            var file = _fileSystem.GetFile(path.MakeAbsolute(_environment));
            if (!file.Exists)
            {
                throw new FileNotFoundException(
                    $"The dotnet-outdated report '{file.Path.FullPath}' could not be found.",
                    file.Path.FullPath);
            }

            using var stream = file.OpenRead();
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            return Parse(reader.ReadToEnd());
        }

        /// <summary>
        /// Parses report JSON.
        /// </summary>
        /// <param name="json">The JSON text.</param>
        /// <returns>The report.</returns>
        public static DotNetOutdatedReport Parse(string json)
        {
            ArgumentNullException.ThrowIfNull(json);

            try
            {
                return JsonSerializer.Deserialize<DotNetOutdatedReport>(json, Options) ?? new DotNetOutdatedReport();
            }
            catch (JsonException exception)
            {
                throw new CakeException("The dotnet-outdated report is not valid JSON: " + exception.Message, exception);
            }
        }

        private sealed class UpgradeSeverityConverter : JsonConverter<DotNetOutdatedUpgradeSeverity>
        {
            public override DotNetOutdatedUpgradeSeverity Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                if (reader.TokenType == JsonTokenType.String
                    && Enum.TryParse(reader.GetString(), true, out DotNetOutdatedUpgradeSeverity value)
                    && Enum.IsDefined(value))
                {
                    return value;
                }

                if (reader.TokenType is JsonTokenType.StartObject or JsonTokenType.StartArray)
                {
                    reader.Skip();
                }

                return DotNetOutdatedUpgradeSeverity.Unknown;
            }

            public override void Write(Utf8JsonWriter writer, DotNetOutdatedUpgradeSeverity value, JsonSerializerOptions options)
            {
                writer.WriteStringValue(value.ToString());
            }
        }
    }
}
