using System.ComponentModel.DataAnnotations;
using Arc.Exceptions;
using ARC.Extensions;
using DocumentFormat.OpenXml.Bibliography;
using DocumentFormat.OpenXml.ExtendedProperties;
using DSharpPlus;
using DSharpPlus.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Serilog;

namespace Arc.Schema;

public class ArcDbContext : DbContext
{
    
    public DbSet<Modmail> Modmails { get; set; }
    public DbSet<UserNote> UserNotes { get; set; }
    public DbSet<Appeal> Appeals { get; set; }
    public DbSet<GuildConfig> GuildConfigs { get; set; }

    public Dictionary<ulong, Dictionary<string, string>> Config
    {
        get
        {
            Dictionary<ulong,Dictionary<string, string>> configs = new();
            foreach (var config in GuildConfigs.ToList())
            {

                if (!configs.ContainsKey((ulong)config.ConfigGuildSnowflake))
                    configs[(ulong)config.ConfigGuildSnowflake] = new Dictionary<string, string>();

                configs[(ulong)config.ConfigGuildSnowflake][config.ConfigKey] = config.ConfigValue;
            }
            return configs;
        }
    }

    private string DbPath { get; }

    public ArcDbContext(string dbPath)
    {
        DbPath = dbPath;
    }

    public ArcDbContext()
    {
        
        var settingsPath = Path.Combine(Directory.GetCurrentDirectory(), "botconfig.json");
        
        var settings = new ConfigurationBuilder()
            .AddJsonFile(settingsPath)
            .Build();
        
        DbPath = settings.GetSection("db:dbstring").Value?? "none";
        
    }

    public List<Modmail> FetchMails(ulong UserID)
    {
        var mails = Modmails.Where(x => x.UserSnowflake == (long)UserID);
        return mails.ToList();
    }

    public async Task CloseModmail(Modmail modmail)
    {
        Modmails.Remove(modmail);
        await modmail.Channel.DeleteAsync();
        await SaveChangesAsync();
    }

    public List<UserNote> GetUserNotes(ulong userSnowflake, ulong guildSnowflake)
    {
        var notes = UserNotes.Where(x => x.UserSnowflake == (long)userSnowflake && x.GuildSnowflake == (long)guildSnowflake).ToList();
        return notes;
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder.UseNpgsql(new NpgsqlConnection(DbPath));

}

[PrimaryKey("ModmailId")]
public class Modmail
{

    public long ModmailId { get; }
    public long ChannelSnowflake { get; set; }
    public long WebhookSnowflake { get; set; }
    public long UserSnowflake { get; set; }

    private DiscordGuild? _guildCreate = null;

    public Modmail(long channelSnowflake, long webhookSnowflake, long userSnowflake)
    {
        ChannelSnowflake = channelSnowflake;
        WebhookSnowflake = webhookSnowflake;
        UserSnowflake = userSnowflake;
    }

    public Modmail(ulong userSnowflake, DiscordGuild guild)
    {
        UserSnowflake = (long)userSnowflake;
        _guildCreate = guild;
    }

    public async Task<bool> CreateSession()
    {

        if (_guildCreate is null)
            return false;

        var guildConfig = Arc.ArcDbContext.Config[_guildCreate.Id];

        if (!guildConfig.ContainsKey("modmailchannel"))
            return false;

        var modmailChannelSnowflake = ulong.Parse(guildConfig["modmailchannel"]);

        var mailCategory = _guildCreate.GetChannel(modmailChannelSnowflake);

        if (!mailCategory.IsCategory)
            return false;

        var mailChannel = await _guildCreate.CreateTextChannelAsync(
            $"Modmail-{User.Username}.{User.Discriminator}",
            mailCategory,
            $"Modmail with user: {User}"
        );

        ChannelSnowflake = (long)mailChannel.Id;
        Uri avatarUri = new Uri(User.GetAvatarUrl(DSharpPlus.ImageFormat.Auto));
        DiscordWebhook discordWebhook;
        using (var httpClient = new HttpClient())
        {
            var uriWoQuery = avatarUri.GetLeftPart(UriPartial.Path);
            var fileExt = Path.GetExtension(uriWoQuery);

            var path = Path.Combine(Path.GetTempPath(), $"avatar{fileExt}");
            var imageBytes = await httpClient.GetByteArrayAsync(avatarUri);
            Stream filestream = new MemoryStream(imageBytes);
            discordWebhook = await mailChannel.CreateWebhookAsync(User.Username, avatar: filestream);
        }
        WebhookSnowflake = (long)discordWebhook.Id;

        return true;

    }

