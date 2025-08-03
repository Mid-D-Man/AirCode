using Microsoft.AspNetCore.Components;
using AirCode.Models.Supabase;
using AirCode.Services.SupaBase;
using AirCode.Services.Attendance;
using AirCode.Utilities.HelperScripts;
using AirCode.Domain.Entities;
using AirCode.Services.Auth;
using ReactorBlazorQRCodeScanner;
using AirCode.Models.Attendance;
using AirCode.Models.Events;

namespace AirCode.Pages.Client
{
    public partial class OfflineScanPage : ComponentBase, IAsyncDisposable
    {
        [Inject] private IOfflineCredentialsService OfllineCredentialsService { get; set; }
        [Inject] private IOfflineAttendanceClientService OfflineAttendanceService { get; set; }
        
        private QRCodeScannerJsInterop? _qrCodeScannerJsInterop;
        private Action<string>? _onQrCodeScanAction;
        private bool isScanning = true;
        private bool isProcessing = false;
        private bool showResult = false;
        private bool resultSuccess = false;
        private bool scanComplete = false;
        private bool isSyncing = false;
        private bool isOnline = false;
        private string resultMessage = string.Empty;
        private int pendingRecordsCount = 0;
        private DateTime? lastSyncTime = null;
        private string currentUserMatricNumber = string.Empty;

        protected override async Task OnInitializedAsync()
        {
            _onQrCodeScanAction = (code) => OnQrCodeScan(code);
            _qrCodeScannerJsInterop = new QRCodeScannerJsInterop(JSRuntime);
            
            try
            {
                await _qrCodeScannerJsInterop.Init(_onQrCodeScanAction);
                MID_HelperFunctions.DebugMessage("Offline QR Scanner initialized successfully", DebugClass.Info);
                
                // Get user credentials
                currentUserMatricNumber = await OfllineCredentialsService.GetMatricNumberAsync();
                
                // Initialize offline service
                await OfflineAttendanceService.InitializeAsync(currentUserMatricNumber);
                
                // Subscribe to events
                SubscribeToServiceEvents();
                
                // Load initial status
                await LoadOfflineStatus();
            }
            catch (Exception ex)
            {
                MID_HelperFunctions.DebugMessage($"Failed to initialize offline scanner: {ex.Message}", DebugClass.Exception);
                ShowResult($"Failed to initialize scanner: {ex.Message}", false);
            }
        }

        private void SubscribeToServiceEvents()
        {
            OfflineAttendanceService.OfflineAttendanceRecorded += OnOfflineAttendanceRecorded;
            OfflineAttendanceService.SyncStatusChanged += OnSyncStatusChanged;
            OfflineAttendanceService.NetworkStatusChanged += OnNetworkStatusChanged;
        }

        private async void OnOfflineAttendanceRecorded(object sender, OfflineAttendanceEventArgs e)
        {
            await LoadOfflineStatus();
            await InvokeAsync(StateHasChanged);
        }

        private async void OnSyncStatusChanged(object sender, SyncStatusEventArgs e)
        {
            isSyncing = e.IsInProgress;
            if (!e.IsInProgress)
            {
                await LoadOfflineStatus();
            }
            await InvokeAsync(StateHasChanged);
        }

        private async void OnNetworkStatusChanged(object sender, NetworkStatusEventArgs e)
        {
            isOnline = e.IsOnline;
            await InvokeAsync(StateHasChanged);
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
                    // Use the service to process the QR code
                    var result = await OfflineAttendanceService.ProcessQRCodeScanAsync(code);
                    
                    if (result.Success)
                    {
                        var mode = result.IsOfflineMode ? "📴 Offline" : "🌐 Online";
                        var syncInfo = result.RequiresSync ? "<br/><small>Will sync when online</small>" : "";
                        ShowResult($"{mode} attendance recorded successfully!{syncInfo}", true);
                        MID_HelperFunctions.DebugMessage("Attendance processed successfully", DebugClass.Info);
                    }
                    else
                    {
                        var userFriendlyMessage = GetUserFriendlyErrorMessage(result.ErrorCode, result.Message);
                        ShowResult(userFriendlyMessage, false);
                        MID_HelperFunctions.DebugMessage($"Attendance processing failed: {result.Message}", DebugClass.Warning);
                    }
                    
                    // Refresh status after processing
                    await LoadOfflineStatus();
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

        private string GetUserFriendlyErrorMessage(string errorCode, string originalMessage)
        {
            return errorCode switch
            {
                "DUPLICATE_ATTENDANCE" => "⚠️ You have already recorded attendance for this session.",
                "DUPLICATE_OFFLINE_RECORD" => "⚠️ You have already recorded attendance for this session offline.",
                "DEVICE_ALREADY_USED_OFFLINE" => "🛡️ This device has already been used for this session by another student.",
                "SESSION_EXPIRED" => "⏰ This attendance session has expired and cannot be recorded.",
                "INVALID_QR_FORMAT" => "📋 Invalid QR code format. Please scan a valid attendance QR code.",
                "STORAGE_ERROR" => "💾 Failed to save offline record. Please try again.",
                "NETWORK_ERROR" => "🌐 Network connection failed. Please check your internet connection.",
                "OFFLINE_NOT_SUPPORTED" => "❌ This session does not support offline attendance. Please connect to the internet.",
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

            // Validate user matric number
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
                var syncStatus = await OfflineAttendanceService.GetSyncStatusAsync();
                pendingRecordsCount = syncStatus.PendingRecordsCount;
                lastSyncTime = syncStatus.LastSyncAttempt;
                isOnline = syncStatus.IsOnline;
                StateHasChanged();
            }
            catch (Exception ex)
            {
                MID_HelperFunctions.DebugMessage($"Failed to load offline status: {ex.Message}", DebugClass.Exception);
            }
        }

        private async Task TrySync()
        {
            if (isSyncing) return;

            try
            {
                MID_HelperFunctions.DebugMessage("Attempting to sync offline records", DebugClass.Info);
                
                var syncResult = await OfflineAttendanceService.ManualSyncAsync();
                
                if (syncResult)
                {
                    ShowResult("✅ Sync completed successfully!", true);
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
            // Unsubscribe from events
            if (OfflineAttendanceService != null)
            {
                OfflineAttendanceService.OfflineAttendanceRecorded -= OnOfflineAttendanceRecorded;
                OfflineAttendanceService.SyncStatusChanged -= OnSyncStatusChanged;
                OfflineAttendanceService.NetworkStatusChanged -= OnNetworkStatusChanged;
            }

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