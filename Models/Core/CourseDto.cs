using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using AirCode.Domain.Enums;

namespace AirCode.Models.Core
{
    // Renamed from Course to CourseDto
    public class CourseDto
    {
        public string Id { get; set; }
        
        public string Name { get; set; }
        
        public string Department { get; set; }
        
        public LevelType Level { get; set; }
        
        public SemesterType Semester { get; set; }
        
        public List<CourseScheduleDto> Schedule { get; set; } = new List<CourseScheduleDto>();
        
        public List<SimpleLecturerDto> Lecturers { get; set; } = new List<SimpleLecturerDto>();
        
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }

    // Renamed to CourseScheduleDto
    public class CourseScheduleDto
    {
        public DayOfWeek Day { get; set; }
        
        public TimeSpan StartTime { get; set; }
        
        public TimeSpan EndTime { get; set; }
        
        public string Location { get; set; }
    }
    
    // Renamed to SimpleLecturerDto
    public class SimpleLecturerDto
    {
        public string Id { get; set; }
    
        public string Name { get; set; }
        
       
    }
}