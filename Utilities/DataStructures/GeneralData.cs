using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using AirCode.Utilities.HelperScripts;

namespace AirCode.Utilities.DataStructures
{
    #region Enums
    public enum SemesterType
    {
        FirstSemester,
        SecondSemester
    }

    public enum LevelType
    {
        Level100,
        Level200,
        Level300,
        Level400
    }

    public enum CourseEnrollmentStatus
    {
        Enrolled,
        Carryover,
        Dropped
    }

    public enum ClassRepStatus
    {
        None,
        AssistantRep,
        MainRep
    }

    public enum LecturerStatus
    {
       Main,
       Assistant
    }
    
    public enum AttendanceType
    {
        Present,
        Late,
        Absent
    }

    // New enum for session type
    public enum SessionMode
    {
        Online,
        Offline,
        Hybrid
    }

    // New enum for attendance verification method
    public enum AttendanceVerificationMethod
    {
        QRCode,
        Biometric,
        Manual,
        GPSLocation
    }
    #endregion
    
    #region Security Interfaces
    // Interface for adding basic security attributes
    public interface ISecureEntity
    {
        string SecurityToken { get; init; }
        DateTime LastModified { get; init; }
        string ModifiedBy { get; init; }
    }
    #endregion
    
    #region Session Data
    public record Session : ISecureEntity
    {
        [Required]
        public string SessionId { get; init; }
        public int YearStart { get; init; }
        public int YearEnd { get; init; }
        public List<Semester> Semesters { get; init; }
        
        // Security attributes
        public string SecurityToken { get; init; }
        public DateTime LastModified { get; init; }
        public string ModifiedBy { get; init; }

        public override string ToString()
        {
            return MID_HelperFunctions.GetStructOrClassMemberValues(this);
        }

        internal string ToJson()
        {
          return  JsonHelper.Serialize(this,true);
        }
    }

    public record Semester : ISecureEntity
    {
        [Required]
        public string SemesterId { get; init; }
        public SemesterType Type { get; init; }
        public string SessionId { get; init; }
        public DateTime StartDate { get; init; }
        public DateTime EndDate { get; init; }
        
        // Security attributes
        public string SecurityToken { get; init; }
        public DateTime LastModified { get; init; }
        public string ModifiedBy { get; init; }
        
        public override string ToString()
        {
            return MID_HelperFunctions.GetStructOrClassMemberValues(this);
        }
        internal string ToJson()
        {
            return  JsonHelper.Serialize(this,true);
        }
    }
    #endregion
    
    #region Department Data
    public record Department : ISecureEntity
    {
        [Required]
        public string DepartmentId { get; init; }
        [Required]
        public string Name { get; init; }
        public List<Level> Levels { get; init; }
        
        // Security attributes
        public string SecurityToken { get; init; }
        public DateTime LastModified { get; init; }
        public string ModifiedBy { get; init; }
        
        public override string ToString()
        {
            return MID_HelperFunctions.GetStructOrClassMemberValues(this);
        }
        internal string ToJson()
        {
            return  JsonHelper.Serialize(this,true);
        }
    }

    public record Level
    {
        public LevelType LevelType { get; init; }
        public string DepartmentId { get; init; }
        public List<Course> Courses { get; init; }
    }
    #endregion
    
    #region Course Data
    public record Course : ISecureEntity
    {
        [Required]
        public string CourseId { get; init; }
        [Required]
        public string Name { get; init; }
        public string DepartmentId { get; init; }
        public LevelType Level { get; init; }
        public SemesterType Semester { get; init; }
        public string Schedule { get; init; }
        public List<string> LecturerIds { get; init; }
        
        // Security attributes
        public string SecurityToken { get; init; }
        public DateTime LastModified { get; init; }
        public string ModifiedBy { get; init; }
    }
    #endregion
    
    #region Lecturer Data
    public record Lecturer : ISecureEntity
    {
        [Required]
        public string LecturerId { get; init; }
        [Required]
        public string Name { get; init; }
        public string DepartmentId { get; init; }
        public List<string> CourseIds { get; init; }
        
        // Security attributes
        public string SecurityToken { get; init; }
        public DateTime LastModified { get; init; }
        public string ModifiedBy { get; init; }
    }
    #endregion
    
    #region Student Data
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
    #endregion
    
    #region Attendance Data
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
    #endregion
    
    #region Course Attendance Reports
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
    #endregion
}