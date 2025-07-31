


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
namespace AirCode.Pages.Admin.Shared
{
    public partial class CreateAttendanceEvent : ComponentBase, IAsyncDisposable
    {
        #region Service Dependencies (Inject these in the razor file)
        
        [Inject] protected SessionStateService SessionStateService { get; set; }
        [Inject] protected AttendanceSessionService AttendanceSessionService { get; set; }
       [Inject] protected IFirestoreAttendanceService FirestoreAttendanceService { get; set; }
        [Inject] protected QRCodeDecoder QRCodeDecoder { get; set; }
        
        #endregion
        #region Variables and stuff
        private bool isRestoringSession = false;
        private bool isSearchingForSessions = false;
        private bool isCreatingSession = false;
        private bool isEndingSession = false;
        private bool isSessionStarted = false;
        private bool isSessionEnded = false;
        private bool showCourseSelection = false;
        private bool showManualAttendancePopup = false;
        private bool allowOfflineSync = false;
        private bool useTemporalKeyRefresh = false;
        private bool isCurrentUserCourseRep = false;
        
        private string restorationMessage = string.Empty;
        private string qrCodePayload = string.Empty;
        private string selectedTheme = "Standard";
        private string currentUserMatricNumber = string.Empty;
        
        private Course selectedCourse;
        private SessionData sessionModel = new SessionData();
        private SessionData currentActiveSession;
        private PartialSessionData manualAttendanceSessionData;
        private QRCodeData generatedQRCode;
        
        private DateTime sessionEndTime;
        private Timer countdownTimer;
        private Timer temporalKeyUpdateTimer;
        
        private List<SessionData> allActiveSessions = new List<SessionData>();
        private AdvancedSecurityFeatures securityFeatures;
        private int temporalKeyRefreshInterval = 5; // minutes
        
        #endregion




        #region Lifecycle Methods




        protected override async Task OnInitializedAsync()
        {
            SessionStateService.StateChanged += OnStateChanged;
            await CheckForExistingSessionAsync();
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
                
                var existingSession = await SessionStateService.GetCurrentSessionAsync("default");
                if (existingSession != null)
                {
                    // Validate session hasn't expired
                    if (existingSession.ExpirationTime > DateTime.UtcNow)
                    {
                        // Restore session state
                        currentActiveSession = existingSession;
                        isSessionStarted = true;
              sessionModel.SessionId = existingSession.SessionId;
                        sessionModel.CourseCode = existingSession.CourseCode;
                        sessionModel.CourseName = existingSession.CourseName;
                        sessionModel.StartTime = existingSession.StartTime;
                        sessionModel.Duration = existingSession.Duration;
                        
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
                        await SessionStateService.RemoveCurrentSessionAsync("default");
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
                    ExpirationTime = sessionEndTime,
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
            return await Task.FromResult(new Course { CourseCode = courseCode });
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

        #endregion
    }
}
