using Arc.Schema;
using Arc.Services;

using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.SlashCommands;

using System.Diagnostics;


namespace ARC.Modules
{
    internal class UtilitiesModule : ArcModule
    {

        public UptimeService UptimeService { get; set; }

        public UtilitiesModule() : base("Utilities") {
            
        }

        protected override void RegisterEvents()
        {
            ClientInstance.ComponentInteractionCreated += ClientInstance_ComponentInteractionCreated;
        }


        #region Utilities commands

        [SlashCommand("Ping", "Gets the latency numbers related to the bot.")]
        public async Task PingCommand(InteractionContext ctx)
        {

            Stopwatch timer = new Stopwatch();
            timer.Start();
            await ctx.Channel.TriggerTypingAsync();
            var msg = await ctx.Channel.SendMessageAsync(".");
            timer.Stop();
            await msg.DeleteAsync();


            string wsText = "Websocket latency";
            string wsPing = ctx.Client.Ping.ToString();

            string rtText = "Roundtrip latency";
            string rtPing = timer.ElapsedMilliseconds.ToString();

            var embed = new DiscordEmbedBuilder()
                        .WithColor(DiscordColor.PhthaloBlue)
                        .WithDescription($"🌐 **{wsText}:** ``{wsPing}ms``\n💬 **{rtText}:** ``{rtPing}ms``");

            var response = new DiscordInteractionResponseBuilder { }
                            .AddEmbed(embed);

            await ctx.CreateResponseAsync(response);

        }

        [SlashCommand("Uptime", "Get the bot's uptime")]
        public async Task UptimeCommand(InteractionContext ctx)
        {

            string uptimeMsg = "Uptime";
            string uptimeDays = "Days";
            string uptimeHours = "Hrs";
            string uptimeMinutes = "Mins";
            string uptimeSeconds = "Sec";

            var uptime = UptimeService.Uptime.Elapsed;

            var embed = new DiscordEmbedBuilder()
                .WithAuthor(ClientInstance.CurrentUser.Username, null, ClientInstance.CurrentUser.AvatarUrl)
                .WithColor(DiscordColor.PhthaloBlue)
                .WithDescription($"**{uptimeMsg}:** ``{uptime.Days}{uptimeDays} {uptime.Hours}{uptimeHours} {uptime.Minutes}{uptimeMinutes} {uptime.Seconds}{uptimeSeconds}``");

            var response = new DiscordInteractionResponseBuilder() { }
                            .AddEmbed(embed);

            await ctx.CreateResponseAsync(response);

        }

        [SlashCommand("Avatar", "Get your own or a user's avatar")]
        public async Task AvatarCommand(InteractionContext ctx, [Option("User", "Select which user to get the avatar from"),] DiscordUser user = null)
        {

            if (user is null)
            {
                user = ctx.User;
            }

            var selectOptions = new List<DiscordSelectComponentOption>() {
                new DiscordSelectComponentOption("Global Avatar", $"global.{user.Id}.{ctx.User.Id}", "Get the user's global avatar", false, new DiscordComponentEmoji("🌐")),
                new DiscordSelectComponentOption("Server Avatar", $"server.{user.Id}.{ctx.User.Id}", "Get the user's server avatar", false, new DiscordComponentEmoji("🖥️"))
            };

            var selectmenu = new DiscordSelectComponent("avatar_component", "Select...", selectOptions, false, 1, 1);

            var res = new DiscordInteractionResponseBuilder()
            .WithContent(user.GetAvatarUrl(ImageFormat.Auto))
            .AddComponents(selectmenu);

            await ctx.CreateResponseAsync(res);

        }

