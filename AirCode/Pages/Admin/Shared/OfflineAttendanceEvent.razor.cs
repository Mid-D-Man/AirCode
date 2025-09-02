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
using AirCode.Domain.Enums;
using AirCode.Domain.ValueObjects;
using AirCode.Models.Forms;
using AirCode.Utilities.DataStructures;
using AirCode.Utilities.HelperScripts;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using BatchSyncResult = AirCode.Domain.Entities.BatchSyncResult;
using Course = AirCode.Domain.Entities.Course;
using SyncResult = AirCode.Domain.Entities.SyncResult;
using SyncStatus = AirCode.Domain.Entities.SyncStatus;

namespace AirCode.Pages.Admin.Shared;

public partial class OfflineAttendanceEvent : ComponentBase, IDisposable
{
    #region Dependency Injection
    
    [Inject] private IJSRuntime JS { get; set; }
    [Inject] private ICryptographyService CryptographyService { get; set; }
    [Inject] private IOfflineSyncService OfflineSyncService { get; set; }
    [Inject] private IAttendanceSessionService AttendanceSessionService { get; set; }
    [Inject] private ICourseService CourseService { get; set; }
    [Inject] private IFirestoreService FirestoreService { get; set; }
    [Inject] private SessionStateService SessionStateService { get; set; }
    [Inject] private ConnectivityService ConnectivityService { get; set; }

    #endregion

    #region Core Session Management
    
    private OfflineSessionData currentOfflineSession = new();
    private List<OfflineAttendanceRecordModel> pendingRecords = new();
    private List<OfflineSessionData> allOfflineSessions = new();
    private DateTime sessionStartTime;
    private DateTime sessionEndTime;
    private bool isOfflineSessionActive = false;
    
    #endregion

    #region UI State Management
    
    private bool isCreatingOfflineSession = false;
    private bool isSyncInProgress = false;
    private bool showOfflineQR = false;
    private bool showSyncResults = false;
    private bool showOfflineFloatingQR = false;
    private bool showInfoPopup = false;
    private bool showCourseSelection = false;
    private string selectedTheme = "Standard";
    
    #endregion

    #region Course Selection
    
    private Course selectedCourse;
    
    #endregion

    #region Offline Configuration
    
    private bool useDeviceGuidCheck = false;
    private AdvancedSecurityFeatures securityFeatures = AdvancedSecurityFeatures.DeviceGuidCheck;
    private bool useAdvancedEncryption = true;
    private int maxOfflineStorageDays = 7;
    private int sessionDuration = 60; // Default 60 minutes
    
    #endregion

    #region Sync Management
    
    private BatchSyncResult lastSyncResult;
    private System.Threading.Timer periodicSyncTimer;
    private int syncIntervalMinutes = 15;
    
    #endregion

    #region Cryptographic Keys
    
    private string sessionEncryptionKey = string.Empty;
    private string deviceGuid = string.Empty;
    
    #endregion

    #region Info Popup Management
    
    private InfoPopup.InfoType currentInfoType = InfoPopup.InfoType.OfflineSync;
    
    #endregion

    #region Floating QR Code
    
    private QRCodeData floatingOfflineSessionData = new();

    #endregion

    #region Component Lifecycle

    protected override async Task OnInitializedAsync()
    {
        await InitializeConnectivityAsync();
        await InitializeOfflineEnvironmentAsync();
        await LoadExistingOfflineSessionsAsync();
        await StartPeriodicSyncAsync();
        
        StateHasChanged();
    }

    #endregion

    #region Connectivity Management

    private async Task InitializeConnectivityAsync()
    {
        try
        {
            await ConnectivityService.InitializeAsync();
            ConnectivityService.ConnectivityChanged += OnConnectivityChanged;
        }
        catch (Exception ex)
        {
         await MID_HelperFunctions.DebugMessageAsync($"Error initializing connectivity service: {ex.Message}",DebugClass.Exception);
        }
    }

    private void OnConnectivityChanged(ConnectivityStatus status)
    {
        InvokeAsync(() =>
        {
            StateHasChanged();
            
            // Auto-sync when coming back online if there are pending records
            if (status.IsOnline && HasPendingRecords())
            {
                _ = Task.Run(async () => await SyncOfflineDataAsync());
            }
        });
    }

    private async Task<bool> CheckConnectivityAsync()
    {
        try
        {
            var status = await ConnectivityService.GetConnectivityStatusAsync();
            return status.IsOnline;
        }
        catch
        {
            return false;
        }
    }

    #endregion

    #region Offline Environment Initialization

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
            
