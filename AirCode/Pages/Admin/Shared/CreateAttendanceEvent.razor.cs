






using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using System.Threading;
using AirCode.Models.Supabase;
using AirCode.Services;
using AirCode.Models;
using AirCode.Components.QRCode;
using AirCode.Services.Attendance;
using AirCode.Services.Firebase; // Or AirCode.Services.Attendance if renamed
using AirCode.Utilities.HelperScripts;
using AirCode.Domain.Entities;
using AirCode.Models.Supabase;
using AirCode.Utilities.DataStructures;
using AirCode.Components.SharedPrefabs.QrCode; // If QR code options are here
namespace AirCode.Pages.Admin.Shared
{
    public partial class CreateAttendanceEvent : ComponentBase, IAsyncDisposable
    {
        #region Service Dependencies (Inject these in the razor file)
        
        [Inject] protected SessionStateService SessionStateService { get; set; }
        [Inject] protected AttendanceSessionService AttendanceSessionService { get; set; }
        [Inject] protected FirebaseAttendanceService FirebaseAttendanceService { get; set; }
        [Inject] protected QRCodeDecoder QRCodeDecoder { get; set; }
        
        #endregion
        #region Variables and what not
        private bool isRestoringSession = false;
        private bool isSearchingForSessions = false;
        private bool isCreatingSession = false;
        private bool isEndingSession = false;
        private bool isSessionStarted = false;
        private bool isSessionEnded = false;
        private bool showCourseSelection = false;
        private bool showManualAttendancePopup = false;
        private bool allowOfflineSync = false;
        private bool useTemporalKeyRefresh = false;
        private bool isCurrentUserCourseRep = false;
        
        private string restorationMessage = string.Empty;
        private string qrCodePayload = string.Empty;
        private string selectedTheme = "Standard";
        private string currentUserMatricNumber = string.Empty;
        
        private Course selectedCourse;
        private SessionModel sessionModel = new SessionModel();
        private SessionData currentActiveSession;
        private PartialSessionData manualAttendanceSessionData;
        private QRCodeData generatedQRCode;
        
        private DateTime sessionEndTime;
        private Timer countdownTimer;
        private Timer temporalKeyUpdateTimer;
        
        private List<SessionData> allActiveSessions = new List<SessionData>();
        private SecurityFeatures securityFeatures;
        private int temporalKeyRefreshInterval = 5; // minutes
        
        #endregion








        #region Lifecycle Methods








        protected override async Task OnInitializedAsync()
        {
            SessionStateService.StateChanged += OnStateChanged;
            await CheckForExistingSessionAsync();
        }








        public async ValueTask DisposeAsync()
        {
            countdownTimer?.Dispose();
            temporalKeyUpdateTimer?.Dispose();
            SessionStateService.StateChanged -= OnStateChanged;
            
            // Clean up any remaining sessions on app shutdown
            if (currentActiveSession != null)
            {
                await SessionStateService.RemoveActiveSessionAsync(currentActiveSession.SessionId);
            }
        }








        public void Dispose()
        {
            countdownTimer?.Dispose();
            temporalKeyUpdateTimer?.Dispose();
            SessionStateService.StateChanged -= OnStateChanged;
        }








        #endregion








        #region Session Management








        private async Task CheckForExistingSessionAsync()
        {
            try
            {
                isRestoringSession = true;
                
                var existingSession = await SessionStateService.GetCurrentSessionAsync("default");
                if (existingSession != null)
                {
                    // Validate session hasn't expired
                    if (existingSession.ExpirationTime > DateTime.UtcNow)
                    {

