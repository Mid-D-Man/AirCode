
using System;
using System.Collections.Generic;
using AirCode.Domain.Enums;
using AirCode.Domain.Interfaces;

namespace AirCode.Domain.Entities
{
    //and start off with courses please or semester , idon no any one ,flip the coin
    //no need for this  but keep for ref if needed,or even u can create this as offline credentials
    //but far less detailed yeah we made a buch of unnesessasry stuff remove them once ya done
    public record UserEntity : ISecureEntity
    {
        public string Id { get; init; } = Guid.NewGuid().ToString();
        public string FirstName { get; init; }
        public string? MiddleName { get; init; }
        public string LastName { get; init; }
        public DateTime DateOfBirth { get; init; }
        public string Gender { get; init; }
        public string Email { get; init; }
        public string Username { get; init; }
        public string? MatriculationNumber { get; init; }
        public string? AdminId { get; init; }
        public string Department { get; init; }
        
        // Role information
        public UserRole Role { get; init; }
        
      
        public List<string> AssignedCourseIds { get; init; } = new List<string>();
        
        // Security attributes
        public string SecurityToken { get; init; }
        public DateTime LastModified { get; init; }
        public string ModifiedBy { get; init; }
        
        // Helper property for full name
        public string FullName => $"{FirstName} {(string.IsNullOrEmpty(MiddleName) ? "" : MiddleName + " ")}{LastName}";

        // Helper methods for role-based checks
        public bool CanEditAttendance() => Role == UserRole.SuperiorAdmin || Role == UserRole.CourseRepAdmin;
        public bool CanStartAttendanceEvent() => Role != UserRole.Student;
        public bool CanViewAllAttendance() => Role == UserRole.SuperiorAdmin;
        public bool CanEditCourseInfo() => Role == UserRole.SuperiorAdmin || Role == UserRole.LecturerAdmin;
    }
    public class StudentLevelInfo
    {
        public string MatricNumber { get; set; }
        public string Email { get; set; }
        public string Level { get; set; }
        public DateTime RegistrationDate { get; set; }
    }
    /// <summary>
    /// Enhanced user credentials model with role-specific data
    /// </summary>
    public class OfflineUserCredentials
    {
        public string UserId { get; set; }
        public string Role { get; set; }
        public DateTime IssuedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public string DeviceGuid { get; set; }
    
        // Role-specific properties
        public string LecturerId { get; set; } // For LecturerAdmin role
        public string MatricNumber { get; set; } // For Student/CourseRepAdmin roles
    }
}