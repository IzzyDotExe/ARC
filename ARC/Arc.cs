
using Arc.Exceptions;
using Arc.Schema;
using Arc.Services;
using ARC.Services;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;

namespace Arc;


public class Arc
{
    
    private static DiscordClient? _clientInstance;
    private static IServiceProvider? _serviceProvider;
    private static ArcDbContext? _arcDbContext;
    private static IConfigurationRoot _globalConfig;

    public static DiscordClient ClientInstance
    {
        get
        {
            if (_clientInstance is not null)
                return _clientInstance;
            throw new ArcNotInitializedException();
        }
    }

    public static IServiceProvider ServiceProvider
    {
        get
        {
            if (_serviceProvider is not null)
                return _serviceProvider;
            throw new ArcNotInitializedException();
        }
    }

    public static ArcDbContext ArcDbContext
    {
        get
        {
            if (_arcDbContext is not null)
                return _arcDbContext;
            throw new ArcNotInitializedException();
        }
    }

    public static IConfigurationRoot GlobalConfig
    {
        get
        {
            if (_globalConfig is not null)
                return _globalConfig;

            throw new ArcNotInitializedException();
        }
    }

    public static void Main(string[] args)
    {
        
        var settingsPath = Path.Combine(Directory.GetCurrentDirectory(), "botconfig.json");

        if (!File.Exists(settingsPath))
            throw new ArcInitFailedException($"File \"${settingsPath}\" does not exist.");

        _globalConfig = new ConfigurationBuilder()
            .AddJsonFile(settingsPath)
            .Build();
        
        _arcDbContext = new ArcDbContext(_globalConfig.GetSection("db:dbstring").Value?? "none");

        StartDiscordBot(_globalConfig).GetAwaiter().GetResult();

    }

    private static async Task StartDiscordBot(IConfigurationRoot settings)
    {
        
        var logFactory = ConfigureLogger();

        var discordConfig = new DiscordConfiguration()
        {
            Token = settings.GetSection("discord:token").Value,
            TokenType = TokenType.Bot,
            Intents = DiscordIntents.All,
            MinimumLogLevel = LogLevel.Debug,
            LoggerFactory = logFactory
        };

        _clientInstance = new DiscordClient(discordConfig);
        _serviceProvider = ConfigureServices(settings);

        // Run any necessary steps before starting the bot here.
        ServiceProvider.GetRequiredService<UptimeService>();
        ServiceProvider.GetRequiredService<ModMailService>();
        ServiceProvider.GetRequiredService<SlashCommandsService>();

        // Connect to discord!
        await _clientInstance.ConnectAsync();
        _clientInstance.Ready += ClientInstanceOnReady;
          
        await Task.Delay(-1);
    }

    private static async Task ClientInstanceOnReady(DiscordClient sender, ReadyEventArgs e)
    {
        await Task.Run(() =>
        { 
            Log.Logger.Information($"Logged in as {sender.CurrentUser}");
            Log.Logger.Information($"Ready!");
            // ClientInstance.BulkOverwriteGlobalApplicationCommandsAsync(new List<DiscordApplicationCommand>() { });
        });
    }

    /// <summary>
    /// Configures logging for the program.
    /// </summary>
    /// <returns>Serilog logger factory</returns>
    private static ILoggerFactory ConfigureLogger() {

        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console()
            .CreateLogger();

        var logFactory = new LoggerFactory();

        return logFactory;

    }

    private static IServiceProvider ConfigureServices(IConfigurationRoot settings)
    {

        var services = new ServiceCollection()
            .AddSingleton<IConfigurationRoot>(settings)
            .AddSingleton<UptimeService>()
            .AddSingleton<ModMailService>()
            .AddSingleton<SlashCommandsService>()
            .BuildServiceProvider();

        return services;

    }
}


