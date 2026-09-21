namespace Cake.DotNetOutdated.GitLab
{
    /// <summary>
    /// The severity of a GitLab Code Quality finding.
    /// </summary>
    public enum GitLabCodeQualitySeverity
    {
        /// <summary>Informational (<c>info</c>).</summary>
        Info,

        /// <summary>Minor (<c>minor</c>).</summary>
        Minor,

        /// <summary>Major (<c>major</c>).</summary>
        Major,

        /// <summary>Critical (<c>critical</c>).</summary>
        Critical,

        /// <summary>Blocker (<c>blocker</c>).</summary>
        Blocker,
    }
}
