using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using AirCode.Domain.Enums;
using AirCode.Domain.Interfaces;
using AirCode.Domain.ValueObjects;

namespace AirCode.Domain.Entities
{
    /// <summary>
    /// yap courses should not be modifiable, only the lecturer id part(which isint modified
    /// in the courses area rather when lecturer adds it to the list of courses he is taking
    /// so do make it a class
    /// </summary>
    public class Course : IModifiableSecurityEntity
    {
        [Required]
        public string CourseId { get; init; }
        [Required]
        public string Name { get; init; }
        public string DepartmentId { get; init; }
        public LevelType Level { get; init; }
        public SemesterType Semester { get; init; }
        public CourseSchedule Schedule { get; init; }
        public IReadOnlyList<string> LecturerIds { get; init; }

        // Constructor to ensure immutability
        public Course(string courseId, string name, string departmentId, 
            LevelType level, SemesterType semester, 
            CourseSchedule schedule, List<string> lecturerIds)
        {
            CourseId = courseId;
            Name = name;
            DepartmentId = departmentId;
            Level = level;
            Semester = semester;
            Schedule = schedule;
            LecturerIds = lecturerIds?.AsReadOnly() ?? new List<string>().AsReadOnly();
        }
        // Security attributes
        public string SecurityToken { get; private set; }
        public DateTime LastModified { get; private set; }
        public string ModifiedBy { get; private set; }
    
        void IModifiableSecurityEntity.SetModificationDetails(string securityToken, DateTime lastModified, string modifiedBy)
        {
            SecurityToken = securityToken;
            LastModified = lastModified;
            ModifiedBy = modifiedBy;
        }
    }
}