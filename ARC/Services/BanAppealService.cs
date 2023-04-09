using System.Reflection.Metadata;
using Arc.Schema;
using Arc.Services;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;

namespace ARC.Services;

public class BanAppealService : ArcService
{
    public BanAppealService() : base("Appeal")
    {
        
        ClientInstance.ComponentInteractionCreated += ClientInstanceOnComponentInteractionCreated;
        ClientInstance.ModalSubmitted += ClientInstanceOnModalSubmitted;
        
    }

    private async Task ClientInstanceOnModalSubmitted(DiscordClient sender, ModalSubmitEventArgs args)
    {
        
        if (args.Interaction.Data.CustomId != "banappeal.recieved") 
            return;

        var appeals = DbContext.GetNextAppeal(args.Interaction.User.Id);
        var appeal = appeals.Any()? appeals[0] : null;
        
        if (appeal == null || appeal.NextAppeal.ToUniversalTime().CompareTo(DateTime.UtcNow) < 0)
        {
            
            if (appeal is not null)
                DbContext.Appeals.Remove(appeal);
            
            appeal = new Appeal((long)args.Interaction.User.Id, DateTime.UtcNow.AddDays(30));   
            
            DbContext.Appeals.Add(appeal);
            await DbContext.SaveChangesAsync();

            appeal = DbContext.GetNextAppeal((ulong)appeal.UserSnowflake)[0];
            
            DiscordEmbed embed = new DiscordEmbedBuilder()
                .WithAuthor($"New Ban Appeal From {args.Interaction.User.Username}#{args.Interaction.User.Discriminator}", iconUrl:args.Interaction.User.GetAvatarUrl(ImageFormat.Auto))
                .AddField("Which moderator banned you?", args.Values["banappeal.response.mod"])
                .AddField("What was the reason given for your ban?", args.Values["banappeal.response.reason"])
                .AddField("Why do you think you should be unbanned?", args.Values["banappeal.response.why"])
                .WithTimestamp(DateTime.UtcNow);

            var buttons = new List<DiscordButtonComponent>() {
                new DiscordButtonComponent(ButtonStyle.Success, $"banappeal.unban.{appeal.AppealId}", "Unban", false, new DiscordComponentEmoji("🔓")),
                new DiscordButtonComponent(ButtonStyle.Danger, $"banappeal.deny.{appeal.AppealId}", "Deny", false, new DiscordComponentEmoji("🔨"))
            };

            var response = new DiscordMessageBuilder()
                .AddEmbed(embed)
                .AddComponents(buttons);

            var appealChannelSnowflake = ulong.Parse(DbContext.Config[args.Interaction.Guild.Id]["appealschannel"]);
            var appealChannel = await ClientInstance.GetChannelAsync(appealChannelSnowflake);

            
            await args.Interaction.CreateResponseAsync(InteractionResponseType.DeferredMessageUpdate);
            await appealChannel.SendMessageAsync(response);
            return;
            
        }

        DiscordEmbed embed2 = new DiscordEmbedBuilder()
            .WithAuthor($"{args.Interaction.User.Username}#{args.Interaction.User.Discriminator}, You've already appealed", iconUrl:args.Interaction.User.GetAvatarUrl(ImageFormat.Auto))
            .WithColor(DiscordColor.Red)
            .WithFooter("Thank you for your patience!", ClientInstance.CurrentUser.GetAvatarUrl(ImageFormat.Auto))
            .WithDescription($"Please wait for the result of your appeal. If you already received it, you can wait out your ban duration. If you were permanently banned, you can appeal again on <t:{(Int32)(appeal.NextAppeal.Subtract(new DateTime(1970, 1, 1))).TotalSeconds}>")
            .WithTimestamp(DateTime.UtcNow);

        var user = await args.Interaction.Guild.GetMemberAsync(args.Interaction.User.Id);
        await user.SendMessageAsync(embed2);
        await args.Interaction.CreateResponseAsync(InteractionResponseType.DeferredMessageUpdate);

    }

    private async Task ClientInstanceOnComponentInteractionCreated(DiscordClient sender, ComponentInteractionCreateEventArgs args)
    {
        
        switch (args.Id)
        {
            case "banappeal.send":
                await HandleBanAppeal(args);
                break;
        }
        
        if (args.Id.Contains("banappeal.deny"))
            await HandleAppealDeny(args);
        
        if (args.Id.Contains("banappeal.unban"))
            await HandleAppealAccept(args);
        
    }

    private async Task HandleAppealAccept(ComponentInteractionCreateEventArgs args)
    {
        
        var appealId = long.Parse(args.Id.Split('.')[2]);
        var appeals = DbContext.Appeals.ToList().Where(x => x.AppealId == appealId).ToList();
        
        if (!appeals.Any())
            await args.Interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                new DiscordInteractionResponseBuilder()
                    .WithContent($"ERROR: appeal not found, you can delete it.")
                    .AsEphemeral());
        
        var appeal = appeals[0];
        
