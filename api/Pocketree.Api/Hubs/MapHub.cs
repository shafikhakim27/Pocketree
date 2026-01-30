using System.Text.RegularExpressions;
using Microsoft.AspNetCore.SignalR;

namespace ADproject.Hubs
{
    public class MapHub : Hub
    {
        // Making provision for Android users to call this for future expansion 
        public async Task JoinMobileGroup()
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, "MobileUsers");
        }

        // Web dashboard calls this upon connection
        public async Task JoinWebGroup()
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, "WebDashboard");
        }
    }
}
