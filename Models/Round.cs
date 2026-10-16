namespace Scrum_Poker_V1.Models;

public class Round
{
    public int Number { get; set; }
    public string Title { get; set; } = string.Empty;
    public bool IsRevealed { get; set; }
    public Dictionary<string, string> Votes { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
