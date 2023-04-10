using Arc.Schema;
using DSharpPlus.Entities;
using DSharpPlus;
namespace ARC.Extensions;

public static class GuildExtensions
{

    private static ArcDbContext DbContext => Arc.Arc.ArcDbContext;
    private static DiscordClient ClientInstance => Arc.Arc.ClientInstance;
    
    public static async Task Log(this DiscordGuild guild, DiscordMessageBuilder message)
    {
        
        if (!DbContext.Config[guild.Id].ContainsKey("logchannel"))
           return;

        ulong logChannelSnowflake = ulong.Parse(DbContext.Config[guild.Id]["logchannel"]);
        var channel = await ClientInstance.GetChannelAsync(logChannelSnowflake);

        await channel.SendMessageAsync(message);
        
    }
    
}