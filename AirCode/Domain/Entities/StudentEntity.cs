using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using AirCode.Domain.Enums;
using AirCode.Domain.Interfaces;

namespace AirCode.Domain.Entities
{
    // Changed from record to class for mutability
    public class Student : ISecureEntity
    {
        [Required]
        public string StudentId { get; init; }//immutable because one black man cannot change matriculation number
        [Required]
        public string Name { get; set; } // Mutable
        public LevelType Level { get; set; } // Mutable
        public string DepartmentId { get; set; } // Mutable
        public ClassRepStatus RepStatus { get; set; } // Mutable
        
        // Changed to a getter-only property with a new list
        // that can be modified but reference can't be changed
        public List<CourseEnrollment> Enrollments { get; } = new List<CourseEnrollment>();
        
        // Security attributes - still immutable
        public string SecurityToken { get; init; }
        public DateTime LastModified { get; init; }
        public string ModifiedBy { get; init; }
        
        // Methods to manage enrollments
        public void AddEnrollment(CourseEnrollment enrollment)
        {
            Enrollments.Add(enrollment);
        }
        
        public void RemoveEnrollment(string courseId)
        {
            var enrollment = Enrollments.Find(e => e.CourseId == courseId);
            if (enrollment != null)
                Enrollments.Remove(enrollment);
        }
        
        public void UpdateEnrollmentStatus(string courseId, CourseEnrollmentStatus status)
        {
            var index = Enrollments.FindIndex(e => e.CourseId == courseId);
            if (index >= 0)
            {
                var existing = Enrollments[index];
                Enrollments[index] = new CourseEnrollment
                {
                    CourseId = existing.CourseId,
                    SessionId = existing.SessionId,
                    Semester = existing.Semester,
                    Status = status
                };
            }
        }
    }

    // Keep this as record since individual enrollments shouldn't change
    public record CourseEnrollment
    {
        public string CourseId { get; init; }
        public CourseEnrollmentStatus Status { get; init; }
        public string SessionId { get; init; }
        public SemesterType Semester { get; init; }
    }
}