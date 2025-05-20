
using Supabase;
using Supabase.Postgrest;
using Supabase.Postgrest.Models;
using Client = Supabase.Client;

namespace AirCode.Services.SupaBase
{
    public class SupabaseDatabase : ISupabaseDatabase
    {
        private readonly string _supabaseUrl;
        private readonly string _supabaseKey;
        private Client _supabaseClient;
        private readonly SupabaseOptions _options;
        private bool _initialized = false;

        public SupabaseDatabase(IConfiguration configuration)
        {
            _supabaseUrl = configuration["Supabase:Url"] ?? throw new ArgumentNullException("Supabase URL is not configured");
            _supabaseKey = configuration["Supabase:AnonKey"] ?? throw new ArgumentNullException("Supabase Key is not configured");
            
            _options = new SupabaseOptions
            {
                AutoConnectRealtime = true
            };
            
            _supabaseClient = new Client(_supabaseUrl, _supabaseKey, _options);
        }

        public async Task InitializeAsync()
        {
            if (!_initialized)
            {
                await _supabaseClient.InitializeAsync();
                _initialized = true;
            }
        }

        public async Task<T?> GetByIdAsync<T>(int id) where T : BaseModel, new()
        {
            await EnsureInitializedAsync();
            
            try
            {
                return await _supabaseClient
                    .From<T>()
                    .Filter("id", Constants.Operator.Equals, id)
                    .Single();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error retrieving by ID: {ex.Message}");
                return null;
            }
        }

        public async Task<List<T>> GetAllAsync<T>() where T : BaseModel, new()
        {
            await EnsureInitializedAsync();
            
            try
            {
                var result = await _supabaseClient
                    .From<T>()
                    .Get();
                
                return result?.Models ?? new List<T>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error retrieving all items: {ex.Message}");
                return new List<T>();
            }
        }

        public async Task<T> InsertAsync<T>(T item) where T : BaseModel, new()
        {
            await EnsureInitializedAsync();
            
            try
            {
                var response = await _supabaseClient
                    .From<T>()
                    .Insert(item);
                
                return response?.Models?[0] ?? item;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error inserting item: {ex.Message}");
                throw;
            }
        }

        public async Task<T> UpdateAsync<T>(T item) where T : BaseModel, new()
        {
            await EnsureInitializedAsync();
            
            try
            {
                var response = await _supabaseClient
                    .From<T>()
                    .Update(item);
                
                return response?.Models?[0] ?? item;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating item: {ex.Message}");
                throw;
            }
        }

        public async Task DeleteAsync<T>(int id) where T : BaseModel, new()
        {
            await EnsureInitializedAsync();
            
            try
            {
                await _supabaseClient
                    .From<T>()
                    .Filter("id", Constants.Operator.Equals, id)
                    .Delete();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deleting item: {ex.Message}");
                throw;
            }
        }

        public async Task<List<T>> GetWithFilterAsync<T>(string columnName, string filterOperator, object value) where T : BaseModel, new()
        {
            await EnsureInitializedAsync();
            
            try
            {
                var op = ParseOperator(filterOperator);
                
                var result = await _supabaseClient
                    .From<T>()
                    .Filter(columnName, op, value)
                    .Get();
                
                return result?.Models ?? new List<T>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error filtering items: {ex.Message}");
                return new List<T>();
            }
        }

        public async Task<T> ExecuteRpcAsync<T>(string functionName, object? parameters)
        {
            await EnsureInitializedAsync();
            
            try
            {
                return await _supabaseClient.Rpc<T>(functionName, parameters);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error executing RPC function: {ex.Message}");
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

        private Constants.Operator ParseOperator(string op)
        {
            return op.ToLower() switch
            {
                "eq" => Constants.Operator.Equals,
                "neq" => Constants.Operator.NotEqual,
                "gt" => Constants.Operator.GreaterThan,
                "gte" => Constants.Operator.GreaterThanOrEqual,
                "lt" => Constants.Operator.LessThan,
                "lte" => Constants.Operator.LessThanOrEqual,
                "like" => Constants.Operator.Like,
                "ilike" => Constants.Operator.ILike,
                "in" => Constants.Operator.In,
                "contains" => Constants.Operator.Contains,
                "containedin" => Constants.Operator.ContainedIn,
                "fts" => Constants.Operator.FTS,
                _ => Constants.Operator.Equals
            };
        }
    }

}