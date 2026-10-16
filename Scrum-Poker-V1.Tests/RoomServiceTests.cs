using Scrum_Poker_V1.Models;
using Scrum_Poker_V1.Services;

namespace Scrum_Poker_V1.Tests;

public class RoomServiceTests
{
    private readonly RoomService _service = new();

    [Fact]
    public void CreateRoom_ReturnsRoomWithCode()
    {
        var room = _service.CreateRoom("Test Sprint", "conn-1");

        Assert.NotNull(room);
        Assert.Equal("Test Sprint", room.Name);
        Assert.Equal("conn-1", room.AdminConnectionId);
        Assert.NotEmpty(room.Code);
        Assert.Equal(6, room.Code.Length);
    }

    [Fact]
    public void CreateRoom_CreatesUniqueCodes()
    {
        var room1 = _service.CreateRoom("Room 1", "conn-1");
        var room2 = _service.CreateRoom("Room 2", "conn-2");

        Assert.NotEqual(room1.Code, room2.Code);
    }

    [Fact]
    public void CreateRoom_DefaultCardsAreFibonacci()
    {
        var room = _service.CreateRoom("Room", "conn-1");

        Assert.Contains("1", room.AvailableCards);
        Assert.Contains("2", room.AvailableCards);
        Assert.Contains("3", room.AvailableCards);
        Assert.Contains("4", room.AvailableCards);
        Assert.Contains("5", room.AvailableCards);
        Assert.Contains("6", room.AvailableCards);
        Assert.Contains("7", room.AvailableCards);
        Assert.Contains("8", room.AvailableCards);
        Assert.Contains("9", room.AvailableCards);
        Assert.Contains("42", room.AvailableCards);
        Assert.Contains("100", room.AvailableCards);
        Assert.Equal(11, room.AvailableCards.Count);
    }

    [Fact]
    public void GetRoom_ExistingRoom_ReturnsRoom()
    {
        var created = _service.CreateRoom("Room", "conn-1");
        var found = _service.GetRoom(created.Code);

        Assert.NotNull(found);
        Assert.Equal(created.Code, found!.Code);
    }

    [Fact]
    public void GetRoom_NonExistingRoom_ReturnsNull()
    {
        var found = _service.GetRoom("XXXXXX");
        Assert.Null(found);
    }

    [Fact]
    public void RoomExists_ReturnsTrueForExistingRoom()
    {
        var room = _service.CreateRoom("Room", "conn-1");
        Assert.True(_service.RoomExists(room.Code));
    }

    [Fact]
    public void RoomExists_ReturnsFalseForNonExistingRoom()
    {
        Assert.False(_service.RoomExists("XXXXXX"));
    }

    [Fact]
    public void AddPlayer_AddsPlayerToRoom()
    {
        var room = _service.CreateRoom("Room", "conn-admin");
        var player = _service.AddPlayer(room.Code, "Alice", PlayerRole.Member, "conn-alice");

        Assert.NotNull(player);
        Assert.Equal("Alice", player!.Name);
        Assert.Equal(PlayerRole.Member, player.Role);
        Assert.Equal("conn-alice", player.ConnectionId);
        Assert.Single(room.Players);
    }

    [Fact]
    public void AddPlayer_DuplicateName_ReturnsNull()
    {
        var room = _service.CreateRoom("Room", "conn-admin");
        _service.AddPlayer(room.Code, "Alice", PlayerRole.Member, "conn-1");
        var duplicate = _service.AddPlayer(room.Code, "alice", PlayerRole.Member, "conn-2");

        Assert.NotNull(duplicate);
        Assert.Equal("alice (2)", duplicate!.Name);
        Assert.Equal(2, room.Players.Count);
    }

