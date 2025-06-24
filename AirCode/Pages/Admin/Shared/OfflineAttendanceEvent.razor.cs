using System.Text.Json;
using AirCode.Domain.Entities;
using AirCode.Models.QRCode;
using AirCode.Models.Supabase;
using AirCode.Services.Attendance;
using AirCode.Services.Courses;
using AirCode.Services.Cryptography;
using AirCode.Services.Firebase;
using AirCode.Components.SharedPrefabs.QrCode;
using AirCode.Components.SharedPrefabs.Others;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using SessionData = AirCode.Services.Attendance.SessionData;
using Course = AirCode.Domain.Entities.Course;
namespace AirCode.Pages.Admin.Shared;

public partial class OfflineAttendanceEvent : ComponentBase, IDisposable
{
    [Inject] private IJSRuntime JS { get; set; }
    [Inject] private ICryptographyService CryptographyService { get; set; }
    [Inject] private IOfflineSyncService OfflineSyncService { get; set; }
    [Inject] private IAttendanceSessionService AttendanceSessionService { get; set; }
    [Inject] private ICourseService CourseService { get; set; }
    [Inject] private IFirestoreService FirestoreService { get; set; }
    [Inject] private SessionStateService SessionStateService { get; set; }

    // Core session management
    private OfflineSessionData currentOfflineSession = new();
    private List<OfflineAttendanceRecord> pendingRecords = new();
    private List<OfflineSessionData> allOfflineSessions = new();
    
    // UI State Management
    private bool isCreatingOfflineSession = false;
    private bool isSyncInProgress = false;
    private bool showOfflineQR = false;
    private bool showSyncResults = false;
    private string selectedTheme = "Standard";
    
    // Course Selection
    private Course selectedCourse;
    private bool showCourseSelection = false;
    
    // Offline Configuration
    private AdvancedSecurityFeatures securityFeatures = AdvancedSecurityFeatures.DeviceGuidCheck;
    private bool useAdvancedEncryption = true;
    private int maxOfflineStorageDays = 7;
    
    // Sync Management
    private BatchSyncResult lastSyncResult;
    private System.Threading.Timer periodicSyncTimer;
    private int syncIntervalMinutes = 15;
    
    // Cryptographic Keys
    private string sessionEncryptionKey = string.Empty;
    private string deviceGuid = string.Empty;
    
    // Session Tracking
    private DateTime sessionStartTime;
    private DateTime sessionEndTime;
    private bool isOfflineSessionActive = false;

    protected override async Task OnInitializedAsync()
    {
        await InitializeOfflineEnvironmentAsync();
        await LoadExistingOfflineSessionsAsync();
        await StartPeriodicSyncAsync();
        
        StateHasChanged();
    }

    private async Task InitializeOfflineEnvironmentAsync()
    {
        try
        {
            // Generate device-specific GUID for session isolation
            deviceGuid = await JS.InvokeAsync<string>("generateDeviceGuid") ?? Guid.NewGuid().ToString();
            
            // Initialize cryptographic components
            sessionEncryptionKey = await CryptographyService.GenerateAesKey(256);
            
            // Load offline storage preferences
            await LoadOfflinePreferencesAsync();
            
            Console.WriteLine("Offline environment initialized successfully");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error initializing offline environment: {ex.Message}");
        }
    }

