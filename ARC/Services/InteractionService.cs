using Arc.Services;
using DSharpPlus.Entities;
using DSharpPlus.Interactivity;
using DSharpPlus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DSharpPlus.Interactivity.Extensions;
using ARC.Modules;
using DSharpPlus.EventArgs;
using Fluent.Architecture.Extensions;
using Serilog;

namespace ARC.Services
{

    internal class PaginationSession
    {
        private DiscordInteraction _interaction;

        private DiscordMessage _message;

        private List<Page> _pages = new List<Page>();

        private int _pageIndex;

        private List<DiscordComponent> PaginationButtons {
            get
            {
                return new List<DiscordComponent>() {
                    new DiscordButtonComponent(ButtonStyle.Primary, "pagination.previous", "", !(_pages.Count > 1), new DiscordComponentEmoji("◀️")),
                    new DiscordButtonComponent(ButtonStyle.Primary, "pagination.stop", "", false, new DiscordComponentEmoji("⏹️")),
                    new DiscordButtonComponent(ButtonStyle.Primary, "pagination.next", "", !(_pages.Count > 1), new DiscordComponentEmoji("▶️"))
                };
            }
        }

        public PaginationSession(DiscordInteraction interaction, List<Page> pages)
        {
            _interaction = interaction;
            _pages = pages;
            
            if (_pages.Count < 1 )
                _pages.Add(new Page(null, new DiscordEmbedBuilder()
                    .WithAuthor("No more pages avalible", null)
                    .WithDescription("```No pages```")));

            Task.Run(async () =>
            {
                await Task.Delay(60000);
                await Stop();
            });
            
        }

        public async Task Start()
        {
            Arc.Arc.ClientInstance.ComponentInteractionCreated += this.PaginationInteractionHandler;
            _message = await _interaction.GetOriginalResponseAsync();
            await Update();
        }

        private async Task PaginationInteractionHandler(DiscordClient sender, DSharpPlus.EventArgs.ComponentInteractionCreateEventArgs args)
        {
            if (!args.Message.Equals(_message))
                return;

            await args.Interaction.CreateResponseAsync(InteractionResponseType.DeferredMessageUpdate);

            switch (args.Interaction.Data.CustomId) {

                case "pagination.previous":
                    DecIndex();
                    await Update();
                    break;

                case "pagination.stop":
                    await Stop();
                    break;

                case "pagination.next":
                    IncIndex();
                    await Update();
                    break;
            }


            if (args.Interaction.Data.CustomId.Contains("delete"))
            {

                var currentPage = _pages[_pageIndex];
                
                var component = currentPage.Components.First(x => x.CustomId.Contains("delete"));
                currentPage.Components.Remove(component);
                currentPage.Embed = new DiscordEmbedBuilder(currentPage.Embed).WithDescription("```Deleted```");
                
                await Update();

            }

        }

        public async Task Stop()
        {

            await Update();
            Arc.Arc.ClientInstance.ComponentInteractionCreated -= this.PaginationInteractionHandler;


            var hook = new DiscordWebhookBuilder().AddEmbed(_pages[_pageIndex].Embed);
            hook.ClearComponents();
            await _interaction.EditOriginalResponseAsync(hook);

        }

        public async Task Update()
        {
            var webhook = new DiscordWebhookBuilder()
                .AddComponents(PaginationButtons)
                .AddEmbed(_pages[_pageIndex].Embed);

            if (_pages[_pageIndex].Components.Count > 0)
            {
                webhook.AddComponents(_pages[_pageIndex].Components);
            }

            await _interaction.EditOriginalResponseAsync(webhook);
        }

        public void IncIndex()
        {
            if (_pageIndex >= _pages.Count - 1)
                _pageIndex = _pageIndex - _pages.Count;

            _pageIndex++;
        }

        public void DecIndex()
        {
            if (_pageIndex <= 0)
                _pageIndex = _pages.Count;
            _pageIndex--;
        }

    }

    internal class InteractionService : ArcService
    {

        public InteractionService() : base("Interactions")
        {
            ClientInstance.UseInteractivity();
        }

        public async Task CreatePaginationResponse(List<Page> pages, DiscordInteraction interaction)
        {
            PaginationSession session = new PaginationSession(interaction, pages);
            await session.Start();
        }

    }
}
