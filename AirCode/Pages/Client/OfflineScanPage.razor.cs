using Microsoft.AspNetCore.Components;
using AirCode.Models.Supabase;
using AirCode.Services.SupaBase;
using AirCode.Services.Attendance;
using AirCode.Utilities.HelperScripts;
using AirCode.Domain.Entities;
using ReactorBlazorQRCodeScanner;

namespace AirCode.Pages.Client
{
    public partial class OfflineScanPage : ComponentBase, IAsyncDisposable
    {
        private QRCodeScannerJsInterop? _qrCodeScannerJsInterop;
        private Action<string>? _onQrCodeScanAction;
        private bool isScanning = true;
        private bool isProcessing = false;
        private bool showResult = false;
        private bool resultSuccess = false;
        private bool scanComplete = false;
        private bool isSyncing = false;
        private string resultMessage = string.Empty;
        private int pendingRecordsCount = 0;
        private DateTime? lastSyncTime = null;

        // TODO: Get actual user data from authentication service
        private string currentUserMatricNumber = "OFFLINE_USER"; // Temporary for offline mode
        private string currentDeviceGuid = GenerateDeviceGuid();

        protected override async Task OnInitializedAsync()
        {
            _onQrCodeScanAction = (code) => OnQrCodeScan(code);
            _qrCodeScannerJsInterop = new QRCodeScannerJsInterop(JSRuntime);
            
            try
            {
                await _qrCodeScannerJsInterop.Init(_onQrCodeScanAction);
                MID_HelperFunctions.DebugMessage("Offline QR Scanner initialized successfully", DebugClass.Info);
                
                // Load offline status
                await LoadOfflineStatus();
            }
            catch (Exception ex)
            {
                MID_HelperFunctions.DebugMessage($"Failed to initialize offline scanner: {ex.Message}", DebugClass.Exception);
                ShowResult($"Failed to initialize scanner: {ex.Message}", false);
            }
        }

        private async void OnQrCodeScan(string code)
        {
            if (!string.IsNullOrEmpty(code) && CanProcessAttendance())
            {
                MID_HelperFunctions.DebugMessage($"Offline QR Code detected: {code}", DebugClass.Info);
            
                // Stop scanning and show processing state
                await PauseScanning();
                isProcessing = true;
                scanComplete = true;
                StateHasChanged();
            
                try
                {
                    // Decode the session data to check offline capability
                    var decodedSessionData = await QRDecoder.DecodeSessionDataAsync(code);
                
                    if (decodedSessionData != null)
                    {
                        MID_HelperFunctions.DebugMessage($"Valid AirCode QR detected for offline processing - Course: {decodedSessionData.CourseCode}", DebugClass.Info);
                    
                        // Check if offline sync is allowed
                        if (!decodedSessionData.AllowOfflineConnectionAndSync)
                        {
                            ShowResult("❌ This session does not support offline attendance. Please connect to the internet.", false);
                            return;
                        }
                    
                        // Process offline attendance
                        await ProcessOfflineAttendance(code, decodedSessionData);
                    }
                    else
                    {
                        MID_HelperFunctions.DebugMessage("QR code is not a valid AirCode attendance QR", DebugClass.Warning);
                        ShowResult("Invalid attendance QR code format", false);
                    }
                }
                catch (Exception ex)
                {
                    MID_HelperFunctions.DebugMessage($"Error processing offline QR code: {ex.Message}", DebugClass.Exception);
                    ShowResult($"Error processing QR code: {ex.Message}", false);
                }
            
                isProcessing = false;
                await InvokeAsync(StateHasChanged);
            }
        }

        private async Task ProcessOfflineAttendance(string qrCode, Models.QRCode.DecodedSessionData decodedData)
        {
            try
            {
                MID_HelperFunctions.DebugMessage($"Processing offline attendance for session: {decodedData.SessionId}", DebugClass.Info);

                // Create offline attendance record
                var offlineRecord = new OfflineAttendanceRecord
                {
                    Id = Guid.NewGuid().ToString(),
                    SessionId = decodedData.SessionId,
                    CourseCode = decodedData.CourseCode,
                    MatricNumber = currentUserMatricNumber,
                    DeviceGuid = currentDeviceGuid,
                    ScanTime = DateTime.UtcNow,
                    EncryptedQrPayload = qrCode, // Store the original encrypted QR payload
                    TemporalKey = decodedData.TemporalKey,
                    UseTemporalKeyRefresh = decodedData.UseTemporalKeyRefresh,
                    SecurityFeatures = (int)decodedData.SecurityFeatures,
                    RecordedAt = DateTime.UtcNow,
                    SyncStatus = SyncStatus.Pending,
                    SyncAttempts = 0
                };
                MID_HelperFunctions.DebugMessage($"Offline attendance record: {MID_HelperFunctions.ToJson(offlineRecord)}", DebugClass.Log);

                // Process the offline record
                var result = await OfflineSyncService.ProcessOfflineAttendanceAsync(offlineRecord);

                if (result.Success)
                {
                    ShowResult($"📴 Offline attendance recorded successfully!<br/><small>Course: {decodedData.CourseCode}<br/>Will sync when online</small>", true);
                    MID_HelperFunctions.DebugMessage("Offline attendance processed successfully", DebugClass.Info);
                    
                    // Update offline status
                    await LoadOfflineStatus();
                }
                else
                {
                    var userFriendlyMessage = GetOfflineErrorMessage(result.ErrorMessage);
                    ShowResult(userFriendlyMessage, false);
                    MID_HelperFunctions.DebugMessage($"Offline attendance processing failed: {result.ErrorMessage}", DebugClass.Warning);
                }
            }
            catch (Exception ex)
            {
                MID_HelperFunctions.DebugMessage($"Exception in ProcessOfflineAttendance: {ex}", DebugClass.Exception);
                ShowResult($"An unexpected error occurred during offline processing.", false);
            }
        }

