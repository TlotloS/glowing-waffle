using Microsoft.AspNetCore.Mvc;
using OT.Assessment.Core.Interfaces;
using OT.Assessment.Core.Models;

namespace OT.Assessment.App.Controllers;

[ApiController]
[Route("api/player")]
public class PlayerController : ControllerBase
{
    private const int MaxPageSize = 100;
    private const int MaxTopSpenders = 100;

    private readonly ICasinoWagerPublisher _wagerPublisher;
    private readonly ICasinoWagerRepository _wagerRepository;
    private readonly IPlayerRepository _playerRepository;

    public PlayerController(
        ICasinoWagerPublisher wagerPublisher,
        ICasinoWagerRepository wagerRepository,
        IPlayerRepository playerRepository)
    {
        _wagerPublisher = wagerPublisher;
        _wagerRepository = wagerRepository;
        _playerRepository = playerRepository;
    }

    // POST api/player/casinowager
    // Returns 202 Accepted because the wager is queued here and saved later by the consumer.
    [HttpPost("casinowager")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AddCasinoWager([FromBody] CasinoWager wager, CancellationToken cancellationToken)
    {
        // [Required] doesn't catch a missing Guid, because it defaults to Guid.Empty instead of null.
        if (wager.WagerId == Guid.Empty)
        {
            return BadRequest("wagerId is required.");
        }

        if (wager.AccountId == Guid.Empty)
        {
            return BadRequest("accountId is required.");
        }

        await _wagerPublisher.PublishAsync(wager, cancellationToken);

        return Accepted();
    }

    // GET api/player/{playerId}/casino?pageSize=10&page=1
    [HttpGet("{playerId:guid}/casino")]
    [ProducesResponseType(typeof(PagedResult<PlayerWager>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetCasinoWagers(
        Guid playerId,
        CancellationToken cancellationToken,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        if (page < 1)
        {
            return BadRequest("page must be 1 or more.");
        }

        if (pageSize < 1 || pageSize > MaxPageSize)
        {
            return BadRequest($"pageSize must be between 1 and {MaxPageSize}.");
        }

        PagedResult<PlayerWager> result = await _wagerRepository.GetByPlayerAsync(playerId, page, pageSize, cancellationToken);

        return Ok(result);
    }

    // GET api/player/topSpenders?count=10
    [HttpGet("topSpenders")]
    [ProducesResponseType(typeof(IReadOnlyList<TopSpender>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetTopSpenders(CancellationToken cancellationToken, [FromQuery] int count = 10)
    {
        if (count < 1 || count > MaxTopSpenders)
        {
            return BadRequest($"count must be between 1 and {MaxTopSpenders}.");
        }

        IReadOnlyList<TopSpender> topSpenders = await _playerRepository.GetTopSpendersAsync(count, cancellationToken);

        return Ok(topSpenders);
    }
}
