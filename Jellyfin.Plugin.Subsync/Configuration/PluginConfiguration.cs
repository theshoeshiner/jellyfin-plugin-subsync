using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.Subsync.Configuration;

/// <summary>
/// Plugin configuration.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PluginConfiguration"/> class.
    /// </summary>
    public PluginConfiguration()
    {
        SubSyncPath = string.Empty;
    }

    public string SubSyncPath { get; set; }

}
