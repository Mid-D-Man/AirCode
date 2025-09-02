using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using AirCode.Components.SharedPrefabs.Scanner;
using AirCode.Components.SharedPrefabs.Cards;
using AirCode.Models.Supabase;
using AirCode.Services.Auth;
using AirCode.Services.Courses;
using AirCode.Services.SupaBase;
using AirCode.Utilities.HelperScripts;
using AirCode.Domain.Enums;
using AirCode.Services.Attendance;
using AirCode.Models.Attendance;

namespace AirCode.Pages.Client;

public partial class ScanPage : ComponentBase
{
    [Inject] public ISupabaseEdgeFunctionService EdgeService { get; set; } = null!;
    [Inject] public QRCodeDecoder QRDecoder { get; set; } = null!;
    [Inject] public IAuthService AuthService { get; set; } = null!;
    [Inject] public ICourseService CourseService { get; set; } = null!;
    [Inject] public IOfflineAttendanceClientService OfflineService { get; set; } = null!;

    private MID_Nmiq_QrCode_Scanner? qrScanner;
    private NotificationComponent? notificationComponent;
    private bool isScanning = false;
    private bool isProcessing = false;
    private string currentUserMatricNumber = "SukaBlak";
    private string deviceGUID = "N";

    // Improved sync indicator state management
    private bool showSyncIndicator = false;
    private string syncMessage = "";
    private string syncStatus = ""; // "syncing", "success", "error"
    private CancellationTokenSource? syncIndicatorCancellation;
    private readonly object syncIndicatorLock = new object();

    protected override async Task OnInitializedAsync()
    {
        try
        {
            currentUserMatricNumber = await AuthService.GetMatricNumberAsync();
            deviceGUID = await AuthService.GetDeviceIdAsync();

            // Initialize offline service
            await OfflineService.InitializeAsync(currentUserMatricNumber);

            // Check for offline records and sync if any exist
            await CheckAndSyncOfflineRecords();

            MID_HelperFunctions.DebugMessage("QR Scanner initialized successfully", DebugClass.Info);
        }
        catch (Exception ex)
        {
            MID_HelperFunctions.DebugMessage($"Failed to initialize scanner: {ex.Message}", DebugClass.Exception);
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            // Scanner OFF by default - wait for user action
        }
    }

    private async Task OnBeforeInternalNavigation(LocationChangingContext context)
    {
        await StopScanning();
        CancelSyncIndicator();
    }

    private async Task CheckAndSyncOfflineRecords()
    {
        try
        {
            var syncStatusInfo = await OfflineService.GetSyncStatusAsync();

            if (syncStatusInfo.HasPendingRecords && syncStatusInfo.IsOnline)
            {
                ShowSyncIndicator("Syncing offline records...", "syncing");

                var syncResult = await OfflineService.ManualSyncAsync();

                if (syncResult)
                {
                    ShowSyncIndicator("✅ Offline records synced successfully", "success");
                    MID_HelperFunctions.DebugMessage("Successfully synced offline records", DebugClass.Info);
                }
                else
                {
                    ShowSyncIndicator("⚠️ Some records failed to sync", "error");
                    MID_HelperFunctions.DebugMessage("Failed to sync some offline records", DebugClass.Warning);
                }

                // Hide indicator after 3 seconds
                _ = HideSyncIndicatorAfterDelay(3000);
            }
            else if (syncStatusInfo.HasPendingRecords && !syncStatusInfo.IsOnline)
            {
                ShowSyncIndicator($"📴 {syncStatusInfo.PendingRecordsCount} records pending sync", "error");
                _ = HideSyncIndicatorAfterDelay(5000);
            }
        }
        catch (Exception ex)
        {
            MID_HelperFunctions.DebugMessage($"Error checking offline records: {ex.Message}", DebugClass.Exception);
            ShowSyncIndicator("⚠️ Sync check failed", "error");
            _ = HideSyncIndicatorAfterDelay(3000);
        }
    }

