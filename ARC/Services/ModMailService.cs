using Arc.Schema;
using ARC.Extensions;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;

using Serilog;

using Arc.Exceptions;

namespace Arc.Services;

public class ModMailService : ArcService
{

    public ModMailService() : base("Modmail")
    {
        ClientInstance.MessageCreated += ClientInstanceOnMessageCreated;
        ClientInstance.ComponentInteractionCreated += ClientInstance_ComponentInteractionCreated;
        ClientInstance.ModalSubmitted += ClientInstanceOnModalSubmitted;
    }

    private async Task ClientInstanceOnModalSubmitted(DiscordClient sender, ModalSubmitEventArgs args)
    {
                
        var eventId = args.Interaction.Data.CustomId;

        if (!eventId.StartsWith("modmail"))
            return;
        
        var eventAction = ClientInstance.GetEventAction(eventId);

        if (eventAction == null)
            return;

        var modmail = DbContext.Modmails.Where(x => x.ModmailId == long.Parse(eventAction.Value.Item2)).First();

        switch (eventAction.Value.Item1)
        {
            case "modmail.ban.confirm":
                await args.Interaction.CreateResponseAsync(InteractionResponseType.UpdateMessage, new DiscordInteractionResponseBuilder().WithContent("Banning user..."));
                var member = modmail.Member;
                await SaveModMailSession(modmail, args.Interaction.User);
                await CloseModMailSession(modmail, args.Interaction.User);
                await BanMailUser(member, args.Values["modmail.ban.reason"]);
                break;
        }

        await args.Interaction.CreateResponseAsync(InteractionResponseType.Pong, new DiscordInteractionResponseBuilder());

    }

    private async Task ClientInstance_ComponentInteractionCreated(DiscordClient sender, ComponentInteractionCreateEventArgs args)
    {
        
        var eventId = args.Id;

        if (!eventId.StartsWith("modmail"))
            return;
        
        var eventAction = ClientInstance.GetEventAction(eventId);

        if (eventAction == null)
            return;

        var modmail = DbContext.Modmails.Where(x => x.ModmailId == long.Parse(eventAction.Value.Item2)).First();

        switch (eventAction.Value.Item1) {

            case "modmail.close": 
                await CloseModMailSession(modmail, args.Interaction.User);
                break;

            case "modmail.save":
                await SaveModMailSession(modmail, args.Interaction.User);
                await CloseModMailSession(modmail, args.Interaction.User);
                break;

            case "modmail.ban":
                await ConfirmBanUser(modmail, args.Interaction);
                break;
        
            default:
                break;

        }
        
    }

    private async Task ClientInstanceOnMessageCreated(DiscordClient sender, MessageCreateEventArgs e)
    {

        // If the channel is not a DM we will handle it as from moderators.
        if (!e.Channel.IsPrivate)
        {
            await HandleMailChannelMessage(sender, e);
            return;
        }
           

        // Find all the users modmail sessions
        var mails = DbContext.FetchMails(e.Author.Id);

        if (!mails.Any())
        {

            /* 
             * IN THIS CASE THERE ARE NO MODMAILS IN THE DATABASE FOR THE USER. WE WANT TO CREATE ONE!
             * We will first check if they said modmail and then if they did, we will create
             * a session.
            */

            if (!e.Message.Content.Contains("modmail") && !e.Message.Content.Contains("mod") && !e.Message.Content.Contains("mail"))
                return;

            // TODO: INSERT SERVER PICKING MECHANISM HERE
            // For now we will simply choose the billie server.
            Modmail? modmail = null;
            try
            {

                var guild = await sender.GetGuildAsync(ulong.Parse(GlobalConfig.GetSection("discord:guild").Value ?? "0"));
                modmail = new Modmail(e.Author.Id, guild);

                var session = await modmail.CreateSession();

                if (!session)
                {
                    throw new ArcInitFailedException("Modmail failed becase the session could not be created!");
                }

                await modmail.SendUserSystem("Your mod mail request was recieved! Please wait and a staff member will contact you shortly!");
                await modmail.SendModmailMenu();

            } catch (Exception ex) {

                Log.Logger.Error($"MODMAIL CREATION FAILED: {ex}");

                if (modmail != null)
                {
                    DbContext.Remove(modmail);
                    await DbContext.SaveChangesAsync();
                }
            }


        } else
        {

            if (e.Author.IsBot)
                return;

            // TODO: REVAMP REPLY SYSTEM TO SUPPORT MULTIPLE SERVERS
            var modmail = mails[0];
            

            if (e.Message.Content.ToLower().Equals("close session"))
            {
                // CLOSE MODMAIL CODE
                await SaveModMailSession(modmail, e.Author);
                await CloseModMailSession(modmail, e.Author);
                return;
            }


            await modmail.SendMods(e.Message);

        }

    }

