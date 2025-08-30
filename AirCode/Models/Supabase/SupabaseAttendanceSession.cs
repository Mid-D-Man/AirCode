
using System.Text.Json;
using System.Text.Json.Serialization;
using AirCode.Utilities.HelperScripts;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace AirCode.Models.Supabase
{
    [Table("attendance_sessions")]
    public class SupabaseAttendanceSession : BaseModel
    {
        
        [PrimaryKey("id")]
        public long Id { get; set; }
        
        [Column("session_id")]
        public string SessionId { get; set; } = string.Empty;
        
        [Column("course_code")]
        public string CourseCode { get; set; } = string.Empty;
        
        [Column("start_time")]
        public DateTime StartTime { get; set; }
        
        [Column("duration")]
        public int Duration { get; set; }
        
        [Column("expiration_time")]
        public DateTime ExpirationTime { get; set; }
        
        [Column("attendance_records")]
        public string AttendanceRecords { get; set; } = "[]";
        
        [Column("created_at")]
        public DateTime CreatedAt { get; set; }
        
        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; }
        
        [Column("temporal_key")]
        public string TemporalKey { get; set; } = string.Empty;
        
        [Column("use_temporal_key_refresh")]
        public bool UseTemporalKeyRefresh { get; set; } = false;

        [Column("allow_offline_sync")]
        public bool AllowOfflineConnectionAndSync { get; set; } = true;

        [Column("security_features")]
        public int SecurityFeatures { get; set; } = 0;

    
        public List<AttendanceRecord> GetAttendanceRecords()
        {
            try
            {
                if (string.IsNullOrEmpty(AttendanceRecords) || AttendanceRecords == "[]")
                    return new List<AttendanceRecord>();

                // Handle both array and malformed JSON structures
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                };

                return JsonSerializer.Deserialize<List<AttendanceRecord>>(AttendanceRecords, options) 
                       ?? new List<AttendanceRecord>();
            }
            catch (JsonException ex)
            {
                //replace logger
                MID_HelperFunctions.DebugMessage(
                    $"Failed to deserialize attendance records for session. Raw data: {AttendanceRecords?.Length }",
                    DebugClass.Exception); 
                   
                return new List<AttendanceRecord>();
            }
        }

        public void SetAttendanceRecords(List<AttendanceRecord> records)
        {
            AttendanceRecords = JsonSerializer.Serialize(records);
        }
    }

    [Table("offline_attendance_sessions")]
    public class SupabaseOfflineAttendanceSession : BaseModel
    {
        [PrimaryKey("id")]
        public long Id { get; set; }
        
        [Column("session_id")]
        public string SessionId { get; set; } = string.Empty;
        
        [Column("course_code")]
        public string CourseCode { get; set; } = string.Empty;
        
        [Column("start_time")]
        public DateTime StartTime { get; set; }
        
        [Column("duration")]
        public int Duration { get; set; }
        
        [Column("expiration_time")]
        public DateTime ExpirationTime { get; set; }
        
        [Column("created_at")]
        public DateTime CreatedAt { get; set; }
        
        [Column("offline_records")]
        public string OfflineRecords { get; set; } = "[]";
        
        [Column("sync_status")]
        public int SyncStatus { get; set; } = 0;
        
        [Column("allow_offline_sync")]
        public bool AllowOfflineSync { get; set; } = true;
        
        [Column("security_features")]
        public int SecurityFeatures { get; set; } = 0;

        public List<OfflineAttendanceRecord> GetOfflineRecords()
        {
            try
            {
                return JsonSerializer.Deserialize<List<OfflineAttendanceRecord>>(OfflineRecords) ?? new List<OfflineAttendanceRecord>();
            }
            catch
            {
                return new List<OfflineAttendanceRecord>();
            }
        }

        public void SetOfflineRecords(List<OfflineAttendanceRecord> records)
        {
            OfflineRecords = JsonSerializer.Serialize(records);
        }
    }

    [Table("archived_attendance_data")]
    public class ArchivedAttendanceData : BaseModel
    {
        [PrimaryKey("id")]
        public long Id { get; set; }
        
        [Column("course_code")]
        public string CourseCode { get; set; } = string.Empty;
        
        [Column("archived_data")]
        public string ArchivedData { get; set; } = string.Empty;
        
        [Column("archived_at")]
        public DateTime ArchivedAt { get; set; }
        
        [Column("data_type")]
        public string DataType { get; set; } = "attendance_events"; // "attendance_events" or "offline_sessions"
        
        [Column("compression_used")]
        public bool CompressionUsed { get; set; } = false;
    }

    public class AttendanceRecord
    {
        public string MatricNumber { get; set; } = string.Empty;
        public bool HasScannedAttendance { get; set; }
        public DateTime? ScanTime { get; set; }
        public bool IsOnlineScan { get; set; }
        public string? DeviceGUID { get; set; } = string.Empty;
    }

    public class OfflineAttendanceRecord
    {
        public string MatricNumber { get; set; } = string.Empty;
        public bool HasScannedAttendance { get; set; }
        public DateTime? ScanTime { get; set; }
        public string? DeviceGUID { get; set; } = string.Empty;
        public int SyncStatus { get; set; } = 0; // 0 = pending, 1 = synced, 2 = failed
    }
}