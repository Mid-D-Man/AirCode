using AirCode.Domain.Enums;
using AirCode.Domain.Interfaces;
using AirCode.Utilities.HelperScripts;

namespace AirCode.Domain.Entities;


    /// <summary>
    /// Session data for active attendance sessions
    /// </summary>
    public record SessionData : ISecureEntity
    {
        public string SessionId { get; set; } = Guid.NewGuid().ToString("N");
        public string CourseCode { get; set; } = string.Empty;
        public string CourseName { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public DateTime CreatedAt { get; set; }
        public int Duration { get; set; } = 60; // Default duration in minutes
        public string QrCodePayload { get; init; } = string.Empty;
        public string Theme { get; set; } = string.Empty;
        public bool UseTemporalKeyRefresh { get; set; }
        public bool OfflineSyncEnabled { get;set; } = true; // Allows offline connection and sync
        public AdvancedSecurityFeatures SecurityFeatures { get; set; } = AdvancedSecurityFeatures.Default;
        public string TemporalKey { get; set; } = string.Empty;
        
        // Security attributes
        public string SecurityToken { get; init; }
        public DateTime LastModified { get; init; }
        public string ModifiedBy { get; init; }

        public override string ToString() => 
            MID_HelperFunctions.GetStructOrClassMemberValues(this);
    }

    public class PartialSessionData
    {
        public string SessionId { get; set; } = string.Empty;
        public string CourseCode { get; set; } = string.Empty;
        public string CourseName { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
        public int Duration { get; set; }
    }
    
