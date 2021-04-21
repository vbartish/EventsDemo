using System.Collections.Generic;
using System.Threading.Tasks;

namespace VBart.EventsDemo.Inventory.Data
{
    public interface IDbAdapter
    {
        Task<IEnumerable<T>> GetMany<T>(string query, object? parameters = null);

        Task<T> GetSingle<T>(string query, object? parameters = null);

        Task<T?> GetSingleOrDefault<T>(string query, object? parameters = null);

        Task<IEnumerable<T>> ExecuteCommand<T>(string query, object? parameters = null);

        Task<int> ExecuteCommand(string query, object? parameters = null);

        Task<T> ExecuteScalarCommand<T>(string query, object? parameters = null);
    }
}