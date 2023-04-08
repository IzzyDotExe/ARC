using DSharpPlus;
using DSharpPlus.Entities;
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

        public ModerationModule() : base("Moderation") { }

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
                                new DiscordButtonComponent(ButtonStyle.Primary, $"viewnotes.{ctx.TargetMember.Id}", $"View {DbContext.GetUserNotes(ctx.TargetMember.Id).Count} Notes", false, new DiscordComponentEmoji("📜"))
                    })
                .AddEmbed(embed)
                .AsEphemeral(true);

            await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, response);

        }

    }
}
