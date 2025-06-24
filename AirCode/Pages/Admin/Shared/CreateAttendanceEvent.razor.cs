using System.Text.Json;
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
using SessionData = AirCode.Services.Attendance.SessionData;
using AirCode.Components.SharedPrefabs.Others;

    public partial class CreateAttendanceEvent : ComponentBase, IDisposable
    {
        [Inject] private IJSRuntime JS { get; set; }
        [Inject] private QRCodeDecoder QRCodeDecoder { get; set; }
        [Inject] private SessionStateService SessionStateService { get; set; }
        [Inject] private ICourseService CourseService { get; set; }
        [Inject] private IFirestoreService FirestoreService { get; set; }
        [Inject] private ISupabaseEdgeFunctionService EdgeService { get; set; }
        [Inject] private IAttendanceSessionService AttendanceSessionService { get; set; }

        private SessionData sessionModel = new();
        private bool isSessionStarted = false;
        private bool isCreatingSession = false;
        private string qrCodePayload = string.Empty;
        private string selectedTheme = "Standard";
        private DateTime sessionEndTime;
        private System.Threading.Timer countdownTimer;
        private QRCodeData generatedQRCode;
        private List<ActiveSessionData> allActiveSessions = new();
        private List<ActiveSessionData> otherActiveSessions = new();
        private ActiveSessionData currentActiveSession;

        // Course selection
        private Course selectedCourse;
        private bool showCourseSelection = false;

        // Floating QR code properties
        private bool showFloatingQR = false;
        private FloatingQRWindow.FloatingSessionData floatingSessionData;
        private bool useTemporalKeyRefresh = false;
        private bool allowOfflineSync = true;
        private AdvancedSecurityFeatures securityFeatures = AdvancedSecurityFeatures.Default;
        
        private System.Threading.Timer temporalKeyUpdateTimer;
        private int temporalKeyRefreshInterval = 5; // Default to 5 minutes
        private bool showInfoPopup = false;
        private InfoPopup.InfoType currentInfoType;
private bool isEndingSession = false;
private bool isSessionEnded = false;

       protected override async Task OnInitializedAsync()
{
    sessionModel.Duration = 30;

    // Initialize SessionStateService with persistence recovery
    await SessionStateService.InitializeAsync();
    
    RefreshSessionLists();
    await CheckForExistingSessionAsync();

    SessionStateService.StateChanged += OnStateChanged;

    countdownTimer = new System.Threading.Timer(
        async _ => await InvokeAsync(async () => {
            await SessionStateService.CleanupExpiredSessionsAsync();
            RefreshSessionLists();
            StateHasChanged();
        }),
        null,
        TimeSpan.Zero,
        TimeSpan.FromSeconds(1)
    );
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
        private void StartTemporalKeyUpdateTimer()
        {
            var interval = TimeSpan.FromMinutes(temporalKeyRefreshInterval);
            
            temporalKeyUpdateTimer = new System.Threading.Timer(
                async _ => await UpdateTemporalKey(),
                null,
                interval,
                interval
            );
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
    var existingSession = SessionStateService.GetCurrentSession("default");
    if (existingSession != null)
    {
        var activeSession = allActiveSessions.FirstOrDefault(s =>
            s.CourseId == existingSession.CourseId &&
            DateTime.UtcNow < s.EndTime);

        if (activeSession != null)
        {
            await RestoreExistingSessionAsync(activeSession, existingSession);
        }
        else
        {
            // Session has expired, clean up
            await SessionStateService.RemoveCurrentSessionAsync("default");
        }
    }
}
private async Task RestoreExistingSessionAsync(ActiveSessionData activeSession, SessionData sessionData)
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
    if (!string.IsNullOrEmpty(sessionData.CourseId))
    {
        selectedCourse = await CourseService.GetCourseByIdAsync(sessionData.CourseId);
    }

    // Restart temporal key timer if it was enabled
    if (useTemporalKeyRefresh)
    {
        StartTemporalKeyUpdateTimer();
    }

    StateHasChanged();
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
            sessionModel.CourseId = course.CourseCode;
            sessionModel.CourseName = course.Name;
            showCourseSelection = false;
            StateHasChanged();
        }
private async Task StartSessionAsync()
{
    if (selectedCourse == null) return;

    // Check if there's already an active session for this course
    if (allActiveSessions.Any(s => s.CourseId == selectedCourse.CourseCode))
    {
        // Show error message - session already exists for this course
        return;
    }

    try
    {
        isCreatingSession = true;
        StateHasChanged();

        sessionModel.StartTime = DateTime.UtcNow;
        sessionModel.Date = DateTime.UtcNow.Date;
        sessionModel.SessionId = Guid.NewGuid().ToString("N");
        
        sessionEndTime = DateTime.UtcNow.AddMinutes(sessionModel.Duration);

        // Generate temporal key if enabled
        string temporalKey = useTemporalKeyRefresh ? 
            GenerateTemporalKey(sessionModel.SessionId, sessionModel.StartTime) : 
            string.Empty;

        // Create Supabase attendance session
        var attendanceSession = new AttendanceSession
        {
            SessionId = sessionModel.SessionId,
            CourseCode = sessionModel.CourseId,
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
        
        // Generate QR code payload with temporal key
        qrCodePayload = await QRCodeDecoder.EncodeSessionDataAsync(
            sessionModel.SessionId,
            sessionModel.CourseId,
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
        currentActiveSession = new ActiveSessionData
        {
            SessionId = sessionModel.SessionId,
            CourseName = sessionModel.CourseName,
            CourseId = sessionModel.CourseId,
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
                // Get all students taking this course
                var allStudentCourses = await CourseService.GetAllStudentCoursesAsync();
                var studentsInCourse = allStudentCourses
                    .Where(sc => sc.GetEnrolledCourses().Any(cr => cr.CourseCode == sessionModel.CourseId))
                    .ToList();

                // Build AttendanceRecords for each student
                var attendanceRecords = new Dictionary<string, object>();
                foreach (var studentCourse in studentsInCourse)
                {
                    attendanceRecords[studentCourse.StudentMatricNumber] = new
                    {
                        MatricNumber = studentCourse.StudentMatricNumber,
                        HasScannedAttendance = false,
                        ScanTime = (DateTime?)null,
                        IsOnlineScan = false
                    };
                }

                var attendanceEventData = new
                {
                    SessionId = sessionModel.SessionId,
                    CourseCode = sessionModel.CourseId,
                    CourseName = sessionModel.CourseName,
                    StartTime = sessionModel.StartTime,
                    Duration = sessionModel.Duration,
                    EndTime = sessionEndTime,
                    Theme = selectedTheme,
                    CreatedAt = DateTime.UtcNow,
                    Status = "Active",
                    TemporalKey = "Sukablak",
                    AttendanceRecords = attendanceRecords
                };

                // Create document with course-based naming: AttendanceEvent_{CourseCode}
                var documentId = $"AttendanceEvent_{sessionModel.CourseId}";

                // Check if document exists, if so, add this as a field
                var existingDoc = await FirestoreService.GetDocumentAsync<Dictionary<string, object>>("ATTENDANCE_EVENTS", documentId);

                if (existingDoc != null)
                {
                    // Add new event as a field within existing document
                    var eventFieldName = $"Event_{sessionModel.SessionId}_{sessionModel.StartTime:yyyyMMdd}";
                    await FirestoreService.AddOrUpdateFieldAsync("ATTENDANCE_EVENTS", documentId, eventFieldName, attendanceEventData);
                }
                else
                {
                    // Create new document with first event
                    var courseEventDocument = new Dictionary<string, object>
                    {
                        ["CourseCode"] = sessionModel.CourseId,
                        ["CourseName"] = sessionModel.CourseName,
                        ["CreatedAt"] = DateTime.UtcNow,
                        ["LastEventAt"] = DateTime.UtcNow,
                        [$"Event_{sessionModel.SessionId}_{sessionModel.StartTime:yyyyMMdd}"] = attendanceEventData
                    };

                    await FirestoreService.AddDocumentAsync("ATTENDANCE_EVENTS", courseEventDocument, documentId);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating Firebase attendance event: {ex.Message}");
                throw;
            }
        }
private async Task EndSessionAsync()
{
    try
    {
        isEndingSession = true;
       
        if (currentActiveSession != null)
        {
            // Stop temporal key timer
            temporalKeyUpdateTimer?.Dispose();
    
            // Update Firebase document to mark session as ended
            await UpdateFirebaseAttendanceEventStatus("Ended");

            // Remove from active sessions with persistence cleanup
            await SessionStateService.RemoveActiveSessionAsync(currentActiveSession.SessionId);
        }

        await SessionStateService.RemoveCurrentSessionAsync("default");
        await MigrateAttendanceDataToFirebase();

        isSessionStarted = false;
        currentActiveSession = null;
        selectedCourse = null;
        RefreshSessionLists();
        StateHasChanged();
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error ending session: {ex.Message}");
        isEndingSession = false;
    }
    finally
    {
        isEndingSession = false;
    }
}
        private async void EndSession()
        {
            try
            {
                isEndingSession = true;
           
                if (currentActiveSession != null)
                {
                    // Stop temporal key timer
                    temporalKeyUpdateTimer?.Dispose();
            
                    // Update Firebase document to mark session as ended
                    await UpdateFirebaseAttendanceEventStatus("Ended");

                    SessionStateService.RemoveActiveSession(currentActiveSession.SessionId);
                }

                SessionStateService.RemoveCurrentSession("default");
                await MigrateAttendanceDataToFirebase();

                isSessionStarted = false;
                currentActiveSession = null;
                selectedCourse = null;
                RefreshSessionLists();
                StateHasChanged();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error ending session: {ex.Message}");
                
                isEndingSession = false;
            }finally{
            isEndingSession = false;
            }
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
        private async Task UpdateFirebaseAttendanceEventStatus(string status)
        {
            try
            {
                var documentId = $"AttendanceEvent_{sessionModel.CourseId}";
                var eventFieldName = $"Event_{sessionModel.SessionId}_{sessionModel.StartTime:yyyyMMdd}";
                var statusFieldName = $"{eventFieldName}.Status";
                var endTimeFieldName = $"{eventFieldName}.ActualEndTime";

                await FirestoreService.AddOrUpdateFieldAsync("ATTENDANCE_EVENTS", documentId, statusFieldName, status);
                await FirestoreService.AddOrUpdateFieldAsync("ATTENDANCE_EVENTS", documentId, endTimeFieldName, DateTime.UtcNow);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating Firebase event status: {ex.Message}");
            }
        }

        private async Task<string> GenerateQrCodePayloadWithTemporalKey()
        {
            string temporalKey = useTemporalKeyRefresh ? 
                GenerateTemporalKey(sessionModel.SessionId, sessionModel.StartTime) : 
                string.Empty;
                
            return await QRCodeDecoder.EncodeSessionDataAsync(
                sessionModel.SessionId,
                sessionModel.CourseId,
                sessionModel.StartTime,
                sessionModel.Duration,
                useTemporalKeyRefresh,
                allowOfflineSync,
                securityFeatures,
                temporalKey
            );
        }

        private void OpenFloatingQR()
        {
            if (isSessionStarted && currentActiveSession != null)
            {
                OpenFloatingQRForSession(currentActiveSession);
            }
        }

        private void OpenFloatingQRForSession(ActiveSessionData session)
{
    floatingSessionData = new FloatingQRWindow.FloatingSessionData
    {
        SessionId = session.SessionId,
        CourseName = session.CourseName,
        CourseId = session.CourseId,
        StartTime = session.StartTime,
        EndTime = session.EndTime,
        Duration = session.Duration,
        QrCodePayload = session.QrCodePayload,
        Theme = session.Theme,
        UseTemporalKeyRefresh = session.UseTemporalKeyRefresh,
        SecurityFeatures = session.SecurityFeatures,
        TemporalKey = session.TemporalKey // Add this if FloatingSessionData supports it
    };
    showFloatingQR = true;
}

        private void CloseFloatingQR()
        {
            showFloatingQR = false;
            floatingSessionData = null;
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

        private async Task ProcessAttendanceCode(string qrCode)
        {
            try
            {
                // Create the main attendance session data for Supabase
                var attendanceSessionData = new
                {
                    session_id = sessionModel.SessionId,
                    course_code = sessionModel.CourseId,
                    start_time = sessionModel.StartTime,
                    duration = sessionModel.Duration,
                    expiration_time = sessionEndTime,
                    attendance_records = new List<object>(),
                    created_at = DateTime.UtcNow,
                    updated_at = DateTime.UtcNow
                };

                Console.WriteLine($"Attendance session data to be saved: {JsonSerializer.Serialize(attendanceSessionData, new JsonSerializerOptions { WriteIndented = true })}");

                // For testing - create a sample attendance record
                var testAttendanceRecord = new AttendanceRecord
                {
                    MatricNumber = "TEST001",
                    HasScannedAttendance = true,
                    IsOnlineScan = true
                };

                var result = await EdgeService.ProcessAttendanceAsync(qrCode, testAttendanceRecord);
        
                Console.WriteLine($"Processing attendance code: {qrCode}, result: {result.ToString()}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing attendance: {ex.Message}");
            }
        }
        private async Task EndSessionWithDataMigration()
        {
            try
            {
                isEndingSession = true;
        
                // Sync any remaining offline records first
               // await OfflineSyncService.SyncPendingRecordsAsync();
        
                // Migrate attendance data from Supabase to Firebase
                await MigrateAttendanceDataToFirebase();
        
                // Clean up Supabase working data (keep backup)
               // await ArchiveSupabaseSessionData();
        
                // End session normally
                 EndSession();
            }
            catch (Exception ex)
            {
                MID_HelperFunctions.DebugMessage($"Error ending session with migration: {ex.Message}", DebugClass.Exception);
            }
            finally
            {
                isEndingSession = false;
            }
        }

        private async Task MigrateAttendanceDataToFirebase()
        {
            // Get final attendance records from Supabase
            var finalAttendanceData = await AttendanceSessionService.GetActiveSessionsAsync();
    
            // Update Firebase with complete attendance records
            var documentId = $"AttendanceEvent_{sessionModel.CourseId}";
            var eventFieldName = $"Event_{sessionModel.SessionId}_{sessionModel.StartTime:yyyyMMdd}";
    
            await FirestoreService.AddOrUpdateFieldAsync(
                "ATTENDANCE_EVENTS", 
                documentId, 
                $"{eventFieldName}.FinalAttendanceRecords", 
                finalAttendanceData
            );
    
            await FirestoreService.AddOrUpdateFieldAsync(
                "ATTENDANCE_EVENTS", 
                documentId, 
                $"{eventFieldName}.Status", 
                "Completed"
            );
        }
        public void Dispose()
        {
            countdownTimer?.Dispose();
            temporalKeyUpdateTimer?.Dispose();
            SessionStateService.StateChanged -= OnStateChanged;
        }
    }
