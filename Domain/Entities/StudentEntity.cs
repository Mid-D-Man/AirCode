using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using AirCode.Domain.Enums;
using AirCode.Domain.Interfaces;

namespace AirCode.Domain.Entities
{
    
    public record Student : ISecureEntity
    {
        [Required]
        public string StudentId { get; init; }
        [Required]
        public string Name { get; init; }
        public LevelType Level { get; init; }
        public string DepartmentId { get; init; }
        public ClassRepStatus RepStatus { get; init; }
        
        // Comprehensive course enrollment tracking
        public List<CourseEnrollment> Enrollments { get; init; }
        
        // Security attributes
        public string SecurityToken { get; init; }
        public DateTime LastModified { get; init; }
        public string ModifiedBy { get; init; }
    }

    public record CourseEnrollment
    {
        public string CourseId { get; init; }
        public CourseEnrollmentStatus Status { get; init; }
        public string SessionId { get; init; }
        public SemesterType Semester { get; init; }
    }
}