
using Arc.Schema;
using DSharpPlus;
using DSharpPlus.SlashCommands;
using Microsoft.Extensions.Configuration;
using Serilog;


namespace ARC.Modules
{
    internal abstract class ArcModule : ApplicationCommandModule
    {
        private static Dictionary<string, bool> _loadedDict = new Dictionary<string, bool>();
        protected readonly ArcDbContext DbContext;
        protected readonly IServiceProvider ServiceProvider;
        protected readonly DiscordClient ClientInstance;
        protected readonly IConfigurationRoot GlobalConfig;

        protected ArcModule(string moduleName)
        {

            var _loaded = _loadedDict.ContainsKey(moduleName);
            
            DbContext = Arc.Arc.ArcDbContext;
            ServiceProvider = Arc.Arc.ServiceProvider;
            ClientInstance = Arc.Arc.ClientInstance;
            GlobalConfig = Arc.Arc.GlobalConfig;

            if (_loaded)
                return;
            RegisterEvents();
            Log.Logger.Information("MODULE LOADED: {ModuleName}", moduleName);
            _loadedDict[moduleName] = true;
        }

        protected abstract void RegisterEvents();

    }
}
