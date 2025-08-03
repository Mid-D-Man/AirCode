using AirCode.Models.Attendance;
using AirCode.Models.Events;

namespace AirCode.Services.Attendance;
using AirCode.Models.QRCode;

    /// <summary>
    /// Interface for client-side offline attendance handling
    /// </summary>
    public interface IOfflineAttendanceClientService : IDisposable
    {
        #region Events
        
        event EventHandler<OfflineAttendanceEventArgs> OfflineAttendanceRecorded;
        event EventHandler<SyncStatusEventArgs> SyncStatusChanged;
        event EventHandler<NetworkStatusEventArgs> NetworkStatusChanged;
        
        #endregion

        #region Core Methods
        
        /// <summary>
        /// Initialize the service for a specific student
        /// </summary>
        /// <param name="matricNumber">Student's matric number</param>
        Task InitializeAsync(string matricNumber);
        
        /// <summary>
        /// Process QR code scan - handles both online and offline scenarios
        /// </summary>
        /// <param name="qrCodeContent">Raw QR code content</param>
        /// <returns>Attendance processing result</returns>
        Task<AttendanceResult> ProcessQRCodeScanAsync(string qrCodeContent);
        
        #endregion

        #region Status and Management
        
        /// <summary>
        /// Get current sync status information
        /// </summary>
        /// <returns>Sync status details</returns>
        Task<SyncStatusInfo> GetSyncStatusAsync();
        
        /// <summary>
        /// Manually trigger sync of offline records
        /// </summary>
        /// <returns>True if sync successful</returns>
        Task<bool> ManualSyncAsync();
        
        /// <summary>
        /// Clear all offline records (emergency function)
        /// </summary>
        /// <returns>True if cleared successfully</returns>
        Task<bool> ClearOfflineRecordsAsync();
        
        #endregion
    }
