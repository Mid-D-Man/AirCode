
using System.ComponentModel.DataAnnotations;
using AirCode.Domain.Enums;
using AirCode.Domain.Interfaces;
using AirCode.Services.Academic;
using AirCode.Utilities.HelperScripts;

namespace AirCode.Domain.Entities
{
    //hmm for this should we leave it as record or sturct
    //should it be possible to edit academic session  or not
    //cause something might happen we dont know that
    //ok leave it as record but make it copyable,deletable just incase of incasities
    //comback and decide this later 
    public record AcademicSession : ISecureEntity
    {
        [Required]
        public string SessionId { get; init; }
        public short YearStart { get; init; }
        public short YearEnd { get; init; }
        public List<Semester> Semesters { get; init; }
        
        // Security attributes from [ISecureEntity]
        public string SecurityToken { get; init; }
        public DateTime LastModified { get; init; }
        public string ModifiedBy { get; init; }

        public override string ToString()
        {
            return MID_HelperFunctions.GetStructOrClassMemberValues(this);
        }

        internal string ToJson()
        {
            return JsonHelper.Serialize(this, true);
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
            return JsonHelper.Serialize(this, true);
        }
    }
    // Domain/Entities/TransitionLog.cs
    public class TransitionLog
    {
        public string Id { get; set; }
        public string SessionId { get; set; }
        public TransitionType TransitionType { get; set; }
        public DateTime ProcessedAt { get; set; }
        public string ProcessedBy { get; set; }
        public string Status { get; set; }
    }
    // Domain/Entities/UserLoginRecord.cs
    public class UserLoginRecord
    {
        public string UserId { get; set; }
        public DateTime LastLoginTime { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
    
    public class PromotionEligibleStudent
    {
        public string MatricNumber { get; set; }
        public string Email { get; set; }
        public string CurrentLevel { get; set; }
        public bool MeetsPromotionCriteria { get; set; }
    }
     #region Result Models
    public class SessionTransitionResult
    {
        public bool HasPendingTransitions { get; set; }
        public List<SessionEndEvent> EndedSessions { get; set; } = new();
        public List<SemesterEndEvent> EndedSemesters { get; set; } = new();
        public List<SessionStartEvent> StartedSessions { get; set; } = new();
        public List<SemesterStartEvent> StartedSemesters { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
        public List<string> Errors { get; set; } = new();
        public DateTime ProcessedAt { get; set; }
        public string ProcessedBy { get; set; }
    }

    public class SessionOverlapResult
    {
        public bool HasOverlap { get; set; }
        public AcademicSession OverlappingSession { get; set; }
        public AcademicSession NewSession { get; set; }
        public OverlapResolutionAction RecommendedAction { get; set; }
        public string Resolution { get; set; }
    }

    public class SystemValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> Issues { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
        public SystemHealthStatus HealthStatus { get; set; }
    }

    public class SessionHealthReport
    {
        public int TotalSessions { get; set; }
        public int ActiveSessions { get; set; }
        public int ArchivedSessions { get; set; }
        public int PendingSessions { get; set; }
        public List<SessionIssue> Issues { get; set; } = new();
        public DateTime LastChecked { get; set; }
        public SystemHealthStatus OverallHealth { get; set; }
    }

    public class SessionIssue
    {
        public string SessionId { get; set; }
        public IssueType Type { get; set; }
        public string Description { get; set; }
        public IssueSeverity Severity { get; set; }
        public DateTime DetectedAt { get; set; }
    }
    #endregion

    #region Event Models
    public class SessionEndEvent
    {
        public AcademicSession Session { get; set; }
        public DateTime ActualEndDate { get; set; }
        public DateTime ProcessedAt { get; set; }
        public string ProcessedBy { get; set; }
        public SessionEndReason Reason { get; set; }
        public bool WasDelayed { get; set; }
        public TimeSpan DelayDuration { get; set; }
    }

    public class SemesterEndEvent
    {
        public Semester Semester { get; set; }
        public string SessionId { get; set; }
        public DateTime ActualEndDate { get; set; }
        public DateTime ProcessedAt { get; set; }
        public string ProcessedBy { get; set; }
        public SemesterEndReason Reason { get; set; }
        public bool WasDelayed { get; set; }
        public TimeSpan DelayDuration { get; set; }
    }

    public class SessionStartEvent
    {
        public AcademicSession Session { get; set; }
        public DateTime ActualStartDate { get; set; }
        public DateTime ProcessedAt { get; set; }
        public string ProcessedBy { get; set; }
        public SessionStartReason Reason { get; set; }
        public bool WasDelayed { get; set; }
        public TimeSpan DelayDuration { get; set; }
    }

    public class SemesterStartEvent
    {
        public Semester Semester { get; set; }
        public string SessionId { get; set; }
        public DateTime ActualStartDate { get; set; }
        public DateTime ProcessedAt { get; set; }
        public string ProcessedBy { get; set; }
        public SemesterStartReason Reason { get; set; }
        public bool WasDelayed { get; set; }
        public TimeSpan DelayDuration { get; set; }
    }
    #endregion
}