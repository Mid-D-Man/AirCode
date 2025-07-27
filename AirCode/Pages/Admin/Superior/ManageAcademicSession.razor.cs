using AirCode.Domain.Entities;
using AirCode.Domain.Enums;
using AirCode.Services.Firebase;
using AirCode.Services.Academic;
using AirCode.Utilities.HelperScripts;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Newtonsoft.Json;
using System.ComponentModel;
using AirCode.Components.SharedPrefabs.Cards;
using AirCode.Models.Forms;

namespace AirCode.Pages.Admin.Superior
{
    public partial class ManageAcademicSession : IDisposable
    {
        [Inject] private IJSRuntime JSRuntime { get; set; }
        [Inject] private NavigationManager NavigationManager { get; set; }
        [Inject] private IFirestoreService FirestoreService { get; set; }
        [Inject] private IAcademicSessionService AcademicSessionService { get; set; }

        #region Private Fields
        private AcademicSession currentSession;
        private AcademicSession nextSession;
        private List<AcademicSession> archivedSessions = new List<AcademicSession>();
        private bool showModal = false;
        private bool showWarning = false;
        private ModalType activeModal;
        private string targetSessionId;
        
        // Form models
        private SessionFormModel sessionForm = new();
        private SemesterFormModel semesterForm = new();
        private SemesterFormModel firstSemesterForm = new();
        private bool includeFirstSemester = true;
        
        // Notification component
        private AirCode.Components.SharedPrefabs.Cards.NotificationComponent notificationComponent;
        
        // Timer for countdown and transition checking
        private System.Threading.Timer timer;
        
        // Connection status
        private bool IsInitialized;
        private bool IsConnected;
        
        // Session health monitoring
        private SessionHealthReport lastHealthReport;
        private DateTime lastTransitionCheck = DateTime.MinValue;

        // Firebase constants
        private const string ACADEMIC_SESSIONS_COLLECTION = "ACADEMIC_SESSIONS";
        #endregion

        #region Component References
        [Parameter] public AirCode.Components.SharedPrefabs.Cards.NotificationComponent NotificationComponent { get; set; }
        #endregion

