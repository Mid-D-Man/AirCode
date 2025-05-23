
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
        public string CourseId { get; init; }
        
        [Required]
        [StringLength(100, ErrorMessage = "Course name cannot exceed 100 characters")]
        public string Name { get; init; }
        [StringLength(10, ErrorMessage = "Course code cannot exceed 1- characters")]
        public string CoursesCode { get; init; }//i.e CYB415,CSC484 e.t.c
        
        [StringLength(5, ErrorMessage = "Course code cannot exceed 1- characters")]
        public string DepartmentId { get; init; }//department id is CYB,CS,AED e.t.c
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
        public Course(string courseId, string name,string code, string departmentId, 
            LevelType level, SemesterType semester, byte creditUnits,
            CourseSchedule schedule, List<string> lecturerIds,
            DateTime? lastModified = null, string modifiedBy = "")
        {
            CourseId = courseId;
            Name = name;
            CoursesCode = code;
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
        public static Course Create(string courseId, string name,string code, string departmentId,
            LevelType level, SemesterType semester, byte creditUnits,
            CourseSchedule schedule, List<string> lecturerIds = null)
        {
            return new Course(courseId, name,code, departmentId, level, semester, 
                creditUnits, schedule, lecturerIds, DateTime.UtcNow, "System");
        }
        
        // Method to create updated version
        public Course WithModification(string modifiedBy)
        {
            return new Course(CourseId, Name,CoursesCode, DepartmentId, Level, Semester, 
                CreditUnits, Schedule, LecturerIds.ToList(), DateTime.UtcNow, modifiedBy);
        }
    }
}