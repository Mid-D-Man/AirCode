using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Supabase.Functions;
using Supabase.Interfaces;

namespace AirCode.Services.SupaBase;

public interface ICatService
{
    public Task<string> GetRandomCatImageAsync();
}
public class CatService:ICatService
{
    private readonly HttpClient _httpClient;
    private readonly string _supabaseUrl;
    private readonly string _supabaseKey;

    public CatService(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _supabaseUrl = configuration["Supabase:Url"] ?? "https://bjwbwcbumfqcdmrsbtkf.supabase.co";
        _supabaseKey = configuration["Supabase:AnonKey"] ?? string.Empty;
    }

    public async Task<string> GetRandomCatImageAsync()
    {
        try
        {
            // For Blazor WebAssembly, we need to handle CORS differently
            // Create a new HttpRequestMessage
            var request = new HttpRequestMessage(HttpMethod.Get, 
                $"{_supabaseUrl}/functions/v1/get-random-cat");
            
            // Add authorization header
            if (!string.IsNullOrEmpty(_supabaseKey))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _supabaseKey);
            }
            
            // Send the request
            var response = await _httpClient.SendAsync(request);
            
            // Ensure success status code
            response.EnsureSuccessStatusCode();
            
            // Parse the response
            var catResponse = await response.Content.ReadFromJsonAsync<CatResponse>();
            return catResponse?.ImageUrl ?? "No image found";
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine($"HTTP Request Error: {ex.Message}");
            // Return a more user-friendly error message
            return $"Failed to get cat image: {ex.Message}";
        }
        catch (Exception ex)
        {
            Console.WriteLine($"General Error: {ex.Message}");
            return $"An error occurred: {ex.Message}";
        }
    }
}
