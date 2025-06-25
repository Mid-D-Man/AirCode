using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using System.ComponentModel.DataAnnotations;

namespace AirCode.Pages.Shared
{/// <summary>
 // this page is supposed to handle clients requesting support and looking up commonly gound issues
 /// </summary>
    public partial class ContactUs : ComponentBase
    {
        #region Constants
        private const string FAQ_SECTION = "faq";
        private const string GUIDES_SECTION = "guides";
        private const string CONTACT_SECTION = "contact";
        private const string STATUS_SECTION = "status";
        
        private const string EMAIL_CONTACT = "email";
        private const string LIVE_CHAT = "live_chat";
        private const string REPORT_BUG = "report_bug";
        #endregion

        #region Fields
        private string activeSection = FAQ_SECTION;
        private string searchTerm = string.Empty;
        private bool showErrorModal = false;
        private bool isSubmitting = false;
        private bool isSubmittingError = false;
        private bool showNotification = false;
        private string notificationMessage = string.Empty;
        private string notificationType = "success";
        private string systemStatus = "All Systems Operational";
        private DateTime lastStatusUpdate = DateTime.Now;

        private readonly HashSet<int> expandedFaqIds = new();
        private readonly ContactFormModel contactForm = new();
        private readonly ErrorReportModel errorReport = new();

        private List<FaqItem> allFaqs = new();
        private List<GuideItem> allGuides = new();
        private List<FaqItem> filteredFaqs = new();
        private List<GuideItem> filteredGuides = new();
        private readonly List<ServiceStatus> serviceStatuses = new();
        private readonly Dictionary<string, string> contactCategories = new();
        #endregion

