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
        
        // Timer for countdown
        private System.Threading.Timer timer;
        
        // Connection status
        private bool IsInitialized;
        private bool IsConnected;

        // Firebase constants
        private const string ACADEMIC_SESSIONS_COLLECTION = "ACADEMIC_SESSIONS";
        private const string ACADEMIC_SESSIONS_DOCUMENT = "AcademicSessions";
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
                
                // Get the document containing all academic sessions
                var sessionsData = await GetFirebaseDocument();
                
                if (sessionsData != null && sessionsData.Any())
                {
                    Console.WriteLine($"Successfully loaded {sessionsData.Count} sessions from Firebase");
                    ProcessLoadedSessions(sessionsData);
                }
                else
                {
                    Console.WriteLine("No sessions found in Firebase, creating demo data");
                    CreateDemoData();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading sessions: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                CreateDemoData();
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

                Console.WriteLine($"Current session: {(currentSession != null ? $"{currentSession.YearStart}-{currentSession.YearEnd}" : "None")}");
                Console.WriteLine($"Next session: {(nextSession != null ? $"{nextSession.YearStart}-{nextSession.YearEnd}" : "None")}");
                Console.WriteLine($"Archived sessions: {archivedSessions.Count}");
            }
        }

        private void CreateDemoData()
        {
            Console.WriteLine("Creating demo data for academic sessions");
            
            // Demo current session
            currentSession = new AcademicSession
            {
                SessionId = Guid.NewGuid().ToString(),
                YearStart = 2024,
                YearEnd = 2025,
                SecurityToken = Guid.NewGuid().ToString(),
                LastModified = DateTime.Now,
                ModifiedBy = "System",
                Semesters = new List<Semester>
                {
                    new Semester
                    {
                        SemesterId = Guid.NewGuid().ToString(),
                        Type = SemesterType.FirstSemester,
                        SessionId = Guid.NewGuid().ToString(),
                        StartDate = new DateTime(2024, 9, 1),
                        EndDate = new DateTime(2025, 1, 31),
                        SecurityToken = Guid.NewGuid().ToString(),
                        LastModified = DateTime.Now,
                        ModifiedBy = "System"
                    }
                }
            };
            
            // Demo archived session
            archivedSessions.Add(CreateDemoArchivedSession());
            
            Console.WriteLine("Demo data created successfully");
        }

        private AcademicSession CreateDemoArchivedSession()
        {
            return new AcademicSession
            {
                SessionId = Guid.NewGuid().ToString(),
                YearStart = 2023,
                YearEnd = 2024,
                SecurityToken = Guid.NewGuid().ToString(),
                LastModified = DateTime.Now.AddYears(-1),
                ModifiedBy = "System",
                Semesters = new List<Semester>
                {
                    new Semester
                    {
                        SemesterId = Guid.NewGuid().ToString(),
                        Type = SemesterType.FirstSemester,
                        SessionId = Guid.NewGuid().ToString(),
                        StartDate = new DateTime(2023, 9, 1),
                        EndDate = new DateTime(2024, 1, 31),
                        SecurityToken = Guid.NewGuid().ToString(),
                        LastModified = DateTime.Now.AddYears(-1),
                        ModifiedBy = "System"
                    },
                    new Semester
                    {
                        SemesterId = Guid.NewGuid().ToString(),
                        Type = SemesterType.SecondSemester,
                        SessionId = Guid.NewGuid().ToString(),
                        StartDate = new DateTime(2024, 2, 1),
                        EndDate = new DateTime(2024, 6, 30),
                        SecurityToken = Guid.NewGuid().ToString(),
                        LastModified = DateTime.Now.AddYears(-1),
                        ModifiedBy = "System"
                    }
                }
            };
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
                }
                else if (activeModal == ModalType.CreateSemester)
                {
                    await CreateNewSemester();
                }
                
                CloseModal();
                CheckForWarnings();
                
                Console.WriteLine("Modal saved successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving modal: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }

        private async Task CreateNewSession()
        {
            Console.WriteLine("Creating new academic session");
            
            var newSession = new AcademicSession
            {
                SessionId = Guid.NewGuid().ToString(),
                YearStart = sessionForm.YearStart,
                YearEnd = sessionForm.YearEnd,
                SecurityToken = Guid.NewGuid().ToString(),
                LastModified = DateTime.Now,
                ModifiedBy = "System",
                Semesters = new List<Semester>()
            };
            
            if (includeFirstSemester)
            {
                AddFirstSemester(newSession);
            }
            
            CategorizeNewSession(newSession);
            await SaveAllSessionsToFirebase();
            
            Console.WriteLine($"New session created: {newSession.YearStart}-{newSession.YearEnd}");
        }

        private void AddFirstSemester(AcademicSession session)
        {
            var newSemester = new Semester
            {
                SemesterId = Guid.NewGuid().ToString(),
                Type = SemesterType.FirstSemester,
                SessionId = session.SessionId,
                StartDate = firstSemesterForm.StartDate,
                EndDate = firstSemesterForm.EndDate,
                SecurityToken = Guid.NewGuid().ToString(),
                LastModified = DateTime.Now,
                ModifiedBy = "System"
            };
            
            session.Semesters.Add(newSemester);
            Console.WriteLine($"Added first semester to session: {session.YearStart}-{session.YearEnd}");
        }

        private void CategorizeNewSession(AcademicSession newSession)
        {
            DateTime startDate = GetStartDate(newSession);
            if (startDate <= DateTime.Now)
            {
                if (currentSession != null)
                {
                    archivedSessions.Add(currentSession);
                    Console.WriteLine($"Moved current session to archived: {currentSession.YearStart}-{currentSession.YearEnd}");
                }
                currentSession = newSession;
                Console.WriteLine($"Set as current session: {newSession.YearStart}-{newSession.YearEnd}");
            }
            else
            {
                nextSession = newSession;
                Console.WriteLine($"Set as next session: {newSession.YearStart}-{newSession.YearEnd}");
            }
        }

        private async Task CreateNewSemester()
        {
            Console.WriteLine("Creating new semester");
            
            AcademicSession targetSession = GetTargetSession();
            
            var newSemester = new Semester
            {
                SemesterId = Guid.NewGuid().ToString(),
                Type = semesterForm.Type,
                SessionId = targetSession.SessionId,
                StartDate = semesterForm.StartDate,
                EndDate = semesterForm.EndDate,
                SecurityToken = Guid.NewGuid().ToString(),
                LastModified = DateTime.Now,
                ModifiedBy = "System"
            };
            
            var updatedSemesters = new List<Semester>(targetSession.Semesters) { newSemester };
            AcademicSession updatedSession = targetSession with { Semesters = updatedSemesters };
            
            UpdateSessionReference(targetSession, updatedSession);
            await SaveAllSessionsToFirebase();
            
            Console.WriteLine($"New semester created for session: {targetSession.YearStart}-{targetSession.YearEnd}");
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
            Console.WriteLine($"Viewing details for session {session.YearStart}-{session.YearEnd}");
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
        private async Task<List<AcademicSession>> GetFirebaseDocument()
        {
            try
            {
                Console.WriteLine($"Attempting to get document from collection: {ACADEMIC_SESSIONS_COLLECTION}, document: {ACADEMIC_SESSIONS_DOCUMENT}");
        
                var result = await FirestoreService.GetDocumentAsync<AcademicSessionsContainer>(
                    ACADEMIC_SESSIONS_COLLECTION, 
                    ACADEMIC_SESSIONS_DOCUMENT
                );
        
                if (result?.Sessions != null)
                {
                    Console.WriteLine($"Successfully retrieved {result.Sessions.Count} sessions from Firebase");
                    return result.Sessions;
                }
                else
                {
                    Console.WriteLine($"Document '{ACADEMIC_SESSIONS_DOCUMENT}' not found or contains no sessions in collection '{ACADEMIC_SESSIONS_COLLECTION}'");
                    return new List<AcademicSession>();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error retrieving Firebase document: {ex.Message}");
                Console.WriteLine($"Collection: {ACADEMIC_SESSIONS_COLLECTION}, Document: {ACADEMIC_SESSIONS_DOCUMENT}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return new List<AcademicSession>();
            }
        }
        
        private async Task SaveAllSessionsToFirebase()
{
    try
    {
        var allSessions = GetAllSessions();
        Console.WriteLine($"Saving {allSessions.Count} sessions to Firebase");
        
        // Wrap the sessions list in a container object
        var sessionContainer = new AcademicSessionsContainer
        {
            Sessions = allSessions,
            LastUpdated = DateTime.Now,
            UpdatedBy = "System"
        };
        
        // Use consistent JSON settings
        var settings = new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.None,
            NullValueHandling = NullValueHandling.Ignore,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore
        };
        
        // Try to update existing document first
        bool updateSuccess = await FirestoreService.UpdateDocumentAsync<AcademicSessionsContainer>(
            ACADEMIC_SESSIONS_COLLECTION,
            ACADEMIC_SESSIONS_DOCUMENT,
            sessionContainer
        );
        
        if (updateSuccess)
        {
            Console.WriteLine("Successfully updated academic sessions in Firebase");
        }
        else
        {
            // If update fails, try to add as new document
            Console.WriteLine("Update failed, attempting to create new document");
            
            var documentId = await FirestoreService.AddDocumentAsync<AcademicSessionsContainer>(
                ACADEMIC_SESSIONS_COLLECTION,
                sessionContainer
            );
            
            if (!string.IsNullOrEmpty(documentId))
            {
                Console.WriteLine($"Successfully created new academic sessions document: {documentId}");
            }
            else
            {
                Console.WriteLine("Failed to create new academic sessions document");
                throw new Exception("Failed to save academic sessions to Firebase");
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error saving academic sessions to Firebase: {ex.Message}");
        Console.WriteLine($"Collection: {ACADEMIC_SESSIONS_COLLECTION}, Document: {ACADEMIC_SESSIONS_DOCUMENT}");
        Console.WriteLine($"Stack trace: {ex.StackTrace}");
        throw; // Re-throw to allow calling method to handle
    }
}
        #endregion

        #region Form Models and Enums
        // Add this class to your ManageAcademicSession.cs file
        public class AcademicSessionsContainer
        {
            public List<AcademicSession> Sessions { get; set; } = new List<AcademicSession>();
            public DateTime LastUpdated { get; set; } = DateTime.Now;
            public string UpdatedBy { get; set; } = "System";
        }
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