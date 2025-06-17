using System.Text.Json;
using AirCode.Models.Supabase;
using AirCode.Services.SupaBase;
using AirCode.Utilities.DataStructures;
using AirCode.Utilities.HelperScripts;
using Microsoft.AspNetCore.Components;
using AttendanceRecord = AirCode.Services.SupaBase.AttendanceRecord;
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
    public partial class CreateAttendanceEvent : ComponentBase, IDisposable
    {
        [Inject] private IJSRuntime JS { get; set; }
        [Inject] private QRCodeDecoder QRCodeDecoder { get; set; }
        [Inject] private SessionStateService SessionStateService { get; set; }
        [Inject] private ICourseService CourseService { get; set; }
        [Inject] private IFirestoreService FirestoreService { get; set; }
//for testing
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
      private bool useTemporalKeyRefresh = false; // Renamed from temporalKeyEnabled // Testing toggle
          private bool allowOfflineSync = true; // NEW - Add this
    private AdvancedSecurityFeatures securityFeatures = AdvancedSecurityFeatures.Default; // NEW - Add this
    
        private System.Threading.Timer temporalKeyUpdateTimer;

        
        protected override void OnInitialized()
        {
            sessionModel.Duration = 30;

            RefreshSessionLists();
            CheckForExistingSession();

            SessionStateService.StateChanged += OnStateChanged;

            countdownTimer = new System.Threading.Timer(
                _ => InvokeAsync(() => {
                    SessionStateService.CleanupExpiredSessions();
                    RefreshSessionLists();
                    StateHasChanged();
                }),
                null,
                TimeSpan.Zero,
                TimeSpan.FromSeconds(1)
            );
        }

// Add these variables to your existing CreateAttendanceEvent.razor.cs file

// Info popup variables
private bool showInfoPopup = false;
private InfoType currentInfoType;

// Temporal key refresh interval (in minutes)
private int temporalKeyRefreshInterval = 5; // Default to 5 minutes

// Enum for info popup types
public enum InfoType
{
    OfflineSync,
    TemporalKeyRefresh,
    RefreshInterval,
    SecurityFeatures
}


private void ShowInfoPopup(InfoType infoType)
{
    currentInfoType = infoType;
    showInfoPopup = true;
}

private void CloseInfoPopup()
{
    showInfoPopup = false;
}

private string GetInfoTitle()
{
    return currentInfoType switch
    {
        InfoType.OfflineSync => "Allow Offline Sync",
        InfoType.TemporalKeyRefresh => "Temporal Key Refresh",
        InfoType.RefreshInterval => "Refresh Interval",
        InfoType.SecurityFeatures => "Security Features",
        _ => "Information"
    };
}

private string GetInfoContent()
{
    return currentInfoType switch
    {
        InfoType.OfflineSync => "When enabled, students can scan QR codes even when offline. Their attendance will be synced when they reconnect to the internet. This ensures attendance is captured even in poor network conditions.",
        
        InfoType.TemporalKeyRefresh => "Temporal Key Refresh adds an extra layer of security by periodically changing the QR code's internal key. This prevents old QR codes from being reused and helps prevent attendance fraud. The QR code appearance remains the same, but the internal security key changes at regular intervals.",
        
        InfoType.RefreshInterval => "This setting determines how often the temporal key is refreshed. Shorter intervals (2-5 minutes) provide better security but may cause more network traffic. Longer intervals (10-30 minutes) are more efficient but slightly less secure. 5 minutes is recommended for most scenarios.",
        
        InfoType.SecurityFeatures => "Security features provide additional validation methods:\n\n• Default: Basic security with standard QR code validation\n• Device GUID Check: Validates that the same device isn't used to scan multiple times, helping prevent attendance fraud through device sharing",
        
        _ => "No information available."
    };
}

// Update the StartTemporalKeyUpdateTimer method to use the interval setting
private void StartTemporalKeyUpdateTimer()
{
    // Use the selected interval instead of hardcoded 2 minutes
    var interval = TimeSpan.FromMinutes(temporalKeyRefreshInterval);
    
    temporalKeyUpdateTimer = new System.Threading.Timer(
        async _ => await UpdateTemporalKey(),
        null,
        interval, // First update after the specified interval
        interval  // Then every specified interval
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

        private void CheckForExistingSession()
        {
            var existingSession = SessionStateService.GetCurrentSession("default");
            if (existingSession != null)
            {
                var activeSession = allActiveSessions.FirstOrDefault(s =>
                    s.CourseId == existingSession.CourseId &&
                    DateTime.UtcNow < s.EndTime);

                if (activeSession != null)
                {
                    RestoreExistingSession(activeSession, existingSession);
                }
            }
        }

        private async void RestoreExistingSession(ActiveSessionData activeSession, SessionData sessionData)
        {
            sessionModel = sessionData;
            currentActiveSession = activeSession;
            isSessionStarted = true;
            qrCodePayload = activeSession.QrCodePayload;
            selectedTheme = activeSession.Theme;
            sessionEndTime = activeSession.EndTime;

            // Restore selected course
            if (!string.IsNullOrEmpty(sessionData.CourseId))
            {
                selectedCourse = await CourseService.GetCourseByIdAsync(sessionData.CourseId);
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

       private async void StartSession()
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

        // Create Supabase attendance session
          var attendanceSession = new AttendanceSession
    {
        SessionId = sessionModel.SessionId,
        CourseCode = sessionModel.CourseId,
        StartTime = sessionModel.StartTime,
        Duration = sessionModel.Duration,
        ExpirationTime = sessionEndTime,
        
        AttendanceRecords = "[]",
        
        // Updated mappings:
        UseTemporalKeyRefresh = useTemporalKeyRefresh,
        AllowOfflineConnectionAndSync = allowOfflineSync, // Add this variable
        SecurityFeatures = (int)securityFeatures, // Cast enum to int
        TemporalKey = useTemporalKeyRefresh ? GenerateTemporalKey(sessionModel.SessionId, sessionModel.StartTime) : string.Empty
    };

        // Generate initial temporal key if enabled
        if (useTemporalKeyRefresh)
        {
            attendanceSession.TemporalKey = GenerateTemporalKey(sessionModel.SessionId, sessionModel.StartTime);
        }

        // Save to Supabase
        var savedSession = await AttendanceSessionService.CreateSessionAsync(attendanceSession);
        
        // Generate QR code payload
        qrCodePayload = await GenerateQrCodePayload();

        // Create Firebase attendance event document (keep existing functionality)
        await CreateFirebaseAttendanceEvent();

        // Add to active sessions via service
        currentActiveSession = new ActiveSessionData
        {
            SessionId = sessionModel.SessionId,
            CourseName = sessionModel.CourseName,
            CourseId = sessionModel.CourseId,
            StartTime = sessionModel.StartTime,
            EndTime = sessionEndTime,
            Duration = sessionModel.Duration,
            QrCodePayload = qrCodePayload,
            Theme = selectedTheme
        };

        SessionStateService.AddActiveSession(currentActiveSession);
        SessionStateService.UpdateCurrentSession("default", sessionModel);

        // Start temporal key update timer if enabled
        if (useTemporalKeyRefresh)
        {
            StartTemporalKeyUpdateTimer();
        }

        isSessionStarted = true;
        RefreshSessionLists();
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error starting session: {ex.Message}");
        // Handle error appropriately
    }
    finally
    {
        isCreatingSession = false;
        StateHasChanged();
    }
}

// ADD THESE NEW METHODS:
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

private void StartTemporalKeyUpdateTimer()
{
    // Update temporal key every 2 minutes when enabled
    temporalKeyUpdateTimer = new System.Threading.Timer(
        async _ => await UpdateTemporalKey(),
        null,
        TimeSpan.FromMinutes(2), // First update after 2 minutes
        TimeSpan.FromMinutes(2)  // Then every 2 minutes
    );
}

private async Task UpdateTemporalKey()
{
    if (!useTemporalKeyRefresh || !isSessionStarted || currentActiveSession == null)
        return;

    try
    {
        string newTemporalKey = GenerateTemporalKey(sessionModel.SessionId, sessionModel.StartTime);
        
        await AttendanceSessionService.UpdateTemporalKeyAsync(sessionModel.SessionId, newTemporalKey);
        
        Console.WriteLine($"Updated temporal key for session: {sessionModel.SessionId}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error updating temporal key: {ex.Message}");
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
                throw; // Re-throw to handle in calling method
            }
        }

        private async void EndSession()
        {
            try
            {
                if (currentActiveSession != null)
                {
                    // Stop temporal key timer
                    temporalKeyUpdateTimer?.Dispose();
            
                    // Update Firebase document to mark session as ended
                    await UpdateFirebaseAttendanceEventStatus("Ended");

                    SessionStateService.RemoveActiveSession(currentActiveSession.SessionId);
                }

                SessionStateService.RemoveCurrentSession("default");

                isSessionStarted = false;
                currentActiveSession = null;
                selectedCourse = null;
                RefreshSessionLists();
                StateHasChanged();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error ending session: {ex.Message}");
            }
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

        private async Task<string> GenerateQrCodePayload()
        {
            return await QRCodeDecoder.EncodeSessionDataAsync(
                sessionModel.SessionId,
                sessionModel.CourseId,
                sessionModel.StartTime,
                sessionModel.Duration);
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
                Theme = session.Theme
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
                    lecture_id = (string)null, // Set to null or assign if you have lecturer info
                    attendance_records = new List<object>(), // Empty initially, will be populated as students scan
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

                // Test with legacy method (will be replaced with new payload structure)
                var result = await EdgeService.ProcessAttendanceAsync(qrCode, testAttendanceRecord);
        
                Console.WriteLine($"Processing attendance code: {qrCode}, result: {result.ToString()}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing attendance: {ex.Message}");
            }
        }
        public void Dispose()
        {
            countdownTimer?.Dispose();
            temporalKeyUpdateTimer?.Dispose();
            SessionStateService.StateChanged -= OnStateChanged;
        }
    }
