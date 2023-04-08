using Arc.Schema;
using ARC.Services;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using DSharpPlus.Interactivity;
using DSharpPlus.SlashCommands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ARC.Extensions;

namespace ARC.Modules
{
    internal class ModerationModule : ArcModule
    {

        public InteractionService InteractivityService { get; set; }
        
        public ModerationModule() : base() {

            ClientInstance.ComponentInteractionCreated += ClientInstance_ComponentInteractionCreated;
            ClientInstance.ModalSubmitted += HandleUserNotesModal;

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
                                new DiscordButtonComponent(ButtonStyle.Primary, $"addnote.{ctx.TargetMember.Id}", "Add Note", false, new DiscordComponentEmoji("📝")),
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

            if (args.Id.StartsWith("addnote."))
                await AddUserNote(args);

            if (args.Id.StartsWith("usernote.delete."))
                await DeleteUserNote(args);

        }

        private async Task DeleteUserNote(ComponentInteractionCreateEventArgs args)
        {
            
            var noteId = long.Parse(args.Interaction.Data.CustomId.Split('.')[2]);
            var notes = DbContext.UserNotes.Where(x => x.NoteId == noteId);

            if (!notes.Any())
                return;

            var note = notes.ToList()[0];

            DbContext.Remove(note);
            
            // TODO: RESTORE BUTTON
            Arc.Arc.ArcDbContext.SaveChanges();
            
            var embed = new DiscordEmbedBuilder()
                .WithAuthor($"A Note was deleted from {note.User.Username}#{note.User.Discriminator}", null, note.User.GetAvatarUrl(ImageFormat.Auto))
                .WithDescription($"```{note.Note}```")
                .AddField("Added By:", $"{note.Author.Mention}", true)
                .AddField("Deleted By:", $"{args.User.Mention}", true)
                .AddField("Time added:", $"<t:{new DateTimeOffset(note.DateAdded).ToUnixTimeSeconds()}:R>", true)
                .AddField("Time deleted:", $"<t:{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}:R>", true)
                .WithFooter($"BillieBot v{ClientInstance.ClientVersion} UserNotes", ClientInstance.CurrentUser.GetAvatarUrl(ImageFormat.Auto))
                .WithTimestamp(DateTime.UtcNow)
                .WithColor(DiscordColor.Red)
                .Build();

            await note.Guild.Log(new DiscordMessageBuilder().WithEmbed(embed));
        }

        private async Task AddUserNote(ComponentInteractionCreateEventArgs eventArgs)
        {
            ulong userId = ulong.Parse(eventArgs.Id.Split('.')[1]);
            var user = await ClientInstance.GetUserAsync(userId);

            var modal = new DiscordInteractionResponseBuilder()
                .WithTitle($"Add Note To {user.Username}")
                .WithCustomId($"addnote.{userId}")
                .AddComponents(new TextInputComponent(label: "Note",
                                                        customId: $"usernote.content",
                                                        placeholder: "Enter user note...",
                                                        required: true,
                                                        style: TextInputStyle.Paragraph));

            await eventArgs.Interaction.CreateResponseAsync(InteractionResponseType.Modal, modal);

        }

        private async Task ViewUserNotes(ComponentInteractionCreateEventArgs eventArgs)
        {

            await eventArgs.Interaction.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource, new DiscordInteractionResponseBuilder() { IsEphemeral = true });

            ulong userSnowflake = ulong.Parse(eventArgs.Id.Split('.')[1]);
            List<UserNote> notes = DbContext.GetUserNotes(userSnowflake, eventArgs.Guild.Id);

            List<Page> pages = new List<Page>();
            foreach (var note in notes)
            {
                var embed = note.CreateEmbedPage();
                var page = new Page(null, embed);
                List<DiscordComponent> buttons = new() {
                    new DiscordButtonComponent(ButtonStyle.Danger, $"usernote.delete.{note.NoteId}", "Delete", false, new DiscordComponentEmoji("🗑️"))
                };

                page.Components = buttons;
                pages.Add(page);
            }

            await InteractivityService.CreatePaginationResponse(pages, eventArgs.Interaction);

        }
        
