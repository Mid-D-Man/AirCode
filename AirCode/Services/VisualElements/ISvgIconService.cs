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
        
        // Emoji functionality
        Task<string> GetEmojiIconAsync(string emojiId);
        Task<IEnumerable<string>> GetAvailableEmojiIdsAsync();
        bool EmojiExists(string emojiId);
        
        // Specific SVG icon methods
        /// <summary>📋 Get add user icon for user management interfaces</summary>
        Task<string> GetAddUserIconAsync();
        
        /// <summary>⚙️ Get admin icon for administrative controls</summary>
        Task<string> GetAdminIconAsync();
        
        /// <summary>✈️ Get airplane icon for navigation/travel contexts</summary>
        Task<string> GetAirplaneIconAsync();
        
        /// <summary>📖 Get book icon for educational content</summary>
        Task<string> GetBookIconAsync();
        
        /// <summary>📚 Get book2 icon for course materials</summary>
        Task<string> GetBook2IconAsync();
        
        /// <summary>📑 Get book3 icon for documentation</summary>
        Task<string> GetBook3IconAsync();
        
        /// <summary>❌ Get close icon for modal/dialog dismissal</summary>
        Task<string> GetCloseIconAsync();
        
        /// <summary>✖️ Get close2 icon for alternative close actions</summary>
        Task<string> GetClose2IconAsync();
        
        /// <summary>📞 Get contact icon for communication features</summary>
        Task<string> GetContactIconAsync();
        
        /// <summary>🎓 Get courses icon for academic course listings</summary>
        Task<string> GetCoursesIconAsync();
        
        /// <summary>📊 Get courses2 icon for course analytics</summary>
        Task<string> GetCourses2IconAsync();
        
        /// <summary>🐦 Get crow guy icon for mascot/branding</summary>
        Task<string> GetCrowGuyIconAsync();
        
        /// <summary>🕒 Get history icon for attendance records</summary>
        Task<string> GetHistoryIconAsync();
        
        /// <summary>🏠 Get home icon for main dashboard navigation</summary>
        Task<string> GetHomeIconAsync();
        
        /// <summary>🔷 Get logo icon for brand identity</summary>
        Task<string> GetLogoIconAsync();
        
        /// <summary>🚪 Get logout icon for session termination</summary>
        Task<string> GetLogoutIconAsync();
        
        /// <summary>📵 Get no connection icon for offline states</summary>
        Task<string> GetNoConnectionIconAsync();
        
        /// <summary>🔔 Get notification icon for alerts/updates</summary>
        Task<string> GetNotificationIconAsync();
        
        /// <summary>🔕 Get notification2 icon for muted notifications</summary>
        Task<string> GetNotification2IconAsync();
        
        /// <summary>📱 Get QR code icon for code generation</summary>
        Task<string> GetQrCodeIconAsync();
        
        /// <summary>📷 Get QR scan icon for scanning functionality</summary>
        Task<string> GetQrScanIconAsync();
        
        /// <summary>📝 Get record2 icon for data entry</summary>
        Task<string> GetRecord2IconAsync();
        
        /// <summary>📋 Get records icon for attendance tracking</summary>
        Task<string> GetRecordsIconAsync();
        
        /// <summary>➖ Get remove user icon for user removal</summary>
        Task<string> GetRemoveUserIconAsync();
        
        /// <summary>📄 Get report icon for analytics/reports</summary>
        Task<string> GetReportIconAsync();
        
        /// <summary>🔍 Get scan QR code icon for scanning interface</summary>
        Task<string> GetScanQrCodeIconAsync();
        
        /// <summary>🔎 Get search icon for search functionality</summary>
        Task<string> GetSearchIconAsync();
        
        /// <summary>⚙️ Get settings icon for configuration</summary>
        Task<string> GetSettingsIconAsync();
        
        /// <summary>📈 Get stats icon for statistics display</summary>
        Task<string> GetStatsIconAsync();
        
        /// <summary>📊 Get stats2 icon for detailed analytics</summary>
        Task<string> GetStats2IconAsync();
        
        /// <summary>👥 Get users icon for user management</summary>
        Task<string> GetUsersIconAsync();
        
        /// <summary>⚠️ Get warning icon for alerts/cautions</summary>
        Task<string> GetWarningIconAsync();
        
        // Specific emoji methods for attendance app
        /// <summary>✅ Get check mark emoji for attendance confirmation</summary>
        Task<string> GetCheckMarkEmojiAsync();
        
        /// <summary>❎ Get cross mark emoji for absence marking</summary>
        Task<string> GetCrossMarkEmojiAsync();
        
        /// <summary>🎯 Get target emoji for accuracy/goals</summary>
        Task<string> GetTargetEmojiAsync();
        
        /// <summary>📊 Get chart emoji for analytics display</summary>
        Task<string> GetChartEmojiAsync();
        
        /// <summary>🕐 Get clock emoji for time tracking</summary>
        Task<string> GetClockEmojiAsync();
        
        /// <summary>👨‍🏫 Get teacher emoji for lecturer identification</summary>
        Task<string> GetTeacherEmojiAsync();
        
        /// <summary>👩‍🎓 Get student emoji for student identification</summary>
        Task<string> GetStudentEmojiAsync();
        
        /// <summary>🏫 Get school emoji for institution branding</summary>
        Task<string> GetSchoolEmojiAsync();
        
        /// <summary>📅 Get calendar emoji for scheduling</summary>
        Task<string> GetCalendarEmojiAsync();
        
        /// <summary>🔐 Get locked emoji for security states</summary>
        Task<string> GetLockedEmojiAsync();
        
        /// <summary>🔓 Get unlocked emoji for accessible states</summary>
        Task<string> GetUnlockedEmojiAsync();
        
        /// <summary>⭐ Get star emoji for ratings/favorites</summary>
        Task<string> GetStarEmojiAsync();
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