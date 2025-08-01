using AirCode.Domain.Enums;
using AirCode.Utilities.HelperScripts;

namespace AirCode.Domain.Entities;
/// <summary>
/// Course attendance statistics for individual students
/// </summary>
public class CourseAttendanceStats
{
    public string CourseCode { get; set; } = string.Empty;
    public string CourseName { get; set; } = string.Empty;
    public string DepartmentId { get; set; } = string.Empty;
    public LevelType Level { get; set; }
    public SemesterType Semester { get; set; }
    public byte CreditUnits { get; set; }
    public double AttendancePercentage { get; set; }
    public int ClassesAttended { get; set; }
    public int TotalClasses { get; set; }
    public int TotalAbsences { get; set; }
    public int ConsecutiveAbsences { get; set; }
    public DateTime? LastAttendanceDate { get; set; }
    public bool IsCarryOver { get; set; }

    public override string ToString() => 
        MID_HelperFunctions.GetStructOrClassMemberValues(this);
}