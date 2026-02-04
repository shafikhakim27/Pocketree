using ADproject.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace Pocketree.Api.Tests.Hubs;

public class MapHubTests
{
    [Fact]
    public async System.Threading.Tasks.Task MapHub_JoinMobileGroup_AddsToMobileUsers()
    {
        // Arrange
        var mockClients = new Mock<IHubCallerClients>();
        var mockGroups = new Mock<IGroupManager>();
        var mockContext = new Mock<HubCallerContext>();

        mockContext.Setup(c => c.ConnectionId).Returns("mobile-connection-123");

        var hub = new MapHub
        {
            Clients = mockClients.Object,
            Groups = mockGroups.Object,
            Context = mockContext.Object
        };

        // Act
        await hub.JoinMobileGroup();

        // Assert
        mockGroups.Verify(g => g.AddToGroupAsync("mobile-connection-123", "MobileUsers", default), Times.Once);
    }

    [Fact]
    public async System.Threading.Tasks.Task MapHub_JoinWebGroup_AddsToWebDashboard()
    {
        // Arrange
        var mockClients = new Mock<IHubCallerClients>();
        var mockGroups = new Mock<IGroupManager>();
        var mockContext = new Mock<HubCallerContext>();

        mockContext.Setup(c => c.ConnectionId).Returns("web-connection-456");

        var hub = new MapHub
        {
            Clients = mockClients.Object,
            Groups = mockGroups.Object,
            Context = mockContext.Object
        };

        // Act
        await hub.JoinWebGroup();

        // Assert
        mockGroups.Verify(g => g.AddToGroupAsync("web-connection-456", "WebDashboard", default), Times.Once);
    }

    [Fact]
    public async System.Threading.Tasks.Task MapHub_MultipleClients_CanJoinDifferentGroups()
    {
        // Arrange
        var mockClients = new Mock<IHubCallerClients>();
        var mockGroups = new Mock<IGroupManager>();

        var mockMobileContext = new Mock<HubCallerContext>();
        var mockWebContext = new Mock<HubCallerContext>();

        mockMobileContext.Setup(c => c.ConnectionId).Returns("mobile-conn-1");
        mockWebContext.Setup(c => c.ConnectionId).Returns("web-conn-1");

        var mobileHub = new MapHub
        {
            Clients = mockClients.Object,
            Groups = mockGroups.Object,
            Context = mockMobileContext.Object
        };

        var webHub = new MapHub
        {
            Clients = mockClients.Object,
            Groups = mockGroups.Object,
            Context = mockWebContext.Object
        };

        // Act
        await mobileHub.JoinMobileGroup();
        await webHub.JoinWebGroup();

        // Assert
        mockGroups.Verify(g => g.AddToGroupAsync("mobile-conn-1", "MobileUsers", default), Times.Once);
        mockGroups.Verify(g => g.AddToGroupAsync("web-conn-1", "WebDashboard", default), Times.Once);
    }

    [Fact]
    public async System.Threading.Tasks.Task MapHub_SameClient_CanJoinBothGroups()
    {
        // Arrange
        var mockClients = new Mock<IHubCallerClients>();
        var mockGroups = new Mock<IGroupManager>();
        var mockContext = new Mock<HubCallerContext>();

        mockContext.Setup(c => c.ConnectionId).Returns("dual-connection");

        var hub = new MapHub
        {
            Clients = mockClients.Object,
            Groups = mockGroups.Object,
            Context = mockContext.Object
        };

        // Act
        await hub.JoinMobileGroup();
        await hub.JoinWebGroup();

        // Assert
        mockGroups.Verify(g => g.AddToGroupAsync("dual-connection", "MobileUsers", default), Times.Once);
        mockGroups.Verify(g => g.AddToGroupAsync("dual-connection", "WebDashboard", default), Times.Once);
    }
}