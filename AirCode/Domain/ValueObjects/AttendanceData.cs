// Domain/ValueObjects/AttendanceData.cs

using AirCode.Domain.Enums;
using AirCode.Utilities.HelperScripts;

namespace AirCode.Domain.ValueObjects
{
    public readonly record struct AttendanceChartData
    {
        public DateTime Date { get; init; }
        public double AttendanceRate { get; init; }
        public int PresentCount { get; init; }
        public int TotalCount { get; init; }
    }

    public readonly record struct CourseAttendanceStats
    {
        public string CourseCode { get; init; }
        public string CourseName { get; init; }
        public string DepartmentId { get; init; }
        public LevelType Level { get; init; }
        public SemesterType Semester { get; init; }
        public byte CreditUnits { get; init; }
        public double AttendancePercentage { get; init; }
        public int ClassesAttended { get; init; }
        public int TotalClasses { get; init; }
        public int TotalAbsences { get; init; }
        public int ConsecutiveAbsences { get; init; }
        public DateTime? LastAttendanceDate { get; init; }
        public bool IsCarryOver { get; init; }
    }

    public enum SyncStatus
    {
        Pending,
        Processing,
        Synced,
        Failed,
        Expired
    }

    public class SyncResult
    {
        public bool Success { get; init; }
        public string Message { get; init; } = string.Empty;
        public string ErrorMessage { get; init; } = string.Empty;
        public string ErrorCode { get; init; } = string.Empty;
        public DateTime ProcessedAt { get; init; } = DateTime.UtcNow;
        public object Data { get; init; }
        public int RetryAttempt { get; init; }

        public override string ToString() => 
            MID_HelperFunctions.GetStructOrClassMemberValues(this);
    }

    public class BatchSyncResult
    {
        public int TotalRecords { get; init; }
        public int ProcessedSuccessfully { get; init; }
        public int Failed { get; init; }
        public List<SyncResult> IndividualResults { get; init; } = new();
        public bool AllSuccessful => Failed == 0;
        public double SuccessRate => TotalRecords > 0 ? (double)ProcessedSuccessfully / TotalRecords : 0;

        public override string ToString() => 
            MID_HelperFunctions.GetStructOrClassMemberValues(this);
    }
}
