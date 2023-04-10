
using DSharpPlus.Entities;


namespace ARC.Extensions
{
    public static class EmbedBuilderExtensions
    {
        public static DiscordEmbedBuilder WithModmailStyle(this DiscordEmbedBuilder builder)
        {
            return builder
                .WithColor(DiscordColor.IndianRed)
                .WithTimestamp(DateTime.UtcNow)
                .WithFooter($"Arc v{Arc.Arc.ClientInstance.ClientVersion} - Modmail", Arc.Arc.ClientInstance.CurrentUser.GetAvatarUrl(DSharpPlus.ImageFormat.Auto))
                ;
        }
    }
}
