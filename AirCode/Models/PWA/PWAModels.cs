// Models/PWA/PWAModels.cs

using AirCode.Utilities.HelperScripts;

namespace AirCode.Models.PWA
{
    public class PWAStatus
    {
        public bool IsInstallable { get; set; }
        public bool IsInstalled { get; set; }
        public bool HasServiceWorker { get; set; }
        public bool UpdateAvailable { get; set; }
        public bool IsOnline { get; set; } = true;
        public bool IsChromiumBased { get; set; }

        public override string ToString() => 
            MID_HelperFunctions.GetStructOrClassMemberValues(this);
    }

    public class OfflineRoute
    {
        public string Url { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;

        public override string ToString() => 
            MID_HelperFunctions.GetStructOrClassMemberValues(this);
    }
}