    private async Task HandleMailChannelMessage(DiscordClient sender, MessageCreateEventArgs e)
    {

        var mail = DbContext.Modmails.ToList().Where(x => x.ChannelSnowflake == (long)e.Channel.Id).ToList();

        if (!mail.Any())
            return;

        if (e.Author.IsBot)
            return;

        if (e.Message.Content.StartsWith("#"))
            return;

        await mail[0].SendUser(e.Message);

    }

    private async Task CloseModMailSession(Modmail modmail, DiscordUser closer)
    {
        await modmail.SendUserSystem($"Your mod mail session was closed by {closer.Mention}!");
        await DbContext.CloseModmail(modmail);
    }

    private async Task SaveModMailSession(Modmail modmail, DiscordUser saver)
    {

        await modmail.SaveTranscript();

        // TODO: SAVE TO DATABASE AND DISPLAY FOR USER DASHBOARD INSTEAD
        var transcrpt = await ClientInstance.GetChannelAsync(ulong.Parse(DbContext.Config[modmail.Guild.Id]["transcriptchannel"]));
        var transcrptfiles = await ClientInstance.GetChannelAsync(ulong.Parse(GlobalConfig.GetSection("discord:debug_log").Value));
        
        var msg = new DiscordMessageBuilder();
        msg.AddFile(new FileStream($"./temp/transcript-{modmail.ModmailId}.html", FileMode.OpenOrCreate));
        var debug_file = await transcrptfiles.SendMessageAsync(msg);

        var embed = new DiscordEmbedBuilder()
            .WithModmailStyle()
            .WithTitle("Modmail Transcirpt")
            .WithDescription($"**Modmail with:** {modmail.User.Mention}\n**Saved** <t:{DateTimeOffset.Now.ToUnixTimeSeconds()}:R> **by** {saver.Mention}\n\n[Transcript]({debug_file.Attachments[0].Url})");

        await transcrpt.SendMessageAsync(embed);

    }
    
    private async Task BanMailUser(DiscordMember member, string reason)
    {
        var Author = Arc.ClientInstance.CurrentUser;

        var embed = new DiscordEmbedBuilder()
            .WithModmailStyle()
            .WithAuthor(Author.Username, "", Author.AvatarUrl)
            .WithDescription($"You have been banned in {member.Guild.Name} for: ``{reason}``")
            .Build();

        await member.SendMessageAsync(embed);
        await member.BanAsync(reason: "Banned during modmail for: "+reason);
    }
    
    private async Task ConfirmBanUser(Modmail modmail, DiscordInteraction interaction)
    {
        
        var resp = new DiscordInteractionResponseBuilder()
        {
            
            Title = "Are you sure you want to ban this user?",
            CustomId = $"modmail.ban.confirm.{modmail.ModmailId}"

        }.AddComponents(new TextInputComponent(label: "Reason",
            customId: $"modmail.ban.reason",
            placeholder: "reason",
            required: true, max_length: 30));
        
        await interaction.CreateResponseAsync(InteractionResponseType.Modal, resp);

    }
    
}