        #region Component Lifecycle
        protected override async Task OnInitializedAsync()
        {
            await CheckConnectionStatus();
            await InitializeSessionService();
            await LoadSessions();
            await ProcessPendingTransitions();
            SetupCountdownTimer();
            await CheckForWarnings();
        }

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender)
            {
                // Subscribe to session service events
                SubscribeToSessionEvents();
                
                // Perform initial health check
                await PerformHealthCheck();
            }
        }

        public void Dispose()
        {
            timer?.Dispose();
            UnsubscribeFromSessionEvents();
        }
        #endregion

        #region Session Service Integration
        private async Task InitializeSessionService()
        {
            try
            {
                // Validate system state before proceeding
                var validationResult = await AcademicSessionService.ValidateSystemStateAsync();
                
                if (!validationResult.IsValid)
                {
                    Console.WriteLine($"System validation issues found: {string.Join(", ", validationResult.Issues)}");
                    NotificationComponent?.ShowWarning($"System issues detected: {validationResult.Issues.FirstOrDefault()}");
                }
                
                if (validationResult.Warnings.Any())
                {
                    Console.WriteLine($"System warnings: {string.Join(", ", validationResult.Warnings)}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error initializing session service: {ex.Message}");
                NotificationComponent?.ShowError("Failed to initialize session service");
            }
        }

        private async Task ProcessPendingTransitions()
        {
            try
            {
                Console.WriteLine("Checking for pending session transitions...");
                
                // Use a default user ID or get from authentication service
                string userId = "current_user"; // Replace with actual user ID from auth service
                
                var transitionResult = await AcademicSessionService.ProcessPendingTransitionsAsync(userId);
                
                if (transitionResult.HasPendingTransitions)
                {
                    Console.WriteLine($"Processed pending transitions: {transitionResult.EndedSessions.Count} ended sessions, {transitionResult.StartedSessions.Count} started sessions");
                    
                    // Show notifications for important transitions
                    foreach (var endedSession in transitionResult.EndedSessions)
                    {
                        NotificationComponent?.ShowInfo($"Session {endedSession.Session.SessionId} has ended and been archived");
                    }
                    
                    foreach (var startedSession in transitionResult.StartedSessions)
                    {
                        NotificationComponent?.ShowSuccess($"Session {startedSession.Session.SessionId} is now active");
                    }
                    
                    // Refresh session data after transitions
                    await RefreshSessions();
                }
                
                // Show any warnings or errors
                foreach (var warning in transitionResult.Warnings)
                {
                    NotificationComponent?.ShowWarning(warning);
                }
                
                foreach (var error in transitionResult.Errors)
                {
                    NotificationComponent?.ShowError(error);
                }
                
                lastTransitionCheck = DateTime.Now;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing pending transitions: {ex.Message}");
                NotificationComponent?.ShowError("Failed to check for pending transitions");
            }
        }

        private async Task RefreshSessions()
        {
            try
            {
                Console.WriteLine("Refreshing session cache...");
                
                await AcademicSessionService.RefreshSessionCacheAsync();
                
                // Reload sessions using the service
                currentSession = await AcademicSessionService.GetCurrentSessionAsync();
                nextSession = await AcademicSessionService.GetNextSessionAsync();
                archivedSessions = await AcademicSessionService.GetArchivedSessionsAsync();
                
                Console.WriteLine($"Sessions refreshed - Current: {currentSession?.SessionId ?? "None"}, Next: {nextSession?.SessionId ?? "None"}, Archived: {archivedSessions.Count}");
                
                StateHasChanged();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error refreshing sessions: {ex.Message}");
                NotificationComponent?.ShowError("Failed to refresh session data");
            }
        }

        private async Task PerformHealthCheck()
        {
            try
            {
                lastHealthReport = await AcademicSessionService.GetSessionHealthReportAsync();
                
                Console.WriteLine($"Health check completed - Status: {lastHealthReport.OverallHealth}, Issues: {lastHealthReport.Issues.Count}");
                
                // Show critical issues as notifications
                var criticalIssues = lastHealthReport.Issues.Where(i => i.Severity == IssueSeverity.Critical).ToList();
                foreach (var issue in criticalIssues)
                {
                    NotificationComponent?.ShowError($"Critical session issue: {issue.Description}");
                }
                
                // Show error issues as warnings
                var errorIssues = lastHealthReport.Issues.Where(i => i.Severity == IssueSeverity.Error).ToList();
                foreach (var issue in errorIssues)
                {
                    NotificationComponent?.ShowWarning($"Session error: {issue.Description}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error performing health check: {ex.Message}");
            }
        }

        private void SubscribeToSessionEvents()
        {
            AcademicSessionService.SessionEnded += OnSessionEnded;
            AcademicSessionService.SessionStarted += OnSessionStarted;
            AcademicSessionService.SemesterEnded += OnSemesterEnded;
            AcademicSessionService.SemesterStarted += OnSemesterStarted;
        }

        private void UnsubscribeFromSessionEvents()
        {
            AcademicSessionService.SessionEnded -= OnSessionEnded;
            AcademicSessionService.SessionStarted -= OnSessionStarted;
            AcademicSessionService.SemesterEnded -= OnSemesterEnded;
            AcademicSessionService.SemesterStarted -= OnSemesterStarted;
        }
        #endregion

        #region Session Service Event Handlers
        private async Task OnSessionEnded(SessionEndEvent endEvent)
        {
            Console.WriteLine($"Session ended event received: {endEvent.Session.SessionId}");
            NotificationComponent?.ShowInfo($"Session {endEvent.Session.SessionId} has ended");
            await RefreshSessions();
        }

        private async Task OnSessionStarted(SessionStartEvent startEvent)
        {
            Console.WriteLine($"Session started event received: {startEvent.Session.SessionId}");
            NotificationComponent?.ShowSuccess($"Session {startEvent.Session.SessionId} is now active");
            await RefreshSessions();
        }

        private async Task OnSemesterEnded(SemesterEndEvent endEvent)
        {
            Console.WriteLine($"Semester ended event received: {endEvent.Semester.SemesterId}");
            NotificationComponent?.ShowInfo($"Semester {GetSemesterName(endEvent.Semester.Type)} has ended");
            await RefreshSessions();
        }

        private async Task OnSemesterStarted(SemesterStartEvent startEvent)
        {
            Console.WriteLine($"Semester started event received: {startEvent.Semester.SemesterId}");
            NotificationComponent?.ShowSuccess($"Semester {GetSemesterName(startEvent.Semester.Type)} is now active");
            await RefreshSessions();
        }
        #endregion

        #region Connection Management
        private async Task CheckConnectionStatus()
        {
            IsInitialized = FirestoreService.IsInitialized;
            IsConnected = await FirestoreService.IsConnectedAsync();
            StateHasChanged();
        }
        #endregion

        #region Data Loading
        private async Task LoadSessions()
        {
            try
            {
                Console.WriteLine("Loading sessions using Academic Session Service...");
                
                // Use the service to load sessions instead of direct Firebase calls
                currentSession = await AcademicSessionService.GetCurrentSessionAsync();
                nextSession = await AcademicSessionService.GetNextSessionAsync();
                archivedSessions = await AcademicSessionService.GetArchivedSessionsAsync();
                
                int totalSessions = (currentSession != null ? 1 : 0) + 
                                   (nextSession != null ? 1 : 0) + 
                                   archivedSessions.Count;
                
                if (totalSessions > 0)
                {
                    Console.WriteLine($"Successfully loaded {totalSessions} sessions using service");
                    NotificationComponent?.ShowInfo($"Loaded {totalSessions} academic sessions");
                }
                else
                {
                    Console.WriteLine("No sessions found");
                    NotificationComponent?.ShowInfo("No academic sessions found. Create your first session to get started.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading sessions: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                
                // Initialize empty state on error
                currentSession = null;
                nextSession = null;
                archivedSessions = new List<AcademicSession>();
                NotificationComponent?.ShowError($"Failed to load sessions: {ex.Message}");
            }
        }
        #endregion

        #region Session ID Generation
        private string GenerateSessionId(int yearStart, int yearEnd)
        {
            return $"{yearStart}_{yearEnd}_Session";
        }

        private string GenerateSemesterId(string sessionId, SemesterType semesterType)
        {
            string semesterCode = semesterType == SemesterType.FirstSemester ? "S1" : "S2";
            return $"{sessionId}_{semesterCode}";
        }

        private string GenerateSecurityToken()
        {
            // Generate a more readable security token while maintaining uniqueness
            return $"SEC_{DateTime.Now:yyyyMMdd}_{Guid.NewGuid().ToString("N")[..8].ToUpper()}";
        }
        #endregion

        #region Modal Management
        public void ShowModal(ModalType modalType, string sessionId = null)
        {
            activeModal = modalType;
            targetSessionId = sessionId;
            
            if (modalType == ModalType.CreateSession)
            {
                InitializeSessionForm();
            }
            else if (modalType == ModalType.CreateSemester)
            {
                InitializeSemesterForm();
            }
            
            showModal = true;
        }
        
        public void CloseModal()
        {
            showModal = false;
        }

        private void InitializeSessionForm()
        {
            int currentYear = DateTime.Now.Year;
            int nextYear = DateTime.Now.Month >= 8 ? currentYear : currentYear - 1;
            
            sessionForm = new SessionFormModel
            {
                YearStart = (short)nextYear,
                YearEnd = (short)(nextYear + 4) // Allow up to 4 years for degree programs
            };
            
            firstSemesterForm = new SemesterFormModel
            {
                Type = SemesterType.FirstSemester,
                StartDate = new DateTime(nextYear, 9, 1),
                EndDate = new DateTime(nextYear + 4, 1, 31) // Extended for degree completion
            };
            
            includeFirstSemester = true;
        }

        private void InitializeSemesterForm()
        {
            AcademicSession targetSession = GetTargetSession();
            SemesterType nextType = GetNextSemesterType(targetSession);
            (DateTime startDate, DateTime endDate) = GetSemesterDates(targetSession, nextType);
                
            semesterForm = new SemesterFormModel
            {
                Type = nextType,
                StartDate = startDate,
                EndDate = endDate
            };
        }

        private AcademicSession GetTargetSession()
        {
            return targetSessionId != null 
                ? nextSession?.SessionId == targetSessionId ? nextSession : currentSession
                : currentSession;
        }

        private SemesterType GetNextSemesterType(AcademicSession targetSession)
        {
            return targetSession.Semesters.Any(s => s.Type == SemesterType.FirstSemester)
                ? SemesterType.SecondSemester
                : SemesterType.FirstSemester;
        }

        private (DateTime startDate, DateTime endDate) GetSemesterDates(AcademicSession targetSession, SemesterType semesterType)
        {
            if (semesterType == SemesterType.FirstSemester)
            {
                return (new DateTime(targetSession.YearStart, 9, 1), 
                        new DateTime(targetSession.YearEnd, 1, 31));
            }
            else
            {
                return (new DateTime(targetSession.YearEnd, 2, 1), 
                        new DateTime(targetSession.YearEnd, 6, 30));
            }
        }

        public string GetModalTitle()
        {
            return activeModal switch
            {
                ModalType.CreateSession => "Create New Academic Session",
                ModalType.CreateSemester => "Add Semester",
                _ => "Modal"
            };
        }
        #endregion

        #region Session Management
        public async Task SaveModal()
        {
            try
            {
                Console.WriteLine($"Saving modal data for {activeModal}");
                
                if (activeModal == ModalType.CreateSession)
                {
                    await CreateNewSession();
                    NotificationComponent?.ShowSuccess($"Academic session {sessionForm.YearStart}-{sessionForm.YearEnd} created successfully!");
                }
                else if (activeModal == ModalType.CreateSemester)
                {
                    await CreateNewSemester();
                    NotificationComponent?.ShowSuccess($"{GetSemesterName(semesterForm.Type)} added successfully!");
                }
                
                CloseModal();
                
                // Refresh sessions and check for warnings
                await RefreshSessions();
                await CheckForWarnings();
                
                Console.WriteLine("Modal saved successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving modal: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                NotificationComponent?.ShowError($"Failed to save: {ex.Message}");
            }
        }

        private async Task CreateNewSession()
        {
            Console.WriteLine("Creating new academic session");
            
            string sessionId = GenerateSessionId(sessionForm.YearStart, sessionForm.YearEnd);
            
            var newSession = new AcademicSession
            {
                SessionId = sessionId,
                YearStart = sessionForm.YearStart,
                YearEnd = sessionForm.YearEnd,
                SecurityToken = GenerateSecurityToken(),
                LastModified = DateTime.Now,
                ModifiedBy = "System",
                Semesters = new List<Semester>()
            };
            
            if (includeFirstSemester)
            {
                AddFirstSemester(newSession);
            }
            
            await SaveSessionToFirebase(newSession);
            
            // Let the service handle session transitions and categorization
            await RefreshSessions();
            
            Console.WriteLine($"New session created: {newSession.SessionId}");
        }

        private void AddFirstSemester(AcademicSession session)
        {
            var newSemester = new Semester
            {
                SemesterId = GenerateSemesterId(session.SessionId, SemesterType.FirstSemester),
                Type = SemesterType.FirstSemester,
                SessionId = session.SessionId,
                StartDate = firstSemesterForm.StartDate,
                EndDate = firstSemesterForm.EndDate,
                SecurityToken = GenerateSecurityToken(),
                LastModified = DateTime.Now,
                ModifiedBy = "System"
            };
            
            session.Semesters.Add(newSemester);
            Console.WriteLine($"Added first semester to session: {session.SessionId}");
        }

        private async Task CreateNewSemester()
        {
            Console.WriteLine("Creating new semester");
            
            AcademicSession targetSession = GetTargetSession();
            
            var newSemester = new Semester
            {
                SemesterId = GenerateSemesterId(targetSession.SessionId, semesterForm.Type),
                Type = semesterForm.Type,
                SessionId = targetSession.SessionId,
                StartDate = semesterForm.StartDate,
                EndDate = semesterForm.EndDate,
                SecurityToken = GenerateSecurityToken(),
                LastModified = DateTime.Now,
                ModifiedBy = "System"
            };
            
            var updatedSemesters = new List<Semester>(targetSession.Semesters) { newSemester };
            AcademicSession updatedSession = targetSession with { Semesters = updatedSemesters };
            
            await SaveSessionToFirebase(updatedSession);
            
            // Refresh sessions to get updated data
            await RefreshSessions();
            
            Console.WriteLine($"New semester created for session: {targetSession.SessionId}");
        }
        #endregion

        #region Warning System
        private async Task CheckForWarnings()
        {
            try
            {
                // Check for standard warnings
                if (currentSession != null && nextSession == null)
                {
                    DateTime endDate = GetEndDate(currentSession);
                    TimeSpan timeRemaining = endDate - DateTime.Now;
                    
                    if (timeRemaining.TotalDays <= 30)
                    {
                        showWarning = true;
                        Console.WriteLine($"Warning: Current session ends in {timeRemaining.TotalDays} days");
                        NotificationComponent?.ShowWarning(
                            $"Current academic session ends in {(int)timeRemaining.TotalDays} days. Consider creating the next session.");
                    }
                }
                
                // Check for session health issues
                if (lastHealthReport != null)
                {
                    var warningIssues = lastHealthReport.Issues.Where(i => i.Severity == IssueSeverity.Warning).ToList();
                    foreach (var issue in warningIssues)
                    {
                        NotificationComponent?.ShowWarning($"Session warning: {issue.Description}");
                    }
                }
                
                // Check for overlapping sessions
                var overlapResult = await AcademicSessionService.ResolveSessionOverlapAsync();
                if (overlapResult.HasOverlap)
                {
                    NotificationComponent?.ShowWarning($"Session overlap detected: {overlapResult.Resolution}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error checking for warnings: {ex.Message}");
            }
        }

        public void DismissWarning()
        {
            showWarning = false;
        }
        #endregion

        #region Utility Methods
        private void SetupCountdownTimer()
        {
            timer = new System.Threading.Timer(async _ =>
            {
                try
                {
                    // Check for transitions every minute
                    if (DateTime.Now.Subtract(lastTransitionCheck) > TimeSpan.FromMinutes(5))
                    {
                        await ProcessPendingTransitions();
                    }
                    
                    await InvokeAsync(StateHasChanged);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error in timer callback: {ex.Message}");
                }
            }, null, TimeSpan.Zero, TimeSpan.FromSeconds(60));
        }

        public DateTime GetStartDate(AcademicSession session)
        {
            return session.Semesters.Any() 
                ? session.Semesters.Min(s => s.StartDate) 
                : new DateTime(session.YearStart, 9, 1);
        }
        
        public DateTime GetEndDate(AcademicSession session)
        {
            return session.Semesters.Any() 
                ? session.Semesters.Max(s => s.EndDate) 
                : new DateTime(session.YearEnd, 6, 30);
        }
        
        public bool IsSessionActive(AcademicSession session)
        {
            DateTime now = DateTime.Now;
            DateTime startDate = GetStartDate(session);
            DateTime endDate = GetEndDate(session);
            
            return now >= startDate && now <= endDate;
        }
        
        public bool IsActiveSemester(Semester semester)
        {
            DateTime now = DateTime.Now;
            return now >= semester.StartDate && now <= semester.EndDate;
        }
        
        public bool IsFutureSemester(Semester semester)
        {
            return DateTime.Now < semester.StartDate;
        }
        
        public bool IsCurrentSemester(Semester semester)
        {
            return IsActiveSemester(semester);
        }
        
        public string GetSemesterName(SemesterType type)
        {
            return type switch
            {
                SemesterType.FirstSemester => "First Semester",
                SemesterType.SecondSemester => "Second Semester",
                _ => type.ToString()
            };
        }
        
        public string GetRemainingTime(DateTime endDate)
        {
            TimeSpan remaining = endDate - DateTime.Now;
            
            if (remaining.TotalDays <= 0)
            {
                return "Expired";
            }
            
            if (remaining.TotalDays > 30)
            {
                int months = (int)(remaining.TotalDays / 30);
                return $"{months} month{(months > 1 ? "s" : "")}";
            }
            
            if (remaining.TotalDays >= 1)
            {
                int days = (int)remaining.TotalDays;
                return $"{days} day{(days > 1 ? "s" : "")}";
            }
            
            int hours = (int)remaining.TotalHours;
            return $"{hours} hour{(hours > 1 ? "s" : "")}";
        }

        public void ViewSessionDetails(AcademicSession session)
        {
            Console.WriteLine($"Viewing details for session {session.SessionId}");
        }

        public short GetMaxAllowedEndYear()
        {
            return (short)(DateTime.Now.Year + 4);
        }

        public short GetMinAllowedStartYear()
        {
            return (short)DateTime.Now.Year;
        }

        private List<AcademicSession> GetAllSessions()
        {
            var allSessions = new List<AcademicSession>();
            
            if (currentSession != null)
                allSessions.Add(currentSession);
                
            if (nextSession != null)
                allSessions.Add(nextSession);
                
            allSessions.AddRange(archivedSessions);
            
            return allSessions;
        }

        public string GetHealthStatusDisplay()
        {
            if (lastHealthReport == null) return "Unknown";
            
            return lastHealthReport.OverallHealth switch
            {
                SystemHealthStatus.Healthy => "Healthy",
                SystemHealthStatus.Warning => "Warning",
                SystemHealthStatus.Error => "Error",
                SystemHealthStatus.Critical => "Critical",
                _ => "Unknown"
            };
        }

        public string GetHealthStatusColor()
        {
            if (lastHealthReport == null) return "text-gray-500";
            
            return lastHealthReport.OverallHealth switch
            {
                SystemHealthStatus.Healthy => "text-green-500",
                SystemHealthStatus.Warning => "text-yellow-500",
                SystemHealthStatus.Error => "text-orange-500",
                SystemHealthStatus.Critical => "text-red-500",
                _ => "text-gray-500"
            };
        }
        #endregion

        #region Firebase Operations
        private async Task SaveSessionToFirebase(AcademicSession session)
        {
            try
            {
                Console.WriteLine($"Saving session {session.SessionId} to Firebase");
                
                // Use session ID as document ID for readable Firebase structure
                var documentId = await FirestoreService.AddDocumentAsync<AcademicSession>(
                    ACADEMIC_SESSIONS_COLLECTION,
                    session,
                    session.SessionId // Use session ID as custom document ID
                );
                
                if (!string.IsNullOrEmpty(documentId))
                {
                    Console.WriteLine($"Successfully saved session: {session.SessionId}");
                }
                else
                {
                    // If add fails, try update (session might already exist)
                    bool updateSuccess = await FirestoreService.UpdateDocumentAsync<AcademicSession>(
                        ACADEMIC_SESSIONS_COLLECTION,
                        session.SessionId,
                        session
                    );
                    
                    if (updateSuccess)
                    {
                        Console.WriteLine($"Successfully updated existing session: {session.SessionId}");
                    }
                    else
                    {
                        Console.WriteLine($"Failed to save or update session: {session.SessionId}");
                        throw new Exception($"Failed to save session {session.SessionId} to Firebase");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving session to Firebase: {ex.Message}");
                Console.WriteLine($"Session ID: {session.SessionId}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                NotificationComponent?.ShowError($"Database error: Failed to save session {session.SessionId}");
                throw; // Re-throw to allow calling method to handle
            }
        }
        #endregion

    }
}
