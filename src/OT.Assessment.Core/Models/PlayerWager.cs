namespace OT.Assessment.Core.Models;

// One row in a player's list of wagers.
public class PlayerWager
{
    public Guid WagerId { get; set; }

    public string Game { get; set; } = string.Empty;

    public string Provider { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public DateTime CreatedDate { get; set; }
}
