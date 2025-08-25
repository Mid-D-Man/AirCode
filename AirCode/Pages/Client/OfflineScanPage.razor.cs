using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using AirCode.Components.SharedPrefabs.Scanner;
using AirCode.Components.SharedPrefabs.Cards;
using AirCode.Models.Supabase;
using AirCode.Services.SupaBase;
using AirCode.Services.Attendance;
using AirCode.Utilities.HelperScripts;
using AirCode.Domain.Entities;
using AirCode.Services.Auth;
using AirCode.Models.Attendance;
using AirCode.Models.Events;
using AirCode.Domain.Enums;
using AirCode.Domain.ValueObjects;

namespace AirCode.Pages.Client
{
    public partial class OfflineScanPage : ComponentBase, IAsyncDisposable
    {
        [Inject] private IOfflineCredentialsService OfllineCredentialsService { get; set; }
        [Inject] private IOfflineAttendanceClientService OfflineAttendanceService { get; set; }
    
        
        private MID_Nmiq_QrCode_Scanner? qrScanner;
        private NotificationComponent? notificationComponent;
        private bool isScanning = false;
        private bool isProcessing = false;
        private bool showResult = false;
        private bool resultSuccess = false;
        private bool isSyncing = false;
        private bool isOnline = false;
        private string resultMessage = string.Empty;
        private int pendingRecordsCount = 0;
        private DateTime? lastSyncTime = null;
        private string currentUserMatricNumber = string.Empty;

