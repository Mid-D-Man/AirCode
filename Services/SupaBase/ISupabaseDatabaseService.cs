namespace AirCode.Services.SupaBase
{
    public interface ISupabaseDatabaseService
    {
        Task<T> GetAsync<T>(string table, Dictionary<string, string> queryParams = null);
        Task<T> GetByIdAsync<T>(string table, string id, Dictionary<string, string> queryParams = null);
        Task<T> InsertAsync<T>(string table, object data);
        Task<T> UpdateAsync<T>(string table, string id, object data);
        Task<T> DeleteAsync<T>(string table, string id);
        Task<T> RpcAsync<T>(string functionName, object parameters = null);
        Task<T> ExecuteFunctionAsync<T>(string functionName, object parameters = null);
        Task<List<OfflineSyncResult>> SyncOfflineOperationsAsync();
        Task StoreOfflineOperationAsync(string operationType, string table, string id, object data);
    }
}