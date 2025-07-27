// Models/UI/NavigationModels.cs

using AirCode.Domain.Enums;
using AirCode.Utilities.HelperScripts;

namespace AirCode.Models.UI
{
    public class NavItemExtended
    {
        public string IconName { get; set; }
        public string Label { get; set; }
        public string? Path { get; set; }
        public bool RequiresOnline { get; set; }
        public string[] AllowedRoles { get; set; }
        public Action? Action { get; set; }
        public bool IsAvailable => true;

        public NavItemExtended(string iconName, string label, string? path, bool requiresOnline, string[] allowedRoles, Action? action = null)
        {
            IconName = iconName;
            Label = label;
            Path = path;
            RequiresOnline = requiresOnline;
            AllowedRoles = allowedRoles;
            Action = action;
        }

        public override string ToString() => 
            MID_HelperFunctions.GetStructOrClassMemberValues(this);
    }

    public class ActivityItem
    {
        public string Type { get; set; } = "";
        public string Title { get; set; } = "";
        public string CourseCode { get; set; } = "";
        public DateTime Time { get; set; }
        public CourseEnrollmentStatus Status { get; set; }

        public override string ToString() => 
            MID_HelperFunctions.GetStructOrClassMemberValues(this);
    }

    public class ScheduleItem
    {
        public string CourseCode { get; set; } = "";
        public string CourseName { get; set; } = "";
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public string Location { get; set; } = "";

        public override string ToString() => 
            MID_HelperFunctions.GetStructOrClassMemberValues(this);
    }
}