        private string GetOfflineErrorMessage(string originalMessage)
        {
            return originalMessage switch
            {
                "DUPLICATE_OFFLINE_RECORD" => "⚠️ You have already recorded attendance for this session offline.",
                "DEVICE_ALREADY_USED_OFFLINE" => "🛡️ This device has already been used for this session by another student.",
                "SESSION_EXPIRED" => "⏰ This attendance session has expired and cannot be recorded.",
                "INVALID_QR_FORMAT" => "📋 Invalid QR code format. Please scan a valid attendance QR code.",
                "STORAGE_ERROR" => "💾 Failed to save offline record. Please try again.",
                _ => $"❌ {originalMessage}"
            };
        }

        private bool CanProcessAttendance()
        {
            // Prevent duplicate processing
            if (isProcessing || showResult)
            {
                MID_HelperFunctions.DebugMessage("Offline attendance processing already in progress or completed", DebugClass.Warning);
                return false;
            }

            // Validate user matric number (in real app, get from offline credentials)
            if (string.IsNullOrWhiteSpace(currentUserMatricNumber))
            {
                ShowResult("User identification not available for offline mode.", false);
                return false;
            }

            return true;
        }

        private async Task LoadOfflineStatus()
        {
            try
            {
                // In a real implementation, this would query the offline storage service
                // For now, simulate loading pending records count
                pendingRecordsCount = await GetPendingRecordsCount();
                lastSyncTime = await GetLastSyncTime();
                StateHasChanged();
            }
            catch (Exception ex)
            {
                MID_HelperFunctions.DebugMessage($"Failed to load offline status: {ex.Message}", DebugClass.Exception);
            }
        }

        private async Task<int> GetPendingRecordsCount()
        {
            // TODO: Implement actual count from offline storage
            await Task.Delay(100); // Simulate async operation
            return new Random().Next(0, 5); // Temporary simulation
        }

        private async Task<DateTime?> GetLastSyncTime()
        {
            // TODO: Implement actual last sync time from offline storage
            await Task.Delay(100); // Simulate async operation
            return DateTime.Now.AddMinutes(-new Random().Next(10, 120)); // Temporary simulation
        }

        private async Task TrySync()
        {
            if (isSyncing) return;

            isSyncing = true;
            StateHasChanged();

            try
            {
                MID_HelperFunctions.DebugMessage("Attempting to sync offline records", DebugClass.Info);
                
                var syncResult = await OfflineSyncService.SyncPendingRecordsAsync();
                
                if (syncResult)
                {
                    ShowResult("✅ Sync completed successfully!", true);
                    await LoadOfflineStatus(); // Refresh status
                }
                else
                {
                    ShowResult("❌ Sync failed. Please check your internet connection.", false);
                }
            }
            catch (Exception ex)
            {
                MID_HelperFunctions.DebugMessage($"Sync error: {ex.Message}", DebugClass.Exception);
                ShowResult("❌ Sync failed due to an error.", false);
            }
            finally
            {
                isSyncing = false;
                StateHasChanged();
            }
        }

        private static string GenerateDeviceGuid()
        {
            // In a real implementation, this should be stored and retrieved from device storage
            // For now, generate a consistent GUID based on some device characteristics
            return Guid.NewGuid().ToString("N")[..16].ToUpper();
        }

        private void ShowResult(string message, bool success)
        {
            resultMessage = message;
            resultSuccess = success;
            showResult = true;
            StateHasChanged();
        }

        private async Task ContinueScanning()
        {
            showResult = false;
            scanComplete = false;
            await ResumeScanning();
            StateHasChanged();
        }

        private async Task PauseScanning()
        {
            if (_qrCodeScannerJsInterop != null && isScanning)
            {
                try
                {
                    await _qrCodeScannerJsInterop.StopRecording();
                    isScanning = false;
                    MID_HelperFunctions.DebugMessage("Offline scanner paused", DebugClass.Log);
                }
                catch (Exception ex)
                {
                    MID_HelperFunctions.DebugMessage($"Failed to pause offline scanner: {ex.Message}", DebugClass.Exception);
                    ShowResult($"Failed to pause scanner: {ex.Message}", false);
                }
            }
        }

        private async Task ResumeScanning()
        {
            if (_qrCodeScannerJsInterop != null && !isScanning)
            {
                try
                {
                    await _qrCodeScannerJsInterop.Init(_onQrCodeScanAction);
                    isScanning = true;
                    MID_HelperFunctions.DebugMessage("Offline scanner resumed", DebugClass.Log);
                }
                catch (Exception ex)
                {
                    MID_HelperFunctions.DebugMessage($"Failed to resume offline scanner: {ex.Message}", DebugClass.Exception);
                    ShowResult($"Failed to resume scanner: {ex.Message}", false);
                }
            }
        }

        private async Task StartScanning()
        {
            await ResumeScanning();
        }

        private async Task StopScanning()
        {
            await PauseScanning();
        }

        public async ValueTask DisposeAsync()
        {
            if (_qrCodeScannerJsInterop != null && isScanning)
            {
                try
                {
                    await _qrCodeScannerJsInterop.StopRecording();
                    MID_HelperFunctions.DebugMessage("Offline scanner disposed", DebugClass.Log);
                }
                catch
                {
                    // Ignore disposal errors
                }
            }
        }
    }
}
