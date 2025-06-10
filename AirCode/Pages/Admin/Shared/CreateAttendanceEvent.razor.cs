using AirCode.Services.SupaBase;
using AirCode.Utilities.DataStructures;
using AirCode.Utilities.HelperScripts;
using Microsoft.AspNetCore.Components;

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

                qrCodePayload = await GenerateQrCodePayload();
                sessionEndTime = DateTime.UtcNow.AddMinutes(sessionModel.Duration);

                // Create Firebase attendance event document
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
                    await ProcessAttendanceCode(JsonHelper.Serialize(attendanceEventData));

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
                    LogoUrl = "/icon-192.png",
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
                // Add your attendance processing logic here
                // This could involve:
                // 1. Decrypting the QR code data
                // 2. Validating the session
                // 3. Recording attendance
                // 4. Showing success/failure feedback
                var randomguy = new AttendanceRecord
                {
                    MatricNumber = "SukaBlak",
                    HasScannedAttendance = true,
                    IsOnlineScan = true
                };
                var result =  await EdgeService.ProcessAttendanceAsync(qrCode,randomguy);
            
                Console.WriteLine($"Processing attendance code: {qrCode}, and found result {result.ToString()}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing attendance: {ex.Message}");
              
            }
        }
        public void Dispose()
        {
            countdownTimer?.Dispose();
            SessionStateService.StateChanged -= OnStateChanged;
        }
    }
