
// Services/SvgIcon/SvgIconService.cs
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
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
        private const string SvgFolder = "/svgs/";
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
            // This makes it easier to get icons by a consistent name
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
            return DefaultIcon; // Default icon as a fallback
        }

        public async Task<string> GetSvgContentAsync(string iconName)
        {
            // First, check if we have the icon in our cache
            if (_iconCache.TryGetValue(iconName, out string cachedSvg))
            {
                return cachedSvg;
            }

            // Normalize the icon name (remove spaces, convert to lowercase)
            string normalizedName = iconName.ToLower().Replace(" ", "_");

            // Check if we have a mapping for this icon name
            string fileName = normalizedName;
            if (_iconNameMap.TryGetValue(normalizedName, out string mappedName))
            {
                fileName = mappedName;
            }

            // Ensure the file extension is correct
            if (!fileName.EndsWith(SvgExtension))
            {
                fileName += SvgExtension;
            }

            // Build the full URL
            string url = _navigationManager.BaseUri.TrimEnd('/') + SvgFolder + fileName;

            try
            {
                string svgContent = await _httpClient.GetStringAsync(url);
                
                // Cache the result
                _iconCache[iconName] = svgContent;
                
                return svgContent;
            }
            catch (Exception)
            {
                // If we can't find the icon, return null (or you could return a default SVG)
                return null;
            }
        }

        public async Task<IEnumerable<string>> GetAvailableIconNamesAsync()
        {
            if (_availableIcons != null)
            {
                return _availableIcons;
            }

            // In a real-world scenario, you might want to fetch the list 
            // of available SVGs from the server
            // For now, we'll return the keys from our map
            _availableIcons = _iconNameMap.Keys.ToList();
            return _availableIcons;
        }

        public bool IconExists(string iconName)
        {
            string normalizedName = iconName.ToLower().Replace(" ", "_");
            return _iconNameMap.ContainsKey(normalizedName);
        }

        // Helper method to get a specific icon by name
        // This allows for IntelliSense suggestions in your IDE
        public async Task<string> GetAddUserIconAsync() => await GetSvgContentAsync("add_user");
        public async Task<string> GetAdminIconAsync() => await GetSvgContentAsync("admin");
        public async Task<string> GetAirplaneIconAsync() => await GetSvgContentAsync("airplane");
        public async Task<string> GetBookIconAsync() => await GetSvgContentAsync("book");
        public async Task<string> GetCloseIconAsync() => await GetSvgContentAsync("close");
        public async Task<string> GetContactIconAsync() => await GetSvgContentAsync("contact");
        public async Task<string> GetCoursesIconAsync() => await GetSvgContentAsync("courses");
        public async Task<string> GetHomeIconAsync() => await GetSvgContentAsync("home");
        public async Task<string> GetLogoIconAsync() => await GetSvgContentAsync("logo");
        public async Task<string> GetLogoutIconAsync() => await GetSvgContentAsync("logout");
        public async Task<string> GetQrCodeIconAsync() => await GetSvgContentAsync("qrcode");
        public async Task<string> GetSearchIconAsync() => await GetSvgContentAsync("search");
        public async Task<string> GetSettingsIconAsync() => await GetSvgContentAsync("settings");
        public async Task<string> GetStatsIconAsync() => await GetSvgContentAsync("stats");
        public async Task<string> GetUsersIconAsync() => await GetSvgContentAsync("users");
        public async Task<string> GetWarningIconAsync() => await GetSvgContentAsync("warning");
    }
}