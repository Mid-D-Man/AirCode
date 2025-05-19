using System;
using System.Threading.Tasks;
using Microsoft.JSInterop;
using System.Text;
using Newtonsoft.Json;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;

namespace AirCode.Services.SupaBase
{
   
    public class SupabaseDatabaseService : ISupabaseDatabaseService
    {
        private readonly IJSRuntime _jsRuntime;
        private readonly string _supabaseUrl;
        private readonly string _supabaseKey;
        private readonly Lazy<Task<IJSObjectReference>> _moduleTask;

        public SupabaseDatabaseService(IJSRuntime jsRuntime, IConfiguration configuration)
        {
            _jsRuntime = jsRuntime;
            _supabaseUrl = configuration["Supabase:Url"] ?? "https://bjwbwcbumfqcdmrsbtkf.supabase.co";
            _supabaseKey = configuration["Supabase:AnonKey"] ?? string.Empty;
           
            _moduleTask = new Lazy<Task<IJSObjectReference>>(async () => 
            {
                // Load the module
                await _jsRuntime.InvokeVoidAsync("import", "./js/supabaseModule.js");
                
                // Initialize the module with configuration
                return await _jsRuntime.InvokeAsync<IJSObjectReference>(
                    "initSupabaseModule", 
                    _supabaseUrl, 
                    _supabaseKey
                );
            });
        }

        private async Task<IJSObjectReference> GetModuleAsync()
        {
            return await _moduleTask.Value;
        }

        public async Task<T> GetAsync<T>(string table, Dictionary<string, string> queryParams = null)
        {
            try
            {
                var module = await GetModuleAsync();
                var result = await module.InvokeAsync<string>("get", table, queryParams ?? new Dictionary<string, string>());
                return JsonConvert.DeserializeObject<T>(result);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetAsync: {ex.Message}");
                throw;
            }
        }

        public async Task<T> GetByIdAsync<T>(string table, string id, Dictionary<string, string> queryParams = null)
        {
            try
            {
                var module = await GetModuleAsync();
                var result = await module.InvokeAsync<string>("getById", table, id, queryParams ?? new Dictionary<string, string>());
                return JsonConvert.DeserializeObject<T>(result);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetByIdAsync: {ex.Message}");
                throw;
            }
        }

        public async Task<T> InsertAsync<T>(string table, object data)
        {
            try
            {
                var module = await GetModuleAsync();
                var jsonData = JsonConvert.SerializeObject(data);
                var result = await module.InvokeAsync<string>("insert", table, jsonData);
                return JsonConvert.DeserializeObject<T>(result);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in InsertAsync: {ex.Message}");
                
                // Store operation for offline sync if network error
                if (ex.Message.Contains("network") || ex.Message.Contains("fetch"))
                {
                    await StoreOfflineOperationAsync("insert", table, null, data);
                }
                
                throw;
            }
        }

        public async Task<T> UpdateAsync<T>(string table, string id, object data)
        {
            try
            {
                var module = await GetModuleAsync();
                var jsonData = JsonConvert.SerializeObject(data);
                var result = await module.InvokeAsync<string>("update", table, id, jsonData);
                return JsonConvert.DeserializeObject<T>(result);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in UpdateAsync: {ex.Message}");
                
                // Store operation for offline sync if network error
                if (ex.Message.Contains("network") || ex.Message.Contains("fetch"))
                {
                    await StoreOfflineOperationAsync("update", table, id, data);
                }
                
                throw;
            }
        }

        public async Task<T> DeleteAsync<T>(string table, string id)
        {
            try
            {
                var module = await GetModuleAsync();
                var result = await module.InvokeAsync<string>("delete", table, id);
                return JsonConvert.DeserializeObject<T>(result);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in DeleteAsync: {ex.Message}");
                
                // Store operation for offline sync if network error
                if (ex.Message.Contains("network") || ex.Message.Contains("fetch"))
                {
                    await StoreOfflineOperationAsync("delete", table, id, null);
                }
                
                throw;
            }
        }

        public async Task<T> RpcAsync<T>(string functionName, object parameters = null)
        {
            try
            {
                var module = await GetModuleAsync();
                var jsonParams = parameters != null ? JsonConvert.SerializeObject(parameters) : "{}";
                var result = await module.InvokeAsync<string>("rpc", functionName, jsonParams);
                return JsonConvert.DeserializeObject<T>(result);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in RpcAsync: {ex.Message}");
                throw;
            }
        }

        public async Task<T> ExecuteFunctionAsync<T>(string functionName, object parameters = null)
        {
            try
            {
                var module = await GetModuleAsync();
                var jsonParams = parameters != null ? JsonConvert.SerializeObject(parameters) : "{}";
                var result = await module.InvokeAsync<string>("executeFunction", functionName, jsonParams);
                return JsonConvert.DeserializeObject<T>(result);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in ExecuteFunctionAsync: {ex.Message}");
                throw;
            }
        }

        public async Task StoreOfflineOperationAsync(string operationType, string table, string id, object data)
        {
            try
            {
                var module = await GetModuleAsync();
                var operation = new
                {
                    type = operationType,
                    table = table,
                    id = id,
                    data = data != null ? JsonConvert.SerializeObject(data) : null
                };
                
                await module.InvokeVoidAsync("storeOfflineOperation", operation);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error storing offline operation: {ex.Message}");
                // Fall back to local storage if module fails
                try
                {
                    await _jsRuntime.InvokeVoidAsync(
                        "eval", 
                        $"const ops = JSON.parse(localStorage.getItem('supabase_offline_operations') || '[]'); " +
                        $"ops.push({{ timestamp: new Date().toISOString(), type: '{operationType}', table: '{table}', id: '{id}', data: {JsonConvert.SerializeObject(data)} }}); " +
                        $"localStorage.setItem('supabase_offline_operations', JSON.stringify(ops));"
                    );
                }
                catch
                {
                    // Silent fallback failure
                }
            }
        }

        public async Task<List<OfflineSyncResult>> SyncOfflineOperationsAsync()
        {
            try
            {
                var module = await GetModuleAsync();
                var jsonResult = await module.InvokeAsync<string>("syncOfflineOperations");
                return JsonConvert.DeserializeObject<List<OfflineSyncResult>>(jsonResult);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error syncing offline operations: {ex.Message}");
                return new List<OfflineSyncResult>
                {
                    new OfflineSyncResult
                    {
                        Success = false,
                        Error = ex.Message
                    }
                };
            }
        }
    }

    public class OfflineSyncResult
    {
        [JsonProperty("success")]
        public bool Success { get; set; }
        
        [JsonProperty("operation")]
        public OfflineOperation Operation { get; set; }
        
        [JsonProperty("result")]
        public object Result { get; set; }
        
        [JsonProperty("error")]
        public string Error { get; set; }
    }

    public class OfflineOperation
    {
        [JsonProperty("timestamp")]
        public string Timestamp { get; set; }
        
        [JsonProperty("type")]
        public string Type { get; set; }
        
        [JsonProperty("table")]
        public string Table { get; set; }
        
        [JsonProperty("id")]
        public string Id { get; set; }
        
        [JsonProperty("data")]
        public string Data { get; set; }
    }
}