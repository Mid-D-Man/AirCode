// Domain/Entities/AttendanceEntity.cs
using System.ComponentModel.DataAnnotations;
using AirCode.Domain.Interfaces;
using AirCode.Domain.Enums;
using AirCode.Utilities.HelperScripts;

namespace AirCode.Domain.Entities
{
    /// <summary>
    /// Immutable attendance record - inherits ISecureEntity for audit trail
    /// </summary>
    public record AttendanceRecord : ISecureEntity
    {
        [Required]
        public string SessionId { get; init; } = string.Empty;
        
        [Required]
        public string MatricNumber { get; init; } = string.Empty;
        
        [Required]
        public string CourseCode { get; init; } = string.Empty;
        
        public bool HasScannedAttendance { get; init; }
        public DateTime? ScanTime { get; init; }
        public bool IsOnlineScan { get; init; }
        public string? DeviceGUID { get; init; } = string.Empty;
        public AttendanceType AttendanceType { get; init; } = AttendanceType.Present;
        public AttendanceVerificationMethod VerificationMethod { get; init; } = AttendanceVerificationMethod.QRCode;
        
        // Security attributes
        public string SecurityToken { get; init; }
        public DateTime LastModified { get; init; }
        public string ModifiedBy { get; init; }

        public override string ToString() => 
            MID_HelperFunctions.GetStructOrClassMemberValues(this);
    }

    /// <summary>
    /// Consolidated offline attendance record model
    /// </summary>
    public class OfflineAttendanceRecord
    {
        public string Id { get; init; } = Guid.NewGuid().ToString();
        public string SessionId { get; init; } = string.Empty;
        public string CourseCode { get; init; } = string.Empty;
        public string MatricNumber { get; init; } = string.Empty;
        public string DeviceGuid { get; init; } = string.Empty;
        public DateTime ScanTime { get; init; }
        public string EncryptedQrPayload { get; init; } = string.Empty;
        public string TemporalKey { get; init; } = string.Empty;
        public bool UseTemporalKeyRefresh { get; init; }
        public int SecurityFeatures { get; init; }
        public DateTime RecordedAt { get; init; }
        public SyncStatus SyncStatus { get; init; } = SyncStatus.Pending;
        public int SyncAttempts { get; init; } = 0;
        public string ErrorDetails { get; init; } = string.Empty;

        public override string ToString() => 
            MID_HelperFunctions.GetStructOrClassMemberValues(this);
    }

    /// <summary>
    /// Session data for active attendance sessions
    /// </summary>
    public record SessionData : ISecureEntity
    {
        public string SessionId { get; init; } = Guid.NewGuid().ToString("N");
        public string CourseCode { get; init; } = string.Empty;
        public string CourseName { get; init; } = string.Empty;
        public DateTime StartTime { get; init; }
        public DateTime EndTime { get; init; }
        public int Duration { get; init; }
        public string QrCodePayload { get; init; } = string.Empty;
        public string Theme { get; init; } = string.Empty;
        public bool UseTemporalKeyRefresh { get; init; }
        public AdvancedSecurityFeatures SecurityFeatures { get; init; } = AdvancedSecurityFeatures.Default;
        public string TemporalKey { get; init; } = string.Empty;
        
        // Security attributes
        public string SecurityToken { get; init; }
        public DateTime LastModified { get; init; }
        public string ModifiedBy { get; init; }

        public override string ToString() => 
            MID_HelperFunctions.GetStructOrClassMemberValues(this);
    }
}
