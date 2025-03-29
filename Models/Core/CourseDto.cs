using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using AirCode.Domain.Enums;

namespace AirCode.Models.Core
{
    // Renamed from Course to CourseDto
    public class CourseDto
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
        public List<CourseScheduleDto> Schedule { get; set; } = new List<CourseScheduleDto>();
        
        [JsonProperty("lecturers")]
        public List<SimpleLecturerDto> Lecturers { get; set; } = new List<SimpleLecturerDto>();
        
        [JsonProperty("createdAt")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        
        [JsonProperty("updatedAt")]
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }

    // Renamed to CourseScheduleDto
    public class CourseScheduleDto
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
    
    // Renamed to SimpleLecturerDto
    public class SimpleLecturerDto
    {
        [JsonProperty("id")]
        public string Id { get; set; }
    
        [JsonProperty("name")]
        public string Name { get; set; }
        
        [JsonProperty("status")]
        public LecturerStatus Status { get; set; }
    }
}