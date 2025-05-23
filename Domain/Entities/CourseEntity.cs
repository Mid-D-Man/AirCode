using System.ComponentModel.DataAnnotations;
using AirCode.Domain.Enums;
using AirCode.Domain.ValueObjects;

namespace AirCode.Domain.Entities
{
    /// <summary>
    /// Simplified Course entity without security interface
    /// </summary>
    public class Course
    {
        [Required]
        [StringLength(10, ErrorMessage = "Course code cannot exceed 10 characters")]
        public string CourseCode { get; init; } // Primary identifier: CYB415, CSC484, etc.
        
        [Required]
        [StringLength(100, ErrorMessage = "Course name cannot exceed 100 characters")]
        public string Name { get; init; }
        
        [StringLength(5, ErrorMessage = "Department code cannot exceed 5 characters")]
        public string DepartmentId { get; init; } // CYB, CS, AED, etc.
        
        public LevelType Level { get; init; }
        
        [Required]
        [Range(1, 10, ErrorMessage = "Credit units must be between 1 and 10")]
        public byte CreditUnits { get; init; }

        public SemesterType Semester { get; init; }
        public CourseSchedule Schedule { get; init; }
        public IReadOnlyList<string> LecturerIds { get; init; }
        
        // Basic modification tracking
        public DateTime LastModified { get; init; }
        public string ModifiedBy { get; init; }

        // Constructor to ensure immutability
        public Course(string courseCode, string name, string departmentId, 
            LevelType level, SemesterType semester, byte creditUnits,
            CourseSchedule schedule, List<string> lecturerIds,
            DateTime? lastModified = null, string modifiedBy = "")
        {
            CourseCode = courseCode;
            Name = name;
            DepartmentId = departmentId;
            Level = level;
            Semester = semester;
            CreditUnits = creditUnits;
            Schedule = schedule;
            LecturerIds = lecturerIds?.AsReadOnly() ?? new List<string>().AsReadOnly();
            LastModified = lastModified ?? DateTime.UtcNow;
            ModifiedBy = modifiedBy;
        }
        
        // Factory method for creating new courses
        public static Course Create(string courseCode, string name, string departmentId,
            LevelType level, SemesterType semester, byte creditUnits,
            CourseSchedule schedule, List<string> lecturerIds = null)
        {
            return new Course(courseCode, name, departmentId, level, semester, 
                creditUnits, schedule, lecturerIds, DateTime.UtcNow, "System");
        }
        
        // Method to create updated version
        public Course WithModification(string modifiedBy)
        {
            return new Course(CourseCode, Name, DepartmentId, Level, Semester, 
                CreditUnits, Schedule, LecturerIds.ToList(), DateTime.UtcNow, modifiedBy);
        }
    }
}