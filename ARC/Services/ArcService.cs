using Arc.Schema;
using DSharpPlus;

using Microsoft.Extensions.Configuration;
using Serilog;

namespace Arc.Services;

public abstract class ArcService
{
    
    protected readonly ArcDbContext DbContext;
    protected readonly IServiceProvider ServiceProvider;
    protected readonly DiscordClient ClientInstance;
    protected readonly IConfigurationRoot GlobalConfig;
    
    protected ArcService(string serviceName)
    {
        DbContext = Arc.ArcDbContext;
        ServiceProvider = Arc.ServiceProvider;
        ClientInstance = Arc.ClientInstance;
        GlobalConfig = Arc.GlobalConfig;
        Log.Logger.Information($"SERVICE LOADED: {serviceName}");
    }
}