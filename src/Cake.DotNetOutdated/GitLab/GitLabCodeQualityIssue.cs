namespace Cake.DotNetOutdated.GitLab
{
    /// <summary>
    /// A single finding in a GitLab Code Quality report.
    /// </summary>
    public sealed class GitLabCodeQualityIssue
    {
        /// <summary>
        /// Gets the human-readable description of the finding.
        /// </summary>
        public string Description { get; init; }

        /// <summary>
        /// Gets the name of the check (rule) that produced the finding.
        /// </summary>
        public string CheckName { get; init; }

        /// <summary>
        /// Gets the fingerprint that uniquely identifies the finding. GitLab shows findings with
        /// identical fingerprints as one entry and compares reports by fingerprint.
        /// </summary>
        public string Fingerprint { get; init; }

        /// <summary>
        /// Gets the severity.
        /// </summary>
        public GitLabCodeQualitySeverity Severity { get; init; }

        /// <summary>
        /// Gets the repository-relative path of the file, using forward slashes and no leading <c>./</c>.
        /// </summary>
        public string Path { get; init; }

        /// <summary>
        /// Gets the 1-based line the finding begins on.
        /// </summary>
        public int Line { get; init; }
    }
}