        var appealGuildSnowflake = ulong.Parse(DbContext.Config[args.Interaction.Guild.Id]["mainserver"]);
        var guild = await ClientInstance.GetGuildAsync(appealGuildSnowflake);

        await guild.UnbanMemberAsync(appeal.User);

        DbContext.Appeals.Remove(appeal);
        await DbContext.SaveChangesAsync();
        
        var msg = args.Message.Embeds[0];

        var mbuilder= new DiscordInteractionResponseBuilder()
            .AddEmbed(new DiscordEmbedBuilder(msg)
                .WithColor(DiscordColor.Green)
                .WithTitle($"Appeal Accepted by {args.User.Username}#{args.User.Discriminator}!"));

        await args.Interaction.CreateResponseAsync(InteractionResponseType.UpdateMessage, mbuilder);

        try
        {
            var member = await args.Guild.GetMemberAsync(appeal.User.Id);
            await member.SendMessageAsync(new DiscordEmbedBuilder()
                .WithAuthor(
                    $"{member.Username}#{member.Discriminator}, your ban appeal was accepted by {args.User.Username}#{args.User.Discriminator}!",
                    iconUrl: member.GetAvatarUrl(ImageFormat.Auto))
                .WithColor(DiscordColor.Green)
                .WithFooter("Thank you for your patience!", ClientInstance.CurrentUser.GetAvatarUrl(ImageFormat.Auto))
                .WithDescription(
                    $"Congrats!, your ban appeal was accepted in {guild.Name}!")
                .WithTimestamp(DateTime.UtcNow));
        }
        catch (Exception ex)
        {
            
        }
    }

    private async Task HandleAppealDeny(ComponentInteractionCreateEventArgs args)
    {
                
        var appealId = long.Parse(args.Id.Split('.')[2]);
        var appeals = DbContext.Appeals.ToList().Where(x => x.AppealId == appealId).ToList();
        
        if (!appeals.Any())
            await args.Interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                new DiscordInteractionResponseBuilder()
                    .WithContent($"ERROR: appeal not found! You can delete it.")
                    .AsEphemeral());
        
        var appeal = appeals[0];
        
        var appealGuildSnowflake = ulong.Parse(DbContext.Config[args.Interaction.Guild.Id]["mainserver"]);
        var guild = await ClientInstance.GetGuildAsync(appealGuildSnowflake);

        var msg = args.Message.Embeds[0];

        var mbuilder= new DiscordInteractionResponseBuilder()
            .AddEmbed(new DiscordEmbedBuilder(msg)
                .WithColor(DiscordColor.Red)
                .WithTitle($"Appeal Denied by {args.User.Username}#{args.User.Discriminator}!"));

        await args.Interaction.CreateResponseAsync(InteractionResponseType.UpdateMessage, mbuilder);

        try
        {
            var member = await args.Guild.GetMemberAsync(appeal.User.Id);
            await member.SendMessageAsync(new DiscordEmbedBuilder()
                .WithAuthor(
                    $"{member.Username}#{member.Discriminator}, your ban appeal was denied by {args.User.Username}#{args.User.Discriminator}!",
                    iconUrl: member.GetAvatarUrl(ImageFormat.Auto))
                .WithColor(DiscordColor.Red)
                .WithFooter("Thank you for your patience!", ClientInstance.CurrentUser.GetAvatarUrl(ImageFormat.Auto))
                .WithDescription(
                    "Unfortunately, your ban appeal was rejected. If you've been temporarily banned, please wait the duration of your ban. If you were permanently banned, you can appeal again in a month.")
                .WithTimestamp(DateTime.UtcNow));
        }
        catch (Exception ex)
        {
            
        }
    }

    private async Task HandleBanAppeal(ComponentInteractionCreateEventArgs args)
    {
        
        var resp = new DiscordInteractionResponseBuilder()
            .WithTitle("Ban appeal")
            .WithCustomId("banappeal.recieved")
            .AddComponents( new TextInputComponent(label:"Which moderator banned you?",
                customId: $"banappeal.response.mod", 
                placeholder:"Izzy#4810", 
                required: true, max_length: 30))
            .AddComponents( new TextInputComponent(label:"What was the reason given for your ban?", 
                customId:"banappeal.response.reason", 
                placeholder:"Reason...", required: true, max_length: 30))
            .AddComponents( new TextInputComponent(label:"Why do you think you should be unbanned?",
                customId:"banappeal.response.why", placeholder:"Explain why...",
                required: true, style: TextInputStyle.Paragraph));
        
        var appealGuildSnowflake = ulong.Parse(DbContext.Config[args.Interaction.Guild.Id]["mainserver"]);
        var guild = await ClientInstance.GetGuildAsync(appealGuildSnowflake);

        var bans = await guild.GetBansAsync();

        if (bans.All(x => x.User.Id != args.User.Id))
            await args.Interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                new DiscordInteractionResponseBuilder()
                    .WithContent($"You are not banned in {guild.Name}")
                    .AsEphemeral());
            
        await args.Interaction.CreateResponseAsync(InteractionResponseType.Modal, resp);

    }
}