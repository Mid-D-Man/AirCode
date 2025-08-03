namespace AirCode.Services.Attendance;
using AirCode.Domain.Entities;
using AirCode.Models.QRCode;
using AirCode.Services.Attendance;
using AirCode.Services.SupaBase;
using AirCode.Utilities.HelperScripts;
using Microsoft.Extensions.Logging;
using System.Net.NetworkInformation;

    /// <summary>
    /// Client-side service for handling offline attendance scanning and management
    /// </summary>
    public class OfflineAttendanceClientService : IOfflineAttendanceClientService
    {
        private readonly IOfflineSyncService _offlineSyncService;
        private readonly ISupabaseEdgeFunctionService _edgeFunctionService;
        private readonly QRCodeDecoder _qrCodeDecoder;
        private readonly ILogger<OfflineAttendanceClientService> _logger;
        
        private Timer _syncTimer;
        private bool _isOnline = true;
        
        public event EventHandler<OfflineAttendanceEventArgs> OfflineAttendanceRecorded;
        public event EventHandler<SyncStatusEventArgs> SyncStatusChanged;
        public event EventHandler<NetworkStatusEventArgs> NetworkStatusChanged;

        public OfflineAttendanceClientService(
            IOfflineSyncService offlineSyncService,
            ISupabaseEdgeFunctionService edgeFunctionService,
            QRCodeDecoder qrCodeDecoder,
            ILogger<OfflineAttendanceClientService> logger)
        {
            _offlineSyncService = offlineSyncService;
            _edgeFunctionService = edgeFunctionService;
            _qrCodeDecoder = qrCodeDecoder;
            _logger = logger;
            
            InitializeNetworkMonitoring();
        }

        #region Public Methods

        /// <summary>
        /// Initialize the service and start monitoring
        /// </summary>
        public async Task InitializeAsync(string matricNumber)
        {
            try
            {
                // Store matric number for offline use
                await _offlineSyncService.StoreMatricNumberAsync(matricNumber);
                
                // Check initial network status
                _isOnline = await CheckNetworkConnectivityAsync();
                
                // Start periodic sync timer
                StartPeriodicSync();
                
                // Try to sync any existing offline records
                if (_isOnline)
                {
                    _ = Task.Run(async () => await TrySyncPendingRecordsAsync());
                }
                
                _logger.LogInformation("OfflineAttendanceClientService initialized for student: {MatricNumber}", matricNumber);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize OfflineAttendanceClientService");
                throw;
            }
        }

        /// <summary>
        /// Process QR code scan - handles both online and offline scenarios
        /// </summary>
        public async Task<AttendanceResult> ProcessQRCodeScanAsync(string qrCodeContent)
        {
            try
            {
                _logger.LogInformation("Processing QR code scan");
                
                // Check network connectivity
                var isOnline = await CheckNetworkConnectivityAsync();
                
                if (isOnline)
                {
                    // Try online processing first
                    var onlineResult = await ProcessOnlineAttendanceAsync(qrCodeContent);
                    if (onlineResult.Success)
                    {
                        return onlineResult;
                    }
                    
                    // If online processing fails, fall back to offline
                    _logger.LogWarning("Online processing failed, falling back to offline: {Error}", onlineResult.Message);
                }
                
                // Process offline
                return await ProcessOfflineAttendanceAsync(qrCodeContent);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing QR code scan");
                return new AttendanceResult
                {
                    Success = false,
                    Message = "An error occurred while processing your attendance. Please try again.",
                    IsOfflineMode = !_isOnline
                };
            }
        }

        /// <summary>
        /// Get current sync status information
        /// </summary>
        public async Task<SyncStatusInfo> GetSyncStatusAsync()
        {
            try
            {
                var pendingCount = await _offlineSyncService.GetPendingOfflineRecordsCountAsync();
                var hasOfflineRecords = await _offlineSyncService.HasPendingOfflineRecordsAsync();
                
                return new SyncStatusInfo
                {
                    IsOnline = _isOnline,
                    PendingRecordsCount = pendingCount,
                    HasPendingRecords = hasOfflineRecords,
                    LastSyncAttempt = DateTime.UtcNow // TODO: Store actual last sync time
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get sync status");
                return new SyncStatusInfo
                {
                    IsOnline = _isOnline,
                    PendingRecordsCount = 0,
                    HasPendingRecords = false,
                    LastSyncAttempt = null
                };
            }
        }

        /// <summary>
        /// Manually trigger sync of offline records
        /// </summary>
        public async Task<bool> ManualSyncAsync()
        {
            try
            {
                if (!await CheckNetworkConnectivityAsync())
                {
                    return false;
                }
                
                NotifySyncStatusChanged("Syncing offline records...", true);
                
                var result = await _offlineSyncService.SyncPendingRecordsAsync();
                
                var message = result ? "All offline records synced successfully" : "Some records failed to sync";
                NotifySyncStatusChanged(message, false);
                
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Manual sync failed");
                NotifySyncStatusChanged("Sync failed: " + ex.Message, false);
                return false;
            }
        }

        /// <summary>
        /// Clear all offline records (emergency function)
        /// </summary>
        public async Task<bool> ClearOfflineRecordsAsync()
        {
            try
            {
                var result = await _offlineSyncService.ClearAllOfflineRecordsAsync();
                if (result)
                {
                    NotifySyncStatusChanged("All offline records cleared", false);
                }
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to clear offline records");
                return false;
            }
        }

        #endregion

        #region Private Methods

        private async Task<AttendanceResult> ProcessOnlineAttendanceAsync(string qrCodeContent)
        {
            try
            {
                // Create edge function request using existing decoder
                var edgeRequest = await _qrCodeDecoder.CreateEdgeFunctionRequestAsync(qrCodeContent, null);
                if (edgeRequest == null)
                {
                    return new AttendanceResult
                    {
                        Success = false,
                        Message = "Invalid or expired QR code",
                        IsOfflineMode = false
                    };
                }

                // Process through online edge function
                var result = await _edgeFunctionService.ProcessAttendanceWithPayloadAsync(edgeRequest);
                
                return new AttendanceResult
                {
                    Success = result.Success,
                    Message = result.Message,
                    SessionData = result.SessionData,
                    IsOfflineMode = false,
                    ErrorCode = result.ErrorCode
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Online attendance processing failed");
                return new AttendanceResult
                {
                    Success = false,
                    Message = "Network error occurred",
                    IsOfflineMode = false
                };
            }
        }

        private async Task<AttendanceResult> ProcessOfflineAttendanceAsync(string qrCodeContent)
        {
            try
            {
                _logger.LogInformation("Processing attendance in offline mode");
                
                // Store encrypted QR payload for later sync
                var success = await _offlineSyncService.StoreOfflineAttendanceAsync(qrCodeContent);
                
                if (success)
                {
                    // Notify that offline attendance was recorded
                    NotifyOfflineAttendanceRecorded(qrCodeContent);
                    
                    return new AttendanceResult
                    {
                        Success = true,
                        Message = "Attendance recorded offline. It will be synced when connection is restored.",
                        IsOfflineMode = true,
                        RequiresSync = true
                    };
                }
                else
                {
                    return new AttendanceResult
                    {
                        Success = false,
                        Message = "Failed to store offline attendance. Please try again.",
                        IsOfflineMode = true
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Offline attendance processing failed");
                return new AttendanceResult
                {
                    Success = false,
                    Message = "Failed to process offline attendance",
                    IsOfflineMode = true
                };
            }
        }

        private async Task<bool> CheckNetworkConnectivityAsync()
        {
            try
            {
                using var ping = new Ping();
                var reply = await ping.SendPingAsync("8.8.8.8", 3000);
                var isOnline = reply.Status == IPStatus.Success;
                
                if (_isOnline != isOnline)
                {
                    _isOnline = isOnline;
                    NotifyNetworkStatusChanged(isOnline);
                    
                    // If we just came online, try to sync
                    if (isOnline)
                    {
                        _ = Task.Run(async () => await TrySyncPendingRecordsAsync());
                    }
                }
                
                return isOnline;
            }
            catch
            {
                return false;
            }
        }

        private async Task TrySyncPendingRecordsAsync()
        {
            try
            {
                if (await _offlineSyncService.HasPendingOfflineRecordsAsync())
                {
                    NotifySyncStatusChanged("Syncing offline records...", true);
                    
                    var result = await _offlineSyncService.SyncPendingRecordsAsync();
                    
                    var message = result 
                        ? "Offline records synced successfully" 
                        : "Some offline records could not be synced";
                    
                    NotifySyncStatusChanged(message, false);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to sync pending records");
                NotifySyncStatusChanged("Sync failed: " + ex.Message, false);
            }
        }

        private void InitializeNetworkMonitoring()
        {
            // Monitor network changes
            NetworkChange.NetworkAvailabilityChanged += async (sender, e) =>
            {
                await CheckNetworkConnectivityAsync();
            };
        }

        private void StartPeriodicSync()
        {
            // Check and sync every 2 minutes
            _syncTimer = new Timer(async _ =>
            {
                if (await CheckNetworkConnectivityAsync())
                {
                    await TrySyncPendingRecordsAsync();
                }
            }, null, TimeSpan.Zero, TimeSpan.FromMinutes(2));
        }

        private void NotifyOfflineAttendanceRecorded(string qrCodeContent)
        {
            OfflineAttendanceRecorded?.Invoke(this, new OfflineAttendanceEventArgs
            {
                QRCodeContent = qrCodeContent,
                Timestamp = DateTime.UtcNow
            });
        }

        private void NotifySyncStatusChanged(string message, bool isInProgress)
        {
            SyncStatusChanged?.Invoke(this, new SyncStatusEventArgs
            {
                Message = message,
                IsInProgress = isInProgress,
                Timestamp = DateTime.UtcNow
            });
        }

        private void NotifyNetworkStatusChanged(bool isOnline)
        {
            NetworkStatusChanged?.Invoke(this, new NetworkStatusEventArgs
            {
                IsOnline = isOnline,
                Timestamp = DateTime.UtcNow
            });
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            _syncTimer?.Dispose();
        }

        #endregion
    }

    #region Event Args and Result Classes

    public class AttendanceResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? ErrorCode { get; set; }
        public QRCodePayloadData? SessionData { get; set; }
        public bool IsOfflineMode { get; set; }
        public bool RequiresSync { get; set; }
    }

    public class SyncStatusInfo
    {
        public bool IsOnline { get; set; }
        public int PendingRecordsCount { get; set; }
        public bool HasPendingRecords { get; set; }
        public DateTime? LastSyncAttempt { get; set; }
    }

    public class OfflineAttendanceEventArgs : EventArgs
    {
        public string QRCodeContent { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
    }

    public class SyncStatusEventArgs : EventArgs
    {
        public string Message { get; set; } = string.Empty;
        public bool IsInProgress { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class NetworkStatusEventArgs : EventArgs
    {
        public bool IsOnline { get; set; }
        public DateTime Timestamp { get; set; }
    }

    #endregion
