
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using System.Threading;
using AirCode.Models.Supabase;
using AirCode.Services.Attendance;
using AirCode.Services.Firebase;
using AirCode.Services.Storage;
using AirCode.Utilities.HelperScripts;
using AirCode.Utilities.DataStructures;
using AirCode.Domain.Entities;
using AirCode.Domain.ValueObjects;
using AirCode.Domain.Enums;
using AirCode.Models.QRCode;
using Course = AirCode.Domain.Entities.Course;
using AirCode.Components.SharedPrefabs.Others;


namespace AirCode.Pages.Admin.Shared
{
    public partial class CreateAttendanceEvent : ComponentBase, IAsyncDisposable
    {
        #region Service Dependencies (Inject these in the razor file)
        
        [Inject] protected SessionStateService SessionStateService { get; set; }
        [Inject] protected AttendanceSessionService AttendanceSessionService { get; set; }
        [Inject] protected IFirestoreAttendanceService FirebaseAttendanceService { get; set; }
        [Inject] protected QRCodeDecoder QRCodeDecoder { get; set; }
        
        #endregion
        
        #region Variables and stuff
        
        // Session restoration and loading states
        private bool isRestoringSession = false;
        private bool isSearchingForSessions = false;
        private bool hasExistingSessions = false;
        private bool isCreatingSession = false;
        private bool isEndingSession = false;
        private bool isSessionStarted = false;
        private bool isSessionEnded = false;
        private bool showCourseSelection = false;
        private bool showManualAttendancePopup = false;
        private bool allowOfflineSync = false;
        private bool useTemporalKeyRefresh = false;
        private bool isCurrentUserCourseRep = false;
        private bool showSessionRestoreDialog = false;
        private bool showInfoPopup = false;
        
        private string restorationMessage = string.Empty;
        private string qrCodePayload = string.Empty;
        private string selectedTheme = "Standard";
        private string currentUserMatricNumber = string.Empty;
        
        private Course selectedCourse;
        private SessionData sessionModel = new SessionData();
        private SessionData currentActiveSession;
        private SessionData selectedStoredSession;
        private InfoPopup.InfoType currentInfoType;
        private PartialSessionData manualAttendanceSessionData;
        private QRCodeData generatedQRCode;
        
        private DateTime sessionEndTime;
        private Timer countdownTimer;
        private Timer temporalKeyUpdateTimer;
        
        private List<SessionData> allActiveSessions = new List<SessionData>();
        private List<SessionData> storedSessions = new();
        private List<SessionData> otherActiveSessions = new();
        private AdvancedSecurityFeatures securityFeatures;
        private int temporalKeyRefreshInterval = 5; // minutes
        
        #endregion


        #region Lifecycle Methods


        protected override async Task OnInitializedAsync()
        {
            SessionStateService.StateChanged += OnStateChanged;
            await CheckForExistingSessionAsync();
            await CheckForStoredSessionsAsync();
        }


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


        public void Dispose()
        {
            countdownTimer?.Dispose();
            temporalKeyUpdateTimer?.Dispose();
            SessionStateService.StateChanged -= OnStateChanged;
        }


        #endregion


        #region Session Management


        private async Task CheckForExistingSessionAsync()
        {
            try
            {
                isRestoringSession = true;
                
                var existingSession = SessionStateService.GetCurrentSession("default");
                if (existingSession != null)
                {
                    // Validate session hasn't expired
                    if (existingSession.StartTime.AddMinutes(existingSession.Duration) > DateTime.UtcNow)
                    {
                        // Restore session state
                        currentActiveSession = existingSession;
                        isSessionStarted = true;
                        sessionModel.SessionId = existingSession.SessionId;
                        sessionModel.CourseCode = existingSession.CourseCode;
                        sessionModel.CourseName = existingSession.CourseName;
                        sessionModel.StartTime = existingSession.StartTime;
                        sessionModel.Duration = existingSession.Duration;
                        
                        sessionEndTime = existingSession.StartTime.AddMinutes(existingSession.Duration);
                        
                        restorationMessage = "Previous session restored successfully";
                        
                        // Start timers if needed
                        if (useTemporalKeyRefresh)
                        {
                            StartTemporalKeyUpdateTimer();
                        }
                    }
                    else
                    {
                        // Session has expired, clean up
                        SessionStateService.RemoveCurrentSession("default");
                        restorationMessage = "Previous session expired - cleaned up";
                    }
                }
                
                isRestoringSession = false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error checking for existing session: {ex.Message}");
                isRestoringSession = false;
                restorationMessage = "Error occurred while restoring session";
            }
        }

