namespace Cake.DotNetOutdated
{
    /// <summary>
    /// Contains the settings used by the upgrade command (<c>dotnet outdated --upgrade</c>).
    /// </summary>
    public class DotNetOutdatedUpgradeSettings : DotNetOutdatedSettings
    {
        /// <summary>
        /// Gets or sets a value indicating whether packages are upgraded without a restore preview
        /// and compatibility check (<c>--no-restore</c>).
        /// </summary>
        public bool NoRestore { get; set; }
    }
}
