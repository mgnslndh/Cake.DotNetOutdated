namespace Cake.DotNetOutdated.Report
{
    /// <summary>
    /// The severity of an available upgrade, as reported by dotnet-outdated.
    /// </summary>
    public enum DotNetOutdatedUpgradeSeverity
    {
        /// <summary>No upgrade is available.</summary>
        None,

        /// <summary>A patch upgrade is available.</summary>
        Patch,

        /// <summary>A minor upgrade is available.</summary>
        Minor,

        /// <summary>A major upgrade is available.</summary>
        Major,

        /// <summary>The severity could not be determined.</summary>
        Unknown,
    }
}
