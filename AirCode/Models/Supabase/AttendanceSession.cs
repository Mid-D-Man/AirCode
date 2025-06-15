using System;
using System.Text.Json;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace AirCode.Models.Supabase
{
    [Table("attendance_sessions")]
    public class AttendanceSession : BaseModel
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
        
        [Column("lecture_id")]
        public string? LectureId { get; set; }
        
        [Column("attendance_records")]
        public string AttendanceRecords { get; set; } = "[]";
        
        [Column("created_at")]
        public DateTime CreatedAt { get; set; }
        
        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; }
        
        [Column("temporal_key")]
        public string TemporalKey { get; set; } = string.Empty;
        
    [Column("use_temporal_key_refresh")]
public bool UseTemporalKeyRefresh { get; set; } = false; // Renamed from TemporalKeyEnabled

[Column("allow_offline_sync")]
public bool AllowOfflineConnectionAndSync { get; set; } = true; // New field

[Column("security_features")]
public int SecurityFeatures { get; set; } = 0; // Maps to AdvancedSecurityFeatures enum

        public List<AttendanceRecord> GetAttendanceRecords()
        {
            try
            {
                return JsonSerializer.Deserialize<List<AttendanceRecord>>(AttendanceRecords) ?? new List<AttendanceRecord>();
            }
            catch
            {
                return new List<AttendanceRecord>();
            }
        }

        public void SetAttendanceRecords(List<AttendanceRecord> records)
        {
            AttendanceRecords = JsonSerializer.Serialize(records);
        }
    }

    public class AttendanceRecord
    {
        public string MatricNumber { get; set; } = string.Empty;
        public bool HasScannedAttendance { get; set; }
        public DateTime? ScanTime { get; set; }
        public bool IsOnlineScan { get; set; }
    }
}
