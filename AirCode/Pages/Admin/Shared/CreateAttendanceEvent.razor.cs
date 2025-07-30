
using System.Text.Json;
using AirCode.Domain.Enums;
using AirCode.Models.Supabase;
using AirCode.Services.SupaBase;
using AirCode.Utilities.DataStructures;
using AirCode.Utilities.HelperScripts;
using Microsoft.AspNetCore.Components;
using AttendanceRecord = AirCode.Models.Supabase.AttendanceRecord;
using AirCode.Models.QRCode;
namespace AirCode.Pages.Admin.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using AirCode.Components.SharedPrefabs.QrCode;
using AirCode.Services;
using AirCode.Services.Attendance;
using AirCode.Services.Courses;
using AirCode.Services.Firebase;
using AirCode.Domain.Entities;
using AirCode.Components.SharedPrefabs.Others;
using AirCode.Services.Auth;
using AirCode.Components.Admin.Shared;
    public partial class CreateAttendanceEvent : ComponentBase, IDisposable
    {
        /// <summary>
        /// so apparently we need to add better comments for project, so yeah lest run that hell even use
        /// ai to add comments instead
        /// </summary>
        
        [Inject] private IJSRuntime JS { get; set; }
        [Inject] private QRCodeDecoder QRCodeDecoder { get; set; }
        [Inject] private SessionStateService SessionStateService { get; set; }
        [Inject] private ICourseService CourseService { get; set; }
        [Inject] private IFirestoreService FirestoreService { get; set; }
        [Inject] private ISupabaseEdgeFunctionService EdgeService { get; set; }
        [Inject] private IAttendanceSessionService AttendanceSessionService { get; set; }
        [Inject] private IAuthService AuthService {get;set;} 
        [Inject] private IFirestoreAttendanceService FirebaseAttendanceService { get; set; }
        
        private SessionData sessionModel = new();
        private bool isSessionStarted = false;
        private bool isCreatingSession = false;
        private string qrCodePayload = string.Empty;
        private string selectedTheme = "Standard";
        private DateTime sessionEndTime;
        private System.Threading.Timer countdownTimer;
        private QRCodeData generatedQRCode;
        private List<SessionData> allActiveSessions = new();
        private List<SessionData> otherActiveSessions = new();
        private SessionData currentActiveSession;


        // Course selection
        private Course selectedCourse;
        private bool showCourseSelection = false;


      
        private bool useTemporalKeyRefresh = false;
        private bool allowOfflineSync = true;
        private AdvancedSecurityFeatures securityFeatures = AdvancedSecurityFeatures.Default;
        
        private System.Threading.Timer temporalKeyUpdateTimer;
        private int temporalKeyRefreshInterval = 5; // Default to 5 minutes
        private bool showInfoPopup = false;
        private InfoPopup.InfoType currentInfoType;
private bool isEndingSession = false;
private bool isSessionEnded = false;


// Session restoration and loading states
private bool isRestoringSession = false;
private bool isSearchingForSessions = false;
private bool hasExistingSessions = false;
private string restorationMessage = string.Empty;
private List<SessionData> storedSessions = new();
private bool showSessionRestoreDialog = false;
private SessionData selectedStoredSession;
public bool isCurrentUserCourseRep = false;
public string currentUserMatricNumber = String.Empty;


private bool showManualAttendancePopup = false;
private PartialSessionData? manualAttendanceSessionData;






protected override async Task OnInitializedAsync()
{
    sessionModel.Duration = 30;


    try
    {
        isSearchingForSessions = true;
        
        restorationMessage = "Searching for existing sessions...";
        StateHasChanged();


        // Initialize SessionStateService with persistence recovery
        await SessionStateService.InitializeAsync();
        
        // Clean up expired sessions first
        await SessionStateService.CleanupExpiredSessionsAsync();
        
        RefreshSessionLists();
        
        // Check for stored sessions - NO AUTOMATIC RESTORATION
        await CheckForStoredSessionsAsync();


        SessionStateService.StateChanged += OnStateChanged;


        countdownTimer = new System.Threading.Timer(
            async _ => await InvokeAsync(async () => {
                await SessionStateService.CleanupExpiredSessionsAsync();
                RefreshSessionLists();
ed while cleaning up sessions");
    }
}

private async Task ClearRestorationStateAsync(string finalMessage = "")
{
    // Reset all restoration-related state
    isRestoringSession = false;
    selectedStoredSession = null;
    showSessionRestoreDialog = false;
    
    // Set final message if provided
    if (!string.IsNullOrEmpty(finalMessage))
    {
        restorationMessage = finalMessage;
        StateHasChanged();
        
        // Clear message after 3 seconds
        await Task.Delay(3000);
        restorationMessage = string.Empty;
    }
    else
    {
        restorationMessage = string.Empty;
    }
    
    StateHasChanged();
}

private async Task DismissSessionRestoreDialogAsync()
{
    // Simply close dialog without any cleanup operations
    showSessionRestoreDialog = false;
    selectedStoredSession = null;
    restorationMessage = string.Empty;
    StateHasChanged();
}

private void SelectStoredSession(SessionData session)
{
    selectedStoredSession = session;
    StateHasChanged(); // Ensure UI updates to show selection
}

private async Task DeleteStoredSessionAsync(SessionData session)
{
    try
    {
        // Remove from storage
        await SessionStateService.RemoveStoredSessionAsync(session.SessionId);
        
        // Remove from active sessions if present
        var activeSession = allActiveSessions.FirstOrDefault(s => s.SessionId == session.SessionId);
        if (activeSession != null)
        {
            SessionStateService.RemoveActiveSession(activeSession.SessionId);
        }
        
        // Update local state
        storedSessions.Remove(session);
        hasExistingSessions = storedSessions.Any();
        
        // Close dialog if no more sessions
        if (!hasExistingSessions)
        {
            showSessionRestoreDialog = false;
            restorationMessage = "All stored sessions removed";
            
            // Clear message after delay
            _ = Task.Delay(3000).ContinueWith(_ => 
            {
                restorationMessage = string.Empty;
                InvokeAsync(StateHasChanged);
            });
        }
        
        // Refresh session lists
        RefreshSessionLists();
        StateHasChanged();
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error deleting stored session: {ex.Message}");
        restorationMessage = "Error occurred while deleting session";
        StateHasChanged();
    }
}

private async Task RestoreExistingSessionAsync(SessionData activeSession, SessionData sessionData)
{
    sessionModel = sessionData;
    currentActiveSession = activeSession;
    isSessionStarted = true;
    qrCodePayload = activeSession.QrCodePayload;
    selectedTheme = activeSession.Theme;
    sessionEndTime = activeSession.EndTime;

    // Restore temporal key settings if available
    useTemporalKeyRefresh = activeSession.UseTemporalKeyRefresh;
    securityFeatures = activeSession.SecurityFeatures;

    // Restore selected course
    if (!string.IsNullOrEmpty(sessionData.CourseCode))
    {
        selectedCourse = await CourseService.GetCourseByIdAsync(sessionData.CourseCode);
    }

    // Restart temporal key timer if it was enabled
    if (useTemporalKeyRefresh)
    {
        StartTemporalKeyUpdateTimer();
    }

    StateHasChanged();
}
        private void ShowInfoPopup(InfoPopup.InfoType infoType)
        {
            currentInfoType = infoType;
            showInfoPopup = true;
        }

        private void CloseInfoPopup()
        {
            showInfoPopup = false;
        }
        private string GetCountdownClass()
        {
            if (!isSessionStarted) return "normal";
    
            var timeRemaining = GetTimeRemainingInSeconds();
    
            if (timeRemaining <= 60) // Last minute - critical
                return "critical";
            else if (timeRemaining <= 300) // Last 5 minutes - warning
                return "warning";
            else
                return "normal";
        }

        private bool IsCountdownCritical()
        {
            return GetTimeRemainingInSeconds() <= 60;
        }

        private int GetTimeRemainingInSeconds()
        {
            if (currentActiveSession == null) return 0;
    
            var endTime = currentActiveSession.StartTime.AddMinutes(currentActiveSession.Duration);
            var remaining = endTime - DateTime.Now;
    
            return Math.Max(0, (int)remaining.TotalSeconds);
        }
    

        private string GetSecurityFeatureDescription(AdvancedSecurityFeatures feature)
        {
            return feature switch
            {
                AdvancedSecurityFeatures.Default => "Standard security with basic validation",
                AdvancedSecurityFeatures.DeviceGuidCheck => "Enhanced security with device-specific verification to prevent unauthorized access",
                _ => "Unknown security feature"
            };
        }

        private string GetSecurityLevelName(AdvancedSecurityFeatures feature)
        {
            return feature switch
            {
                AdvancedSecurityFeatures.Default => "Standard",
                AdvancedSecurityFeatures.DeviceGuidCheck => "Enhanced",
                _ => "Unknown"
            };
        }

        private void RefreshSessionLists()
        {
            allActiveSessions = SessionStateService.GetActiveSessions();

            if (isSessionStarted && currentActiveSession != null)
            {
                otherActiveSessions = allActiveSessions
                    .Where(s => s.SessionId != currentActiveSession.SessionId)
                    .ToList();
            }
            else
            {
                otherActiveSessions = allActiveSessions.ToList();
            }
        }
private async Task CheckForExistingSessionAsync()
{
    try
    {
        var existingSession = SessionStateService.GetCurrentSession("default");
        if (existingSession != null)
        {
            isRestoringSession = true;
            restorationMessage = "Restoring active session...";
            StateHasChanged();

            var activeSession = allActiveSessions.FirstOrDefault(s =>
                s.CourseCode == existingSession.CourseCode &&
                DateTime.UtcNow < s.EndTime);

            if (activeSession != null)
            {
                await RestoreExistingSessionAsync(activeSession, existingSession);
                restorationMessage = "Active session restored";
            }
            else
            {
                // Session has expired, clean up
                await SessionStateService.RemoveCurrentSessionAsync("default");
                restorationMessage = "Previous session expired - cleaned up";
            }
            
            isRestoringSession = false;
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error checking for existing session: {ex.Message}");
        isRestoringSession = false;
        restorationMessage = "Error occurred while restoring session";
    }
}


        private void OnStateChanged()
        {
            InvokeAsync(() => {
                RefreshSessionLists();
                StateHasChanged();
            });
        }

        private void ShowCourseSelection()
        {
            showCourseSelection = true;
        }

        private void HideCourseSelection()
        {
            showCourseSelection = false;
        }

        private void HandleCourseSelected(Course course)
        {
            selectedCourse = course;
            sessionModel.CourseCode = course.CourseCode;
            sessionModel.CourseName = course.Name;
            showCourseSelection = false;
            StateHasChanged();
        }
private async Task StartSessionAsync()
{
    if (selectedCourse == null) return;

    // Prevent starting if we're in the middle of restoration
    if (isRestoringSession || isSearchingForSessions)
    {
        return;
    }

    // Check if there's already an active session for this course
    if (allActiveSessions.Any(s => s.CourseCode == selectedCourse.CourseCode))
    {
        restorationMessage = "Session already exists for this course";
        return;
    }

    try
    {
        isCreatingSession = true;
        StateHasChanged();

        sessionModel.StartTime = DateTime.UtcNow;
        sessionModel.CreatedAt = DateTime.UtcNow.Date;
        sessionModel.SessionId = Guid.NewGuid().ToString("N");
        
        sessionEndTime = DateTime.UtcNow.AddMinutes(sessionModel.Duration);

        // Generate temporal key if enabled
        string temporalKey = useTemporalKeyRefresh ? 
            GenerateTemporalKey(sessionModel.SessionId, sessionModel.StartTime) : 
            string.Empty;

        // Create Supabase attendance session
        var attendanceSession = new SupabaseAttendanceSession
        {
            SessionId = sessionModel.SessionId,
            CourseCode = sessionModel.CourseCode,
            StartTime = sessionModel.StartTime,
            Duration = sessionModel.Duration,
            ExpirationTime = sessionEndTime,
            AttendanceRecords = "[]",
            UseTemporalKeyRefresh = useTemporalKeyRefresh,
            AllowOfflineConnectionAndSync = allowOfflineSync,
            SecurityFeatures = (int)securityFeatures,
            TemporalKey = temporalKey
        };

        // Save to Supabase
        var savedSession = await AttendanceSessionService.CreateSessionAsync(attendanceSession);

        // CRITICAL FIX: Create offline session entry when offline sync is enabled
        if (allowOfflineSync)
        {
            var offlineSession = new SupabaseOfflineAttendanceSession
            {
                SessionId = sessionModel.SessionId,
                CourseCode = sessionModel.CourseCode,
                StartTime = sessionModel.StartTime,
                Duration = sessionModel.Duration,
                ExpirationTime = sessionEndTime,
                AttendanceRecords = "[]",
                UseTemporalKeyRefresh = useTemporalKeyRefresh,
                SecurityFeatures = (int)securityFeatures,
                TemporalKey = temporalKey,
                SyncStatus = "pending", // or appropriate initial status
                CreatedAt = DateTime.UtcNow
            };

            await AttendanceSessionService.CreateOfflineSessionAsync(offlineSession);
        }
        
        // Generate QR code payload with temporal key
        qrCodePayload = await QRCodeDecoder.EncodeSessionDataAsync(
            sessionModel.SessionId,
            sessionModel.CourseCode,
            sessionModel.StartTime,
            sessionModel.Duration,
            allowOfflineSync,
            useTemporalKeyRefresh,
            securityFeatures,
            temporalKey
        );

        // Create Firebase attendance event document (keep existing functionality)
        await CreateFirebaseAttendanceEvent();

        // Create active session data
        currentActiveSession = new SessionData
        {
            SessionId = sessionModel.SessionId,
            CourseName = sessionModel.CourseName,
            CourseCode = sessionModel.CourseCode,
            StartTime = sessionModel.StartTime,
            EndTime = sessionEndTime,
            Duration = sessionModel.Duration,
            QrCodePayload = qrCodePayload,
            Theme = selectedTheme,
            UseTemporalKeyRefresh = useTemporalKeyRefresh,
            SecurityFeatures = securityFeatures,
            TemporalKey = temporalKey
        };
        
        // Add to active sessions with persistence
        await SessionStateService.AddActiveSessionAsync(currentActiveSession);
        await SessionStateService.UpdateCurrentSessionAsync("default", sessionModel);

        // Start temporal key update timer if enabled
        if (useTemporalKeyRefresh)
        {
            StartTemporalKeyUpdateTimer();
        }

        if (isCurrentUserCourseRep && !string.IsNullOrEmpty(currentUserMatricNumber))
        {
            await AutoSignCourseRepAsync();
        }
        
        isSessionStarted = true;
        RefreshSessionLists();
        
        isCreatingSession = false;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error starting session: {ex.Message}");
    }
    finally
    {
        isCreatingSession = false;
        StateHasChanged();
    }
}
        private string GenerateTemporalKey(string sessionId, DateTime startTime)
        {
            var keyData = $"{sessionId}:{startTime:yyyyMMddHHmm}:{DateTime.UtcNow.Ticks}";
            var keyBytes = System.Text.Encoding.UTF8.GetBytes(keyData);
            
            using (var sha256 = System.Security.Cryptography.SHA256.Create())
            {
                var hashBytes = sha256.ComputeHash(keyBytes);
                return Convert.ToBase64String(hashBytes).Substring(0, 16);
            }
        }

private async Task UpdateTemporalKeyAsync()
{
    if (!useTemporalKeyRefresh || !isSessionStarted || currentActiveSession == null)
        return;

    try
    {
        string newTemporalKey = GenerateTemporalKey(sessionModel.SessionId, sessionModel.StartTime);

        // Update Supabase first
        await AttendanceSessionService.UpdateTemporalKeyAsync(sessionModel.SessionId, newTemporalKey);

        // Update current active session's temporal key
        currentActiveSession.TemporalKey = newTemporalKey;
        await SessionStateService.UpdateActiveSessionAsync(currentActiveSession);

        // Trigger QR component refresh via parameter change
        await InvokeAsync(StateHasChanged);

        Console.WriteLine($"Updated temporal key for session: {sessionModel.SessionId}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error updating temporal key: {ex.Message}");
    }
}
// Update the timer callback to use async version
private void StartTemporalKeyUpdateTimer()
{
    var interval = TimeSpan.FromMinutes(temporalKeyRefreshInterval);
    
    temporalKeyUpdateTimer = new System.Threading.Timer(
        async _ => await UpdateTemporalKeyAsync(),
        null,
        interval,
        interval
    );
}

// Update Dispose to clean up persistence if needed
public async ValueTask DisposeAsync()
{
    countdownTimer?.Dispose();
    temporalKeyUpdateTimer?.Dispose();
    SessionStateService.StateChanged -= OnStateChanged;
    
    // Clean up any remaining sessions on app shutdown
    if (currentActiveSession != null)
    {
        await SessionStateService.RemoveActiveSessionAsync(currentActiveSession.SessionId);
    }
}

        private async Task CreateFirebaseAttendanceEvent()
        {
            try
            {
               
               await FirebaseAttendanceService.CreateAttendanceEventAsync(sessionModel.SessionId,sessionModel.CourseCode,sessionModel.CourseName,DateTime.UtcNow,sessionModel.Duration,selectedTheme);
               
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating Firebase attendance event: {ex.Message}");
                throw;
            }
        }

private async Task EndSession()
{
    try
    {
        isEndingSession = true;
        StateHasChanged();

        if (currentActiveSession != null)
        {
            // Stop temporal key timer
            temporalKeyUpdateTimer?.Dispose();

            // Update Firebase document to mark session as ended
           bool completedSucess =  await FirebaseAttendanceService.CompleteAttendanceSessionAsync(currentActiveSession.SessionId,currentActiveSession.CourseCode);

           if (!completedSucess) return;
           await MigrateAttendanceDataToFirebase(currentActiveSession.SessionId,currentActiveSession.CourseCode);

            // Remove from active sessions
            SessionStateService.RemoveActiveSession(currentActiveSession.SessionId);
            
            // Remove stored session as well
            await SessionStateService.RemoveStoredSessionAsync(currentActiveSession.SessionId);
        }

        // Clean up current session
        SessionStateService.RemoveCurrentSession("default");
     
        // Reset component state
        isSessionStarted = false;
        currentActiveSession = null;
        selectedCourse = null;
        
        RefreshSessionLists();
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error ending session: {ex.Message}");
    }
    finally
    {
        isEndingSession = false;
        StateHasChanged();
    }
}
// UI Helper Methods for Message Display
private string GetStatusMessageClass(string message)
{
    if (IsErrorMessage(message))
        return "error";
    else if (IsWarningMessage(message))
        return "warning";
    else
        return "success";
}

private bool IsErrorMessage(string message)
{
    if (string.IsNullOrEmpty(message)) return false;
    
    var errorKeywords = new[] { "error", "failed", "exception", "unable" };
    return errorKeywords.Any(keyword => message.ToLower().Contains(keyword));
}

private bool IsWarningMessage(string message)
{
    if (string.IsNullOrEmpty(message)) return false;
    
    var warningKeywords = new[] { "expired", "removed", "unavailable", "cleaned" };
    return warningKeywords.Any(keyword => message.ToLower().Contains(keyword));
}
        private async Task HandleSessionEnd()
        {
            // This is where you'll add your session end logic
            // For example:
            // - Process final attendance data
            // - Send notifications
            // - Update database
            // - Generate reports
            // etc.
    
            Console.WriteLine("Session ended - processing final data...");
        }

        private void ResetSession()
        {
            // Reset all session states to start fresh
            isSessionStarted = false;
            isSessionEnded = false;
            isCreatingSession = false;
            isEndingSession = false;
            currentActiveSession = null;
    
            StateHasChanged();
        }

        
        private QRCodeTheme ConvertStringToTheme(string theme)
        {
            return theme switch
            {
                "Gradient" => QRCodeTheme.Gradient,
                "Branded" => QRCodeTheme.Branded,
                "GradientWithLogo" => QRCodeTheme.GradientWithLogo,
                _ => QRCodeTheme.Standard
            };
        }

        private QRCodeBaseOptions GenerateQRCodeOptions()
        {
            QRCodeBaseOptions options = new QRCodeBaseOptions
            {
                Content = qrCodePayload,
                Size = 300,
                DarkColor = "#000000",
                LightColor = "#FFFFFF",
                ErrorLevel = ErrorCorrectionLevel.M,
                Margin = 4
            };

            return selectedTheme switch
            {
                "Gradient" => new QRCodeGradientOptions
                {
                    Content = qrCodePayload,
                    Size = 300,
                    DarkColor = "#000000",
                    LightColor = "#FFFFFF",
                    ErrorLevel = ErrorCorrectionLevel.M,
                    Margin = 4,
                    GradientColor1 = "#3498db",
                    GradientColor2 = "#9b59b6",
                    Direction = GradientDirection.Diagonal
                },
                "Branded" => new QRCodeBrandedOptions
                {
                    Content = qrCodePayload,
                    Size = 300,
                    DarkColor = "#000000",
                    LightColor = "#FFFFFF",
                    ErrorLevel = ErrorCorrectionLevel.H,
                    Margin = 4,
                    LogoUrl = "icon-192.png",
                    LogoSizeRatio = 0.2f,
                    AddLogoBorder = true,
                    LogoBorderColor = "#FFFFFF",
                    LogoBorderWidth = 2,
                    LogoBorderRadius = 5
                },
                "GradientWithLogo" => new QRCodeGradientBrandedOptions
                {
                    Content = qrCodePayload,
                    Size = 300,
                    DarkColor = "#000000",
                    LightColor = "#FFFFFF",
                    ErrorLevel = ErrorCorrectionLevel.H,
                    Margin = 4,
                    GradientColor1 = "#3498db",
                    GradientColor2 = "#9b59b6",
                    Direction = GradientDirection.Radial,
                    LogoUrl = "/icon-192.png",
                    LogoSizeRatio = 0.2f,
                    AddLogoBorder = true,
                    LogoBorderColor = "#FFFFFF",
                    LogoBorderWidth = 2,
                    LogoBorderRadius = 5
                },
                _ => options
            };
        }
private async Task AutoSignCourseRepAsync()
{
    try
    {
    
        bool success = await FirebaseAttendanceService.AutoSignCourseRepAsync(
            sessionModel.SessionId, 
            sessionModel.CourseCode);
            
        if (success)
        {
            Console.WriteLine($"Course rep auto-signed successfully: {currentUserMatricNumber}");
        }
        else
        {
            Console.WriteLine($"Failed to auto-sign course rep: {currentUserMatricNumber}");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error auto-signing course rep: {ex.Message}");
        // Don't throw - session should still start even if auto-sign fails
    }
}
private void OpenManualAttendancePopup()
{
    if (currentActiveSession != null && selectedCourse != null)
    {
        manualAttendanceSessionData = new PartialSessionData
        {
            SessionId = currentActiveSession.SessionId,
            CourseCode = selectedCourse.CourseCode,
            CourseName = selectedCourse.Name,
            StartTime = currentActiveSession.StartTime,
            Duration = currentActiveSession.Duration
        };
        showManualAttendancePopup = true;
    }
}

private void CloseManualAttendancePopup()
{
    showManualAttendancePopup = false;
    manualAttendanceSessionData = null;
}

private async Task HandleManualAttendanceSigned(string matricNumber)
{
    // Optional: Add any additional logic when attendance is manually signed
    // For example, refresh attendance data, show notifications, etc.
    
    // You can add a status message here
    restorationMessage = $"Attendance manually signed for {matricNumber}";
    StateHasChanged();

}
        private string FormatTimeRemaining()
        {
            if (!isSessionStarted || currentActiveSession == null) return "--:--:--";

            TimeSpan remaining = sessionEndTime - DateTime.UtcNow;
            if (remaining.TotalSeconds <= 0)
            {
                EndSession();
                return "00:00:00";
            }

            return $"{remaining.Hours:00}:{remaining.Minutes:00}:{remaining.Seconds:00}";
        }

        private Task HandleQRCodeGenerated(QRCodeData qrCode)
        {
            generatedQRCode = qrCode;
            return Task.CompletedTask;
        }

        private async Task MigrateAttendanceDataToFirebase(string sessionId, string courseCode)
        {
            try
            {
                // Add null validation before database operations
                if (string.IsNullOrEmpty(sessionId) || string.IsNullOrEmpty(courseCode))
                {
                    MID_HelperFunctions.DebugMessage("Invalid parameters for migration: SessionId={SessionId}, CourseCode={CourseCode}", DebugClass.Warning);
                    MID_HelperFunctions.DebugMessage($"Invalid parameters for migration: SessionId={sessionId}, CourseCode={courseCode}",DebugClass.Warning);
                    return;
                }

                // Use direct database query with error handling
                var session = await AttendanceSessionService.GetSessionByIdAsync(sessionId);
                if (session == null)
                {
                   MID_HelperFunctions.DebugMessage($"Session not found for migration for session: {sessionId}",DebugClass.Warning);
                    return;
                }

                var attendanceRecords = session.GetAttendanceRecords();
                if (attendanceRecords?.Any() == true)
                {
                    await FirebaseAttendanceService.UpdateAttendanceRecordsAsync(
                        sessionId, courseCode, attendanceRecords);
                }
            }
            catch (Exception ex)
            {
                MID_HelperFunctions.DebugMessage( "Migration failed for session {SessionId}", DebugClass.Exception);
                throw; // Re-throw to maintain error handling flow
            }
        }
        public void Dispose()
        {
            countdownTimer?.Dispose();
            temporalKeyUpdateTimer?.Dispose();
            SessionStateService.StateChanged -= OnStateChanged;
        }
    }