        private async Task ClientInstance_ComponentInteractionCreated(DiscordClient sender, DSharpPlus.EventArgs.ComponentInteractionCreateEventArgs eventArgs)
        {
            if (!eventArgs.Id.Equals("avatar_component"))
                return;

            string response = "";

            if (eventArgs.Interaction.Data.Values[0].Split(".")[0] == "server")
            {
                response = eventArgs.Guild.Members[ulong.Parse(eventArgs.Interaction.Data.Values[0].Split(".")[1])].GetGuildAvatarUrl(ImageFormat.Auto);
            }

            if (eventArgs.Interaction.Data.Values[0].Split(".")[0] == "global")
            {
                var user = await eventArgs.Guild.GetMemberAsync(ulong.Parse(eventArgs.Interaction.Data.Values[0].Split(".")[1]));
                response = user.GetAvatarUrl(ImageFormat.Auto);
            }

            if (ulong.Parse(eventArgs.Interaction.Data.Values[0].Split(".")[2]) == eventArgs.Interaction.User.Id)
            {
                await eventArgs.Interaction.CreateResponseAsync(InteractionResponseType.UpdateMessage, new DiscordInteractionResponseBuilder()
                .WithContent(response)
                                                                                                    .AddComponents(eventArgs.Message.Components));
            }
            else
            {
                await eventArgs.Interaction.CreateResponseAsync(InteractionResponseType.UpdateMessage, new DiscordInteractionResponseBuilder()
                .WithContent(eventArgs.Message.Content)
                                                                                                    .AddComponents(eventArgs.Message.Components));
            }
 
        }

        #endregion

        #region server management

        [SlashCommand("SetConfig", "Set a config string"),
         SlashCommandPermissions(Permissions.ManageGuild)]
        public async Task SetConfigCommand(InteractionContext ctx, [Option("key", "The key name of the config string")] string configKey, [Option("value", "The value of the config string")] string configValue)
        {
            var config = DbContext.GuildConfigs.Where(c => c.ConfigGuildSnowflake == (long)ctx.Guild.Id && c.ConfigKey.Equals(configKey));

            if (config.Any())
                config.First().ConfigValue = configValue;
            else
            {
                GuildConfig configin = new((long)ctx.Guild.Id, configKey, configValue);
                DbContext.GuildConfigs.Add(configin);
       
            }

            await DbContext.SaveChangesAsync();

            var embed = new DiscordEmbedBuilder()
                .WithTitle("Config update was successful!")
                .WithDescription($"``{configKey} --> {configValue}``");

            var response = new DiscordInteractionResponseBuilder()
            {
                IsEphemeral = true
            }
            .AddEmbed(embed);

            await ctx.CreateResponseAsync(response);

        }

        [SlashCommand("GetConfig", "Set a config string"),
         SlashCommandPermissions(Permissions.ManageGuild)]
        public async Task GetConfigCommand(InteractionContext ctx, [Option("key", "The key name of the config string")] string configKey)
        {
            var config = DbContext.GuildConfigs.Where(c => c.ConfigGuildSnowflake == (long)ctx.Guild.Id && c.ConfigKey.Equals(configKey));
            string? configvalue = null;
            string descriptionString;
            if (config.Any())
                configvalue = config.First().ConfigValue;

            descriptionString = $"``{configKey}`` is currently set to ``{configvalue}``";

            if (configvalue == null || string.IsNullOrWhiteSpace(configvalue) || configvalue.ToLower().Equals("null"))
                descriptionString = $"``{configKey}`` is not currently set to anything";

            var embed = new DiscordEmbedBuilder()
                .WithTitle($"Config for {ctx.Guild}")
                .WithDescription(descriptionString);

            var response = new DiscordInteractionResponseBuilder()
            {
                IsEphemeral = true
            }
            .AddEmbed(embed);

            await ctx.CreateResponseAsync(response);

        }

        [SlashCommand("BanAppealMsg", "Send the ban appeal message"),
         SlashCommandPermissions(Permissions.Administrator)]
        public async Task BanAppealMessage(InteractionContext ctx)
        {
            
            DiscordEmbed embedBuild = new DiscordEmbedBuilder()
                .WithColor(DiscordColor.DarkRed)
                .WithTitle("Ban Appeal")
                .WithDescription($"Welcome to {ctx.Guild.Name}.\n\nTo open a ban appeal, please click the button below.")
                .WithThumbnail("https://www.pngkey.com/png/full/382-3821512_tak-icon-hammer-01-hammer.png")
                .Build();

            var buttons = new List<DiscordButtonComponent>() {
                new DiscordButtonComponent(ButtonStyle.Primary, $"banappeal.send", "Open A Ban Appeal")
            };

            var message = new DiscordMessageBuilder().WithEmbed(embedBuild).AddComponents(buttons);

            await ctx.Channel.SendMessageAsync(message);
            await ctx.Interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder().WithContent("Success!").AsEphemeral(true));

        }
        
        #endregion

    }
}