    public async Task OnQrCodeScanned(string qrCodeData)
    {
        if (!string.IsNullOrEmpty(qrCodeData) && CanProcessAttendance())
        {
            MID_HelperFunctions.DebugMessage($"QR Code detected: {qrCodeData}", DebugClass.Info);

            isProcessing = true;
            await InvokeAsync(StateHasChanged);

            try
            {
                var decodedSessionData = await QRDecoder.DecodeSessionDataAsync(qrCodeData);

                if (decodedSessionData != null)
                {
                    MID_HelperFunctions.DebugMessage(
                        $"Valid AirCode QR detected for course: {decodedSessionData.CourseCode}", DebugClass.Info);
                    
                    var myCourses = await CourseService.GetStudentCoursesByMatricAsync(currentUserMatricNumber);
                    var courseRefs = myCourses.GetEnrolledCourses();

                    bool isTakingCourse =
                        courseRefs.FirstOrDefault(course => course.CourseCode == decodedSessionData.CourseCode) != null;
                    
                    if (isTakingCourse)
                    {
                        await ProcessAttendanceWithOfflineSupport(qrCodeData);
                    }
                    else
                    {
                        ShowNotification("You are not enrolled in this course", NotificationType.Warning);
                        MID_HelperFunctions.DebugMessage("You are not taking this course", DebugClass.Warning);
                    }
                }
                else
                {
                    ShowNotification("Invalid QR code format", NotificationType.Error);
                    MID_HelperFunctions.DebugMessage("QR code is not a valid AirCode attendance QR",
                        DebugClass.Warning);
                }
            }
            catch (Exception ex)
            {
                ShowNotification("Error processing QR code", NotificationType.Error);
                MID_HelperFunctions.DebugMessage($"Error processing QR code: {ex.Message}", DebugClass.Exception);
            }
            finally
            {
                isProcessing = false;
                await InvokeAsync(StateHasChanged);
            }
        }
    }

    private async Task ProcessAttendanceWithOfflineSupport(string qrCode)
    {
        try
        {
            MID_HelperFunctions.DebugMessage($"Processing attendance with offline support for QR: {qrCode}",
                DebugClass.Info);

            var result = await OfflineService.ProcessQRCodeScanAsync(qrCode);

            if (result.Success)
            {
                if (result.IsOfflineMode)
                {
                    ShowNotification("📴 Attendance recorded offline - will sync when online", NotificationType.Success);
                    ShowSyncIndicator("📴 Offline record saved", "error");
                    _ = HideSyncIndicatorAfterDelay(3000);
                    MID_HelperFunctions.DebugMessage("Attendance recorded offline successfully", DebugClass.Info);
                }
                else
                {
                    ShowNotification("✅ Attendance recorded successfully!", NotificationType.Success);
                    ShowSyncIndicator("✅ Synced online", "success");
                    _ = HideSyncIndicatorAfterDelay(2000);
                    MID_HelperFunctions.DebugMessage("Attendance recorded online successfully", DebugClass.Info);
                }

                // Auto stop scanning on successful attendance
                await AutoStopScanningOnSuccess();
            }
            else
            {
                var userFriendlyMessage = GetUserFriendlyErrorMessage(result.ErrorCode, result.Message);
                ShowNotification(userFriendlyMessage, NotificationType.Error);
                MID_HelperFunctions.DebugMessage($"Attendance processing failed: {result.Message}", DebugClass.Warning);
            }
        }
        catch (Exception ex)
        {
            ShowNotification("Failed to process attendance", NotificationType.Error);
            MID_HelperFunctions.DebugMessage($"Exception in ProcessAttendanceWithOfflineSupport: {ex}",
                DebugClass.Exception);
        }
    }

    private async Task AutoStopScanningOnSuccess()
    {
        try
        {
            if (isScanning)
            {
                MID_HelperFunctions.DebugMessage("Auto-stopping scanner after successful scan", DebugClass.Info);
                
                // Add a small delay for better UX
                await Task.Delay(1000);
                
                await StopScanning();
                
                // Reset UI state
                isProcessing = false;
                await InvokeAsync(StateHasChanged);
                
                MID_HelperFunctions.DebugMessage("Scanner stopped and UI reset", DebugClass.Info);
            }
        }
        catch (Exception ex)
        {
            MID_HelperFunctions.DebugMessage($"Error in auto-stop scanning: {ex.Message}", DebugClass.Exception);
        }
    }

    private void ShowNotification(string message, NotificationType type)
    {
        notificationComponent?.ShowNotification(message, type);
    }

    private void ShowSyncIndicator(string message, string status)
    {
        lock (syncIndicatorLock)
        {
            // Cancel any existing timer
            CancelSyncIndicator();

            syncMessage = message;
            syncStatus = status;
            showSyncIndicator = true;

            InvokeAsync(StateHasChanged);
            MID_HelperFunctions.DebugMessage($"Sync indicator shown: {message} ({status})", DebugClass.Info);
        }
    }

