using System.Collections.Concurrent;
using Scrum_Poker_V1.Models;

namespace Scrum_Poker_V1.Services;

public class RoomService
{
    private readonly ConcurrentDictionary<string, Room> _rooms = new();

    public Room CreateRoom(string name, string adminConnectionId)
    {
        var cleanName = name?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(cleanName)) cleanName = "Raum";
        if (cleanName.Length > 12) cleanName = cleanName[..12];

        for (int i = 0; i < 10; i++)
        {
            var code = GenerateRoomCode();
            var room = new Room
            {
                Code = code,
                Name = cleanName,
                AdminConnectionId = adminConnectionId
            };
            if (_rooms.TryAdd(code, room))
                return room;
        }
        throw new InvalidOperationException("Konnte keinen eindeutigen Raum-Code generieren.");
    }

    public Room? GetRoom(string code)
    {
        _rooms.TryGetValue(code, out var room);
        return room;
    }

    public bool RoomExists(string code) => _rooms.ContainsKey(code);

    public Player? AddPlayer(string roomCode, string name, PlayerRole role, string connectionId)
    {
        var room = GetRoom(roomCode);
        if (room == null) return null;

        lock (room.PlayersLock)
        {
            var baseName = name?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(baseName) || baseName.Length > 12)
                return null;

            var finalName = baseName;
            if (room.Players.Any(p => p.Name.Equals(finalName, StringComparison.OrdinalIgnoreCase)))
            {
                int i = 2;
                while (room.Players.Any(p => p.Name.Equals(finalName, StringComparison.OrdinalIgnoreCase)))
                {
                    var suffix = $" ({i++})";
                    finalName = baseName + suffix;
                    if (i > 99) break;
                }
            }

            var player = new Player
            {
                Name = finalName,
                ConnectionId = connectionId,
                Role = role
            };
            room.Players.Add(player);
            return player;
        }
    }

    public void RemovePlayer(string roomCode, string connectionId)
    {
        var room = GetRoom(roomCode);
        if (room == null) return;

        lock (room.PlayersLock)
        {
            room.Players.RemoveAll(p => p.ConnectionId == connectionId);

            if (room.Players.Count == 0)
            {
                _rooms.TryRemove(roomCode, out _);
            }
        }
    }

    public Player? GetPlayerByConnection(string roomCode, string connectionId)
    {
        var room = GetRoom(roomCode);
        if (room == null) return null;

        lock (room.PlayersLock)
        {
            return room.Players.FirstOrDefault(p => p.ConnectionId == connectionId);
        }
    }

    public void SelectCard(string roomCode, string connectionId, string card)
    {
        var player = GetPlayerByConnection(roomCode, connectionId);
        if (player == null || player.Role == PlayerRole.Observer) return;

        player.SelectedCard = card;
        player.HasVoted = true;
    }

    public Round? StartNewRound(string roomCode, string title)
    {
        var room = GetRoom(roomCode);
        if (room == null) return null;

        lock (room.PlayersLock)
        {
            foreach (var player in room.Players)
            {
                player.HasVoted = false;
                player.SelectedCard = null;
            }
        }

        lock (room.RoundsLock)
        {
            var round = new Round
            {
                Number = room.Rounds.Count + 1,
                Title = title
            };
            room.Rounds.Add(round);
            return round;
        }
    }

    public bool RevealCards(string roomCode)
    {
        var room = GetRoom(roomCode);
        if (room?.CurrentRound == null) return false;

        lock (room.PlayersLock)
        {
            var currentRound = room.CurrentRound;
            currentRound.IsRevealed = true;

            foreach (var player in room.Players.Where(p => p.HasVoted))
            {
                currentRound.Votes[player.Name] = player.SelectedCard ?? "?";
            }
        }
        return true;
    }

    public bool UpdateAvailableCards(string roomCode, List<string> cards)
    {
        var room = GetRoom(roomCode);
        if (room == null) return false;

        room.AvailableCards = cards;
        return true;
    }

    public IEnumerable<string> GetRoomByConnection(string connectionId)
    {
        return _rooms.Where(kvp =>
        {
            lock (kvp.Value.PlayersLock)
            {
                return kvp.Value.Players.Any(p => p.ConnectionId == connectionId);
            }
        }).Select(kvp => kvp.Key);
    }

    public List<Player> GetPlayersCopy(string roomCode)
    {
        var room = GetRoom(roomCode);
        if (room == null) return new List<Player>();

        lock (room.PlayersLock)
        {
            return room.Players.ToList();
        }
    }

    private static string GenerateRoomCode()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        return new string(Enumerable.Repeat(chars, 6)
            .Select(s => s[Random.Shared.Next(s.Length)]).ToArray());
    }
}
