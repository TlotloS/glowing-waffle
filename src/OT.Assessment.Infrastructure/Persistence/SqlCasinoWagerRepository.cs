using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using OT.Assessment.Core.Interfaces;
using OT.Assessment.Core.Models;

namespace OT.Assessment.Infrastructure.Persistence;

public class SqlCasinoWagerRepository : ICasinoWagerRepository
{
    private readonly SqlConnectionFactory _connectionFactory;

    public SqlCasinoWagerRepository(SqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task InsertBatchAsync(IReadOnlyList<CasinoWager> wagers, CancellationToken cancellationToken)
    {
        DataTable wagerTable = CreateWagerTable(wagers);

        var parameters = new DynamicParameters();
        parameters.Add("@Wagers", wagerTable.AsTableValuedParameter("dbo.CasinoWagerTableType"));

        var command = new CommandDefinition(
            "dbo.usp_CasinoWager_InsertBatch",
            parameters,
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken);

        await using SqlConnection connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(command);
    }

    public async Task<PagedResult<PlayerWager>> GetByPlayerAsync(Guid accountId, int page, int pageSize, CancellationToken cancellationToken)
    {
        var parameters = new
        {
            AccountId = accountId,
            Page = page,
            PageSize = pageSize
        };

        var command = new CommandDefinition(
            "dbo.usp_CasinoWager_GetByPlayer",
            parameters,
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken);

        await using SqlConnection connection = _connectionFactory.CreateConnection();
        using SqlMapper.GridReader results = await connection.QueryMultipleAsync(command);

        int total = await results.ReadSingleAsync<int>();
        List<PlayerWager> wagers = (await results.ReadAsync<PlayerWager>()).ToList();

        // SQL Server stores the dates in UTC but returns them without a kind, so mark them as UTC.
        foreach (PlayerWager wager in wagers)
        {
            wager.CreatedDate = DateTime.SpecifyKind(wager.CreatedDate, DateTimeKind.Utc);
        }

        return PagedResult<PlayerWager>.Create(wagers, page, pageSize, total);
    }

    // The column order must match dbo.CasinoWagerTableType.
    private static DataTable CreateWagerTable(IReadOnlyList<CasinoWager> wagers)
    {
        var table = new DataTable();
        table.Columns.Add("WagerId", typeof(Guid));
        table.Columns.Add("AccountId", typeof(Guid));
        table.Columns.Add("Username", typeof(string));
        table.Columns.Add("ProviderName", typeof(string));
        table.Columns.Add("GameName", typeof(string));
        table.Columns.Add("Amount", typeof(decimal));
        table.Columns.Add("CreatedDateTimeUtc", typeof(DateTime));

        foreach (CasinoWager wager in wagers)
        {
            table.Rows.Add(
                wager.WagerId,
                wager.AccountId,
                wager.Username,
                wager.Provider,
                wager.GameName,
                wager.Amount,
                wager.CreatedDateTime.UtcDateTime);
        }

        return table;
    }
}
