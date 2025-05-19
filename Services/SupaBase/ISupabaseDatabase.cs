
namespace AirCode.Services.SupaBase
{
    public interface ISupabaseDatabase
    {
        /// <summary>
        /// Initialize the Supabase client connection
        /// </summary>
        Task InitializeAsync();
        
        /// <summary>
        /// Get a single item from a table by its ID
        /// </summary>
        Task<T?> GetByIdAsync<T>(int id) where T : class, new();
        
        /// <summary>
        /// Get all items from a table
        /// </summary>
        Task<List<T>> GetAllAsync<T>() where T : class, new();
        
        /// <summary>
        /// Insert a new item into a table
        /// </summary>
        Task<T> InsertAsync<T>(T item) where T : class, new();
        
        /// <summary>
        /// Update an existing item in a table
        /// </summary>
        Task<T> UpdateAsync<T>(T item) where T : class, new();
        
        /// <summary>
        /// Delete an item from a table by its ID
        /// </summary>
        Task DeleteAsync<T>(int id) where T : class, new();
        
        /// <summary>
        /// Get items from a table with a filter
        /// </summary>
        Task<List<T>> GetWithFilterAsync<T>(string columnName, string filterOperator, object value) where T : class, new();
        
        /// <summary>
        /// Execute a custom RPC function
        /// </summary>
        Task<T> ExecuteRpcAsync<T>(string functionName, object? parameters);
    }
}