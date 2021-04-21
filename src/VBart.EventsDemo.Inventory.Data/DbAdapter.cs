using System.Collections.Generic;
using System.Data.Common;
using System.Threading.Tasks;
using Dapper;

namespace VBart.EventsDemo.Inventory.Data
{
    public class DbAdapter: IDbAdapter
    {
        private readonly DbProviderFactory _factory;
        private readonly DbConnectionStringBuilder _connectionStringBuilder;

        public DbAdapter(DbProviderFactory factory, DbConnectionStringBuilder connectionStringBuilder)
        {
            _factory = factory;
            _connectionStringBuilder = connectionStringBuilder;
        }

        public async Task<IEnumerable<T>> GetMany<T>(string query, object? parameters = null)
        {
            await using var connection = _factory.CreateConnection();
            connection!.ConnectionString = _connectionStringBuilder.ConnectionString;
            return await connection.QueryAsync<T>(query, parameters);
        }

        public async Task<T> GetSingle<T>(string query, object? parameters = null)
        {
            await using var connection = _factory.CreateConnection();
            connection!.ConnectionString = _connectionStringBuilder.ConnectionString;
            return await connection.QuerySingleAsync<T>(query, parameters);
        }

        public async Task<T?> GetSingleOrDefault<T>(string query, object? parameters = null)
        {
            await using var connection = _factory.CreateConnection();
            connection!.ConnectionString = _connectionStringBuilder.ConnectionString;
            return await connection.QuerySingleOrDefaultAsync<T>(query, parameters);
        }

        public async Task<IEnumerable<T>> ExecuteCommand<T>(string query, object? parameters = null)
        {
            await using var connection = _factory.CreateConnection();
            connection!.ConnectionString = _connectionStringBuilder.ConnectionString;
            return await connection.QueryAsync<T>(query, parameters);
        }

        public async Task<T> ExecuteScalarCommand<T>(string query, object? parameters = null)
        {
            await using var connection = _factory.CreateConnection();
            connection!.ConnectionString = _connectionStringBuilder.ConnectionString;
            return await connection.QuerySingleAsync<T>(query, parameters);
        }

        public async Task<int> ExecuteCommand(string query, object? parameters = null)
        {
            await using var connection = _factory.CreateConnection();
            connection!.ConnectionString = _connectionStringBuilder.ConnectionString;
            return await connection.ExecuteAsync(query, parameters);
        }
    }
}