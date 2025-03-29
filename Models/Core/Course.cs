using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using AirCode.Domain.Enums;

namespace AirCode.Models.Core
{
    public class Course
    {
        [JsonProperty("id")]
        public string Id { get; set; }
        
        [JsonProperty("name")]
        public string Name { get; set; }
        
        [JsonProperty("department")]
        public string Department { get; set; }
        
        [JsonProperty("level")]
        public LevelType Level { get; set; }
        
        [JsonProperty("semester")]
        public SemesterType Semester { get; set; }
        
        [JsonProperty("schedule")]
        public List<CourseSchedule> Schedule { get; set; } = new List<CourseSchedule>();
        
        [JsonProperty("lecturers")]
        public List<SimpleLecturer> Lecturers { get; set; } = new List<SimpleLecturer>();
        
        [JsonProperty("createdAt")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        
        [JsonProperty("updatedAt")]
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }

    public class CourseSchedule
    {
        [JsonProperty("day")]
        public DayOfWeek Day { get; set; }
        
        [JsonProperty("startTime")]
        public TimeSpan StartTime { get; set; }
        
        [JsonProperty("endTime")]
        public TimeSpan EndTime { get; set; }
        
        [JsonProperty("location")]
        public string Location { get; set; }
    }
    
    public class SimpleLecturer
    {
        [JsonProperty("id")]
        public string Id { get; set; }
    
        [JsonProperty("name")]
        public string Name { get; set; }
        
        [JsonProperty("status")]
        public LecturerStatus Status { get; set; }
    }
}