using System.ComponentModel.DataAnnotations;

namespace OT.Assessment.Domain.Models;

// The wager event posted to the API and published to the queue.
// The payload has more fields than this, but these are the only ones we store.
public class CasinoWager
{
    [Required]
    public Guid WagerId { get; set; }

    [Required]
    public Guid AccountId { get; set; }

    [Required]
    [MaxLength(100)]
    public string Username { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string Provider { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string GameName { get; set; } = string.Empty;

    [Range(0, double.MaxValue)]
    public decimal Amount { get; set; }

    [Required]
    public DateTimeOffset CreatedDateTime { get; set; }
}
