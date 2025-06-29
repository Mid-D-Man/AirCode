// Services/VisualElements/SvgIconService.cs
using Microsoft.AspNetCore.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace AirCode.Services.VisualElements
{
    public class SvgIconService : ISvgIconService
    {
        private readonly HttpClient _httpClient;
        private readonly NavigationManager _navigationManager;
        private readonly Dictionary<string, string> _iconCache = new Dictionary<string, string>();
        private readonly Dictionary<string, string> _iconNameMap = new Dictionary<string, string>();
        private List<string> _availableIcons = null;
        private const string DefaultIcon = "fas fa-circle";
        private const string SvgFolder = "svgs/";
        private const string SvgExtension = ".svg";

        public SvgIconService(HttpClient httpClient, NavigationManager navigationManager)
        {
            _httpClient = httpClient;
            _navigationManager = navigationManager;
            InitializeIconNameMap();
        }

        private void InitializeIconNameMap()
        {
            // Map icon names to their file names
            _iconNameMap.Add("add_user", "AddUser_Icon");
            _iconNameMap.Add("admin", "Admin_Icon");
            _iconNameMap.Add("airplane", "Airplane_Icon");
            _iconNameMap.Add("book", "Book_Icon");
            _iconNameMap.Add("book2", "Book2_Icon");
            _iconNameMap.Add("book3", "Book3_Icon");
            _iconNameMap.Add("close", "Close_Icon");
            _iconNameMap.Add("close2", "Close2_Icon");
            _iconNameMap.Add("contact", "ContactUs_Icon");
            _iconNameMap.Add("courses", "Courses_Icon");
            _iconNameMap.Add("courses2", "Courses2_Icon");
            _iconNameMap.Add("crow_guy", "CrowGuy_Icon");
            _iconNameMap.Add("history", "History_Icon");
            _iconNameMap.Add("home", "Home_Icon");
            _iconNameMap.Add("logo", "Logo_Icon");
            _iconNameMap.Add("logout", "Logout_Icon");
            _iconNameMap.Add("no_connection", "NoConnection_Icon");
            _iconNameMap.Add("notification", "Notification_Icon");
            _iconNameMap.Add("notification2", "Notification2_Icon");
            _iconNameMap.Add("qrcode", "QRCode_Icon");
            _iconNameMap.Add("qr_scan", "QrScan_Icon");
            _iconNameMap.Add("record2", "Record2_Icon");
            _iconNameMap.Add("records", "Records_Icon");
            _iconNameMap.Add("remove_user", "RemoveUser_Icon");
            _iconNameMap.Add("report", "Report_Icon");
            _iconNameMap.Add("scan_qrcode", "ScanQrCode_Icon");
            _iconNameMap.Add("search", "Search_Icon");
            _iconNameMap.Add("settings", "Settings_Icon");
            _iconNameMap.Add("stats", "Stats_Icon");
            _iconNameMap.Add("stats2", "Stats2_Icon");
            _iconNameMap.Add("users", "Users_Icon");
            _iconNameMap.Add("warning", "Warning_Icon");
        }

        public string GetIconClass(string iconName)
        {
            return DefaultIcon;
        }

        public async Task<string> GetSvgContentAsync(string iconName)
        {
            // Check cache first
            if (_iconCache.TryGetValue(iconName, out string cachedSvg))
            {
                return cachedSvg;
            }

            // Normalize the icon name
            string normalizedName = iconName.ToLower().Replace(" ", "_");

            // Check for mapping
            string fileName = normalizedName;
            if (_iconNameMap.TryGetValue(normalizedName, out string mappedName))
            {
                fileName = mappedName;
            }

            // Ensure correct file extension
            if (!fileName.EndsWith(SvgExtension))
            {
                fileName += SvgExtension;
            }

            // Construct URL
            string baseUrl = _navigationManager.BaseUri.TrimEnd('/');
            string url = $"{baseUrl}/{SvgFolder}{fileName}";

            try
            {
                string svgContent = await _httpClient.GetStringAsync(url);
                _iconCache[iconName] = svgContent;
                return svgContent;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to load SVG icon '{iconName}' from '{url}': {ex.Message}");
                return null;
            }
        }

        public async Task<string> CreateDynamicSvgAsync(string iconName, SvgOptions options = null)
        {
            var opt = options ?? new SvgOptions();
            
            // Try to get existing SVG first
            var existingSvg = await GetSvgContentAsync(iconName);
            if (!string.IsNullOrEmpty(existingSvg))
            {
                return ApplySvgOptions(existingSvg, opt);
            }

            // Generate basic SVG if not found
            return GenerateBasicSvg(iconName, opt);
        }

        private string ApplySvgOptions(string svgContent, SvgOptions options)
        {
            // Parse and modify existing SVG with new options
            var modifiedSvg = svgContent;
            
            // Replace width and height
            modifiedSvg = System.Text.RegularExpressions.Regex.Replace(
                modifiedSvg, 
                @"width=""\d+""", 
                $"width=\"{options.Width}\"");
            
            modifiedSvg = System.Text.RegularExpressions.Regex.Replace(
                modifiedSvg, 
                @"height=""\d+""", 
                $"height=\"{options.Height}\"");

            // Replace fill color
            modifiedSvg = System.Text.RegularExpressions.Regex.Replace(
                modifiedSvg, 
                @"fill=""[^""]*""", 
                $"fill=\"{options.Fill}\"");

            // Add custom attributes if any
            foreach (var attr in options.CustomAttributes)
            {
                modifiedSvg = modifiedSvg.Replace("<svg", $"<svg {attr.Key}=\"{attr.Value}\"");
            }

            return modifiedSvg;
        }

        private string GenerateBasicSvg(string iconName, SvgOptions options)
        {
            var svg = new StringBuilder();
            svg.AppendLine($"<svg width=\"{options.Width}\" height=\"{options.Height}\" viewBox=\"0 0 {options.Width} {options.Height}\" xmlns=\"http://www.w3.org/2000/svg\">");
            svg.AppendLine($"  <circle cx=\"{options.Width/2}\" cy=\"{options.Height/2}\" r=\"{Math.Min(options.Width, options.Height)/4}\" fill=\"{options.Fill}\" stroke=\"{options.Color}\" stroke-width=\"{options.StrokeWidth}\"/>");
            svg.AppendLine($"  <text x=\"{options.Width/2}\" y=\"{options.Height/2 + 3}\" text-anchor=\"middle\" font-size=\"8\" fill=\"{options.Color}\">?</text>");
            svg.AppendLine("</svg>");
            
            return svg.ToString();
        }

        public async Task<IEnumerable<string>> GetAvailableIconNamesAsync()
        {
            if (_availableIcons != null)
            {
                return _availableIcons;
            }

            _availableIcons = _iconNameMap.Keys.ToList();
            return _availableIcons;
        }

        public bool IconExists(string iconName)
        {
            string normalizedName = iconName.ToLower().Replace(" ", "_");
            return _iconNameMap.ContainsKey(normalizedName);
        }

        // Specific icon helper methods - all mapped from existing implementation
        public async Task<string> GetAddUserIconAsync() => await GetSvgContentAsync("add_user");
        public async Task<string> GetAdminIconAsync() => await GetSvgContentAsync("admin");
        public async Task<string> GetAirplaneIconAsync() => await GetSvgContentAsync("airplane");
        public async Task<string> GetBookIconAsync() => await GetSvgContentAsync("book");
        public async Task<string> GetBook2IconAsync() => await GetSvgContentAsync("book2");
        public async Task<string> GetBook3IconAsync() => await GetSvgContentAsync("book3");
        public async Task<string> GetCloseIconAsync() => await GetSvgContentAsync("close");
        public async Task<string> GetClose2IconAsync() => await GetSvgContentAsync("close2");
        public async Task<string> GetContactIconAsync() => await GetSvgContentAsync("contact");
        public async Task<string> GetCoursesIconAsync() => await GetSvgContentAsync("courses");
        public async Task<string> GetCourses2IconAsync() => await GetSvgContentAsync("courses2");
        public async Task<string> GetCrowGuyIconAsync() => await GetSvgContentAsync("crow_guy");
        public async Task<string> GetHistoryIconAsync() => await GetSvgContentAsync("history");
        public async Task<string> GetHomeIconAsync() => await GetSvgContentAsync("home");
        public async Task<string> GetLogoIconAsync() => await GetSvgContentAsync("logo");
        public async Task<string> GetLogoutIconAsync() => await GetSvgContentAsync("logout");
        public async Task<string> GetNoConnectionIconAsync() => await GetSvgContentAsync("no_connection");
        public async Task<string> GetNotificationIconAsync() => await GetSvgContentAsync("notification");
        public async Task<string> GetNotification2IconAsync() => await GetSvgContentAsync("notification2");
        public async Task<string> GetQrCodeIconAsync() => await GetSvgContentAsync("qrcode");
        public async Task<string> GetQrScanIconAsync() => await GetSvgContentAsync("qr_scan");
        public async Task<string> GetRecord2IconAsync() => await GetSvgContentAsync("record2");
        public async Task<string> GetRecordsIconAsync() => await GetSvgContentAsync("records");
        public async Task<string> GetRemoveUserIconAsync() => await GetSvgContentAsync("remove_user");
        public async Task<string> GetReportIconAsync() => await GetSvgContentAsync("report");
        public async Task<string> GetScanQrCodeIconAsync() => await GetSvgContentAsync("scan_qrcode");
        public async Task<string> GetSearchIconAsync() => await GetSvgContentAsync("search");
        public async Task<string> GetSettingsIconAsync() => await GetSvgContentAsync("settings");
        public async Task<string> GetStatsIconAsync() => await GetSvgContentAsync("stats");
        public async Task<string> GetStats2IconAsync() => await GetSvgContentAsync("stats2");
        public async Task<string> GetUsersIconAsync() => await GetSvgContentAsync("users");
        public async Task<string> GetWarningIconAsync() => await GetSvgContentAsync("warning");
    }
}