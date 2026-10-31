using Jellyfin.Plugin.ComicInfoExtras.Characters;
using Jellyfin.Plugin.ComicInfoExtras.Pages;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.ComicInfoExtras;

/// <summary>
/// Registers the plugin's services.
/// </summary>
public class PluginServiceRegistrator : IPluginServiceRegistrator
{
    /// <inheritdoc />
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.AddHostedService<CharacterSyncService>();
        serviceCollection.AddSingleton<PageCountService>();
        serviceCollection.AddTransient<IStartupFilter, WebScriptInjector>();
    }
}
