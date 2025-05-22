using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using AirCode.Domain.Enums;

namespace AirCode.Models.Core
{
    /// <summary>
    /// Data Transfer Object for Course entity
    /// Updated to include CreditUnits field
    /// </summary>
    public class CourseDto
    {
        public string Id { get; set; }
        
        [Required(ErrorMessage = "Course name is required")]
        [StringLength(100, ErrorMessage = "Course name cannot exceed 100 characters")]
        public string Name { get; set; }
        
        [Required(ErrorMessage = "Department is required")]
        public string Department { get; set; }
        
        [Required(ErrorMessage = "Level is required")]
        public LevelType Level { get; set; }
        
        [Required(ErrorMessage = "Semester is required")]
        public SemesterType Semester { get; set; }
        
        [Required(ErrorMessage = "Credit units is required")]
        [Range(1, 10, ErrorMessage = "Credit units must be between 1 and 10")]
        public byte CreditUnits { get; set; } = 2; // Default to 2 credit units
        
        public List<CourseScheduleDto> Schedule { get; set; } = new List<CourseScheduleDto>();
        
        public List<SimpleLecturerDto> Lecturers { get; set; } = new List<SimpleLecturerDto>();
        
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        
        // Security fields (for immutable Course entity)
        public string SecurityToken { get; set; }
        public DateTime LastModified { get; set; }
        public string ModifiedBy { get; set; }
    }

    /// <summary>
    /// Schedule item for a course
    /// </summary>
    public class CourseScheduleDto
    {
        public DayOfWeek Day { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public string Location { get; set; }
        
        /// <summary>
        /// Validate that the schedule item is valid
        /// </summary>
        public bool IsValid()
        {
            return EndTime > StartTime && !string.IsNullOrWhiteSpace(Location);
        }
        
        /// <summary>
        /// Get a formatted string representation of the schedule
        /// </summary>
        public string ToFormattedString()
        {
            return $"{Day}: {StartTime:hh\\:mm} - {EndTime:hh\\:mm} @ {Location}";
        }
    }

    /// <summary>
    /// Simplified lecturer information for course display
    /// </summary>
    public class SimpleLecturerDto
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
    }
}