        private async Task CheckForStoredSessionsAsync()
        {
            try
            {
                // Get all stored sessions that haven't been restored yet
                var persistentSessions = await SessionStateService.GetStoredSessionsAsync();
                storedSessions = persistentSessions
                    .Where(s => s.StartTime.AddMinutes(s.Duration) > DateTime.UtcNow)
                    .Select(ps => new SessionData
                    {
                        SessionId = ps.SessionId,
                        CourseCode = ps.CourseCode,
                        CourseName = ps.CourseName,
                        StartTime = ps.StartTime,
                        Duration = ps.Duration,
                    })
                    .ToList();

                hasExistingSessions = storedSessions.Any();

                if (hasExistingSessions && !isSessionStarted)
                {
                    restorationMessage = $"Found {storedSessions.Count} stored session(s) available for restoration";
                    showSessionRestoreDialog = true;
                }
                else
                {
                    // Clear any lingering restoration messages
                    restorationMessage = string.Empty;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error checking for stored sessions: {ex.Message}");
                restorationMessage = "Error occurred while checking for stored sessions";
            }
        }

        private async Task RestoreSelectedSessionAsync()
        {
            if (selectedStoredSession == null) return;

            try
            {
                isRestoringSession = true;
                restorationMessage = "Restoring selected session...";
                showSessionRestoreDialog = false; // Close dialog immediately
                StateHasChanged();

                // Find corresponding active session
                var activeSession = allActiveSessions.FirstOrDefault(s =>
                    s.SessionId == selectedStoredSession.SessionId);

                if (activeSession != null)
                {
                    await RestoreExistingSessionAsync(activeSession, selectedStoredSession);

                    // Clear restoration state completely
                    await ClearRestorationStateAsync("Session restored successfully");
                }
                else
                {
                    // Session might have expired or been removed
                    await SessionStateService.RemoveStoredSessionAsync(selectedStoredSession.SessionId);
                    await ClearRestorationStateAsync("Session no longer available - removed from storage");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error restoring session: {ex.Message}");
                await ClearRestorationStateAsync("Failed to restore session");
            }
        }

        private async Task RestoreExistingSessionAsync(SessionData activeSession, SessionData storedSession)
        {
            try
            {
                // Restore active session state
                currentActiveSession = activeSession;
                isSessionStarted = true;
                
                sessionModel.SessionId = activeSession.SessionId;
                sessionModel.CourseCode = activeSession.CourseCode;
                sessionModel.CourseName = activeSession.CourseName;
                sessionModel.StartTime = activeSession.StartTime;
                sessionModel.Duration = activeSession.Duration;
                
                sessionEndTime = activeSession.StartTime.AddMinutes(activeSession.Duration);
                
                // Update selected course if we can find it
                selectedCourse = CreateCourseInstance(activeSession.CourseCode, activeSession.CourseName);
                
                // Regenerate QR code payload
                string temporalKey = useTemporalKeyRefresh ? activeSession.TemporalKey ?? string.Empty : string.Empty;
                qrCodePayload = await QRCodeDecoder.EncodeSessionDataAsync(
                    activeSession.SessionId,
                    activeSession.CourseCode,
                    activeSession.StartTime,
                    activeSession.Duration,
                    allowOfflineSync,
                    useTemporalKeyRefresh,
                    securityFeatures,
                    temporalKey
                );

                // Start temporal key refresh if enabled
                if (useTemporalKeyRefresh)
                {
                    StartTemporalKeyUpdateTimer();
                }

                // Set as current session
                await SessionStateService.UpdateCurrentSessionAsync("default", activeSession);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error restoring existing session: {ex.Message}");
                throw;
            }
        }

        private async Task AbandonStoredSessionsAsync()
        {
            try
            {
                isRestoringSession = true;
                restorationMessage = "Cleaning up stored sessions...";
                showSessionRestoreDialog = false; // Close dialog immediately
                StateHasChanged();

                // Remove all stored sessions (equivalent to EndSession cleanup)
                foreach (var session in storedSessions.ToList())
                {
                    await SessionStateService.RemoveStoredSessionAsync(session.SessionId);

                    // Also remove from active sessions if present
                    var activeSession = allActiveSessions.FirstOrDefault(s => s.SessionId == session.SessionId);
                    if (activeSession != null)
                    {
                        SessionStateService.RemoveActiveSession(activeSession.SessionId);
                    }
                }

                // Clear local state
                storedSessions.Clear();
                hasExistingSessions = false;

                await ClearRestorationStateAsync("Previous sessions cleaned up - ready for new session");

                // Refresh the session lists
                RefreshSessionLists();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error abandoning stored sessions: {ex.Message}");
                await ClearRestorationStateAsync("Error occurred while cleaning up sessions");
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
                    AllowOfflineConnectionAndSync = allowOfflineSync,
                    SecurityFeatures = (int)securityFeatures,
                    TemporalKey = temporalKey,
                    UseTemporalKeyRefresh = useTemporalKeyRefresh,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                // Save to Supabase
                var savedSession = await AttendanceSessionService.CreateSessionAsync(attendanceSession);

                // Create offline session entry when offline sync is enabled
                if (allowOfflineSync)
                {
                    var offlineSession = new SupabaseOfflineAttendanceSession
                    {
                        SessionId = sessionModel.SessionId,
                        CourseCode = sessionModel.CourseCode,
                        StartTime = sessionModel.StartTime,
                        Duration = sessionModel.Duration,
                        ExpirationTime = sessionEndTime,
                        OfflineRecords = "[]",
                        AllowOfflineSync = allowOfflineSync,
                        SecurityFeatures = (int)securityFeatures,
                        SyncStatus = 0,
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
                    CourseCode = sessionModel.CourseCode,
                    CourseName = sessionModel.CourseName,
                    StartTime = sessionModel.StartTime,
                    Duration = sessionModel.Duration,
                    TemporalKey = temporalKey
                };

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
                    bool completedSuccess = await FirebaseAttendanceService.CompleteAttendanceSessionAsync(
                        currentActiveSession.SessionId, currentActiveSession.CourseCode);

                    if (!completedSuccess) return;
                    
                    await MigrateAttendanceDataToFirebase(currentActiveSession.SessionId, currentActiveSession.CourseCode);

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

        #endregion

        #region Course Management

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

        private async Task<Course> GetCourseByCodeAsync(string courseCode)
        {
            // Implement course retrieval logic
            return await Task.FromResult(CreateCourseInstance(courseCode, ""));
        }

        private Course CreateCourseInstance(string courseCode, string name)
        {
            // Create Course with all required parameters
      return new Course(
        courseCode: courseCode,
        name: name,
        departmentId: string.Empty, // Add appropriate department ID
        level: LevelType.Level100, // Use correct enum value
        semester: SemesterType.FirstSemester, // Use correct enum value
        creditUnits: 3, // byte type for credit units
        schedule: new CourseSchedule(), // Create appropriate schedule
        lecturerIds: new List<string>(), // List of lecturer IDs
        lastModified: DateTime.UtcNow,
        modifiedBy: "System"
    );
        }

        #endregion

        #region Temporal Key Management

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

        #endregion

        #region Firebase Integration

        private async Task CreateFirebaseAttendanceEvent()
        {
            try
            {
                await FirebaseAttendanceService.CreateAttendanceEventAsync(
                    sessionModel.SessionId,
                    sessionModel.CourseCode,
                    sessionModel.CourseName,
                    DateTime.UtcNow,
                    sessionModel.Duration,
                    selectedTheme);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating Firebase attendance event: {ex.Message}");
                throw;
            }
        }

        private async Task MigrateAttendanceDataToFirebase(string sessionId, string courseCode)
        {
            try
            {
                // Add null validation before database operations
                if (string.IsNullOrEmpty(sessionId) || string.IsNullOrEmpty(courseCode))
                {
                    MID_HelperFunctions.DebugMessage("Invalid parameters for migration: SessionId={SessionId}, CourseCode={CourseCode}", DebugClass.Warning);
                    MID_HelperFunctions.DebugMessage($"Invalid parameters for migration: SessionId={sessionId}, CourseCode={courseCode}", DebugClass.Warning);
                    return;
                }

                // Use direct database query with error handling
                var session = await AttendanceSessionService.GetSessionByIdAsync(sessionId);
                if (session == null)
                {
                    MID_HelperFunctions.DebugMessage($"Session not found for migration for session: {sessionId}", DebugClass.Warning);
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
                MID_HelperFunctions.DebugMessage("Migration failed for session {SessionId}", DebugClass.Exception);
                throw; // Re-throw to maintain error handling flow
            }
        }

        #endregion

        #region Manual Attendance

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

        #endregion

        #region QR Code Management

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

        private Task HandleQRCodeGenerated(QRCodeData qrCode)
        {
            generatedQRCode = qrCode;
            return Task.CompletedTask;
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

        #endregion

        #region UI Helper Methods

        private void OnStateChanged()
        {
            InvokeAsync(() => {
                RefreshSessionLists();
                StateHasChanged();
            });
        }

        private void RefreshSessionLists()
        {
            // Implement session list refresh logic
            allActiveSessions = SessionStateService.GetActiveSessions().ToList();
        }

        private string FormatTimeRemaining()
        {
            if (!isSessionStarted || currentActiveSession == null) return "--:--:--";

            TimeSpan remaining = sessionEndTime - DateTime.UtcNow;
            if (remaining.TotalSeconds <= 0)
            {
                _ = EndSession();
                return "00:00:00";
            }

            return $"{remaining.Hours:00}:{remaining.Minutes:00}:{remaining.Seconds:00}";
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

        private void ShowInfoPopup(InfoPopup.InfoType infoType)
        {
            currentInfoType = infoType;
            showInfoPopup = true;
        }

        private void CloseInfoPopup()
        {
            showInfoPopup = false;
        }

        #endregion

        #region Additional Helper Methods

        private string GetSessionStatusText()
        {
            if (isCreatingSession) return "Creating session...";
            if (isEndingSession) return "Ending session...";
            if (isRestoringSession) return "Restoring session...";
            if (isSessionStarted) return "Session active";
            return "Ready to start";
        }

        private string GetSessionStatusClass()
        {
            if (isCreatingSession || isEndingSession || isRestoringSession) return "processing";
            if (isSessionStarted) return "active";
            return "ready";
        }

        private bool CanStartSession()
        {
            return selectedCourse != null && 
                   !isSessionStarted && 
                   !isCreatingSession && 
                   !isRestoringSession && 
                   !isSearchingForSessions;
        }

        private bool CanEndSession()
        {
            return isSessionStarted && 
                   !isEndingSession && 
                   !isCreatingSession && 
                   currentActiveSession != null;
        }

        private string GetFormattedStartTime()
        {
            if (currentActiveSession == null) return "--:--";
            return currentActiveSession.StartTime.ToString("HH:mm");
        }

        private string GetFormattedEndTime()
        {
            if (currentActiveSession == null) return "--:--";
            var endTime = currentActiveSession.StartTime.AddMinutes(currentActiveSession.Duration);
            return endTime.ToString("HH:mm");
        }

        private string GetSessionDurationText()
        {
            if (currentActiveSession == null) return "-- min";
            return $"{currentActiveSession.Duration} min";
        }

        private double GetSessionProgress()
        {
            if (!isSessionStarted || currentActiveSession == null) return 0;
            
            var totalDuration = currentActiveSession.Duration * 60; // Convert to seconds
            var elapsed = (DateTime.UtcNow - currentActiveSession.StartTime).TotalSeconds;
            var progress = Math.Min(100, Math.Max(0, (elapsed / totalDuration) * 100));
            
            return progress;
        }

        private bool IsSessionExpiringSoon()
        {
            return GetTimeRemainingInSeconds() <= 300; // 5 minutes
        }

        private bool IsSessionExpired()
        {
            return GetTimeRemainingInSeconds() <= 0;
        }

        private string GetOfflineSyncStatusText()
        {
            return allowOfflineSync ? "Enabled" : "Disabled";
        }

        private string GetTemporalKeyStatusText()
        {
            return useTemporalKeyRefresh ? $"Enabled ({temporalKeyRefreshInterval} min)" : "Disabled";
        }

        private string GetSecurityFeaturesText()
        {
            return securityFeatures.ToString().Replace("_", " ");
        }

        private async Task ValidateSessionStateAsync()
        {
            if (isSessionStarted && currentActiveSession != null)
            {
                // Check if session has expired
                var endTime = currentActiveSession.StartTime.AddMinutes(currentActiveSession.Duration);
                if (DateTime.UtcNow > endTime)
                {
                    // Auto-end expired session
                    await EndSession();
                    restorationMessage = "Session automatically ended due to expiration";
                    StateHasChanged();
                }
            }
        }

        private void UpdateCountdownDisplay()
        {
            if (isSessionStarted)
            {
                StateHasChanged(); // Force UI update for countdown
            }
        }

        private async Task HandleEmergencySessionCleanup()
        {
            try
            {
                // Emergency cleanup - remove all sessions
                if (currentActiveSession != null)
                {
                    SessionStateService.RemoveActiveSession(currentActiveSession.SessionId);
                    await SessionStateService.RemoveStoredSessionAsync(currentActiveSession.SessionId);
                }

                // Clear all stored sessions
                foreach (var session in storedSessions.ToList())
                {
                    await SessionStateService.RemoveStoredSessionAsync(session.SessionId);
                }

                // Reset component state
                ResetAllState();
                
                restorationMessage = "Emergency cleanup completed - all sessions removed";
                StateHasChanged();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during emergency cleanup: {ex.Message}");
                restorationMessage = "Error occurred during emergency cleanup";
                StateHasChanged();
            }
        }

        private void ResetAllState()
        {
            // Reset all component state to initial values
            isRestoringSession = false;
            isSearchingForSessions = false;
            hasExistingSessions = false;
            isCreatingSession = false;
            isEndingSession = false;
            isSessionStarted = false;
            isSessionEnded = false;
            showCourseSelection = false;
            showManualAttendancePopup = false;
            showSessionRestoreDialog = false;
            showInfoPopup = false;
            
            restorationMessage = string.Empty;
            qrCodePayload = string.Empty;
            
            selectedCourse = null;
            currentActiveSession = null;
            selectedStoredSession = null;
            manualAttendanceSessionData = null;
            generatedQRCode = null;
            
            sessionModel = new SessionData();
            allActiveSessions.Clear();
            storedSessions.Clear();
            otherActiveSessions.Clear();
            
            // Dispose timers
            countdownTimer?.Dispose();
            temporalKeyUpdateTimer?.Dispose();
            countdownTimer = null;
            temporalKeyUpdateTimer = null;
        }

        private bool HasPendingOperations()
        {
            return isCreatingSession || isEndingSession || isRestoringSession || isSearchingForSessions;
        }

        private string GetPendingOperationText()
        {
            if (isCreatingSession) return "Creating session, please wait...";
            if (isEndingSession) return "Ending session, please wait...";
            if (isRestoringSession) return "Restoring session, please wait...";
            if (isSearchingForSessions) return "Searching for sessions, please wait...";
            return string.Empty;
        }

        #endregion
    }
}
