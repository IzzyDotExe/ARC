using Arc.Schema;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using DSharpPlus.SlashCommands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ARC.Modules
{
    internal class ModerationModule : ArcModule
    {

        public ModerationModule() : base("Moderation") {

            ClientInstance.ComponentInteractionCreated += ClientInstance_ComponentInteractionCreated;

        }

        [ContextMenu(DSharpPlus.ApplicationCommandType.UserContextMenu, "User Notes", false),
         SlashCommandPermissions(DSharpPlus.Permissions.ManageMessages)]
        public async Task UserNotes(ContextMenuContext ctx)
        {

            var embed = new DiscordEmbedBuilder()
                        .WithAuthor($"{ctx.TargetUser} Notes", null, ctx.TargetMember.GetAvatarUrl(ImageFormat.Auto))
                        .WithColor(DiscordColor.Blurple);

            var response = new DiscordInteractionResponseBuilder()
                .AddComponents(new List<DiscordButtonComponent>() {
                                new DiscordButtonComponent(ButtonStyle.Primary, $"addusernote.{ctx.TargetMember.Id}", "Add Note", false, new DiscordComponentEmoji("📝")),
                                new DiscordButtonComponent(ButtonStyle.Primary, $"viewnotes.{ctx.TargetMember.Id}", $"View {DbContext.GetUserNotes(ctx.TargetMember.Id, ctx.Guild.Id).Count} Notes", false, new DiscordComponentEmoji("📜"))
                    })
                .AddEmbed(embed)
                .AsEphemeral(true);

            await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, response);

        }

        private async Task ClientInstance_ComponentInteractionCreated(DiscordClient sender, DSharpPlus.EventArgs.ComponentInteractionCreateEventArgs args)
        {

            if (args.Id.StartsWith("viewnotes."))
                await ViewUserNotes(args);

        }

        private async Task ViewUserNotes(ComponentInteractionCreateEventArgs eventArgs)
        {

            await eventArgs.Interaction.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource, new DiscordInteractionResponseBuilder() { IsEphemeral = true });

            ulong userSnowflake = ulong.Parse(eventArgs.Id.Split('.')[1]);
            List<UserNote> notes = DbContext.GetUserNotes(userSnowflake, eventArgs.Guild.Id);

        }
    }
}
