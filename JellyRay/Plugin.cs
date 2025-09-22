using System.Text.RegularExpressions;
using JellRay.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using Microsoft.Extensions.Logging;

namespace JellyRay;

public class Plugin : BasePlugin<PluginConfiguration>, IPlugin, IHasWebPages
{
    private readonly ILogger<Plugin> _logger;
    private readonly IServerConfigurationManager _config;

    public override string Name => "JellyRay";
    public override Guid Id => Guid.Parse("C2EAC02A-9691-402A-A537-AE3583E6B02B");

    public static Plugin? Instance { get; private set; }

    public Plugin(
        IApplicationPaths applicationPaths,
        IXmlSerializer xmlSerializer, ILogger<Plugin> logger, IServerConfigurationManager config) : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
        _logger = logger;
        _config = config;

        if (string.IsNullOrWhiteSpace(applicationPaths.WebPath))
            return;

        var indexFile = Path.Combine(applicationPaths.WebPath, "index.html");
        if (!File.Exists(indexFile))
            return;

        string indexContents = File.ReadAllText(indexFile);
        string basePath = "";

        // Get base path from network config
        try
        {
            var networkConfig = _config.GetConfiguration("network");
            var configType = networkConfig.GetType();
            var basePathField = configType.GetProperty("BaseUrl");
            var confBasePath = basePathField?.GetValue(networkConfig)?.ToString()?.Trim('/');

            if (!string.IsNullOrEmpty(confBasePath))
                basePath = $"/{confBasePath}";
        }
        catch (Exception e)
        {
            logger.LogError("Unable to get base path from config, using '/': {0}", e);
        }

        // Don't run if script already exists
        string scriptReplace = "<script plugin=\"JellyRay\".*?></script>";
        string scriptElement =
            string.Format(
                "<script plugin=\"JellyRay\" version=\"1.0.0.2\" src=\"{0}/JellyRay/jellyray.js\"></script>",
                basePath);

        if (!indexContents.Contains(scriptElement))
        {
            logger.LogInformation("Attempting to inject preview script code in {0}", indexFile);

            // Replace old Jellyscrub scrips
            indexContents = Regex.Replace(indexContents, scriptReplace, "");

            // Insert script last in body
            int bodyClosing = indexContents.LastIndexOf("</body>", StringComparison.Ordinal);
            if (bodyClosing != -1)
            {
                indexContents = indexContents.Insert(bodyClosing, scriptElement);

                try
                {
                    File.WriteAllText(indexFile, indexContents);
                    logger.LogInformation("Finished injecting preview script code in {0}", indexFile);
                }
                catch (Exception e)
                {
                    logger.LogError("Encountered exception while writing to {0}: {1}", indexFile, e);
                }
            }
            else
            {
                logger.LogInformation("Could not find closing body tag in {0}", indexFile);
            }
        }
        else
        {
            logger.LogInformation("Found client script injected in {0}", indexFile);
        }
    }

    public IEnumerable<PluginPageInfo> GetPages()
    {
        return new List<PluginPageInfo>
        {
            new PluginPageInfo
            {
                Name = "configPage.html",
                EmbeddedResourcePath = "JellyRay.Web.configPage.html"
            }
        };
    }
}