    public async Task SendUser(DiscordMessage message)
    {

        var embed = new DiscordEmbedBuilder()
             .WithModmailStyle()
             .WithAuthor(message.Author.Username, "", message.Author.AvatarUrl)
             .WithDescription(message.Content)
             .Build();

        if (!string.IsNullOrWhiteSpace(message.Content))
            await Member.SendMessageAsync(embed);

        if (message.Attachments.Count > 0)
        {
            foreach (var attachment in message.Attachments)
            {
                var emb = new DiscordEmbedBuilder()
                    .WithModmailStyle()
                    .WithAuthor(message.Author.Username, "", message.Author.AvatarUrl)
                    .WithDescription("You've received an image!")
                    .WithImageUrl(attachment.ProxyUrl)
                    .Build();

                await Member.SendMessageAsync(emb);
            }
        }

    }

    public async Task SendUserSystem(string message)
    {
        var Author = Arc.ClientInstance.CurrentUser;

        var embed = new DiscordEmbedBuilder()
            .WithModmailStyle()
            .WithAuthor(Author.Username, "", Author.AvatarUrl)
            .WithDescription(message)
            .Build();

        await Member.SendMessageAsync(embed);
    }

    public async Task SendMods(DiscordMessage message)
    {

        if (message.Attachments.Count > 0)
        {
            foreach (var attachment in message.Attachments)
            {

                var attachmentWebhook = new DiscordWebhookBuilder()
                    .WithAvatarUrl(User.GetAvatarUrl(DSharpPlus.ImageFormat.Auto))
                    .WithContent(attachment.ProxyUrl);

                await attachmentWebhook.SendAsync(Webhook);

            }
        }

        var webhook = new DiscordWebhookBuilder()
            .WithAvatarUrl(User.GetAvatarUrl(DSharpPlus.ImageFormat.Auto))
            .WithContent(message.Content);

        await webhook.SendAsync(Webhook);

    }

    public async Task SendModmailMenu()
    {

        var embed = new DiscordEmbedBuilder()
                    .WithModmailStyle()
                    .WithTitle("Mod Mail")
                    .WithDescription($"A Mod Mail session was opened by {Member.Mention}");

        var buttons = new List<DiscordButtonComponent>() {
                                new DiscordButtonComponent(ButtonStyle.Secondary, $"modmail.save.{ModmailId}", "Save and Close", emoji: new DiscordComponentEmoji("📝")),
                                new DiscordButtonComponent(ButtonStyle.Danger, $"modmail.close.{ModmailId}", "Close", emoji: new DiscordComponentEmoji("🔒")),
                                new DiscordButtonComponent(ButtonStyle.Danger, $"modmail.ban.{ModmailId}", "Ban", emoji: new DiscordComponentEmoji("🔨"))
                            };

        DiscordMessageBuilder message = new DiscordMessageBuilder()
                                .WithEmbed(embed)
                                .AddComponents(buttons);

        await Channel.SendMessageAsync(message);
    }

