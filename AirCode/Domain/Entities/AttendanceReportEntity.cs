// Domain/Entities/ReportEntity.cs 

using AirCode.Domain.Enums;
using AirCode.Domain.Interfaces;
using AirCode.Utilities.HelperScripts;

namespace AirCode.Domain.Entities
{
    /// <summary>
    /// Consolidated attendance report structures
    /// </summary>
    public record AttendanceReport : ISecureEntity
    {
        public string CourseCode { get; init; } = string.Empty;
        public LevelType CourseLevel { get; init; }
        public DateTime GeneratedAt { get; init; }
        public int TotalSessions { get; init; }
        public int TotalStudentsEnrolled { get; set; }
        public double AverageAttendancePercentage { get; set; }
        public int StudentsWithPerfectAttendance { get; set; }
        public int StudentsWithPoorAttendance { get; set; }
        public List<StudentAttendanceReport> StudentReports { get; init; } = new();
        
        // Security attributes
        public string SecurityToken { get; init; }
        public DateTime LastModified { get; init; }
        public string ModifiedBy { get; init; }

        public override string ToString() => 
            MID_HelperFunctions.GetStructOrClassMemberValues(this);
    }

    public record StudentAttendanceReport
    {
        public string MatricNumber { get; set; } = string.Empty;
        public LevelType StudentLevel { get; set; }
        public int TotalPresent { get; set; }
        public int TotalAbsent { get; set; }
        public double AttendancePercentage { get; set; }
        public List<SessionAttendanceRecord> SessionAttendance { get; set; } = new();

        public override string ToString() => 
            MID_HelperFunctions.GetStructOrClassMemberValues(this);
    }

    public record SessionAttendanceRecord
    {
        public string SessionId { get; init; } = string.Empty;
        public DateTime SessionDate { get; init; }
        public int Duration { get; init; }
        public bool IsPresent { get; init; }
        public DateTime? ScanTime { get; init; }
        public bool IsOnlineScan { get; init; }
        public string? DeviceGUID { get; init; }

        public override string ToString() => 
            MID_HelperFunctions.GetStructOrClassMemberValues(this);
    }
}
