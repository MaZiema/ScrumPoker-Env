using Microsoft.AspNetCore.SignalR;
using Moq;
using Scrum_Poker_V1.Hubs;
using Scrum_Poker_V1.Models;
using Scrum_Poker_V1.Services;

namespace Scrum_Poker_V1.Tests;

public class ScrumPokerHubTests
{
    private readonly RoomService _roomService = new();
    private readonly ScrumPokerHub _hub;
    private readonly Mock<IHubCallerClients> _clientsMock;
    private readonly Mock<IGroupManager> _groupsMock;
    private readonly Mock<IClientProxy> _callerProxyMock;
    private readonly Mock<IClientProxy> _groupProxyMock;
    private readonly Mock<ISingleClientProxy> _singleCallerProxyMock;
    private readonly string _adminConnId = "conn-admin";
    private readonly string _aliceConnId = "conn-alice";
    private readonly string _bobConnId = "conn-bob";

    public ScrumPokerHubTests()
    {
        _hub = new ScrumPokerHub(_roomService);

        _callerProxyMock = new Mock<IClientProxy>();
        _groupProxyMock = new Mock<IClientProxy>();
        _singleCallerProxyMock = new Mock<ISingleClientProxy>();

        _clientsMock = new Mock<IHubCallerClients>();
        _clientsMock.Setup(c => c.Caller).Returns(_singleCallerProxyMock.Object);
        _clientsMock.Setup(c => c.Group(It.IsAny<string>())).Returns(_groupProxyMock.Object);
        _clientsMock.Setup(c => c.Client(It.IsAny<string>())).Returns(_singleCallerProxyMock.Object);

        _groupsMock = new Mock<IGroupManager>();
        _groupsMock.Setup(g => g.AddToGroupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                   .Returns(Task.CompletedTask);
        _groupsMock.Setup(g => g.RemoveFromGroupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                   .Returns(Task.CompletedTask);

        var contextMock = new Mock<HubCallerContext>();
        contextMock.Setup(c => c.ConnectionId).Returns(_adminConnId);

        _hub.Clients = _clientsMock.Object;
        _hub.Context = contextMock.Object;
        _hub.Groups = _groupsMock.Object;
    }

    private void SetConnectionId(string connectionId)
    {
        var contextMock = new Mock<HubCallerContext>();
        contextMock.Setup(c => c.ConnectionId).Returns(connectionId);
        _hub.Context = contextMock.Object;
    }

    private void SetupCallerFor(string connectionId)
    {
        SetConnectionId(connectionId);
        _clientsMock.Setup(c => c.Caller).Returns(_singleCallerProxyMock.Object);
    }

    [Fact]
    public async Task JoinRoom_ValidRoom_SendsJoinedRoomToCaller()
    {
        var room = _roomService.CreateRoom("Test", _adminConnId);
        SetupCallerFor(_adminConnId);

        await _hub.JoinRoom(room.Code, "Admin", PlayerRole.Admin);

        _singleCallerProxyMock.Verify(c => c.SendCoreAsync(
            "JoinedRoom",
            It.Is<object[]>(args => args.Length == 1),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task JoinRoom_AdminRole_SetsAdminConnectionId()
    {
        var room = _roomService.CreateRoom("Test", "");
        SetupCallerFor(_adminConnId);

        await _hub.JoinRoom(room.Code, "Admin", PlayerRole.Admin);

        Assert.Equal(_adminConnId, room.AdminConnectionId);
    }

    [Fact]
    public async Task JoinRoom_MemberRole_DoesNotSetAdminConnectionId()
    {
        var room = _roomService.CreateRoom("Test", "");
        SetupCallerFor(_aliceConnId);

        await _hub.JoinRoom(room.Code, "Alice", PlayerRole.Member);

        Assert.Equal("", room.AdminConnectionId);
    }

    [Fact]
    public async Task JoinRoom_AddsPlayerToGroup()
    {
        var room = _roomService.CreateRoom("Test", "");
        SetupCallerFor(_aliceConnId);

        await _hub.JoinRoom(room.Code, "Alice", PlayerRole.Member);

        _groupsMock.Verify(g => g.AddToGroupAsync(
            _aliceConnId, room.Code, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task JoinRoom_BroadcastsPlayerJoined()
    {
        var room = _roomService.CreateRoom("Test", "");
        SetupCallerFor(_aliceConnId);

        await _hub.JoinRoom(room.Code, "Alice", PlayerRole.Member);

        _groupProxyMock.Verify(c => c.SendCoreAsync(
            "PlayerJoined",
            It.Is<object[]>(args => args.Length == 1),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task JoinRoom_NonExistingRoom_SendsError()
    {
        SetupCallerFor(_aliceConnId);

        await _hub.JoinRoom("XXXXXX", "Alice", PlayerRole.Member);

        _singleCallerProxyMock.Verify(c => c.SendCoreAsync(
            "Error",
            It.Is<object[]>(args => args.Length == 1),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task JoinRoom_DuplicateName_SendsError()
    {
        var room = _roomService.CreateRoom("Test", "");
        SetupCallerFor(_aliceConnId);
        await _hub.JoinRoom(room.Code, "Alice", PlayerRole.Member);

        SetupCallerFor("conn-alice2");
        await _hub.JoinRoom(room.Code, "Alice", PlayerRole.Member);

        // Duplikate werden jetzt via Suffix "Alice (2)" erlaubt – kein Error
        _singleCallerProxyMock.Verify(c => c.SendCoreAsync(
            "Error",
            It.Is<object[]>(args => args.Length == 1),
            It.IsAny<CancellationToken>()), Times.Never);
        Assert.Equal(2, room.Players.Count);
        Assert.Equal("Alice (2)", room.Players[1].Name);
    }

    [Fact]
    public async Task SelectCard_Member_CardSelectedToCaller()
    {
        var room = _roomService.CreateRoom("Test", _adminConnId);
        SetupCallerFor(_adminConnId);
        await _hub.JoinRoom(room.Code, "Admin", PlayerRole.Admin);

        SetupCallerFor(_adminConnId);
        await _hub.SelectCard(room.Code, "5");

        var player = _roomService.GetPlayerByConnection(room.Code, _adminConnId);
        Assert.Equal("5", player!.SelectedCard);
        Assert.True(player.HasVoted);
    }

    [Fact]
    public async Task SelectCard_Observer_CannotVote()
    {
        var room = _roomService.CreateRoom("Test", _adminConnId);
        SetupCallerFor(_aliceConnId);
        await _hub.JoinRoom(room.Code, "Charlie", PlayerRole.Observer);

        SetupCallerFor(_aliceConnId);
        await _hub.SelectCard(room.Code, "5");

        var player = _roomService.GetPlayerByConnection(room.Code, _aliceConnId);
        Assert.Null(player!.SelectedCard);
        Assert.False(player.HasVoted);
    }

    [Fact]
    public async Task StartNewRound_AdminOnly_AllowsAdmin()
    {
        var room = _roomService.CreateRoom("Test", _adminConnId);
        SetupCallerFor(_adminConnId);
        await _hub.JoinRoom(room.Code, "Admin", PlayerRole.Admin);

        SetupCallerFor(_adminConnId);
        await _hub.StartNewRound(room.Code, "Task 1");

        Assert.Single(room.Rounds);
        Assert.Equal("Task 1", room.Rounds[0].Title);
    }

    [Fact]
    public async Task StartNewRound_NonAdmin_DoesNotStart()
    {
        var room = _roomService.CreateRoom("Test", _adminConnId);
        SetupCallerFor(_adminConnId);
        await _hub.JoinRoom(room.Code, "Admin", PlayerRole.Admin);

        SetupCallerFor(_aliceConnId);
        await _hub.JoinRoom(room.Code, "Alice", PlayerRole.Member);

        SetupCallerFor(_aliceConnId);
        await _hub.StartNewRound(room.Code, "Task 1");

        Assert.Empty(room.Rounds);
    }

    [Fact]
    public async Task RevealCards_AdminOnly_AllowsAdmin()
    {
        var room = _roomService.CreateRoom("Test", _adminConnId);
        SetupCallerFor(_adminConnId);
        await _hub.JoinRoom(room.Code, "Admin", PlayerRole.Admin);

        SetupCallerFor(_adminConnId);
        await _hub.StartNewRound(room.Code, "Task");
        await _hub.SelectCard(room.Code, "5");

        SetupCallerFor(_adminConnId);
        await _hub.RevealCards(room.Code);

        Assert.True(room.CurrentRound!.IsRevealed);
        Assert.Equal("5", room.CurrentRound.Votes["Admin"]);
    }

    [Fact]
    public async Task RevealCards_NonAdmin_DoesNotReveal()
    {
        var room = _roomService.CreateRoom("Test", _adminConnId);
        SetupCallerFor(_adminConnId);
        await _hub.JoinRoom(room.Code, "Admin", PlayerRole.Admin);
        SetupCallerFor(_adminConnId);
        await _hub.StartNewRound(room.Code, "Task");

        SetupCallerFor(_aliceConnId);
        await _hub.JoinRoom(room.Code, "Alice", PlayerRole.Member);

        SetupCallerFor(_aliceConnId);
        await _hub.RevealCards(room.Code);

        Assert.False(room.CurrentRound!.IsRevealed);
    }

    [Fact]
    public async Task UpdateAvailableCards_AdminOnly_AllowsAdmin()
    {
        var room = _roomService.CreateRoom("Test", _adminConnId);
        SetupCallerFor(_adminConnId);
        await _hub.JoinRoom(room.Code, "Admin", PlayerRole.Admin);

        var newCards = new List<string> { "1", "2", "3" };
        SetupCallerFor(_adminConnId);
        await _hub.UpdateAvailableCards(room.Code, newCards);

        Assert.Equal(newCards, room.AvailableCards);
    }

    [Fact]
    public async Task UpdateAvailableCards_NonAdmin_DoesNotUpdate()
    {
        var room = _roomService.CreateRoom("Test", _adminConnId);
        SetupCallerFor(_adminConnId);
        await _hub.JoinRoom(room.Code, "Admin", PlayerRole.Admin);

        SetupCallerFor(_aliceConnId);
        await _hub.JoinRoom(room.Code, "Alice", PlayerRole.Member);

        var originalCards = new List<string>(room.AvailableCards);
        SetupCallerFor(_aliceConnId);
        await _hub.UpdateAvailableCards(room.Code, new List<string> { "1" });

        Assert.Equal(originalCards, room.AvailableCards);
    }

    [Fact]
    public async Task KickPlayer_AdminCanKick()
    {
        var room = _roomService.CreateRoom("Test", _adminConnId);
        SetupCallerFor(_adminConnId);
        await _hub.JoinRoom(room.Code, "Admin", PlayerRole.Admin);

        SetupCallerFor(_aliceConnId);
        await _hub.JoinRoom(room.Code, "Alice", PlayerRole.Member);

        SetupCallerFor(_adminConnId);
        await _hub.KickPlayer(room.Code, "Alice");

        Assert.Single(room.Players);
        Assert.Equal("Admin", room.Players[0].Name);
    }

    [Fact]
    public async Task KickPlayer_NonAdmin_CannotKick()
    {
        var room = _roomService.CreateRoom("Test", _adminConnId);
        SetupCallerFor(_adminConnId);
        await _hub.JoinRoom(room.Code, "Admin", PlayerRole.Admin);

        SetupCallerFor(_aliceConnId);
        await _hub.JoinRoom(room.Code, "Alice", PlayerRole.Member);

        SetupCallerFor(_aliceConnId);
        await _hub.KickPlayer(room.Code, "Admin");

        Assert.Equal(2, room.Players.Count);
    }

    [Fact]
    public async Task FullGameFlow_CreateJoinVoteReveal()
    {
        var room = _roomService.CreateRoom("Sprint Planning", "");
        SetupCallerFor(_adminConnId);
        await _hub.JoinRoom(room.Code, "Admin", PlayerRole.Admin);

        SetupCallerFor(_aliceConnId);
        await _hub.JoinRoom(room.Code, "Alice", PlayerRole.Member);

        SetupCallerFor(_bobConnId);
        await _hub.JoinRoom(room.Code, "Bob", PlayerRole.Member);

        Assert.Equal(3, room.Players.Count);

        SetupCallerFor(_adminConnId);
        await _hub.StartNewRound(room.Code, "User Story 1");

        Assert.Single(room.Rounds);

        SetupCallerFor(_adminConnId);
        await _hub.SelectCard(room.Code, "5");

        SetupCallerFor(_aliceConnId);
        await _hub.SelectCard(room.Code, "8");

        SetupCallerFor(_bobConnId);
        await _hub.SelectCard(room.Code, "3");

        Assert.True(room.Players.All(p => p.HasVoted));

        SetupCallerFor(_adminConnId);
        await _hub.RevealCards(room.Code);

        Assert.True(room.CurrentRound!.IsRevealed);
        Assert.Equal(3, room.CurrentRound.Votes.Count);
        Assert.Equal("5", room.CurrentRound.Votes["Admin"]);
        Assert.Equal("8", room.CurrentRound.Votes["Alice"]);
        Assert.Equal("3", room.CurrentRound.Votes["Bob"]);
    }

    [Fact]
    public async Task JoinRoom_BroadcastsPlayerList()
    {
        var room = _roomService.CreateRoom("Test", "");
        SetupCallerFor(_aliceConnId);

        await _hub.JoinRoom(room.Code, "Alice", PlayerRole.Member);

        _groupProxyMock.Verify(c => c.SendCoreAsync(
            "PlayerListUpdated",
            It.Is<object[]>(args => args.Length == 1),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
