
using Arc.Schema;
using DSharpPlus;
using DSharpPlus.SlashCommands;
using Microsoft.Extensions.Configuration;
using Serilog;


namespace ARC.Modules
{
    internal abstract class ArcModule : ApplicationCommandModule
    {

        protected bool _loaded = false;
        protected readonly ArcDbContext DbContext;
        protected readonly IServiceProvider ServiceProvider;
        protected readonly DiscordClient ClientInstance;
        protected readonly IConfigurationRoot GlobalConfig;

        protected ArcModule(string moduleName)
        {
            
            DbContext = Arc.Arc.ArcDbContext;
            ServiceProvider = Arc.Arc.ServiceProvider;
            ClientInstance = Arc.Arc.ClientInstance;
            GlobalConfig = Arc.Arc.GlobalConfig;

            if (_loaded)
                return;
            
            RegisterEvents();
            Log.Logger.Information($"MODULE LOADED: {moduleName}");
            _loaded = true;
        }

        protected abstract void RegisterEvents();

    }
}
