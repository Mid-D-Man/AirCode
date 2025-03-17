// AirQrCode/Models/Course.cs
using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace AirCode.Models
{
    public class Course
    {
        [JsonProperty("id")]
        public string Id { get; set; }
        
        [JsonProperty("name")]
        public string Name { get; set; }
        
        [JsonProperty("semester")]
        public int Semester { get; set; }
        
        [JsonProperty("level")]
        public string Level { get; set; }
        
        [JsonProperty("schedule")]
        public List<CourseSchedule> Schedule { get; set; } = new List<CourseSchedule>();
        
        [JsonProperty("lecturers")]
        public List<Lecturer> Lecturers { get; set; } = new List<Lecturer>();

        
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
    public class Lecturer
    {
        [JsonProperty("id")]
        public string Id { get; set; }
    
        [JsonProperty("name")]
        public string Name { get; set; }
    }

}