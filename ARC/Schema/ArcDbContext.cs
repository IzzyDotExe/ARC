
using System.ComponentModel.DataAnnotations.Schema;
using ARC.Extensions;

using DSharpPlus;
using DSharpPlus.Entities;
using Microsoft.EntityFrameworkCore;

using Microsoft.Extensions.Configuration;
using Npgsql;


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

    public List<Appeal> GetNextAppeal(ulong userSnowflake)
    {
        return Appeals.ToList().Where(x => x.UserSnowflake == (long)userSnowflake).ToList();
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

        
        DiscordWebhook discordWebhook;
        discordWebhook = await mailChannel.CreateWebhookAsync(User.Username);
        
        ChannelSnowflake = (long)mailChannel.Id;
        WebhookSnowflake = (long)discordWebhook.Id;
                
        await Arc.ArcDbContext.Modmails.AddAsync(this);
        await Arc.ArcDbContext.SaveChangesAsync();
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
                                //new DiscordButtonComponent(ButtonStyle.Danger, $"modmail.ban.{ModmailId}", "Ban", emoji: new DiscordComponentEmoji("🔨"))
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

        await File.AppendAllTextAsync($"./temp/transcript-{ModmailId}.html", $@"
            <div class=preamble>
            <div class=preamble__guild-icon-container><img class=preamble__guild-icon
                                                        src='{Guild.GetIconUrl(ImageFormat.Auto)}''
            alt='Guild icon' loading=lazy></div>
            <div class=preamble__entries-container>
            <div class=preamble__entry>{Guild.Name}</div>
            <div class=preamble__entry>{Channel.Name}</div>
            </div>
            </div>
        ");
        DiscordUser? author = null;
        for (int i = (msgs.Count - 1); i >= 0; i--)
        {
            var message = msgs[i];
            var newauthor = !message.Author.Equals(author);

            if (newauthor)
            {
                author = message.Author;
                await File.AppendAllTextAsync($"./temp/transcript-{ModmailId}.html", $@"
                    </div>
                    <div class=chatlog__message-group>
                ");
            }

            await File.AppendAllTextAsync($"./temp/transcript-{ModmailId}.html",
                $@"
                    <div id=chatlog__message-container-{message.Id} class=chatlog__message-container
                    data-message-id={message.Id}>
                    <div class=chatlog__message>
                    <div class=chatlog__message-aside>
                    "
            );

            if (newauthor)
            {
                await File.AppendAllTextAsync($"./temp/transcript-{ModmailId}.html",
                    $@"
                        <img class=chatlog__avatar
                        src='{author.GetAvatarUrl(ImageFormat.Auto)}'
                        alt=Avatar loading=lazy></div>
                    "
                );
            }
            else
            {
                await File.AppendAllTextAsync($"./temp/transcript-{ModmailId}.html",
                    $@"<div class=chatlog__short-timestamp title='{message.Timestamp}'>{message.Timestamp.Hour}:{message.Timestamp.Minute}</div></div>"
                );
            }
        
            if (newauthor)

                await File.AppendAllTextAsync($"./temp/transcript-{ModmailId}.html", $@"
           
                    <div class=chatlog__message-primary>
                    <div class=chatlog__header><span class=chatlog__author style=color:rgb(155,89,182) title={author.Username}
                    data-user-id={author.Id}>{author.Username}</span> <span class=chatlog__timestamp><a
                    href=#chatlog__message-container-{message.Id}>{message.Timestamp}</a></span>
                    </div>
                    <div class='chatlog__content chatlog__markdown'><span class=chatlog__markdown-preserve>{message.Content}</span></div>
                    </div>
                    </div>
                    </div>

                ");
            else 
                

                await File.AppendAllTextAsync($"./temp/transcript-{ModmailId}.html", $@"
           
                    <div class=chatlog__message-primary>
                    <div class=chatlog__header>
                    </div>
                    <div class='chatlog__content chatlog__markdown'><span class=chatlog__markdown-preserve>{message.Content}</span></div>
                    </div>
                    </div>
                    </div>

                ");
        }

        await File.AppendAllTextAsync($"./temp/transcript-{ModmailId}.html", $@"</div>
        <div class=postamble>
        <div class=postamble__entry>Saved {MessageCount} message(s)</div>
        </div>
        </body>
        </html>"
        );
    }

    public DiscordUser User => Arc.ClientInstance.GetUserAsync((ulong)UserSnowflake).GetAwaiter().GetResult();
    public DiscordWebhook Webhook => Channel.GetWebhooksAsync().GetAwaiter().GetResult()[0];
    public DiscordChannel Channel => Arc.ClientInstance.GetChannelAsync((ulong)ChannelSnowflake).GetAwaiter().GetResult();
    public DiscordGuild Guild => Arc.ClientInstance.GetGuildAsync(Channel.Guild.Id).GetAwaiter().GetResult();
    public DiscordMember Member => Guild.GetMemberAsync(User.Id).GetAwaiter().GetResult();
    [NotMapped] public int MessageCount => Channel.GetMessagesAsync().GetAwaiter().GetResult().Count;
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
    public DiscordUser User => Arc.ClientInstance.GetUserAsync((ulong)UserSnowflake).GetAwaiter().GetResult();
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

    internal DiscordEmbedBuilder CreateEmbedPage()
    {
        return new DiscordEmbedBuilder()
            .WithAuthor($"{User} Note #{NoteId}", null, User.GetAvatarUrl(ImageFormat.Auto))
            .WithDescription($"```{Note}```")
            .WithTimestamp(DateAdded)
            .WithFooter($"Note added by {Author.Username}#{Author.Discriminator}", Author.GetAvatarUrl(ImageFormat.Auto));
    }
}


