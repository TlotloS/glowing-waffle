using Microsoft.Data.SqlClient;

namespace OT.Assessment.Infrastructure.Persistence;

public class SqlConnectionFactory
{
    private readonly string _connectionString;

    public SqlConnectionFactory(string connectionString)
    {
        _connectionString = connectionString;
    }

    // Connections are pooled by SqlClient, so opening a new one per query is cheap.
    public SqlConnection CreateConnection()
    {
        var connection = new SqlConnection(_connectionString);
        return connection;
    }
}
