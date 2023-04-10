using DSharpPlus;

namespace ARC.Extensions
{
    public static class ClientExtensions
    {

        public static (string, string)? GetEventAction( this DiscordClient client, string eventId)
        {
            int indx = eventId.LastIndexOf(".");
            if (indx != -1)
            {
                var eventAction = eventId.Substring(0, indx);
                var eventIdentifier = eventId.Substring(indx+1, Math.Abs(indx+1 - eventId.Length));
                return (eventAction, eventIdentifier);
            }

            return null;
        }

    }
}
