using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using AirCode.Domain.Enums;
using AirCode.Domain.Interfaces;

namespace AirCode.Domain.Entities
{
    public record AttendanceEvent : ISecureEntity
    {
        [Required]
        public string AttendanceEventId { get; init; }
        [Required]
        public string CourseCode { get; init; }
        public DateTime Date { get; init; }
        public List<AttendanceRecord> Records { get; init; }
        
        // New properties for session details
        public SessionMode SessionMode { get; init; }
        public AttendanceVerificationMethod VerificationMethod { get; init; }
        public string Location { get; init; }
        public bool IsOfflineSync { get; init; }
        
        // Verification and tracking details
        public string InitiatedBy { get; init; }  // Who started the attendance (lecturer, rep, etc.)
        public bool IsVerified { get; init; }
        
        // Security attributes
        public string SecurityToken { get; init; }
        public DateTime LastModified { get; init; }
        public string ModifiedBy { get; init; }
    }

    public record AttendanceRecord : ISecureEntity
    {
        [Required]
        public string StudentId { get; init; }
        [Required]
        public string CourseCode { get; init; }
        public DateTime Timestamp { get; init; }
        public string Location { get; set; }
        public AttendanceType Type { get; set; }
        
        // Additional verification details
        public AttendanceVerificationMethod VerificationMethod { get; init; }
        public bool IsManuallyVerified { get; init; }
        
        // Security attributes
        public string SecurityToken { get; init; }
        public DateTime LastModified { get; init; }
        public string ModifiedBy { get; init; }
    }
}