        private async Task HandleUserNotesModal(DiscordClient sender, ModalSubmitEventArgs args)
        {
            if (args.Interaction.Data.CustomId.StartsWith("addnote."))
            {
                
                ulong userSnowflake = ulong.Parse(args.Interaction.Data.CustomId.Split('.')[1]);
                var author = args.Interaction.User;
                var user = await ClientInstance.GetUserAsync(userSnowflake);
                String content = args.Values["usernote.content"];
                DateTime dateadded = DateTime.UtcNow;
                var guild = args.Interaction.Guild;

                var note = new UserNote((long)guild.Id, (long)user.Id, content, dateadded, (long)author.Id);

                DbContext.UserNotes.Add(note);
                await DbContext.SaveChangesAsync();

                var embed = new DiscordEmbedBuilder()
                    .WithAuthor($"A New Note was Added to {user.Username}#{user.Discriminator}", null, user.GetAvatarUrl(ImageFormat.Auto))
                    .WithDescription($"```{content}```")
                    .AddField("Added By:", $"{author.Mention}", true)
                    .AddField("Time added:", $"<t:{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}:R>", true)
                    .WithFooter($"BillieBot v{ClientInstance.ClientVersion} UserNotes", ClientInstance.CurrentUser.GetAvatarUrl(ImageFormat.Auto))
                    .WithTimestamp(dateadded)
                    .WithColor(DiscordColor.Green)
                    .Build();
                
                await guild.Log(
                    new DiscordMessageBuilder()
                                .WithEmbed(embed));

                var embed2 = new DiscordEmbedBuilder()
                    .WithAuthor($"{user} Notes", null, user.GetAvatarUrl(ImageFormat.Auto))
                    .WithColor(DiscordColor.Blurple);
                
                var resp = new DiscordInteractionResponseBuilder()
                    .AddEmbed(embed2)
                    .AddComponents(new List<DiscordButtonComponent>()
                    {
                        new DiscordButtonComponent(ButtonStyle.Primary, $"addnote.{user.Id}", "Add Note", false,
                            new DiscordComponentEmoji("📝")),
                        new DiscordButtonComponent(ButtonStyle.Primary, $"viewnotes.{user.Id}",
                            $"View {DbContext.GetUserNotes(user.Id, guild.Id).Count} Notes", false,
                            new DiscordComponentEmoji("📜"))
                    });
                
                await args.Interaction.CreateResponseAsync(InteractionResponseType.UpdateMessage, resp);
            }
        }
        
        /*
         *
         *            
            if (eventArgs.Interaction.Data.CustomId.StartsWith("noteaddmodal.")) {

                ulong userId = ulong.Parse(eventArgs.Interaction.Data.CustomId.Split('.')[1]);
                var adder = eventArgs.Interaction.User;
                var user = await client.GetUserAsync(userId);
                String content = eventArgs.Values["usernote.content"];
                DateTime dateAdded = DateTime.UtcNow;

                await _dbservice.AddUserNote(content, userId, adder.Id, dateAdded);

                ulong logChannel = ulong.Parse(_config.GetSection("usernotes:logchannel").Value);

                DiscordChannel channel = await _client.GetChannelAsync(logChannel);

                var embed = new DiscordEmbedBuilder()
                                .WithAuthor($"A New Note was Added to {user.Username}#{user.Discriminator}", null, user.GetAvatarUrl(ImageFormat.Auto))
                                .WithDescription($"```{content}```")
                                .AddField("Added By:", $"{adder.Mention}", true)
                                .AddField("Time added:", $"<t:{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}:R>", true)
                                .WithFooter($"BillieBot v{_locale.TranslatableText("versionstring", 393165866285662208)} UserNotes", client.CurrentUser.GetAvatarUrl(ImageFormat.Auto))
                                .WithTimestamp(dateAdded)
                                .WithColor(DiscordColor.Green)
                                .Build();

                await channel.SendMessageAsync(embed);

                await eventArgs.Interaction.CreateResponseAsync(InteractionResponseType.DeferredMessageUpdate);
            }
         */
        
        
    }
}
