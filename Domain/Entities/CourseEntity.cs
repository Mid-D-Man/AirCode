using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using AirCode.Domain.Enums;
using AirCode.Domain.Interfaces;
using AirCode.Utilities.HelperScripts;

namespace AirCode.Domain.Entities
{
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
}