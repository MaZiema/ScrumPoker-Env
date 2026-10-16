namespace Scrum_Poker_V1.Models;

public enum PlayerRole
{
    Admin,
    Member,
    Observer
}

public class Player
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string ConnectionId { get; set; } = string.Empty;
    public PlayerRole Role { get; set; }
    public bool HasVoted { get; set; }
    public string? SelectedCard { get; set; }
}
