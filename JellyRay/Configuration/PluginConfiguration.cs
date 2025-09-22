using MediaBrowser.Model.Plugins;

namespace JellRay.Configuration;

public class PluginConfiguration : BasePluginConfiguration
{
    public int NumFrames { get; set; } = 5;
    public double FrameWindowSeconds { get; set; } = 5.0;
    public string RecognizerApiUrl { get; set; } = "http://10.65.0.100:5000";
}