            MID_HelperFunctions.DebugMessage("Offline environment initialized successfully");
        }
        catch (Exception ex)
        {
            MID_HelperFunctions.DebugMessage($"Error initializing offline environment: {ex.Message}");
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
                sessionDuration = config?.SessionDuration ?? 60;
            }
        }
        catch (Exception ex)
        {
            MID_HelperFunctions.DebugMessage($"Error loading offline preferences: {ex.Message}");
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
            MID_HelperFunctions.DebugMessage($"Error loading offline sessions: {ex.Message}");
        }
    }

    #endregion

    #region Session Creation and Management

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
                CourseCode = selectedCourse.CourseCode,
                CourseName = selectedCourse.Name,
                StartTime = DateTime.UtcNow,
                Duration = sessionDuration,
                CreatedAt = DateTime.UtcNow.Date,
                OfflineSyncEnabled = true,
                SecurityFeatures = securityFeatures
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
                PendingAttendanceRecords = new List<OfflineAttendanceRecordModel>(),
                SyncStatus = SyncStatus.Pending
            };

            // Create offline session record in database if online
            if (ConnectivityService.IsOnline)
            {
                await OfflineSyncService.CreateOfflineSessionRecordAsync(sessionData);
            }

            // Store session locally with encryption
            await SaveOfflineSessionAsync(currentOfflineSession);
            allOfflineSessions.Add(currentOfflineSession);

            isOfflineSessionActive = true;
            showOfflineQR = true;
            
            MID_HelperFunctions.DebugMessage($"Offline session created: {sessionData.SessionId}");
        }
        catch (Exception ex)
        {
            MID_HelperFunctions.DebugMessage($"Error creating offline session: {ex.Message}");
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
            CourseId = sessionData.CourseCode,
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

    private async Task EndOfflineSessionAsync()
    {
        if (currentOfflineSession == null) return;

        try
        {
            // Attempt final sync before ending if online
            if (ConnectivityService.IsOnline && HasPendingRecords())
            {
                await SyncOfflineDataAsync();
            }

            isOfflineSessionActive = false;
            showOfflineQR = false;
            currentOfflineSession = null;
            selectedCourse = null;

            MID_HelperFunctions.DebugMessage("Offline session ended");
            StateHasChanged();
        }
        catch (Exception ex)
        {
            MID_HelperFunctions.DebugMessage($"Error ending offline session: {ex.Message}");
        }
    }

    #endregion

    #region Attendance Processing

    private async Task ProcessOfflineAttendanceAsync(string qrCode, string matricNumber)
    {
        if (!isOfflineSessionActive || currentOfflineSession == null) return;

        try
        {
            var attendanceRecord = new OfflineAttendanceRecordModel
            {
                Id = Guid.NewGuid().ToString(),
                SessionId = currentOfflineSession.SessionId,
                CourseCode = currentOfflineSession.SessionDetails.CourseCode,
                MatricNumber = matricNumber,
                DeviceGuid = deviceGuid,
                EncryptedQrPayload = qrCode,
             
                RecordedAt = DateTime.UtcNow,
                ScannedAt = DateTime.UtcNow,
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

            MID_HelperFunctions.DebugMessage($"Offline attendance recorded for {matricNumber}");
            StateHasChanged();
        }
        catch (Exception ex)
        {
            MID_HelperFunctions.DebugMessage($"Error processing offline attendance: {ex.Message}");
        }
    }

    #endregion

    #region Sync Operations

    private async Task SyncOfflineDataAsync()
    {
        // Check connectivity first
        if (!await CheckConnectivityAsync())
        {
            MID_HelperFunctions.DebugMessage("Cannot sync: Device is offline");
            return;
        }

        try
        {
            isSyncInProgress = true;
            StateHasChanged();

            var pendingSessions = allOfflineSessions.Where(s => s.SyncStatus == SyncStatus.Pending).ToList();
            var syncResults = new List<SyncResult>();

            foreach (var session in pendingSessions)
            {
                session.SyncStatus = SyncStatus.Pending;
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

            MID_HelperFunctions.DebugMessage($"Sync completed: {lastSyncResult.ProcessedSuccessfully}/{lastSyncResult.TotalRecords} successful");
        }
        catch (Exception ex)
        {
            MID_HelperFunctions.DebugMessage($"Error during sync: {ex.Message}");
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
                if (!isSyncInProgress && ConnectivityService.IsOnline && HasPendingRecords())
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

    #endregion

    #region Local Storage Management

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
            MID_HelperFunctions.DebugMessage($"Error saving offline session: {ex.Message}");
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
            MID_HelperFunctions.DebugMessage($"Error saving offline sessions list: {ex.Message}");
        }
    }

    #endregion

    #region UI Event Handlers

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

    private void ShowInfoPopup(InfoPopup.InfoType infoType)
    {
        currentInfoType = infoType;
        showInfoPopup = true;
    }

    private void CloseInfoPopup()
    {
        showInfoPopup = false;
    }

    private void OpenFloatingQRForOfflineSession()
    {
        if (currentOfflineSession != null)
        {
            floatingOfflineSessionData = new QRCodeData
            {
                Id = currentOfflineSession.SessionId,
                Content = JsonSerializer.Serialize(currentOfflineSession.SessionDetails),
                Theme = ConvertStringToTheme(selectedTheme),
                ValidDuration = sessionDuration
            };
            showOfflineFloatingQR = true;
        }
    }

    private void CloseFloatingOfflineQR()
    {
        showOfflineFloatingQR = false;
    }

    private void HandleOfflineQRCodeGenerated(QRCodeData qrData)
    {
        floatingOfflineSessionData = qrData;
        StateHasChanged();
    }

    private void HandleQRCodeGenerated(QRCodeData qrData)
    {
        // Handle QR code generation completion
        MID_HelperFunctions.DebugMessage($"QR Code generated for session: {qrData.Id}");
    }

    #endregion

    #region QR Code Generation

    private QRCodeBaseOptions GenerateOfflineQRCodeOptions()
    {
        var theme = ConvertStringToTheme(selectedTheme);
        
        return theme switch
        {
            QRCodeTheme.Gradient => new QRCodeGradientOptions
            {
                Content = floatingOfflineSessionData.Content,
                Size = 300,
                GradientColor1 = "#007bff",
                GradientColor2 = "#00bfff",
                Direction = GradientDirection.Radial
            },
            QRCodeTheme.Branded => new QRCodeBrandedOptions
            {
                Content = floatingOfflineSessionData.Content,
                Size = 300,
                LogoUrl = "/images/logo.png",
                LogoSizeRatio = 0.25f
            },
            QRCodeTheme.GradientWithLogo => new QRCodeGradientBrandedOptions
            {
                Content = floatingOfflineSessionData.Content,
                Size = 300,
                GradientColor1 = "#007bff",
                GradientColor2 = "#00bfff",
                Direction = GradientDirection.Radial,
                LogoUrl = "/images/logo.png",
                LogoSizeRatio = 0.25f
            },
            _ => new QRCodeBaseOptions
            {
                Content = floatingOfflineSessionData.Content,
                Size = 300
            }
        };
    }

    #endregion

    #region Helper Methods

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

    private string GetSyncStatusColor(SyncStatus status)
    {
        return status switch
        {
            SyncStatus.Pending => "blue",
            SyncStatus.Synced => "green",
            SyncStatus.Failed => "red",
            SyncStatus.Invalid => "gray",
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

    private string GetCountdownClass()
    {
        if (!isOfflineSessionActive) return "countdown-inactive";
        
        var remaining = sessionEndTime - DateTime.UtcNow;
        var totalMinutes = remaining.TotalMinutes;
        
        return totalMinutes switch
        {
            <= 5 => "countdown-critical",
            <= 15 => "countdown-warning",
            _ => "countdown-normal"
        };
    }

    private bool IsCountdownCritical()
    {
        if (!isOfflineSessionActive) return false;
        
        var remaining = sessionEndTime - DateTime.UtcNow;
        return remaining.TotalMinutes <= 5;
    }

    #endregion

    #region Configuration Properties

    private bool DeviceGuidCheck
    {
        get => securityFeatures.HasFlag(AdvancedSecurityFeatures.DeviceGuidCheck);
        set
        {
            if (value)
                securityFeatures |= AdvancedSecurityFeatures.DeviceGuidCheck;
            else
                securityFeatures &= ~AdvancedSecurityFeatures.DeviceGuidCheck;
        }
    }

    private bool AdvancedEncryption
    {
        get => useAdvancedEncryption;
        set => useAdvancedEncryption = value;
    }

    private int StorageRetention
    {
        get => maxOfflineStorageDays;
        set => maxOfflineStorageDays = value;
    }

    private int SyncFrequency
    {
        get => syncIntervalMinutes;
        set => syncIntervalMinutes = value;
    }

    private int OfflineStorage
    {
        get => maxOfflineStorageDays;
        set => maxOfflineStorageDays = value;
    }

    private int SyncInterval
    {
        get => syncIntervalMinutes;
        set => syncIntervalMinutes = value;
    }

    #endregion

    #region Disposal

    public void Dispose()
    {
        periodicSyncTimer?.Dispose();
        if (ConnectivityService != null)
        {
            ConnectivityService.ConnectivityChanged -= OnConnectivityChanged;
        }
    }

    #endregion
}