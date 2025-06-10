using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AirCode.Models.Supabase;
using AirCode.Utilities.HelperScripts;

namespace AirCode.Services.SupaBase;

public class SupabaseEdgeFunctionService : ISupabaseEdgeFunctionService
{
    private readonly HttpClient _httpClient;
    private readonly string _supabaseUrl;
    private readonly string _supabaseKey;
    private readonly JsonSerializerOptions _jsonOptions;

    public SupabaseEdgeFunctionService(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _supabaseUrl = configuration["Supabase:Url"] ?? "https://bjwbwcbumfqcdmrsbtkf.supabase.co";
        _supabaseKey = configuration["Supabase:AnonKey"] ?? string.Empty;
        
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
    }

    public async Task<AttendanceProcessingResult> ProcessAttendanceAsync(string qrCodePayload, AttendanceRecord attendanceData)
    {
        try
        {
            var requestPayload = new
            {
                qrCodePayload,
                attendanceData
            };

            var response = await SendEdgeFunctionRequestAsync("process-attendance-data", requestPayload);
            
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<AttendanceProcessingResult>(_jsonOptions);
                return result ?? new AttendanceProcessingResult 
                { 
                    Success = false, 
                    Message = "Invalid response format" 
                };
            }

            var errorContent = await response.Content.ReadAsStringAsync();
            return new AttendanceProcessingResult
            {
                Success = false,
                Message = $"Request failed: {response.StatusCode}",
                ErrorDetails = errorContent
            };
        }
        catch (HttpRequestException ex)
        {
            return new AttendanceProcessingResult
            {
                Success = false,
                Message = $"Network error: {ex.Message}",
                ErrorDetails = ex.ToString()
            };
        }
        catch (Exception ex)
        {
            return new AttendanceProcessingResult
            {
                Success = false,
                Message = $"Processing error: {ex.Message}",
                ErrorDetails = ex.ToString()
            };
        }
    }

    public async Task<QRValidationResult> ValidateQRCodeAsync(string qrCodePayload)
    {
        try
        {
            var requestPayload = new { qrCodePayload };
            var response = await SendEdgeFunctionRequestAsync("process-attendance-data", requestPayload);
            
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<QRValidationResult>(_jsonOptions);
                return result ?? new QRValidationResult 
                { 
                    IsValid = false, 
                    Message = "Invalid response format" 
                };
            }

            return new QRValidationResult
            {
                IsValid = false,
                Message = $"Validation failed: {response.StatusCode}"
            };
        }
        catch (Exception ex)
        {
            return new QRValidationResult
            {
                IsValid = false,
                Message = $"Validation error: {ex.Message}"
            };
        }
    }

    public async Task<string> GetRandomCatImageAsync()
    {
        try
        {
            var response = await SendEdgeFunctionRequestAsync("get-random-cat", null, HttpMethod.Get);
            
            if (response.IsSuccessStatusCode)
            {
                var catResponse = await response.Content.ReadFromJsonAsync<CatResponse>(_jsonOptions);
                return catResponse?.ImageUrl ?? "No image found";
            }

            return $"Failed to get cat image: {response.StatusCode}";
        }
        catch (Exception ex)
        {
            return $"Error getting cat image: {ex.Message}";
        }
    }

    private async Task<HttpResponseMessage> SendEdgeFunctionRequestAsync(
        string functionName, 
        object payload = null, 
        HttpMethod method = null)
    {
        method ??= HttpMethod.Post;
        
        var request = new HttpRequestMessage(method, $"{_supabaseUrl}/functions/v1/{functionName}");
        
        // Add authorization header
        if (!string.IsNullOrEmpty(_supabaseKey))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _supabaseKey);
        }

        // Add content type header
        request.Headers.Add("Content-Type", "application/json");

        // Add payload for POST requests
        if (payload != null && method == HttpMethod.Post)
        {
            request.Content = JsonContent.Create(payload, options: _jsonOptions);
        }

        return await _httpClient.SendAsync(request);
    }
}

// Response Models
public class AttendanceProcessingResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string ErrorDetails { get; set; } = string.Empty;
    public DecodedSessionData SessionData { get; set; }
    public AttendanceRecord ProcessedAttendance { get; set; }

    public override string ToString()
    {
        return MID_HelperFunctions.GetStructOrClassMemberValues(this);
    }
}

public class QRValidationResult
{
    public bool IsValid { get; set; }
    public string Message { get; set; } = string.Empty;
    public DecodedSessionData SessionData { get; set; }
    public DateTime? ExpirationTime { get; set; }
    public bool IsExpired { get; set; }
    public override string ToString()
    {
        return JsonHelper.Serialize(this, false);
    }
}

public class DecodedSessionData
{
    public string SessionId { get; set; } = string.Empty;
    public string CourseCode { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public int Duration { get; set; }
    public DateTime GeneratedTime { get; set; }
    public DateTime ExpirationTime { get; set; }
    public string Nonce { get; set; } = string.Empty;
    public string LectureId { get; set; } = string.Empty;
    public override string ToString()
    {
        return JsonHelper.Serialize(this, false);
    }
}

public class AttendanceRecord
{
    public string MatricNumber { get; set; } = string.Empty;
    public bool HasScannedAttendance { get; set; }
    public bool IsOnlineScan { get; set; }

    public override string ToString()
    {
        return JsonHelper.Serialize(this, false);
    }
}

public class CatResponse
{
    public string ImageUrl { get; set; } = string.Empty;
}