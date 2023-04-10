using Arc.Services;
using ARC.Modules;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.SlashCommands;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DSharpPlus.EventArgs;

namespace ARC.Services
{
    internal class SlashCommandsService : ArcService
    {

        public SlashCommandsService() : base("Slashcmds")
        {
            var slashCommandsConfig = new SlashCommandsConfiguration()
            {
                Services = ServiceProvider,

            };

            var slashCommands = ClientInstance.UseSlashCommands(slashCommandsConfig);

            // Register slash command modules here
            slashCommands.RegisterCommands<UtilitiesModule>();
            slashCommands.RegisterCommands<ModerationModule>();

            slashCommands.SlashCommandErrored += SlashCommands_SlashCommandErrored;
            ClientInstance.ClientErrored += ClientInstanceOnClientErrored;
        }

        private async Task ClientInstanceOnClientErrored(DiscordClient sender, ClientErrorEventArgs args)
        {
            Log.Logger.Error(args.Exception.ToString());
        }

        private async Task SlashCommands_SlashCommandErrored(SlashCommandsExtension sender, DSharpPlus.SlashCommands.EventArgs.SlashCommandErrorEventArgs args)
        {

            Log.Logger.Error(args.Exception.ToString());

            var errorEmbed = new DiscordEmbedBuilder()
                .WithTitle("Error!")
                .WithColor(DiscordColor.Red)
                .WithDescription($"***An error occured! Please report this to your server admin or Izzy***\n```{args.Exception}```");

            await args.Context.Interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder()
                .AddEmbed(errorEmbed)
                .AsEphemeral());

        }
    }
}
