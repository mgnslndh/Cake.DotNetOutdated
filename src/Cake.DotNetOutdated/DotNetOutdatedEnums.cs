namespace Cake.DotNetOutdated
{
    /// <summary>
    /// Specifies whether pre-release package versions are considered (<c>--pre-release</c>).
    /// </summary>
    public enum DotNetOutdatedPreRelease
    {
        /// <summary>Consider pre-release versions only when the current version is a pre-release (tool default).</summary>
        Auto,

        /// <summary>Always look for pre-release versions.</summary>
        Always,

        /// <summary>Never look for pre-release versions.</summary>
        Never,
    }

    /// <summary>
    /// Specifies whether a package is locked to its current major or minor version (<c>--version-lock</c>).
    /// </summary>
    public enum DotNetOutdatedVersionLock
    {
        /// <summary>Do not lock the version (tool default).</summary>
        None,

        /// <summary>Lock to the current major version.</summary>
        Major,

        /// <summary>Lock to the current minor version.</summary>
        Minor,
    }

    /// <summary>
    /// Specifies the format of the generated report file (<c>--output-format</c>).
    /// </summary>
    public enum DotNetOutdatedOutputFormat
    {
        /// <summary>JSON (tool default).</summary>
        Json,

        /// <summary>Comma separated values.</summary>
        Csv,

        /// <summary>Markdown.</summary>
        Markdown,
    }

    /// <summary>
    /// Specifies the minimum log level of the NuGet credential service (<c>--nuget-cred-log-level</c>).
    /// </summary>
    public enum DotNetOutdatedCredentialLogLevel
    {
        /// <summary>Debug.</summary>
        Debug,

        /// <summary>Verbose.</summary>
        Verbose,

        /// <summary>Information.</summary>
        Information,

        /// <summary>Minimal.</summary>
        Minimal,

        /// <summary>Warning (tool default).</summary>
        Warning,

        /// <summary>Error.</summary>
        Error,
    }
}