    private async Task LoadOfflinePreferencesAsync()
    {
        try
        {
            var preferences = await JS.InvokeAsync<string>("localStorage.getItem", "offline_attendance_preferences");
            if (!string.IsNullOrEmpty(preferences))
            {
                var config = JsonSerializer.Deserialize<OfflinePreferences>(preferences);
                maxOfflineStorageDays = config?.MaxStorageDays ?? 7;
                syncIntervalMinutes = config?.SyncIntervalMinutes ?? 15;
                useAdvancedEncryption = config?.UseAdvancedEncryption ?? true;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading offline preferences: {ex.Message}");
        }
    }

    private async Task LoadExistingOfflineSessionsAsync()
    {
        try
        {
            var storedSessions = await JS.InvokeAsync<string>("localStorage.getItem", "offline_sessions");
            if (!string.IsNullOrEmpty(storedSessions))
            {
                allOfflineSessions = JsonSerializer.Deserialize<List<OfflineSessionData>>(storedSessions) ?? new();
                
                // Clean up expired sessions
                var cutoffDate = DateTime.UtcNow.AddDays(-maxOfflineStorageDays);
                allOfflineSessions = allOfflineSessions.Where(s => s.CreatedAt > cutoffDate).ToList();
                
                await SaveOfflineSessionsAsync();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading offline sessions: {ex.Message}");
        }
    }

    private async Task CreateOfflineSessionAsync()
    {
        if (selectedCourse == null) return;

        try
        {
            isCreatingOfflineSession = true;
            StateHasChanged();

            var sessionData = new SessionData
            {
                SessionId = Guid.NewGuid().ToString("N"),
                CourseId = selectedCourse.CourseCode,
                CourseName = selectedCourse.Name,
                StartTime = DateTime.UtcNow,
                Duration = 60, // Default 1 hour for offline sessions
                Date = DateTime.UtcNow.Date
            };

            sessionStartTime = DateTime.UtcNow;
            sessionEndTime = sessionStartTime.AddMinutes(sessionData.Duration);

            // Generate temporal key for offline validation
            string temporalKey = await GenerateOfflineTemporalKeyAsync(sessionData.SessionId);
            
            // Create encrypted QR payload for offline use
            var qrPayload = await CreateEncryptedOfflinePayloadAsync(sessionData, temporalKey);

            currentOfflineSession = new OfflineSessionData
            {
                SessionId = sessionData.SessionId,
                CreatedAt = DateTime.UtcNow,
                SessionDetails = sessionData,
                PendingAttendanceRecords = new List<OfflineAttendanceRecord>(),
                SyncStatus = SyncStatus.Pending
            };

            // Store session locally with encryption
            await SaveOfflineSessionAsync(currentOfflineSession);
            allOfflineSessions.Add(currentOfflineSession);

            isOfflineSessionActive = true;
            showOfflineQR = true;
            
            Console.WriteLine($"Offline session created: {sessionData.SessionId}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error creating offline session: {ex.Message}");
        }
        finally
        {
            isCreatingOfflineSession = false;
            StateHasChanged();
        }
    }

    private async Task<string> GenerateOfflineTemporalKeyAsync(string sessionId)
    {
        var keyData = $"OFFLINE:{sessionId}:{DateTime.UtcNow:yyyyMMddHHmm}:{deviceGuid}";
        return await CryptographyService.HashData(keyData);
    }

    private async Task<string> CreateEncryptedOfflinePayloadAsync(SessionData sessionData, string temporalKey)
    {
        var payloadData = new
        {
            SessionId = sessionData.SessionId,
            CourseId = sessionData.CourseId,
            StartTime = sessionData.StartTime,
            Duration = sessionData.Duration,
            TemporalKey = temporalKey,
            DeviceGuid = deviceGuid,
            SecurityFeatures = (int)securityFeatures,
            OfflineMode = true,
            CreatedAt = DateTime.UtcNow
        };

        var jsonPayload = JsonSerializer.Serialize(payloadData);
        
        if (useAdvancedEncryption)
        {
            var iv = await CryptographyService.GenerateIv();
            var encryptedPayload = await CryptographyService.EncryptData(jsonPayload, sessionEncryptionKey, iv);
            return $"{iv}:{encryptedPayload}";
        }

        return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(jsonPayload));
    }

    private async Task ProcessOfflineAttendanceAsync(string qrCode, string matricNumber)
    {
        if (!isOfflineSessionActive || currentOfflineSession == null) return;

        try
        {
            var attendanceRecord = new OfflineAttendanceRecord
            {
                Id = Guid.NewGuid().ToString(),
                SessionId = currentOfflineSession.SessionId,
                CourseCode = currentOfflineSession.SessionDetails.CourseId,
                MatricNumber = matricNumber,
                DeviceGuid = deviceGuid,
                ScanTime = DateTime.UtcNow,
                EncryptedQrPayload = qrCode,
                TemporalKey = await GenerateOfflineTemporalKeyAsync(currentOfflineSession.SessionId),
                UseTemporalKeyRefresh = false,
                SecurityFeatures = (int)securityFeatures,
                RecordedAt = DateTime.UtcNow,
                ScannedAt = DateTime.UtcNow,
                DeviceId = deviceGuid,
                Status = SyncStatus.Pending,
                SyncStatus = SyncStatus.Pending
            };

            // Encrypt sensitive data
            if (useAdvancedEncryption)
            {
                var iv = await CryptographyService.GenerateIv();
                attendanceRecord.EncryptedQrPayload = await CryptographyService.EncryptData(
                    attendanceRecord.EncryptedQrPayload, sessionEncryptionKey, iv);
            }

            currentOfflineSession.PendingAttendanceRecords.Add(attendanceRecord);
            pendingRecords.Add(attendanceRecord);

            await SaveOfflineSessionAsync(currentOfflineSession);

            Console.WriteLine($"Offline attendance recorded for {matricNumber}");
            StateHasChanged();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error processing offline attendance: {ex.Message}");
        }
    }

    private async Task SyncOfflineDataAsync()
    {
        try
        {
            isSyncInProgress = true;
            StateHasChanged();

            var pendingSessions = allOfflineSessions.Where(s => s.SyncStatus == SyncStatus.Pending).ToList();
            var syncResults = new List<SyncResult>();

            foreach (var session in pendingSessions)
            {
                session.SyncStatus = SyncStatus.Processing;
                var result = await OfflineSyncService.SyncOfflineSessionAsync(session);
                syncResults.Add(result);

                if (result.Success)
                {
                    session.SyncStatus = SyncStatus.Synced;
                    // Remove synced records from local storage
                    session.PendingAttendanceRecords.Clear();
                }
                else
                {
                    session.SyncStatus = SyncStatus.Failed;
                }
            }

            lastSyncResult = new BatchSyncResult
            {
                TotalRecords = syncResults.Count,
                ProcessedSuccessfully = syncResults.Count(r => r.Success),
                Failed = syncResults.Count(r => !r.Success),
                IndividualResults = syncResults
            };

            await SaveOfflineSessionsAsync();
            showSyncResults = true;

            Console.WriteLine($"Sync completed: {lastSyncResult.ProcessedSuccessfully}/{lastSyncResult.TotalRecords} successful");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during sync: {ex.Message}");
        }
        finally
        {
            isSyncInProgress = false;
            StateHasChanged();
        }
    }

    private async Task StartPeriodicSyncAsync()
    {
        await OfflineSyncService.SchedulePeriodicSync();
        
        var interval = TimeSpan.FromMinutes(syncIntervalMinutes);
        periodicSyncTimer = new System.Threading.Timer(
            async _ => await InvokeAsync(async () => {
                if (!isSyncInProgress && HasPendingRecords())
                {
                    await SyncOfflineDataAsync();
                }
            }),
            null,
            interval,
            interval
        );
    }

    private bool HasPendingRecords()
    {
        return allOfflineSessions.Any(s => s.SyncStatus == SyncStatus.Pending && s.PendingAttendanceRecords.Any());
    }

    private async Task SaveOfflineSessionAsync(OfflineSessionData session)
    {
        try
        {
            // Encrypt session data before storage
            var jsonData = JsonSerializer.Serialize(session);
            if (useAdvancedEncryption)
            {
                var iv = await CryptographyService.GenerateIv();
                var encryptedData = await CryptographyService.EncryptData(jsonData, sessionEncryptionKey, iv);
                await JS.InvokeVoidAsync("localStorage.setItem", $"offline_session_{session.SessionId}", $"{iv}:{encryptedData}");
            }
            else
            {
                await JS.InvokeVoidAsync("localStorage.setItem", $"offline_session_{session.SessionId}", jsonData);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving offline session: {ex.Message}");
        }
    }

    private async Task SaveOfflineSessionsAsync()
    {
        try
        {
            var jsonData = JsonSerializer.Serialize(allOfflineSessions);
            await JS.InvokeVoidAsync("localStorage.setItem", "offline_sessions", jsonData);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving offline sessions list: {ex.Message}");
        }
    }

    private async Task EndOfflineSessionAsync()
    {
        if (currentOfflineSession == null) return;

        try
        {
            // Attempt final sync before ending
            if (HasPendingRecords())
            {
                await SyncOfflineDataAsync();
            }

            isOfflineSessionActive = false;
            showOfflineQR = false;
            currentOfflineSession = null;
            selectedCourse = null;

            Console.WriteLine("Offline session ended");
            StateHasChanged();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error ending offline session: {ex.Message}");
        }
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
        showCourseSelection = false;
        StateHasChanged();
    }

    private void CloseSyncResults()
    {
        showSyncResults = false;
    }

    private string GetSyncStatusColor(SyncStatus status)
    {
        return status switch
        {
            SyncStatus.Pending => "orange",
            SyncStatus.Processing => "blue",
            SyncStatus.Synced => "green",
            SyncStatus.Failed => "red",
            SyncStatus.Expired => "gray",
            _ => "black"
        };
    }

    private string FormatTimeRemaining()
    {
        if (!isOfflineSessionActive) return "--:--:--";

        var remaining = sessionEndTime - DateTime.UtcNow;
        if (remaining.TotalSeconds <= 0)
        {
            _ = EndOfflineSessionAsync();
            return "00:00:00";
        }

        return $"{remaining.Hours:00}:{remaining.Minutes:00}:{remaining.Seconds:00}";
    }

    public void Dispose()
    {
        periodicSyncTimer?.Dispose();
    }

    // Helper classes for configuration
    private class OfflinePreferences
    {
        public int MaxStorageDays { get; set; } = 7;
        public int SyncIntervalMinutes { get; set; } = 15;
        public bool UseAdvancedEncryption { get; set; } = true;
    }
}