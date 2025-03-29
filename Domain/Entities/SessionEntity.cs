using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using AirCode.Domain.Enums;
using AirCode.Domain.Interfaces;
using AirCode.Utilities.HelperScripts;

namespace AirCode.Domain.Entities
{
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
}