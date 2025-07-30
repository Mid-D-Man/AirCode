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
              OfflineRecords = ]",
                         AllowOfflineConnectionAndSync = allowOfflineSync,
              SecurityFeatures = (int)securityFeatures                    };


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
                SyncStatus = 0, // or appropriate initial status
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
