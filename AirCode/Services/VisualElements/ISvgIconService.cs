// Services/VisualElements/ISvgIconService.cs
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AirCode.Services.VisualElements
{
    public interface ISvgIconService
    {
        // Core functionality
        Task<string> GetSvgContentAsync(string iconName);
        Task<IEnumerable<string>> GetAvailableIconNamesAsync();
        bool IconExists(string iconName);
        
        // Dynamic SVG creation
        Task<string> CreateDynamicSvgAsync(string iconName, SvgOptions options = null);
        
        // Specific icon helper methods
        Task<string> GetAddUserIconAsync();
        Task<string> GetAdminIconAsync();
        Task<string> GetAirplaneIconAsync();
        Task<string> GetBookIconAsync();
        Task<string> GetBook2IconAsync();
        Task<string> GetBook3IconAsync();
        Task<string> GetCloseIconAsync();
        Task<string> GetClose2IconAsync();
        Task<string> GetContactIconAsync();
        Task<string> GetCoursesIconAsync();
        Task<string> GetCourses2IconAsync();
        Task<string> GetCrowGuyIconAsync();
        Task<string> GetHistoryIconAsync();
        Task<string> GetHomeIconAsync();
        Task<string> GetLogoIconAsync();
        Task<string> GetLogoutIconAsync();
        Task<string> GetNoConnectionIconAsync();
        Task<string> GetNotificationIconAsync();
        Task<string> GetNotification2IconAsync();
        Task<string> GetQrCodeIconAsync();
        Task<string> GetQrScanIconAsync();
        Task<string> GetRecord2IconAsync();
        Task<string> GetRecordsIconAsync();
        Task<string> GetRemoveUserIconAsync();
        Task<string> GetReportIconAsync();
        Task<string> GetScanQrCodeIconAsync();
        Task<string> GetSearchIconAsync();
        Task<string> GetSettingsIconAsync();
        Task<string> GetStatsIconAsync();
        Task<string> GetStats2IconAsync();
        Task<string> GetUsersIconAsync();
        Task<string> GetWarningIconAsync();
    }
    
    public class SvgOptions
    {
        public string Color { get; set; } = "#000000";
        public int Width { get; set; } = 24;
        public int Height { get; set; } = 24;
        public string StrokeWidth { get; set; } = "1";
        public string Fill { get; set; } = "currentColor";
        public Dictionary<string, string> CustomAttributes { get; set; } = new Dictionary<string, string>();
    }
}