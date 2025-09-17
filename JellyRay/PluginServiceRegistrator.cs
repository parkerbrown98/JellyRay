using JellyRay.Services;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace JellyRay;

public class PluginServiceRegistrator : IPluginServiceRegistrator
{
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost appHost)
    {
        serviceCollection.AddSingleton<FaceProcessingService>();
        serviceCollection.AddHostedService<FaceProcessingService>(p => p.GetService<FaceProcessingService>()!);
    }
}