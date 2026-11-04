using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using OT.Assessment.Domain.Interfaces;
using OT.Assessment.Domain.Models;

namespace OT.Assessment.Infrastructure.Persistence;

public class SqlPlayerRepository : IPlayerRepository
{
    private readonly SqlConnectionFactory _connectionFactory;

    public SqlPlayerRepository(SqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<TopSpender>> GetTopSpendersAsync(int count, CancellationToken cancellationToken)
    {
        var parameters = new
        {
            Count = count
        };

        var command = new CommandDefinition(
            "dbo.usp_Player_GetTopSpenders",
            parameters,
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken);

        await using SqlConnection connection = _connectionFactory.CreateConnection();
        IEnumerable<TopSpender> topSpenders = await connection.QueryAsync<TopSpender>(command);

        return topSpenders.ToList();
    }
}
