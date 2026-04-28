using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace ArmsFair.Server.Hubs;

[Authorize]
public class GameHub : Hub
{
    // Full implementation in Task 14
}
