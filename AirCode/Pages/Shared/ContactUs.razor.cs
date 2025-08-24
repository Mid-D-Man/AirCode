using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using System.ComponentModel.DataAnnotations;

namespace AirCode.Pages.Shared
{
    public partial class ContactUs : ComponentBase
    {
        private IJSObjectReference? jsModule;
        private bool moduleLoaded = false;

        [Inject]
        private NavigationManager NavigationManager { get; set; }
        [Inject]
 private ConnectivityService ConnectivityService { get; set; } = null!;
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
        private bool showGuideModal = false;
        private GuideItem? selectedGuide = null;
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
                await LoadJavaScriptModule();
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (jsModule != null)
            {
                try
                {
                    await jsModule.DisposeAsync();
                }
                catch (JSDisconnectedException)
                {
                    // Expected during circuit termination
                }
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
            public string FullContent { get; set; } = string.Empty;
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

        private async Task LoadJavaScriptModule()
        {
            try
            {
                jsModule = await JSRuntime.InvokeAsync<IJSObjectReference>(
                    "import", "./Pages/Shared/ContactUs.razor.js");
                
                await jsModule.InvokeVoidAsync("initializeContactAnimations");
                moduleLoaded = true;
            }
            catch (JSException ex)
            {
                try
                {
                    await JSRuntime.InvokeVoidAsync("initializeContactAnimations");
                    moduleLoaded = true;
                }
                catch (JSException)
                {
                    Console.WriteLine($"JavaScript initialization failed: {ex.Message}");
                }
            }
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
                    Icon = "👨‍🏫",
                    Question = "How do I manage my assigned courses as a lecturer?",
                    Answer = "As a lecturer, you can view and manage the courses assigned to you through your dashboard. Students enroll in courses independently - you cannot add students to courses.",
                    ActionText = "View My Courses",
                    ActionType = "navigate_courses",
                    Tags = new List<string> { "lecturer", "course", "management" }
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
                    FullContent = @"# Getting Started with AirCode

Welcome to AirCode! This guide will help you get up and running quickly.

## Step 1: Account Setup
1. Create your account using your institutional email
2. Verify your email address
3. Complete your profile information

## Step 2: Understanding Your Role
- **Students**: You can enroll in courses and mark attendance
- **Lecturers**: You can manage your assigned courses and view attendance
- **Admins**: You have full system access

## Step 3: First Login
1. Navigate to the login page
2. Enter your credentials
3. Complete any required setup steps

## Next Steps
- Explore your dashboard
- Set up your profile
- Join your first course (students) or review assigned courses (lecturers)",
                    Difficulty = "Beginner",
                    EstimatedTime = "5 min",
                    Tags = new List<string> { "setup", "beginner", "getting started" }
                },
                new GuideItem
                {
                    Id = 2,
                    Icon = "📚",
                    Title = "Course Management",
                    Description = "How to manage your courses",
                    FullContent = @"# Course Management Guide

## For Students
### Enrolling in Courses
1. Go to your dashboard
2. Click 'Browse Courses'
3. Search for your desired courses
4. Click 'Enroll' on the courses you want to join

### Viewing Your Courses
- Access your enrolled courses from the dashboard
- View course schedules and attendance requirements

## For Lecturers
### Managing Your Assigned Courses
1. View courses assigned to you in your dashboard
2. Monitor student attendance
3. Generate attendance reports
4. Update course information as needed

### Important Notes
- Lecturers cannot add students to courses
- Students must enroll themselves
- Course assignments are managed by administrators",
                    Difficulty = "Intermediate",
                    EstimatedTime = "10 min",
                    Tags = new List<string> { "courses", "management", "enrollment" }
                },
                new GuideItem
                {
                    Id = 3,
                    Icon = "📱",
                    Title = "QR Code Scanning",
                    Description = "Best practices for scanning attendance QR codes",
                    FullContent = @"# QR Code Scanning Guide

## How to Scan QR Codes for Attendance
1. Navigate to the scan page
2. Allow camera permissions when prompted
3. Point your camera at the QR code
4. Hold steady until the code is recognized

## Troubleshooting Scanning Issues
### Common Problems and Solutions:
- **Poor lighting**: Move to a well-lit area
- **Blurry camera**: Clean your camera lens
- **Code not scanning**: Try moving closer or further away
- **Camera not working**: Check browser permissions

## Best Practices
- Ensure the QR code is clearly visible
- Hold your device steady
- Scan from about 6-12 inches away
- Use manual code entry if scanning fails

## Manual Code Entry
If scanning fails, you can enter the code manually:
1. Click 'Manual Entry'
2. Type the attendance code
3. Submit",
                    Difficulty = "Beginner",
                    EstimatedTime = "3 min",
                    Tags = new List<string> { "qr code", "scanning", "attendance" }
                },
                new GuideItem
                {
                    Id = 4,
                    Icon = "📊",
                    Title = "Generating Reports",
                    Description = "Create attendance and performance reports",
                    FullContent = @"# Generating Reports

## Available Report Types
### For Students
- Personal attendance history
- Course performance summary
- Monthly attendance statistics

### For Lecturers
- Class attendance reports
- Student performance analytics
- Course completion rates

### For Admins
- Institution-wide statistics
- Detailed user analytics
- System usage reports

## How to Generate Reports
1. Navigate to the Reports section
2. Select your report type
3. Choose date range and filters
4. Click 'Generate Report'
5. Download or view online

## Report Formats
- PDF for formal documentation
- Excel for data analysis
- Online viewing for quick checks

## Scheduling Reports
- Set up automatic report generation
- Receive reports via email
- Configure report frequency",
                    Difficulty = "Advanced",
                    EstimatedTime = "15 min",
                    Tags = new List<string> { "reports", "analytics", "advanced" }
                },
                new GuideItem
                {
                    Id = 5,
                    Icon = "🔧",
                    Title = "Troubleshooting Common Issues",
                    Description = "Solutions for technical problems",
                    FullContent = @"# Troubleshooting Guide

## Login Issues
### Can't log in?
1. Check your email and password
2. Use 'Forgot Password' if needed
3. Clear browser cache and cookies
4. Try a different browser

## Performance Issues
### App running slowly?
1. Clear browser cache
2. Disable browser extensions
3. Check internet connection
4. Try incognito mode

## QR Code Issues
### Scanner not working?
1. Check camera permissions
2. Clean camera lens
3. Improve lighting
4. Use manual code entry

## Sync Issues
### Data not syncing?
1. Check internet connection
2. Refresh the page
3. Log out and log back in
4. Contact support if issues persist

## Browser Compatibility
Supported browsers:
- Chrome (recommended)
- Firefox
- Safari
- Edge

## When to Contact Support
Contact support if you experience:
- Persistent login failures
- Data loss or corruption
- Critical functionality not working
- Security concerns",
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
                new ServiceStatus { Name = "Authentication Service", Description = "User login and authentication", Status = "Operational" },
                new ServiceStatus { Name = "QR Code Generation", Description = "Attendance QR code creation", Status = "Operational" },
                new ServiceStatus { Name = "Database Service", Description = "Data storage and retrieval", Status = "Operational" },
                new ServiceStatus { Name = "Offline Sync", Description = "Synchronization when back online", Status = "Operational" },
                new ServiceStatus { Name = "Report Generation", Description = "Attendance and analytics reports", Status = "Operational" }
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
                expandedFaqIds.Remove(faqId);
            else
                expandedFaqIds.Add(faqId);
            StateHasChanged();
        }

        private async Task HandleFaqAction(string actionType)
        {
            var navigationMap = new Dictionary<string, string>
            {
                { "navigate_login", "Authentication" },
                { "navigate_scan", "Client/ScanPage" },
                { "navigate_stats", "Client/ClientStats" },
                { "navigate_courses", "Admin/LecturerCoursesPage" }
            };

            if (navigationMap.TryGetValue(actionType, out var route))
            {
                NavigationManager.NavigateTo(route);
            }
            else if (actionType == "check_connection")
            {
                await JSRuntime.InvokeVoidAsync("alert", ConnectivityService.IsOnline ? "Internet connection is active" : "No internet connection detected");
            }
            else if (actionType == "clear_cache")
            {
                await JSRuntime.InvokeVoidAsync("alert", "Please clear your browser cache manually and refresh the page.");
            }
        }

        private void NavigateToGuide(int guideId)
        {
            selectedGuide = allGuides.FirstOrDefault(g => g.Id == guideId);
            if (selectedGuide != null)
            {
                showGuideModal = true;
                StateHasChanged();
            }
        }

        private void CloseGuideModal()
        {
            showGuideModal = false;
            selectedGuide = null;
            StateHasChanged();
        }

        private async Task HandleContactMethod(string method)
        {
            switch (method)
            {
                case EMAIL_CONTACT:
                    ShowSection(CONTACT_SECTION);
                    break;
                case LIVE_CHAT:
                    ShowNotification("Live chat is not available. Please use the contact form or report issues on GitHub.", "info");
                    break;
                case REPORT_BUG:
                    NavigationManager.NavigateTo("https://github.com/mid-d-man/AirCode/issues", true);
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
                    await Task.Delay(2000); // Simulate processing
                    ShowNotification("Your message has been received. We'll get back to you via email soon!", "success");
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
                    NavigationManager.NavigateTo("https://github.com/mid-d-man/AirCode/issues", true);
                    CloseErrorModal();
                }
                catch (Exception ex)
                {
                    ShowNotification($"Error: {ex.Message}", "error");
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
                ShowNotification("Please fill in all required fields correctly.", "error");

            return isValid;
        }

        private bool ValidateErrorReport()
        {
            var isValid = !string.IsNullOrWhiteSpace(errorReport.Description);
            if (!isValid)
                ShowNotification("Please provide an error description.", "error");
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
        #endregion
    }
}