        #region Lifecycle
        protected override async Task OnInitializedAsync()
        {
            InitializeData();
            FilterContent();
            await base.OnInitializedAsync();
        }

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender)
            {
                await JSRuntime.InvokeVoidAsync("initializeContactAnimations");
            }
        }
        #endregion

        #region Data Models
        public class FaqItem
        {
            public int Id { get; set; }
            public string Icon { get; set; } = string.Empty;
            public string Question { get; set; } = string.Empty;
            public string Answer { get; set; } = string.Empty;
            public string ActionText { get; set; } = string.Empty;
            public string ActionType { get; set; } = string.Empty;
            public List<string> Tags { get; set; } = new();
        }

        public class GuideItem
        {
            public int Id { get; set; }
            public string Icon { get; set; } = string.Empty;
            public string Title { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public string Difficulty { get; set; } = string.Empty;
            public string EstimatedTime { get; set; } = string.Empty;
            public List<string> Tags { get; set; } = new();
        }

        public class ServiceStatus
        {
            public string Name { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public string Status { get; set; } = string.Empty;
        }

        public class ContactFormModel
        {
            [Required(ErrorMessage = "Subject is required")]
            public string Subject { get; set; } = string.Empty;

            [Required(ErrorMessage = "Category is required")]
            public string Category { get; set; } = string.Empty;

            [Required(ErrorMessage = "Message is required")]
            [MinLength(10, ErrorMessage = "Message must be at least 10 characters")]
            public string Message { get; set; } = string.Empty;

            [Required(ErrorMessage = "Email is required")]
            [EmailAddress(ErrorMessage = "Invalid email format")]
            public string Email { get; set; } = string.Empty;
        }

        public class ErrorReportModel
        {
            [Required(ErrorMessage = "Error description is required")]
            public string Description { get; set; } = string.Empty;

            public string Steps { get; set; } = string.Empty;
        }
        #endregion

        #region Initialization
        private void InitializeData()
        {
            InitializeFaqs();
            InitializeGuides();
            InitializeServiceStatuses();
            InitializeContactCategories();
        }

        private void InitializeFaqs()
        {
            allFaqs = new List<FaqItem>
            {
                new FaqItem
                {
                    Id = 1,
                    Icon = "🔐",
                    Question = "How do I reset my password?",
                    Answer = "To reset your password, go to the login page and click 'Forgot Password'. Enter your email address and follow the instructions sent to your email.",
                    ActionText = "Go to Login",
                    ActionType = "navigate_login",
                    Tags = new List<string> { "password", "login", "authentication", "reset" }
                },
                new FaqItem
                {
                    Id = 2,
                    Icon = "📱",
                    Question = "QR code not scanning properly?",
                    Answer = "Ensure your camera has proper lighting and the QR code is clearly visible. Clean your camera lens and try holding the device steady. If issues persist, try using the manual code entry option.",
                    ActionText = "Scan QR Code",
                    ActionType = "navigate_scan",
                    Tags = new List<string> { "qr code", "scanner", "camera", "attendance" }
                },
                new FaqItem
                {
                    Id = 3,
                    Icon = "📊",
                    Question = "How do I view my attendance records?",
                    Answer = "Your attendance records are available in your dashboard. Navigate to 'My Stats' to view detailed attendance history, including dates, courses, and attendance percentages.",
                    ActionText = "View Stats",
                    ActionType = "navigate_stats",
                    Tags = new List<string> { "attendance", "records", "stats", "dashboard" }
                },
                new FaqItem
                {
                    Id = 4,
                    Icon = "🌐",
                    Question = "App not working offline?",
                    Answer = "AirCode supports offline functionality. Ensure you've logged in at least once while online. Your attendance will be synced when you reconnect to the internet.",
                    ActionText = "Check Connection",
                    ActionType = "check_connection",
                    Tags = new List<string> { "offline", "sync", "connection", "internet" }
                },
                new FaqItem
                {
                    Id = 5,
                    Icon = "👥",
                    Question = "How do I add students to my course?",
                    Answer = "As a lecturer, go to your course management dashboard, select the course, and use the 'Add Students' option. You can add students individually or bulk import from a CSV file.",
                    ActionText = "Manage Courses",
                    ActionType = "navigate_courses",
                    Tags = new List<string> { "students", "course", "lecturer", "management" }
                },
                new FaqItem
                {
                    Id = 6,
                    Icon = "⚡",
                    Question = "App running slowly?",
                    Answer = "Clear your browser cache and cookies. Ensure you're running the latest version of your browser. If the problem persists, try using a different browser or device.",
                    ActionText = "Clear Cache",
                    ActionType = "clear_cache",
                    Tags = new List<string> { "performance", "slow", "cache", "browser" }
                }
            };
        }

        private void InitializeGuides()
        {
            allGuides = new List<GuideItem>
            {
                new GuideItem
                {
                    Id = 1,
                    Icon = "🚀",
                    Title = "Getting Started Guide",
                    Description = "Complete setup guide for new users",
                    Difficulty = "Beginner",
                    EstimatedTime = "5 min",
                    Tags = new List<string> { "setup", "beginner", "getting started" }
                },
                new GuideItem
                {
                    Id = 2,
                    Icon = "📚",
                    Title = "Course Management",
                    Description = "How to create and manage courses as an admin",
                    Difficulty = "Intermediate",
                    EstimatedTime = "10 min",
                    Tags = new List<string> { "admin", "courses", "management" }
                },
                new GuideItem
                {
                    Id = 3,
                    Icon = "📱",
                    Title = "QR Code Scanning",
                    Description = "Best practices for scanning attendance QR codes",
                    Difficulty = "Beginner",
                    EstimatedTime = "3 min",
                    Tags = new List<string> { "qr code", "scanning", "attendance" }
                },
                new GuideItem
                {
                    Id = 4,
                    Icon = "📊",
                    Title = "Generating Reports",
                    Description = "Create detailed attendance and performance reports",
                    Difficulty = "Advanced",
                    EstimatedTime = "15 min",
                    Tags = new List<string> { "reports", "analytics", "advanced" }
                },
                new GuideItem
                {
                    Id = 5,
                    Icon = "🔧",
                    Title = "Troubleshooting Common Issues",
                    Description = "Solutions for the most common technical problems",
                    Difficulty = "Intermediate",
                    EstimatedTime = "8 min",
                    Tags = new List<string> { "troubleshooting", "issues", "solutions" }
                }
            };
        }

        private void InitializeServiceStatuses()
        {
            serviceStatuses.AddRange(new[]
            {
                new ServiceStatus
                {
                    Name = "Authentication Service",
                    Description = "User login and authentication",
                    Status = "Operational"
                },
                new ServiceStatus
                {
                    Name = "QR Code Generation",
                    Description = "Attendance QR code creation",
                    Status = "Operational"
                },
                new ServiceStatus
                {
                    Name = "Database Service",
                    Description = "Data storage and retrieval",
                    Status = "Operational"
                },
                new ServiceStatus
                {
                    Name = "Offline Sync",
                    Description = "Synchronization when back online",
                    Status = "Operational"
                },
                new ServiceStatus
                {
                    Name = "Report Generation",
                    Description = "Attendance and analytics reports",
                    Status = "Operational"
                }
            });
        }

        private void InitializeContactCategories()
        {
            contactCategories.Add("technical", "Technical Issue");
            contactCategories.Add("account", "Account Problem");
            contactCategories.Add("feature", "Feature Request");
            contactCategories.Add("bug", "Bug Report");
            contactCategories.Add("general", "General Question");
            contactCategories.Add("feedback", "Feedback");
        }
        #endregion

        #region Event Handlers
        private void ShowSection(string section)
        {
            activeSection = section;
            StateHasChanged();
        }

        private void HandleSearchChanged(string newSearchTerm)
        {
            searchTerm = newSearchTerm;
            FilterContent();
            StateHasChanged();
        }

        private void ToggleFaq(int faqId)
        {
            if (expandedFaqIds.Contains(faqId))
            {
                expandedFaqIds.Remove(faqId);
            }
            else
            {
                expandedFaqIds.Add(faqId);
            }
            StateHasChanged();
        }

        private async Task HandleFaqAction(string actionType)
        {
            switch (actionType)
            {
                case "navigate_login":
                    await JSRuntime.InvokeVoidAsync("navigateToPage", "Authentication");
                    break;
                case "navigate_scan":
                    await JSRuntime.InvokeVoidAsync("navigateToPage", "Client/ScanPage");
                    break;
                case "navigate_stats":
                    await JSRuntime.InvokeVoidAsync("navigateToPage", "Client/ClientStats");
                    break;
                case "navigate_courses":
                    await JSRuntime.InvokeVoidAsync("navigateToPage", "Admin/LecturerCoursesPage");
                    break;
                case "check_connection":
                    await JSRuntime.InvokeVoidAsync("checkInternetConnection");
                    break;
                case "clear_cache":
                    await JSRuntime.InvokeVoidAsync("clearBrowserCache");
                    break;
            }
        }

        private async Task NavigateToGuide(int guideId)
        {
            await JSRuntime.InvokeVoidAsync("navigateToGuide", guideId);
        }

        private async Task HandleContactMethod(string method)
        {
            switch (method)
            {
                case EMAIL_CONTACT:
                    ShowSection(CONTACT_SECTION);
                    break;
                case LIVE_CHAT:
                    await JSRuntime.InvokeVoidAsync("openLiveChat");
                    break;
                case REPORT_BUG:
                    showErrorModal = true;
                    StateHasChanged();
                    break;
            }
        }

        private async Task SubmitContactForm()
        {
            if (ValidateContactForm())
            {
                isSubmitting = true;
                StateHasChanged();

                try
                {
                    // Simulate API call
                    await Task.Delay(2000);
                    
                    // Process form submission
                    await ProcessContactSubmission();
                    
                    ShowNotification("Message sent successfully! We'll get back to you soon.", "success");
                    ClearContactForm();
                }
                catch (Exception ex)
                {
                    ShowNotification($"Error sending message: {ex.Message}", "error");
                }
                finally
                {
                    isSubmitting = false;
                    StateHasChanged();
                }
            }
        }

        private void ClearContactForm()
        {
            contactForm.Subject = string.Empty;
            contactForm.Category = string.Empty;
            contactForm.Message = string.Empty;
            contactForm.Email = string.Empty;
            StateHasChanged();
        }

        private async Task SubmitErrorReport()
        {
            if (ValidateErrorReport())
            {
                isSubmittingError = true;
                StateHasChanged();

                try
                {
                    await Task.Delay(1500);
                    await ProcessErrorReport();
                    
                    ShowNotification("Error report submitted successfully. Thank you for helping us improve!", "success");
                    CloseErrorModal();
                }
                catch (Exception ex)
                {
                    ShowNotification($"Error submitting report: {ex.Message}", "error");
                }
                finally
                {
                    isSubmittingError = false;
                    StateHasChanged();
                }
            }
        }

        private void CloseErrorModal()
        {
            showErrorModal = false;
            errorReport.Description = string.Empty;
            errorReport.Steps = string.Empty;
            StateHasChanged();
        }

        private void ShowNotification(string message, string type)
        {
            notificationMessage = message;
            notificationType = type;
            showNotification = true;
            StateHasChanged();

            // Auto-hide notification after 5 seconds
            Task.Delay(5000).ContinueWith(_ =>
            {
                showNotification = false;
                InvokeAsync(StateHasChanged);
            });
        }
        #endregion

        #region Helper Methods
        private void FilterContent()
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
            {
                filteredFaqs = allFaqs;
                filteredGuides = allGuides;
            }
            else
            {
                var searchLower = searchTerm.ToLower();
                
                filteredFaqs = allFaqs.Where(f => 
                    f.Question.ToLower().Contains(searchLower) ||
                    f.Answer.ToLower().Contains(searchLower) ||
                    f.Tags.Any(t => t.ToLower().Contains(searchLower))
                ).ToList();

                filteredGuides = allGuides.Where(g => 
                    g.Title.ToLower().Contains(searchLower) ||
                    g.Description.ToLower().Contains(searchLower) ||
                    g.Tags.Any(t => t.ToLower().Contains(searchLower))
                ).ToList();
            }
        }

        private bool ValidateContactForm()
        {
            var isValid = !string.IsNullOrWhiteSpace(contactForm.Subject) &&
                         !string.IsNullOrWhiteSpace(contactForm.Category) &&
                         !string.IsNullOrWhiteSpace(contactForm.Message) &&
                         !string.IsNullOrWhiteSpace(contactForm.Email) &&
                         contactForm.Message.Length >= 10 &&
                         IsValidEmail(contactForm.Email);

            if (!isValid)
            {
                ShowNotification("Please fill in all required fields correctly.", "error");
            }

            return isValid;
        }

        private bool ValidateErrorReport()
        {
            var isValid = !string.IsNullOrWhiteSpace(errorReport.Description);

            if (!isValid)
            {
                ShowNotification("Please provide an error description.", "error");
            }

            return isValid;
        }

        private bool IsValidEmail(string email)
        {
            try
            {
                var addr = new System.Net.Mail.MailAddress(email);
                return addr.Address == email;
            }
            catch
            {
                return false;
            }
        }

        private async Task ProcessContactSubmission()
        {
            // Implement actual contact form submission logic
            await JSRuntime.InvokeVoidAsync("submitContactForm", new
            {
                subject = contactForm.Subject,
                category = contactForm.Category,
                message = contactForm.Message,
                email = contactForm.Email,
                timestamp = DateTime.UtcNow
            });
        }

        private async Task ProcessErrorReport()
        {
            // Implement actual error report submission logic
            await JSRuntime.InvokeVoidAsync("submitErrorReport", new
            {
                description = errorReport.Description,
                steps = errorReport.Steps,
                userAgent = await JSRuntime.InvokeAsync<string>("getUserAgent"),
                timestamp = DateTime.UtcNow
            });
        }
        #endregion
    }
}