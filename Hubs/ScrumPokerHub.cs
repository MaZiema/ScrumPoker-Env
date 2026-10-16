using Microsoft.AspNetCore.SignalR;
using Scrum_Poker_V1.Models;
using Scrum_Poker_V1.Services;

namespace Scrum_Poker_V1.Hubs;

public class ScrumPokerHub : Hub
{
    private readonly RoomService _roomService;

    public ScrumPokerHub(RoomService roomService)
    {
        _roomService = roomService;
    }

    public async Task CreateRoom(string roomName)
    {
        var room = _roomService.CreateRoom(roomName, Context.ConnectionId);
        await Clients.Caller.SendAsync("RoomCreated", room.Code, room.Name);
    }

    public async Task JoinRoom(string roomCode, string playerName, PlayerRole role)
    {
        var room = _roomService.GetRoom(roomCode);
        if (room == null)
        {
            await Clients.Caller.SendAsync("Error", "Raum nicht gefunden.");
            return;
        }

        var player = _roomService.AddPlayer(roomCode, playerName, role, Context.ConnectionId);
        if (player == null)
        {
            await Clients.Caller.SendAsync("Error", "Konnte dem Raum nicht beitreten.");
            return;
        }

        if (role == PlayerRole.Admin)
        {
            room.AdminConnectionId = Context.ConnectionId;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, roomCode);

        var players = _roomService.GetPlayersCopy(roomCode);
        await Clients.Caller.SendAsync("JoinedRoom", new
        {
            RoomCode = room.Code,
            RoomName = room.Name,
            PlayerId = player.Id,
            PlayerName = player.Name,
            Role = player.Role.ToString(),
            Players = players.Select(p => new { p.Name, Role = p.Role.ToString(), p.HasVoted, p.SelectedCard }).ToList(),
            CurrentRound = room.CurrentRound != null ? new
            {
                room.CurrentRound.Number,
                room.CurrentRound.Title,
                room.CurrentRound.IsRevealed
            } : null,
            AvailableCards = room.AvailableCards
        });

        await Clients.Group(roomCode).SendAsync("PlayerJoined", new
        {
            Name = player.Name,
            Role = player.Role.ToString()
        });

        await SendPlayerList(roomCode);
    }

    public async Task SelectCard(string roomCode, string card)
    {
        _roomService.SelectCard(roomCode, Context.ConnectionId, card);

        var player = _roomService.GetPlayerByConnection(roomCode, Context.ConnectionId);
        if (player == null) return;

        await Clients.Caller.SendAsync("CardSelected", card);
        await Clients.Group(roomCode).SendAsync("PlayerVoted", new { Name = player.Name });
        await SendPlayerList(roomCode);
    }

    public async Task StartNewRound(string roomCode, string title)
    {
        var room = _roomService.GetRoom(roomCode);
        if (room == null || room.AdminConnectionId != Context.ConnectionId) return;

        var round = _roomService.StartNewRound(roomCode, title);
        if (round == null) return;
        await Clients.Group(roomCode).SendAsync("NewRoundStarted", new { Title = title, Number = round.Number });
        await SendPlayerList(roomCode);
    }

    public async Task RevealCards(string roomCode)
    {
        var room = _roomService.GetRoom(roomCode);
        if (room == null || room.AdminConnectionId != Context.ConnectionId) return;

        var revealed = _roomService.RevealCards(roomCode);
        if (!revealed) return;

        var currentRound = room.CurrentRound;
        if (currentRound == null) return;

        await Clients.Group(roomCode).SendAsync("CardsRevealed", new
        {
            Votes = currentRound.Votes,
            Number = currentRound.Number,
            Title = currentRound.Title
        });
    }

    public async Task UpdateAvailableCards(string roomCode, List<string> cards)
    {
        var room = _roomService.GetRoom(roomCode);
        if (room == null || room.AdminConnectionId != Context.ConnectionId) return;

        _roomService.UpdateAvailableCards(roomCode, cards);
        await Clients.Group(roomCode).SendAsync("CardsUpdated", cards);
    }

    public async Task KickPlayer(string roomCode, string playerName)
    {
        var room = _roomService.GetRoom(roomCode);
        if (room == null || room.AdminConnectionId != Context.ConnectionId) return;

        Player? player;
        lock (room.PlayersLock)
        {
            player = room.Players.FirstOrDefault(p => p.Name == playerName);
            if (player == null) return;
        }

        await Clients.Group(roomCode).SendAsync("PlayerKicked", playerName);
        await Clients.Client(player.ConnectionId).SendAsync("KickedFromRoom");
        await Groups.RemoveFromGroupAsync(player.ConnectionId, roomCode);

        _roomService.RemovePlayer(roomCode, player.ConnectionId);
        await SendPlayerList(roomCode);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var roomCode = GetRoomCodeFromConnection(Context.ConnectionId);
        if (roomCode != null)
        {
            var player = _roomService.GetPlayerByConnection(roomCode, Context.ConnectionId);
            if (player != null)
            {
                await Clients.Group(roomCode).SendAsync("PlayerLeft", new
                {
                    Name = player.Name,
                    Role = player.Role.ToString()
                });

                _roomService.RemovePlayer(roomCode, Context.ConnectionId);
                await SendPlayerList(roomCode);
            }
        }

        await base.OnDisconnectedAsync(exception);
    }

    private async Task SendPlayerList(string roomCode)
    {
        var room = _roomService.GetRoom(roomCode);
        if (room == null) return;

        var players = _roomService.GetPlayersCopy(roomCode);
        var playerDtos = players.Select(p => new
        {
            p.Name,
            Role = p.Role.ToString(),
            p.HasVoted,
            p.SelectedCard
        }).ToList();

        await Clients.Group(roomCode).SendAsync("PlayerListUpdated", playerDtos);
    }

    private string? GetRoomCodeFromConnection(string connectionId)
    {
        foreach (var room in _roomService.GetRoomByConnection(connectionId))
        {
            return room;
        }
        return null;
    }
}
