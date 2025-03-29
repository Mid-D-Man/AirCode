using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using AirCode.Domain.Interfaces;

namespace AirCode.Domain.Entities
{
    public record CourseAttendanceReport : ISecureEntity
    {
        [Required]
        public string CourseId { get; init; }
        public int TotalSessions { get; init; }
        public Dictionary<string, double> StudentAttendancePercentages { get; init; }
        public double AverageAttendanceRate { get; init; }
        
        // Security attributes
        public string SecurityToken { get; init; }
        public DateTime LastModified { get; init; }
        public string ModifiedBy { get; init; }
    }

    public record SemesterAttendanceReport : ISecureEntity
    {
        [Required]
        public string SemesterId { get; init; }
        public List<CourseAttendanceReport> CourseReports { get; init; }
        public double OverallAttendanceRate { get; init; }
        
        // Security attributes
        public string SecurityToken { get; init; }
        public DateTime LastModified { get; init; }
        public string ModifiedBy { get; init; }
    }
}