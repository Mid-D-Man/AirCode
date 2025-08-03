using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AirCode.Models.EdgeFunction;
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
        
        // Edge function names
        private const string OnlineAttendanceFunctionName = "process-attendance-data";
        private const string OfflineAttendanceFunctionName = "process-offline-attendance";

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
        /// Process attendance with unencrypted payload structure (online mode)
        /// </summary>
        public async Task<AttendanceProcessingResult> ProcessAttendanceWithPayloadAsync(EdgeFunctionRequest request)
        {
            return await ProcessAttendanceInternalAsync(request, OnlineAttendanceFunctionName, "online");
        }

        /// <summary>
        /// Process offline attendance record through offline edge function
        /// </summary>
        public async Task<AttendanceProcessingResult> ProcessOfflineAttendanceAsync(EdgeFunctionRequest request)
        {
            return await ProcessAttendanceInternalAsync(request, OfflineAttendanceFunctionName, "offline");
        }

        /// <summary>
        /// Internal method to handle both online and offline attendance processing
        /// </summary>
        private async Task<AttendanceProcessingResult> ProcessAttendanceInternalAsync(
            EdgeFunctionRequest request, 
            string functionName, 
            string mode)
        {
            try
            {
                Console.WriteLine($"Sending {mode} edge function request: {JsonSerializer.Serialize(request, _jsonOptions)}");

                var response = await SendEdgeFunctionRequestAsync(functionName, request);
                
                var responseContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"{mode} Response Status: {response.StatusCode}");
                Console.WriteLine($"{mode} Response Content: {responseContent}");
                
                // Parse the response regardless of status code
                EdgeFunctionResponse edgeResponse;
                try
                {
                    edgeResponse = JsonSerializer.Deserialize<EdgeFunctionResponse>(responseContent, _jsonOptions);
                }
                catch (JsonException)
                {
                    return new AttendanceProcessingResult
                    {
                        Success = false,
                        Message = "Invalid response format from server",
                        ErrorDetails = responseContent
                    };
                }

                if (response.IsSuccessStatusCode && edgeResponse?.Success == true)
                {
                    // Success case
                    return new AttendanceProcessingResult
                    {
                        Success = true,
                        Message = edgeResponse.Message,
                        SessionData = edgeResponse.SessionData != null ? new QRCodePayloadData
                        {
                            SessionId = edgeResponse.SessionData.SessionId,
                            CourseCode = edgeResponse.SessionData.CourseCode,
                            StartTime = edgeResponse.SessionData.StartTime,
                            EndTime = edgeResponse.SessionData.EndTime
                        } : null,
                        ProcessedAttendance = edgeResponse.ProcessedAttendance != null ? new AttendanceRecord
                        {
                            MatricNumber = edgeResponse.ProcessedAttendance.MatricNumber,
                            ScanTime = edgeResponse.ProcessedAttendance.ScannedAt,
                            IsOnlineScan = !edgeResponse.ProcessedAttendance.IsOnlineScan 
                        } : null
                    };
                }
                else
                {
                    // Error case - return the specific error message from Edge function
                    return new AttendanceProcessingResult
                    {
                        Success = false,
                        Message = edgeResponse?.Message ?? "Unknown error occurred",
                        ErrorCode = edgeResponse?.ErrorCode ?? "UNKNOWN_ERROR",
                        ErrorDetails = $"HTTP {response.StatusCode}: {responseContent}"
                    };
                }
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"HTTP Request Exception in {mode} mode: {ex}");
                return new AttendanceProcessingResult
                {
                    Success = false,
                    Message = "Network connection failed. Please check your internet connection.",
                    ErrorCode = "NETWORK_ERROR",
                    ErrorDetails = ex.ToString()
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"General Exception in {mode} mode: {ex}");
                return new AttendanceProcessingResult
                {
                    Success = false,
                    Message = $"An unexpected error occurred: {ex.Message}",
                    ErrorCode = "PROCESSING_ERROR",
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
        /// Check if offline session exists for given session ID
        /// </summary>
        public async Task<bool> CheckOfflineSessionExistsAsync(string sessionId)
        {
            try
            {
                var checkRequest = new
                {
                    sessionId = sessionId,
                    checkOnly = true
                };

                var response = await SendEdgeFunctionRequestAsync("check-offline-session", checkRequest);
                
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>(_jsonOptions);
                    return result?.ContainsKey("exists") == true && (bool)result["exists"];
                }

                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error checking offline session: {ex.Message}");
                return false;
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

}