    [Fact]
    public void AddPlayer_DifferentNames_AllowMultiple()
    {
        var room = _service.CreateRoom("Room", "conn-admin");
        _service.AddPlayer(room.Code, "Alice", PlayerRole.Member, "conn-1");
        _service.AddPlayer(room.Code, "Bob", PlayerRole.Member, "conn-2");
        _service.AddPlayer(room.Code, "Charlie", PlayerRole.Observer, "conn-3");

        Assert.Equal(3, room.Players.Count);
    }

    [Fact]
    public void AddPlayer_NonExistingRoom_ReturnsNull()
    {
        var result = _service.AddPlayer("XXXXXX", "Alice", PlayerRole.Member, "conn-1");
        Assert.Null(result);
    }

    [Fact]
    public void RemovePlayer_RemovesPlayerFromRoom()
    {
        var room = _service.CreateRoom("Room", "conn-admin");
        _service.AddPlayer(room.Code, "Alice", PlayerRole.Member, "conn-alice");

        _service.RemovePlayer(room.Code, "conn-alice");

        Assert.Empty(room.Players);
    }

    [Fact]
    public void RemovePlayer_LastPlayer_DeletesRoom()
    {
        var room = _service.CreateRoom("Room", "conn-admin");
        _service.RemovePlayer(room.Code, "conn-admin");

        Assert.False(_service.RoomExists(room.Code));
    }

    [Fact]
    public void RemovePlayer_NonExistingConnection_DoesNotThrow()
    {
        var room = _service.CreateRoom("Room", "conn-admin");
        var ex = Record.Exception(() => _service.RemovePlayer(room.Code, "conn-nonexistent"));
        Assert.Null(ex);
    }

    [Fact]
    public void GetPlayerByConnection_ReturnsCorrectPlayer()
    {
        var room = _service.CreateRoom("Room", "conn-admin");
        _service.AddPlayer(room.Code, "Alice", PlayerRole.Member, "conn-alice");
        _service.AddPlayer(room.Code, "Bob", PlayerRole.Member, "conn-bob");

        var player = _service.GetPlayerByConnection(room.Code, "conn-bob");

        Assert.NotNull(player);
        Assert.Equal("Bob", player!.Name);
    }

    [Fact]
    public void GetPlayerByConnection_NonExisting_ReturnsNull()
    {
        var room = _service.CreateRoom("Room", "conn-admin");
        var player = _service.GetPlayerByConnection(room.Code, "conn-nonexistent");
        Assert.Null(player);
    }

    [Fact]
    public void SelectCard_SetsCardAndHasVoted()
    {
        var room = _service.CreateRoom("Room", "conn-admin");
        _service.AddPlayer(room.Code, "Alice", PlayerRole.Member, "conn-alice");

        _service.SelectCard(room.Code, "conn-alice", "5");

        var player = _service.GetPlayerByConnection(room.Code, "conn-alice");
        Assert.Equal("5", player!.SelectedCard);
        Assert.True(player.HasVoted);
    }

    [Fact]
    public void SelectCard_Observer_CannotVote()
    {
        var room = _service.CreateRoom("Room", "conn-admin");
        _service.AddPlayer(room.Code, "Charlie", PlayerRole.Observer, "conn-charlie");

        _service.SelectCard(room.Code, "conn-charlie", "5");

        var player = _service.GetPlayerByConnection(room.Code, "conn-charlie");
        Assert.Null(player!.SelectedCard);
        Assert.False(player.HasVoted);
    }

    [Fact]
    public void SelectCard_NonExistingPlayer_DoesNotThrow()
    {
        var room = _service.CreateRoom("Room", "conn-admin");
        var ex = Record.Exception(() => _service.SelectCard(room.Code, "conn-nonexistent", "5"));
        Assert.Null(ex);
    }

    [Fact]
    public void StartNewRound_CreatesNewRound()
    {
        var room = _service.CreateRoom("Room", "conn-admin");
        var round = _service.StartNewRound(room.Code, "User Story 123");

        Assert.NotNull(round);
        Assert.Equal(1, round.Number);
        Assert.Equal("User Story 123", round.Title);
        Assert.False(round.IsRevealed);
        Assert.Single(room.Rounds);
    }

