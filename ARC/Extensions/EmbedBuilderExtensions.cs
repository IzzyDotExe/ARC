using Arc.Schema;
using DSharpPlus.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
