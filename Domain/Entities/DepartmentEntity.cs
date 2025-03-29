using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using AirCode.Domain.Enums;
using AirCode.Domain.Interfaces;
using AirCode.Utilities.HelperScripts;

namespace AirCode.Domain.Entities
{
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
            return JsonHelper.Serialize(this, true);
        }
    }

    public record Level
    {
        public LevelType LevelType { get; init; }
        public string DepartmentId { get; init; }
        public List<Course> Courses { get; init; }
    }
}