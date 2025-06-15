using AirCode.Domain.Entities;
using AirCode.Domain.Enums;
using AirCode.Services.Firebase;
using AirCode.Utilities.HelperScripts;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Newtonsoft.Json;
using System.ComponentModel;

namespace AirCode.Pages.Admin.Superior
{
    public partial class ManageAcademicSession : IDisposable
    {
        [Inject] private IJSRuntime JSRuntime { get; set; }
        [Inject] private NavigationManager NavigationManager { get; set; }
        [Inject] private IFirestoreService FirestoreService { get; set; }

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
        // Add this with other private fields
private AirCode.Components.SharedPrefabs.Cards.NotificationComponent notificationComponent;
        // Timer for countdown
        private System.Threading.Timer timer;
        
        // Connection status
        private bool IsInitialized;
        private bool IsConnected;

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
            await LoadSessions();
            SetupCountdownTimer();
            CheckForWarnings();
        }

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender)
            {
                // Additional initialization logic if needed
            }
        }

        public void Dispose()
        {
            timer?.Dispose();
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
        Console.WriteLine($"Loading sessions from Firebase collection: {ACADEMIC_SESSIONS_COLLECTION}");
        
        var allSessions = await GetAllSessionsFromFirebase();
        
        if (allSessions != null && allSessions.Any())
        {
            Console.WriteLine($"Successfully loaded {allSessions.Count} sessions from Firebase");
            ProcessLoadedSessions(allSessions);
            NotificationComponent?.ShowInfo($"Loaded {allSessions.Count} academic sessions");
        }
        else
        {
            Console.WriteLine("No sessions found in Firebase");
            // Initialize empty lists - no demo data creation
            currentSession = null;
            nextSession = null;
            archivedSessions = new List<AcademicSession>();
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
        private void ProcessLoadedSessions(List<AcademicSession> allSessions)
        {
            if (allSessions?.Count > 0)
            {
                DateTime now = DateTime.Now;
                
                Console.WriteLine($"Processing {allSessions.Count} loaded sessions");
                
                // Categorize sessions
                currentSession = allSessions.FirstOrDefault(s => IsSessionActive(s));
                nextSession = allSessions.FirstOrDefault(s => GetStartDate(s) > now);
                archivedSessions = allSessions.Where(s => GetEndDate(s) < now).ToList();

                Console.WriteLine($"Current session: {(currentSession != null ? currentSession.SessionId : "None")}");
                Console.WriteLine($"Next session: {(nextSession != null ? nextSession.SessionId : "None")}");
                Console.WriteLine($"Archived sessions: {archivedSessions.Count}");
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
        CheckForWarnings();
        
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
            
            CategorizeNewSession(newSession);
            await SaveSessionToFirebase(newSession);
            
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

        private void CategorizeNewSession(AcademicSession newSession)
        {
            DateTime startDate = GetStartDate(newSession);
            if (startDate <= DateTime.Now)
            {
                if (currentSession != null)
                {
                    archivedSessions.Add(currentSession);
                    Console.WriteLine($"Moved current session to archived: {currentSession.SessionId}");
                }
                currentSession = newSession;
                Console.WriteLine($"Set as current session: {newSession.SessionId}");
            }
            else
            {
                nextSession = newSession;
                Console.WriteLine($"Set as next session: {newSession.SessionId}");
            }
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
            
            UpdateSessionReference(targetSession, updatedSession);
            await SaveSessionToFirebase(updatedSession);
            
            Console.WriteLine($"New semester created for session: {targetSession.SessionId}");
        }

        private void UpdateSessionReference(AcademicSession originalSession, AcademicSession updatedSession)
        {
            if (originalSession == currentSession)
            {
                currentSession = updatedSession;
            }
            else if (originalSession == nextSession)
            {
                nextSession = updatedSession;
            }
        }
        #endregion

        #region Warning System
        private void CheckForWarnings()
{
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
                await InvokeAsync(StateHasChanged);
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
        #endregion

        #region Firebase Operations
        private async Task<List<AcademicSession>> GetAllSessionsFromFirebase()
        {
            try
            {
                Console.WriteLine($"Retrieving all sessions from collection: {ACADEMIC_SESSIONS_COLLECTION}");
        
                var allSessions = await FirestoreService.GetCollectionAsync<AcademicSession>(ACADEMIC_SESSIONS_COLLECTION);
                
                if (allSessions != null && allSessions.Any())
                {
                    Console.WriteLine($"Successfully retrieved {allSessions.Count} sessions from Firebase");
                    return allSessions;
                }
                else
                {
                    Console.WriteLine($"No sessions found in collection '{ACADEMIC_SESSIONS_COLLECTION}'");
                    return new List<AcademicSession>();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error retrieving sessions from Firebase: {ex.Message}");
                Console.WriteLine($"Collection: {ACADEMIC_SESSIONS_COLLECTION}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return new List<AcademicSession>();
            }
        }
        
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

        #region Form Models and Enums
        public class SessionFormModel
        {
            public short YearStart { get; set; }
            public short YearEnd { get; set; }
        }
        
        public class SemesterFormModel
        {
            public SemesterType Type { get; set; }
            public DateTime StartDate { get; set; }
            public DateTime EndDate { get; set; }
        }
        
        public enum ModalType
        {
            CreateSession,
            CreateSemester
        }
        #endregion
    }
}