    private async Task HideSyncIndicatorAfterDelay(int delayMs)
    {
        lock (syncIndicatorLock)
        {
            CancelSyncIndicator();
            syncIndicatorCancellation = new CancellationTokenSource();
        }

        try
        {
            await Task.Delay(delayMs, syncIndicatorCancellation.Token);
            
            lock (syncIndicatorLock)
            {
                if (!syncIndicatorCancellation.Token.IsCancellationRequested)
                {
                    showSyncIndicator = false;
                    syncMessage = "";
                    syncStatus = "";
                    
                    InvokeAsync(StateHasChanged);
                    MID_HelperFunctions.DebugMessage("Sync indicator hidden after delay", DebugClass.Info);
                }
            }
        }
        catch (TaskCanceledException)
        {
            // Expected when cancellation occurs
            MID_HelperFunctions.DebugMessage("Sync indicator hide cancelled", DebugClass.Info);
        }
        catch (Exception ex)
        {
            MID_HelperFunctions.DebugMessage($"Error hiding sync indicator: {ex.Message}", DebugClass.Exception);
        }
        finally
        {
            lock (syncIndicatorLock)
            {
                syncIndicatorCancellation?.Dispose();
                syncIndicatorCancellation = null;
            }
        }
    }

    private void CancelSyncIndicator()
    {
        lock (syncIndicatorLock)
        {
            try
            {
                syncIndicatorCancellation?.Cancel();
                syncIndicatorCancellation?.Dispose();
                syncIndicatorCancellation = null;
            }
            catch (Exception ex)
            {
                MID_HelperFunctions.DebugMessage($"Error cancelling sync indicator: {ex.Message}", DebugClass.Exception);
            }
        }
    }

    private string GetUserFriendlyErrorMessage(string errorCode, string originalMessage)
    {
        return errorCode switch
        {
            "DUPLICATE_ATTENDANCE" => "You have already marked attendance for this session",
            "SESSION_NOT_FOUND" => "This attendance session does not exist",
            "TEMPORAL_KEY_EXPIRED" => "QR code has expired. Please scan the latest code",
            "TEMPORAL_KEY_MISSING" => "Please scan a fresh QR code",
            "DEVICE_SECURITY_VIOLATION" => "This device has already been used by another student",
            "DEVICE_GUID_MISSING" => "Device identification is required",
            "MATRIC_NUMBER_MISSING" => "Student identification number is required",
            "SESSION_QUERY_ERROR" => "Database connection failed. Check your connection",
            "ATTENDANCE_UPDATE_ERROR" => "Failed to save attendance. Please try again",
            "NETWORK_ERROR" => "Network connection failed. Check your internet",
            "INVALID_JSON" => "Invalid request format. Please try scanning again",
            "MISSING_PARAMETERS" => "Required information missing. Please try again",
            _ => originalMessage
        };
    }

    private bool CanProcessAttendance()
    {
        if (isProcessing) 
        {
            MID_HelperFunctions.DebugMessage("Cannot process attendance - already processing", DebugClass.Warning);
            return false;
        }

        if (string.IsNullOrWhiteSpace(currentUserMatricNumber))
        {
            ShowNotification("User not logged in", NotificationType.Error);
            MID_HelperFunctions.DebugMessage("Cannot process attendance - user not logged in", DebugClass.Warning);
            return false;
        }

        return true;
    }

    private async Task StartScanning()
    {
        try
        {
            if (qrScanner != null && !isScanning)
            {
                isScanning = true;
                await qrScanner.StartScanningAsync();
                
                MID_HelperFunctions.DebugMessage("Scanner started successfully", DebugClass.Info);
                await InvokeAsync(StateHasChanged);
            }
        }
        catch (Exception ex)
        {
            isScanning = false;
            MID_HelperFunctions.DebugMessage($"Error starting scanner: {ex.Message}", DebugClass.Exception);
            ShowNotification("Failed to start camera", NotificationType.Error);
            await InvokeAsync(StateHasChanged);
        }
    }

    private async Task StopScanning()
    {
        try
        {
            if (qrScanner != null && isScanning)
            {
                isScanning = false;
                isProcessing = false; // Reset processing state
                
                await qrScanner.StopScanningAsync();
                
                MID_HelperFunctions.DebugMessage("Scanner stopped successfully", DebugClass.Info);
                await InvokeAsync(StateHasChanged);
            }
        }
        catch (Exception ex)
        {
            MID_HelperFunctions.DebugMessage($"Error stopping scanner: {ex.Message}", DebugClass.Exception);
            // Still update UI state even if stop fails
            isScanning = false;
            isProcessing = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    private async Task SwitchCamera()
    {
        try
        {
            if (qrScanner != null && isScanning)
            {
                await qrScanner.SwitchCameraAsync();
                MID_HelperFunctions.DebugMessage("Camera switched", DebugClass.Info);
            }
        }
        catch (Exception ex)
        {
            MID_HelperFunctions.DebugMessage($"Error switching camera: {ex.Message}", DebugClass.Exception);
            ShowNotification("Failed to switch camera", NotificationType.Warning);
        }
    }

    public void Dispose()
    {
        CancelSyncIndicator();
    }
}