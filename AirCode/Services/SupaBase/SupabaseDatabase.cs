
using AirCode.Services.Storage;
using Microsoft.AspNetCore.Components.Authorization;
using Newtonsoft.Json;
using Supabase.Postgrest.Models;
using Supabase.Postgrest;
using static Supabase.Postgrest.Constants;

namespace AirCode.Services.SupaBase
{
   

    public class SupabaseDatabase : ISupabaseDatabase
    {
        private readonly Supabase.Client _client;
        private readonly AuthenticationStateProvider _authStateProvider;
        private readonly IBlazorAppLocalStorageService _localStorage;
        private readonly ILogger<SupabaseDatabase> _logger;
        private bool _initialized = false;

        public SupabaseDatabase(
            Supabase.Client client,
            AuthenticationStateProvider authStateProvider,
            IBlazorAppLocalStorageService localStorage,
            ILogger<SupabaseDatabase> logger)
        {
            _logger = logger;
            _logger.LogInformation("------------------- DATABASE SERVICE CONSTRUCTOR -------------------");
            _client = client;
            _authStateProvider = authStateProvider;
            _localStorage = localStorage;
        }

        public async Task InitializeAsync()
        {
            if (!_initialized)
            {
                try
                {
                    await _client.InitializeAsync();
                    _initialized = true;
                    _logger.LogInformation("Supabase client initialized successfully");
                }
                catch (Supabase.Realtime.Exceptions.RealtimeException ex) when (ex.InnerException is System.PlatformNotSupportedException)
                {
                    // WebSockets not supported in WebAssembly - continue without realtime
                    _logger.LogWarning("Realtime features disabled due to WebAssembly platform limitations: {Message}", ex.Message);
                    _initialized = true; // Mark as initialized to continue with basic functionality
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to initialize Supabase client");
                    throw;
                }
            }
        }

        public async Task<IReadOnlyList<TModel>> GetAllAsync<TModel>() where TModel : BaseModel, new()
        {
            await EnsureInitializedAsync();
            
            try
            {
                var response = await _client.From<TModel>().Get();
                return response.Models;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving all items of type {ModelType}", typeof(TModel).Name);
                return new List<TModel>();
            }
        }

        public async Task<TModel?> GetByIdAsync<TModel>(int id) where TModel : BaseModel, new()
        {
            await EnsureInitializedAsync();
            
            try
            {
                return await _client
                    .From<TModel>()
                    .Filter("id", Operator.Equals, id)
                    .Single();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving {ModelType} by ID: {Id}", typeof(TModel).Name, id);
                return null;
            }
        }

        public async Task<List<TModel>> InsertAsync<TModel>(TModel item) where TModel : BaseModel, new()
        {
            await EnsureInitializedAsync();
            
            try
            {
                var response = await _client.From<TModel>().Insert(item);
                _logger.LogInformation("Successfully inserted {ModelType}", typeof(TModel).Name);
                return response.Models;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error inserting {ModelType}", typeof(TModel).Name);
                throw;
            }
        }

        public async Task<List<TModel>> UpdateAsync<TModel>(TModel item) where TModel : BaseModel, new()
        {
            await EnsureInitializedAsync();
            
            try
            {
                var response = await _client.From<TModel>().Update(item);
                _logger.LogInformation("Successfully updated {ModelType}", typeof(TModel).Name);
                return response.Models;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating {ModelType}", typeof(TModel).Name);
                throw;
            }
        }

        public async Task<List<TModel>> DeleteAsync<TModel>(TModel item) where TModel : BaseModel, new()
        {
            await EnsureInitializedAsync();
            
            try
            {
                var response = await _client.From<TModel>().Delete(item);
                _logger.LogInformation("Successfully deleted {ModelType}", typeof(TModel).Name);
                return response.Models;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting {ModelType}", typeof(TModel).Name);
                throw;
            }
        }

        public async Task<List<TModel>> SoftDeleteAsync<TModel>(TModel item) where TModel : BaseModel, new()
        {
            await EnsureInitializedAsync();
            
            try
            {
                /*
                // This implementation assumes your model has SoftDelete and SoftDeletedAt properties
                // Adjust the property names based on your actual model structure
                var response = await _client.Postgrest
                    .Table<TModel>()
                    .Set(x => new KeyValuePair<object, object>("soft_delete", true))
                    .Set(x => new KeyValuePair<object, object>("soft_deleted_at", DateTime.UtcNow))
                    .Where(x => ((dynamic)x).Id == ((dynamic)item).Id)
                    .Update();
                */
              /*  _logger.LogInformation("Successfully soft deleted {ModelType} with ID: {Id}", 
                    typeof(TModel).Name, ((dynamic)item).Id);*/
                //return response.Models;
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error soft deleting {ModelType}", typeof(TModel).Name);
                throw;
            }
        }

        public async Task<List<TModel>> GetWithFilterAsync<TModel>(string columnName, Operator filterOperator, object value) 
            where TModel : BaseModel, new()
        {
            await EnsureInitializedAsync();
    
            try
            {
                var result = await _client
                    .From<TModel>()
                    .Filter(columnName, filterOperator, value)
                    .Get();
        
                return result?.Models ?? new List<TModel>();
            }
            catch (JsonException jsonEx)
            {
                _logger.LogError(jsonEx, "JSON deserialization error for {ModelType}. Column: {Column}, Value: {Value}", 
                    typeof(TModel).Name, columnName, value);
        
                // Return empty list to prevent cascade failures
                return new List<TModel>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Database query error for {ModelType}. Column: {Column}, Value: {Value}", 
                    typeof(TModel).Name, columnName, value);
                return new List<TModel>();
            }
        }

        public async Task<T> ExecuteRpcAsync<T>(string functionName, object? parameters)
        {
            await EnsureInitializedAsync();
            
            try
            {
                return await _client.Rpc<T>(functionName, parameters);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing RPC function: {FunctionName}", functionName);
                throw;
            }
        }

        private async Task EnsureInitializedAsync()
        {
            if (!_initialized)
            {
                await InitializeAsync();
            }
        }
    }
}