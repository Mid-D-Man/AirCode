using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using AirCode.Domain.Enums;
using AirCode.Domain.Interfaces;
using AirCode.Utilities.HelperScripts;

namespace AirCode.Domain.Entities
{
    public class Department : IModifiableSecurityEntity
    {
        [Required]
        public string DepartmentId { get; set; }
        [Required]
        public string Name { get; set; }
        public List<Level> Levels { get; set; } = new List<Level>();
        

        public override string ToString()
        {
            return MID_HelperFunctions.GetStructOrClassMemberValues(this);
        }
        
        public string ToJson()
        {
            return JsonHelper.Serialize(this, true);
        }

        public void AddLevel(Level level)
        {
            Levels.Add(level);
        }

        public bool RemoveLevel(LevelType levelType)
        {
            int index = Levels.FindIndex(l => l.LevelType == levelType);
            if (index != -1)
            {
                Levels.RemoveAt(index);
                return true;
            }
            return false;
        }

        public string SecurityToken { get; internal set; }
        public DateTime LastModified { get; internal set; }
        public string ModifiedBy { get; internal set; }

       public void SetModificationDetails(string securityToken, DateTime lastModified, string modifiedBy ="")
        {
            SecurityToken = securityToken;
            LastModified = lastModified;
            ModifiedBy = modifiedBy;
        }
        
    }

    public class Level
    {
        public LevelType LevelType { get; set; }
        public string DepartmentId { get; set; }
        public List<string> CourseIds { get; set; } = new List<string>();

        public void AddCourseId(string courseId)
        {
            if (!CourseIds.Contains(courseId))
            {
                CourseIds.Add(courseId);
            }
        }

        public bool RemoveCourseId(string courseId)
        {
            return CourseIds.Remove(courseId);
        }
    }
}