    [Fact]
    public void StartNewRound_ResetsPlayerVotes()
    {
        var room = _service.CreateRoom("Room", "conn-admin");
        _service.AddPlayer(room.Code, "Alice", PlayerRole.Member, "conn-alice");
        _service.SelectCard(room.Code, "conn-alice", "5");

        _service.StartNewRound(room.Code, "New Task");

        var player = _service.GetPlayerByConnection(room.Code, "conn-alice");
        Assert.False(player!.HasVoted);
        Assert.Null(player.SelectedCard);
    }

    [Fact]
    public void StartNewRound_MultipleRounds_IncrementsNumber()
    {
        var room = _service.CreateRoom("Room", "conn-admin");
        _service.StartNewRound(room.Code, "Task 1");
        _service.StartNewRound(room.Code, "Task 2");
        var round3 = _service.StartNewRound(room.Code, "Task 3")!;

        Assert.Equal(3, round3.Number);
        Assert.Equal(3, room.Rounds.Count);
    }

    [Fact]
    public void RevealCards_SetsRevealedAndCollectsVotes()
    {
        var room = _service.CreateRoom("Room", "conn-admin");
        _service.AddPlayer(room.Code, "Alice", PlayerRole.Member, "conn-alice");
        _service.AddPlayer(room.Code, "Bob", PlayerRole.Member, "conn-bob");
        _service.StartNewRound(room.Code, "Task");
        _service.SelectCard(room.Code, "conn-alice", "5");
        _service.SelectCard(room.Code, "conn-bob", "8");

        var result = _service.RevealCards(room.Code);

        Assert.True(result);
        Assert.True(room.CurrentRound!.IsRevealed);
        Assert.Equal("5", room.CurrentRound.Votes["Alice"]);
        Assert.Equal("8", room.CurrentRound.Votes["Bob"]);
    }

    [Fact]
    public void RevealCards_NoRound_ReturnsFalse()
    {
        var room = _service.CreateRoom("Room", "conn-admin");
        var result = _service.RevealCards(room.Code);
        Assert.False(result);
    }

    [Fact]
    public void UpdateAvailableCards_UpdatesRoomCards()
    {
        var room = _service.CreateRoom("Room", "conn-admin");
        var newCards = new List<string> { "1", "2", "3", "5" };

        var result = _service.UpdateAvailableCards(room.Code, newCards);

        Assert.True(result);
        Assert.Equal(newCards, room.AvailableCards);
    }

    [Fact]
    public void UpdateAvailableCards_NonExistingRoom_ReturnsFalse()
    {
        var result = _service.UpdateAvailableCards("XXXXXX", new List<string> { "1" });
        Assert.False(result);
    }

    [Fact]
    public void GetRoomByConnection_ReturnsCorrectRoom()
    {
        var room1 = _service.CreateRoom("Room 1", "conn-admin");
        var room2 = _service.CreateRoom("Room 2", "conn-admin2");
        _service.AddPlayer(room1.Code, "Alice", PlayerRole.Member, "conn-alice");
        _service.AddPlayer(room2.Code, "Alice", PlayerRole.Member, "conn-alice2");

        var rooms = _service.GetRoomByConnection("conn-alice").ToList();

        Assert.Single(rooms);
        Assert.Equal(room1.Code, rooms[0]);
    }

    [Fact]
    public void GetRoomByConnection_NonExistingConnection_ReturnsEmpty()
    {
        _service.CreateRoom("Room", "conn-admin");
        var rooms = _service.GetRoomByConnection("conn-nonexistent").ToList();
        Assert.Empty(rooms);
    }

    [Fact]
    public void StartNewRound_NonExistingRoom_ReturnsNull()
    {
        var result = _service.StartNewRound("XXXXXX", "Task");
        Assert.Null(result);
    }
}
