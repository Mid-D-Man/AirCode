// Models/Forms/FormModels.cs

using AirCode.Domain.Enums;
using AirCode.Utilities.HelperScripts;

namespace AirCode.Models.Forms
{
    public class SessionFormModel
    {
        public short YearStart { get; set; }
        public short YearEnd { get; set; }

        public override string ToString() => 
            MID_HelperFunctions.GetStructOrClassMemberValues(this);
    }

    public class SemesterFormModel
    {
        public SemesterType Type { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }

        public override string ToString() => 
            MID_HelperFunctions.GetStructOrClassMemberValues(this);
    }

    public class OfflinePreferences
    {
        public int MaxStorageDays { get; set; } = 7;
        public int SyncIntervalMinutes { get; set; } = 15;
        public bool UseAdvancedEncryption { get; set; } = true;
        public int SessionDuration { get; set; } = 60;

        public override string ToString() => 
            MID_HelperFunctions.GetStructOrClassMemberValues(this);
    }
}