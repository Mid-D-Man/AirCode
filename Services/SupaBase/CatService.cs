using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Supabase.Functions;
using Supabase.Interfaces;

namespace AirCode.Services.SupaBase;

public class CatService
{
    private readonly HttpClient _httpClient;

    public CatService(HttpClient httpClient)
    {
       
        _httpClient = httpClient;
    }

    public async Task<string> GetRandomCatImageAsync()
    {
        var response = await _httpClient.GetFromJsonAsync<CatResponse>("https://bjwbwcbumfqcdmrsbtkf.supabase.co/functions/v1/get-random-cat");
        return response?.ImageUrl;
    }
}

public class CatResponse
{
    public string ImageUrl { get; set; }
}