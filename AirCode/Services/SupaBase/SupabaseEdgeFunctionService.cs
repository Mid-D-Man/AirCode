using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AirCode.Models.Supabase;
using AirCode.Models.QRCode;
using AirCode.Utilities.HelperScripts;

namespace AirCode.Services.SupaBase
{
    public class SupabaseEdgeFunctionService : ISupabaseEdgeFunctionService
    {
        private readonly HttpClient _httpClient;
        private readonly string _supabaseUrl;
        private readonly string _supabaseKey;
        private readonly JsonSerializerOptions _jsonOptions;
        private readonly QRCodeDecoder _qrCodeDecoder;

        public SupabaseEdgeFunctionService(
            HttpClient httpClient, 
            IConfiguration configuration,
            QRCodeDecoder qrCodeDecoder)
        {
            _httpClient = httpClient;
            _supabaseUrl = configuration["Supabase:Url"] ?? "https://bjwbwcbumfqcdmrsbtkf.supabase.co";
            _supabaseKey = configuration["Supabase:AnonKey"] ?? string.Empty;
            _qrCodeDecoder = qrCodeDecoder;
            
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };
        }

        /// <summary>
        /// Process attendance with unencrypted payload structure
        /// </summary>
        public async Task<AttendanceProcessingResult> ProcessAttendanceWithPayloadAsync(EdgeFunctionRequest request)
        {
            try
            {
                Console.WriteLine($"Sending edge function request: {JsonSerializer.Serialize(request, _jsonOptions)}");

                var response = await SendEdgeFunctionRequestAsync("process-attendance-data", request);
                
                var responseContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"Response Status: {response.StatusCode}");
                Console.WriteLine($"Response Content: {responseContent}");
                
                if (response.IsSuccessStatusCode)
                {
                    var result = JsonSerializer.Deserialize<AttendanceProcessingResult>(responseContent, _jsonOptions);
                    return result ?? new AttendanceProcessingResult 
                    { 
                        Success = false, 
                        Message = "Invalid response format" 
                    };
                }

                return new AttendanceProcessingResult
                {
                    Success = false,
                    Message = $"Request failed: {response.StatusCode}",
                    ErrorDetails = responseContent
                };
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"HTTP Request Exception: {ex}");
                return new AttendanceProcessingResult
                {
                    Success = false,
                    Message = $"Network error: {ex.Message}",
                    ErrorDetails = ex.ToString()
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"General Exception: {ex}");
                return new AttendanceProcessingResult
                {
                    Success = false,
                    Message = $"Processing error: {ex.Message}",
                    ErrorDetails = ex.ToString()
                };
            }
        }

        /// <summary>
        /// Validate QR code payload data with signature verification
        /// </summary>
        public async Task<QRValidationResult> ValidateQRPayloadAsync(QRCodePayloadData payloadData, string signature)
        {
            try
            {
                var validationRequest = new
                {
                    payloadData = payloadData,
                    signature = signature,
                    validationOnly = true
                };

                var response = await SendEdgeFunctionRequestAsync("validate-qr-payload", validationRequest);
                
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

        /// <summary>
        /// Legacy method - deprecated, use ProcessAttendanceWithPayloadAsync instead
        /// </summary>
        [Obsolete("Use ProcessAttendanceWithPayloadAsync for new payload structure")]
        public async Task<AttendanceProcessingResult> ProcessAttendanceAsync(
            string qrCodeContent, 
            AttendanceRecord attendanceData)
        {
            try
            {
                var edgeFunctionRequest = await _qrCodeDecoder.CreateEdgeFunctionRequestAsync(
                    qrCodeContent, attendanceData);

                if (edgeFunctionRequest == null)
                {
                    return new AttendanceProcessingResult
                    {
                        Success = false,
                        Message = "Invalid or expired QR code"
                    };
                }

                return await ProcessAttendanceWithPayloadAsync(edgeFunctionRequest);
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

        /// <summary>
        /// Legacy QR validation - deprecated
        /// </summary>
        [Obsolete("Use ValidateQRPayloadAsync for new payload structure")]
        public async Task<QRValidationResult> ValidateQRCodeAsync(string qrCodeContent)
        {
            try
            {
                var payloadData = await _qrCodeDecoder.ExtractPayloadDataAsync(qrCodeContent);
                if (payloadData == null)
                {
                    return new QRValidationResult
                    {
                        IsValid = false,
                        Message = "Invalid QR code format or expired"
                    };
                }

                var validationRequest = new
                {
                    qrCodePayload = payloadData,
                    validationOnly = true
                };

                var response = await SendEdgeFunctionRequestAsync("validate-qr-code", validationRequest);
                
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
        /// <summary>
        /// Get server time from Supabase edge function
        /// </summary>
        public async Task<string> GetServerTimeAsync(string timeType = "utc")
        {
            try
            {
                var response = await SendEdgeFunctionRequestAsync($"get-server-time?type={timeType}", null, HttpMethod.Get);
                
                if (response.IsSuccessStatusCode)
                {
                    var timeResponse = await response.Content.ReadFromJsonAsync<ServerTimeResponse>(_jsonOptions);
                    return timeResponse?.Time?.ToString() ?? DateTime.UtcNow.ToString("O");
                }

                // Fallback to local time if server call fails
                return DateTime.UtcNow.ToString("O");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting server time: {ex.Message}");
                // Fallback to local time
                return DateTime.UtcNow.ToString("O");
            }
        }
        private async Task<HttpResponseMessage> SendEdgeFunctionRequestAsync(
            string functionName, 
            object payload = null, 
            HttpMethod method = null)
        {
            method ??= HttpMethod.Post;
            
            var request = new HttpRequestMessage(method, $"{_supabaseUrl}/functions/v1/{functionName}");
            
            if (!string.IsNullOrEmpty(_supabaseKey))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _supabaseKey);
            }

            if (payload != null && method == HttpMethod.Post)
            {
                request.Content = JsonContent.Create(payload, options: _jsonOptions);
            }

            return await _httpClient.SendAsync(request);
        }
    }
    
    // Response model for server time
    public class ServerTimeResponse
    {
        public object Time { get; set; }
    }
    // Response Models remain the same
    public class AttendanceProcessingResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string ErrorDetails { get; set; } = string.Empty;
        public QRCodePayloadData SessionData { get; set; }
        public AttendanceRecord ProcessedAttendance { get; set; }

        public override string ToString()
        {
            return JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        }
    }

    public class QRValidationResult
    {
        public bool IsValid { get; set; }
        public string Message { get; set; } = string.Empty;
        public QRCodePayloadData SessionData { get; set; }
        public DateTime? ExpirationTime { get; set; }
        public bool IsExpired { get; set; }
        
        public override string ToString()
        {
            return JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        }
    }

    public class AttendanceRecord
    {
        public string MatricNumber { get; set; } = string.Empty;
        public bool HasScannedAttendance { get; set; }
        public bool IsOnlineScan { get; set; }

        public override string ToString()
        {
            return JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        }
    }

    public class CatResponse
    {
        public string ImageUrl { get; set; } = string.Empty;
    }
}