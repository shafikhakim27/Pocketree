using Microsoft.AspNetCore.SignalR;
using System.Reflection.Metadata.Ecma335;
using System.Security.Claims;

namespace Pocketree.Api.Hubs
{
    public class CustomUser : IUserIdProvider
    {
        // Extract UserID claim from the cookie
        public string GetUserId(HubConnectionContext connection)
        {
            return connection.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        }
    }
}
