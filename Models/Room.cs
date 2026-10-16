namespace Scrum_Poker_V1.Models;

public class Room
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string AdminConnectionId { get; set; } = string.Empty;
    public List<Player> Players { get; set; } = new();
    public List<string> AvailableCards { get; set; } = new()
    {
        "1", "2", "3", "4", "5", "6", "7", "8", "9", "42", "100"
    };
    public List<Round> Rounds { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public object PlayersLock { get; } = new();
    public object RoundsLock { get; } = new();

    public Round? CurrentRound
    {
        get
        {
            lock (RoundsLock)
            {
                return Rounds.LastOrDefault();
            }
        }
    }
}