    public async Task SaveTranscript()
    {
        File.Delete($"./temp/transcript-{ModmailId}.html");
        File.Copy("./template.html", $"./temp/transcript-{ModmailId}.html");

        IReadOnlyList<DiscordMessage> msgs = await Channel.GetMessagesAsync(2000);

        for (int i = (msgs.Count - 1); i >= 0; i--)
        {
            var message = msgs[i];

            await File.AppendAllTextAsync($"./temp/transcript-{ModmailId}.html",

            $@"

                            <div class='discord-message'>
                            <div class='discord-author-avatar'><img src='{message.Author.GetAvatarUrl(ImageFormat.Auto)}' alt='{message.Author.Username}'></div>
                            <div class='discord-message-content'>
                                <div><span class='discord-author-info'><span class='discord-author-username' style=''>
                                    {message.Author.Username}

                                

                                    </span></span> <span class='discord-message-timestamp'>
                                    {message.Timestamp.ToString()}
                                    </span>
                                </div>
                                <div class='discord-message-body'>
                                    <!---->
                                    {message.Content}
                                    <!---->
                                </div>
                            </div>
                        </div>
                    
                    "

            );

        }

        await File.AppendAllTextAsync($"./temp/transcript-{ModmailId}.html", @"</div>
                            </body>
                            </html>");
    }

    public DiscordUser User => Arc.ClientInstance.GetUserAsync((ulong)UserSnowflake).GetAwaiter().GetResult();
    public DiscordWebhook Webhook => Arc.ClientInstance.GetWebhookAsync((ulong) WebhookSnowflake).GetAwaiter().GetResult();
    public DiscordChannel Channel => Arc.ClientInstance.GetChannelAsync((ulong)ChannelSnowflake).GetAwaiter().GetResult();
    public DiscordGuild Guild => Arc.ClientInstance.GetGuildAsync(Channel.Guild.Id).GetAwaiter().GetResult();
    public DiscordMember Member => Guild.GetMemberAsync(User.Id).GetAwaiter().GetResult();

    private Modmail()
    {
        
    }

}

[PrimaryKey("ConfigId")]
public class GuildConfig
{
    public int ConfigId { get; }
    public long ConfigGuildSnowflake { get; set; }
    public string ConfigKey { get; set; }
    public string ConfigValue { get; set; }

    public GuildConfig(long configGuildSnowflake, string configKey, string configValue) {
        ConfigGuildSnowflake = configGuildSnowflake;
        ConfigKey = configKey;
        ConfigValue = configValue;
    }

    private GuildConfig()
    {

    }
}

[PrimaryKey("AppealId")]
public class Appeal
{
    
    public long AppealId { get; }
    public long UserSnowflake { get; set; }
    public DateTime NextAppeal { get; set; }

    public Appeal(long userSnowflake, DateTime nextAppeal)
    {
        UserSnowflake = userSnowflake;
        NextAppeal = nextAppeal;
    }

    private Appeal()
    {
        
    }
    
}

[PrimaryKey("NoteId")]
public class UserNote
{
    
    public long NoteId { get; }
    public long UserSnowflake { get; set; }
    public long GuildSnowflake { get; set; }
    public string Note { get; set; }
    public DateTime DateAdded { get; set; }
    public long AuthorSnowflake { get; set; }

    public UserNote(long guildSnowflake, long userSnowflake, string note, DateTime dateAdded, long authorSnowflake)
    {

        GuildSnowflake = guildSnowflake;
        UserSnowflake = userSnowflake;
        Note = note;
        DateAdded = dateAdded;
        AuthorSnowflake = authorSnowflake;

    }

    private UserNote()
    {
        
    }

    public DiscordUser User => Arc.ClientInstance.GetUserAsync((ulong)UserSnowflake).GetAwaiter().GetResult();
    public DiscordGuild Guild => Arc.ClientInstance.GetGuildAsync((ulong)GuildSnowflake).GetAwaiter().GetResult();
    public DiscordMember Member => Guild.GetMemberAsync(User.Id).GetAwaiter().GetResult();
    public DiscordUser Author => Arc.ClientInstance.GetUserAsync((ulong)AuthorSnowflake).GetAwaiter().GetResult();
    public DiscordMember AuthorMember => Guild.GetMemberAsync(Author.Id).GetAwaiter().GetResult();

}


