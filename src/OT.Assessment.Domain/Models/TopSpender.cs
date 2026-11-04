namespace OT.Assessment.Domain.Models;

public class TopSpender
{
    public Guid AccountId { get; set; }

    public string Username { get; set; } = string.Empty;

    public decimal TotalAmountSpend { get; set; }
}