        protected override async Task OnInitializedAsync()
        {
            try
            {
                // Initialize connectivity service first
                await ConnectivityService.InitializeAsync();
                ConnectivityService.ConnectivityChanged += OnConnectivityChanged;
                
                // Get initial connectivity status
                isOnline = await ConnectivityService.GetSimpleOnlineStatusAsync();
                
                // Get user credentials
                currentUserMatricNumber = await OfllineCredentialsService.GetMatricNumberAsync();
                
                // Initialize offline service
                await OfflineAttendanceService.InitializeAsync(currentUserMatricNumber);
                
                // Subscribe to service events
                SubscribeToServiceEvents();
                
                // Load initial status
                await LoadOfflineStatus();
                
                MID_HelperFunctions.DebugMessage("Offline QR Scanner initialized successfully", DebugClass.Info);
            }
            catch (Exception ex)
            {
                MID_HelperFunctions.DebugMessage($"Failed to initialize offline scanner: {ex.Message}", DebugClass.Exception);
                ShowNotification($"Failed to initialize scanner: {ex.Message}", NotificationType.Error);
            }
        }

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender)
            {
                // Scanner OFF by default - wait for user action
            }
        }

        private void SubscribeToServiceEvents()
        {
            try
            {
                OfflineAttendanceService.OfflineAttendanceRecorded += OnOfflineAttendanceRecorded;
                OfflineAttendanceService.SyncStatusChanged += OnSyncStatusChanged;
                OfflineAttendanceService.NetworkStatusChanged += OnNetworkStatusChanged;
            }
            catch (Exception ex)
            {
                MID_HelperFunctions.DebugMessage($"Failed to subscribe to service events: {ex.Message}", DebugClass.Exception);
            }
        }

        private async void OnConnectivityChanged(ConnectivityStatus status)
        {
            try
            {
                var wasOnline = isOnline;
                isOnline = status.IsOnline;
                
                // Log connectivity change
                MID_HelperFunctions.DebugMessage($"Connectivity changed: {(isOnline ? "Online" : "Offline")}", DebugClass.Info);
                
                // If we just came back online, try to sync pending records
                if (!wasOnline && isOnline && pendingRecordsCount > 0)
                {
                    // Add a small delay to ensure connection is stable
                    await Task.Delay(1000);
                    await TriggerAutoSync();
                }

                await InvokeAsync(() =>
                {
                    try
                    {
                        StateHasChanged();
                    }
                    catch (Exception ex)
                    {
                        MID_HelperFunctions.DebugMessage($"Error in StateHasChanged during connectivity change: {ex.Message}", DebugClass.Exception);
                    }
                });
            }
            catch (Exception ex)
            {
                MID_HelperFunctions.DebugMessage($"Error handling connectivity change: {ex.Message}", DebugClass.Exception);
            }
        }

        private async void OnOfflineAttendanceRecorded(object sender, OfflineAttendanceEventArgs e)
        {
            try
            {
                await LoadOfflineStatus();
                await InvokeAsync(StateHasChanged);
            }
            catch (Exception ex)
            {
                MID_HelperFunctions.DebugMessage($"Error handling offline attendance recorded: {ex.Message}", DebugClass.Exception);
            }
        }

        private async void OnSyncStatusChanged(object sender, SyncStatusEventArgs e)
        {
            try
            {
                isSyncing = e.IsInProgress;
                if (!e.IsInProgress)
                {
                    await LoadOfflineStatus();
                }
                await InvokeAsync(StateHasChanged);
            }
            catch (Exception ex)
            {
                MID_HelperFunctions.DebugMessage($"Error handling sync status change: {ex.Message}", DebugClass.Exception);
            }
        }

        private async void OnNetworkStatusChanged(object sender, NetworkStatusEventArgs e)
        {
            try
            {
                isOnline = e.IsOnline;
                await InvokeAsync(StateHasChanged);
            }
            catch (Exception ex)
            {
                MID_HelperFunctions.DebugMessage($"Error handling network status change: {ex.Message}", DebugClass.Exception);
            }
        }

        private async Task OnBeforeInternalNavigation(LocationChangingContext context)
        {
            await StopScanning();
        }

        public async Task OnQrCodeScanned(string qrCodeData)
        {
            if (!string.IsNullOrEmpty(qrCodeData) && CanProcessAttendance())
            {
                MID_HelperFunctions.DebugMessage($"Offline QR Code detected: {qrCodeData}", DebugClass.Info);
            
                isProcessing = true;
                await InvokeAsync(StateHasChanged);
            
                try
                {
                    // Use the service to process the QR code
                    var result = await OfflineAttendanceService.ProcessQRCodeScanAsync(qrCodeData);
                    
                    if (result.Success)
                    {
                        var mode = result.IsOfflineMode ? "📴 Offline" : "🌐 Online";
                        var syncInfo = result.RequiresSync ? "<br/><small>Will sync when online</small>" : "";
                        ShowResult($"{mode} attendance recorded successfully!{syncInfo}", true);
                        ShowNotification("Attendance recorded successfully!", NotificationType.Success);
                        MID_HelperFunctions.DebugMessage("Attendance processed successfully", DebugClass.Info);
                    }
                    else
                    {
                        var userFriendlyMessage = GetUserFriendlyErrorMessage(result.ErrorCode, result.Message);
                        ShowResult(userFriendlyMessage, false);
                        ShowNotification(userFriendlyMessage, NotificationType.Error);
                        MID_HelperFunctions.DebugMessage($"Attendance processing failed: {result.Message}", DebugClass.Warning);
                    }
                    
                    // Refresh status after processing
                    await LoadOfflineStatus();
                }
                catch (Exception ex)
                {
                    MID_HelperFunctions.DebugMessage($"Error processing offline QR code: {ex.Message}", DebugClass.Exception);
                    var errorMessage = $"Error processing QR code: {ex.Message}";
                    ShowResult(errorMessage, false);
                    ShowNotification(errorMessage, NotificationType.Error);
                }
                finally
                {
                    isProcessing = false;
                    await InvokeAsync(StateHasChanged);
                }
            }
        }

        private void ShowNotification(string message, NotificationType type)
        {
            try
            {
                notificationComponent?.ShowNotification(message, type);
            }
            catch (Exception ex)
            {
                MID_HelperFunctions.DebugMessage($"Error showing notification: {ex.Message}", DebugClass.Exception);
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
                ShowNotification("User identification not available for offline mode.", NotificationType.Error);
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
                
                // Update connectivity status from service
                isOnline = await ConnectivityService.GetSimpleOnlineStatusAsync();
                
                await InvokeAsync(StateHasChanged);
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
                    ShowNotification("Sync completed successfully!", NotificationType.Success);
                }
                else
                {
                    ShowResult("❌ Sync failed. Please check your internet connection.", false);
                    ShowNotification("Sync failed. Please check your internet connection.", NotificationType.Error);
                }
            }
            catch (Exception ex)
            {
                MID_HelperFunctions.DebugMessage($"Sync error: {ex.Message}", DebugClass.Exception);
                var errorMessage = "❌ Sync failed due to an error.";
                ShowResult(errorMessage, false);
                ShowNotification(errorMessage, NotificationType.Error);
            }
        }

        private async Task TriggerAutoSync()
        {
            if (isSyncing || pendingRecordsCount <= 0) return;

            try
            {
                MID_HelperFunctions.DebugMessage("Auto-syncing offline records after coming online", DebugClass.Info);
                
                var syncResult = await OfflineAttendanceService.ManualSyncAsync();
                
                if (syncResult)
                {
                    ShowNotification($"Auto-synced {pendingRecordsCount} pending records!", NotificationType.Success);
                }
            }
            catch (Exception ex)
            {
                MID_HelperFunctions.DebugMessage($"Auto-sync error: {ex.Message}", DebugClass.Exception);
            }
        }

        private void ShowResult(string message, bool success)
        {
            try
            {
                resultMessage = message;
                resultSuccess = success;
                showResult = true;
                StateHasChanged();
            }
            catch (Exception ex)
            {
                MID_HelperFunctions.DebugMessage($"Error showing result: {ex.Message}", DebugClass.Exception);
            }
        }

        private async Task ContinueScanning()
        {
            try
            {
                showResult = false;
                await StartScanning();
                StateHasChanged();
            }
            catch (Exception ex)
            {
                MID_HelperFunctions.DebugMessage($"Error continuing scanning: {ex.Message}", DebugClass.Exception);
            }
        }

        private async Task StartScanning()
        {
            try
            {
                if (qrScanner != null)
                {
                    isScanning = true;
                    await qrScanner.StartScanningAsync();
                    StateHasChanged();
                    MID_HelperFunctions.DebugMessage("Offline scanner started", DebugClass.Log);
                }
            }
            catch (Exception ex)
            {
                MID_HelperFunctions.DebugMessage($"Failed to start offline scanner: {ex.Message}", DebugClass.Exception);
                ShowNotification($"Failed to start scanner: {ex.Message}", NotificationType.Error);
            }
        }

        private async Task StopScanning()
        {
            try
            {
                if (qrScanner != null)
                {
                    isScanning = false;
                    await qrScanner.StopScanningAsync();
                    StateHasChanged();
                    MID_HelperFunctions.DebugMessage("Offline scanner stopped", DebugClass.Log);
                }
            }
            catch (Exception ex)
            {
                MID_HelperFunctions.DebugMessage($"Failed to stop offline scanner: {ex.Message}", DebugClass.Exception);
            }
        }

        private async Task SwitchCamera()
        {
            try
            {
                if (qrScanner != null)
                {
                    await qrScanner.SwitchCameraAsync();
                }
            }
            catch (Exception ex)
            {
                MID_HelperFunctions.DebugMessage($"Failed to switch camera: {ex.Message}", DebugClass.Exception);
                ShowNotification("Failed to switch camera", NotificationType.Warning);
            }
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                // Unsubscribe from connectivity service
                if (ConnectivityService != null)
                {
                    ConnectivityService.ConnectivityChanged -= OnConnectivityChanged;
                    await ConnectivityService.DisposeAsync();
                }

                // Unsubscribe from offline service events
                if (OfflineAttendanceService != null)
                {
                    OfflineAttendanceService.OfflineAttendanceRecorded -= OnOfflineAttendanceRecorded;
                    OfflineAttendanceService.SyncStatusChanged -= OnSyncStatusChanged;
                    OfflineAttendanceService.NetworkStatusChanged -= OnNetworkStatusChanged;
                }

                // Stop scanner
                if (qrScanner != null && isScanning)
                {
                    await qrScanner.StopScanningAsync();
                    MID_HelperFunctions.DebugMessage("Offline scanner disposed", DebugClass.Log);
                }
            }
            catch (Exception ex)
            {
                // Ignore disposal errors but log them
                MID_HelperFunctions.DebugMessage($"Error during disposal: {ex.Message}", DebugClass.Warning);
            }
        }
    }
}
