using AirCode.Utilities.HelperScripts;
// Models/Admin/DashboardModels.cs
namespace AirCode.Models.Admin
{
    public class AcademicSessionSummary
    {
        public string Name { get; set; } = "";
        public string Status { get; set; } = "";
        public bool IsActive { get; set; }
        public int StudentCount { get; set; }

        public override string ToString() => 
            MID_HelperFunctions.GetStructOrClassMemberValues(this);
    }

    public class SystemAlert
    {
        public string Title { get; set; } = "";
        public string Severity { get; set; } = "";
        public DateTime Timestamp { get; set; }

        public override string ToString() => 
            MID_HelperFunctions.GetStructOrClassMemberValues(this);
    }

    public class AdminActivity
    {
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public string Type { get; set; } = "";
        public string User { get; set; } = "";
        public DateTime Timestamp { get; set; }

        public override string ToString() => 
            MID_HelperFunctions.GetStructOrClassMemberValues(this);
    }
}