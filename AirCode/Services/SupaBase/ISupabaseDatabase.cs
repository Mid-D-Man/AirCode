using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Supabase.Postgrest;
using Supabase.Postgrest.Models;

namespace AirCode.Services.SupaBase
{
    public interface ISupabaseDatabase
    {
        /// <summary>
        /// Initialize the Supabase client connection
        /// </summary>
        Task InitializeAsync();
        
        /// <summary>
        /// Get all items from a table
        /// </summary>
        Task<IReadOnlyList<TModel>> GetAllAsync<TModel>() where TModel : BaseModel, new();
        
        /// <summary>
        /// Get a single item from a table by its ID
        /// </summary>
        Task<TModel?> GetByIdAsync<TModel>(int id) where TModel : BaseModel, new();
        
        /// <summary>
        /// Insert a new item into a table
        /// </summary>
        Task<List<TModel>> InsertAsync<TModel>(TModel item) where TModel : BaseModel, new();
        
        /// <summary>
        /// Update an existing item in a table
        /// </summary>
        Task<List<TModel>> UpdateAsync<TModel>(TModel item) where TModel : BaseModel, new();
        
        /// <summary>
        /// Delete an item from a table
        /// </summary>
        Task<List<TModel>> DeleteAsync<TModel>(TModel item) where TModel : BaseModel, new();
        
        /// <summary>
        /// Soft delete implementation for items with SoftDelete property
        /// </summary>
        Task<List<TModel>> SoftDeleteAsync<TModel>(TModel item) where TModel : BaseModel, new();
        
        /// <summary>
        /// Get items from a table with filtering
        /// </summary>
        Task<List<TModel>> GetWithFilterAsync<TModel>(string columnName, Constants.Operator filterOperator, object value) where TModel : BaseModel, new();
        
        /// <summary>
        /// Execute a custom RPC function
        /// </summary>
        Task<T> ExecuteRpcAsync<T>(string functionName, object? parameters);
    }
}