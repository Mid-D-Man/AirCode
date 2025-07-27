
using AirCode.Models.Search;
using AirCode.Services.Auth;
using Microsoft.AspNetCore.Components;
namespace AirCode.Services.Search;

public class SearchContextService : ISearchContextService
{
    private readonly NavigationManager _navigationManager;
    private readonly Dictionary<string, ISearchContextProvider> _contextProviders = new();

    public string CurrentContext { get; private set; } = "General";

    public SearchContextService(
        NavigationManager navigationManager
     )
    {
        _navigationManager = navigationManager;
       

        // Register built-in context providers
        RegisterDefaultProviders();
    }

    public void SetContext(string context)
    {
        CurrentContext = context ?? "General";
    }

    public Task<List<SearchSuggestion>> GetSuggestionsAsync(string searchTerm, int maxResults = 10)
    {
        return GetSuggestionsForContextAsync(CurrentContext, searchTerm, maxResults);
    }

    public async Task<List<SearchSuggestion>> GetSuggestionsForContextAsync(string context, string searchTerm,
        int maxResults = 10)
    {
        if (string.IsNullOrWhiteSpace(searchTerm) || searchTerm.Length < 2)
        {
            return new List<SearchSuggestion>();
        }

        var suggestions = new List<SearchSuggestion>();

        // Get context-specific suggestions
        if (_contextProviders.TryGetValue(context, out var provider))
        {
            suggestions.AddRange(await provider.GetSuggestionsAsync(searchTerm, maxResults));
        }

        // Always add general suggestions
        if (context != "General" && _contextProviders.TryGetValue("General", out var generalProvider))
        {
            suggestions.AddRange(await generalProvider.GetSuggestionsAsync(searchTerm, maxResults / 2));
        }

        // Sort by priority and relevance (higher priority first)
        return suggestions
            .OrderByDescending(s => s.Priority)
            .ThenBy(s => s.Text)
            .Take(maxResults)
            .ToList();
    }

    public async Task<List<SearchResult>> SearchAsync(string searchTerm, string context = null, int maxResults = 20)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return new List<SearchResult>();
        }

        context ??= CurrentContext;
        var results = new List<SearchResult>();

        // Get context-specific results
        if (_contextProviders.TryGetValue(context, out var provider))
        {
            results.AddRange(await provider.SearchAsync(searchTerm, maxResults));
        }

        // Always include general results
        if (context != "General" && _contextProviders.TryGetValue("General", out var generalProvider))
        {
            results.AddRange(await generalProvider.SearchAsync(searchTerm, maxResults / 2));
        }

        // Sort by relevance (higher first)
        return results
            .OrderByDescending(r => r.Relevance)
            .Take(maxResults)
            .ToList();
    }

    public void RegisterContextProvider(string context, ISearchContextProvider provider)
    {
        _contextProviders[context] = provider;
    }

    private void RegisterDefaultProviders()
    {
        // General context provider
        RegisterContextProvider("General", new GeneralSearchProvider(_navigationManager));

        /*
        // Courses context provider
        RegisterContextProvider("Courses", new CoursesSearchProvider(_courseService, _navigationManager));

        // Sessions context provider
        RegisterContextProvider("Sessions", new SessionsSearchProvider(_navigationManager));

        // Users context provider
        RegisterContextProvider("Users", new UsersSearchProvider(_userService, _navigationManager));
        */
    }

    // Built-in search providers

    private class GeneralSearchProvider : ISearchContextProvider
    {
        private readonly NavigationManager _navigationManager;
        private readonly List<SearchSuggestion> _staticSuggestions;

        public string Context => "General";

        public GeneralSearchProvider(NavigationManager navigationManager)
        {
            _navigationManager = navigationManager;

            // Define static navigation suggestions
            _staticSuggestions = new List<SearchSuggestion>
            {
                new()
                {
                    Text = "Dashboard", Context = "Navigation", Url = "/Admin/Dashboard",
                    IconPath = "/svgs/Admin_Icon.svg", Priority = 10
                },
                new()
                {
                    Text = "Create Session", Context = "Activity", Url = "/Admin/CreateSession",
                    IconPath = "/svgs/QRCode_Icon.svg", Priority = 9
                },
                new()
                {
                    Text = "User Management", Context = "Admin", Url = "/TestPage", IconPath = "/svgs/Users_Icon.svg",
                    Priority = 8
                },
                new()
                {
                    Text = "Admin Management", Context = "Admin", Url = "/admin/admins",
                    IconPath = "/svgs/Admin_Icon.svg", Priority = 7
                },
                new()
                {
                    Text = "Manage Courses", Context = "Courses", Url = "/Admin/ManageCourses",
                    IconPath = "/svgs/Admin_Icon.svg", Priority = 7
                },
                new()
                {
                    Text = "Reports", Context = "Analysis", Url = "/Admin/ShaderPage1",
                    IconPath = "/svgs/Report_Icon.svg", Priority = 6
                },
                new()
                {
                    Text = "Statistics", Context = "Analysis", Url = "/admin/stats", IconPath = "/svgs/Stats_Icon.svg",
                    Priority = 6
                },
                new()
                {
                    Text = "Settings", Context = "Preferences", Url = "/admin/settings",
                    IconPath = "/svgs/Admin_Icon.svg", Priority = 5
                },
                new()
                {
                    Text = "Contact", Context = "Support", Url = "/admin/contact",
                    IconPath = "/svgs/ContactUs_Icon.svg", Priority = 4
                },
                new()
                {
                    Text = "Logout", Context = "Auth", Url = "/auth", IconPath = "/svgs/Logout_Icon.svg", Priority = 3
                }
            };
        }

        public Task<List<SearchSuggestion>> GetSuggestionsAsync(string searchTerm, int maxResults)
        {
            searchTerm = searchTerm.ToLower();

            var suggestions = _staticSuggestions
                .Where(s => s.Text.ToLower().Contains(searchTerm) || s.Context.ToLower().Contains(searchTerm))
                .OrderByDescending(s => s.Priority)
                .ThenBy(s => s.Text)
                .Take(maxResults)
                .ToList();

            return Task.FromResult(suggestions);
        }

        public Task<List<SearchResult>> SearchAsync(string searchTerm, int maxResults)
        {
            searchTerm = searchTerm.ToLower();

            var results = _staticSuggestions
                .Where(s => s.Text.ToLower().Contains(searchTerm) || s.Context.ToLower().Contains(searchTerm))
                .Select(s => new SearchResult
                {
                    Title = s.Text,
                    Description = $"Navigate to {s.Text}",
                    Url = s.Url,
                    Context = s.Context,
                    IconPath = s.IconPath,
                    Relevance = CalculateRelevance(s.Text, s.Context, searchTerm)
                })
                .OrderByDescending(r => r.Relevance)
                .Take(maxResults)
                .ToList();

            return Task.FromResult(results);
        }

        private double CalculateRelevance(string text, string context, string searchTerm)
        {
            // Simple relevance calculation - could be improved
            double relevance = 0;

            if (text.ToLower() == searchTerm)
            {
                relevance = 1.0; // Exact title match
            }
            else if (text.ToLower().StartsWith(searchTerm))
            {
                relevance = 0.8; // Title starts with search term
            }
            else if (text.ToLower().Contains(searchTerm))
            {
                relevance = 0.6; // Title contains search term
            }
            else if (context.ToLower().Contains(searchTerm))
            {
                relevance = 0.4; // Context contains search term
            }

            return relevance;
        }
    }
/*
    private class CoursesSearchProvider : ISearchContextProvider
    {
        private readonly ICourseService _courseService;
        private readonly NavigationManager _navigationManager;

        public string Context => "Courses";

        public CoursesSearchProvider(ICourseService courseService, NavigationManager navigationManager)
        {
            _courseService = courseService;
            _navigationManager = navigationManager;
        }

        public async Task<List<SearchSuggestion>> GetSuggestionsAsync(string searchTerm, int maxResults)
        {
            searchTerm = searchTerm.ToLower();
            var suggestions = new List<SearchSuggestion>();

            // Add action suggestions
            suggestions.Add(new SearchSuggestion
            {
                Text = "Add Course",
                Context = "Action",
                Url = "/Admin/ManageCourses/Add",
                IconPath = "/svgs/Admin_Icon.svg",
                Priority = 10
            });

            // Get courses from service
            try
            {
                var courses = await _courseService.GetCoursesByLevelAsync("200","200");

                foreach (var course in courses.Where(c =>
                             c.CourseId.ToLower().Contains(searchTerm) ||
                             c.Name.ToLower().Contains(searchTerm)))
                {
                    suggestions.Add(new SearchSuggestion
                    {
                        Text = course.CourseCode,
                        Context = course.CourseName,
                        Url = $"/Admin/ManageCourses/{course.CourseCode}",
                        IconPath = "/svgs/Admin_Icon.svg",
                        Priority = 5
                    });
                }
            }
            catch
            {
                // Fallback to sample data if service fails
                var sampleCourses = new[]
                {
                    new { Code = "CSC2001", Name = "Computer Science" },
                    new { Code = "MAT1000", Name = "Mathematics" },
                    new { Code = "PHY1004", Name = "Physics" },
                    new { Code = "ENG2023", Name = "Engineering" }
                };

                foreach (var course in sampleCourses.Where(c =>
                             c.Code.ToLower().Contains(searchTerm) ||
                             c.Name.ToLower().Contains(searchTerm)))
                {
                    suggestions.Add(new SearchSuggestion
                    {
                        Text = course.Code,
                        Context = course.Name,
                        Url = $"/Admin/ManageCourses/{course.Code}",
                        IconPath = "/svgs/Admin_Icon.svg",
                        Priority = 5
                    });
                }
            }

            return suggestions.Take(maxResults).ToList();
        }

        public async Task<List<SearchResult>> SearchAsync(string searchTerm, int maxResults)
        {
            var suggestions = await GetSuggestionsAsync(searchTerm, maxResults);

            return suggestions.Select(s => new SearchResult
            {
                Title = s.Text,
                Description = s.Context,
                Url = s.Url,
                Context = "Courses",
                IconPath = s.IconPath,
                Relevance = s.Text.ToLower() == searchTerm.ToLower() ? 1.0 : 0.7
            }).ToList();
        }
    }

    private class SessionsSearchProvider : ISearchContextProvider
    {
        private readonly NavigationManager _navigationManager;

        public string Context => "Sessions";

        public SessionsSearchProvider(NavigationManager navigationManager)
        {
            _navigationManager = navigationManager;
        }

        public Task<List<SearchSuggestion>> GetSuggestionsAsync(string searchTerm, int maxResults)
        {
            searchTerm = searchTerm.ToLower();

            // Sample session suggestions
            var suggestions = new List<SearchSuggestion>
            {
                new()
                {
                    Text = "Start Attendance Event", Context = "Action", Url = "/Admin/CreateSession/New", Priority = 10
                },
                new() { Text = "View Active Sessions", Context = "Action", Url = "/Admin/ManageSession", Priority = 9 },
                new() { Text = "CSC2001 Wed 10:00", Context = "Recent", Url = "/Admin/ManageSession/5", Priority = 5 },
                new() { Text = "MAT1000 Thu 14:00", Context = "Recent", Url = "/Admin/ManageSession/6", Priority = 4 },
                new() { Text = "PHY1004 Mon 09:00", Context = "Recent", Url = "/Admin/ManageSession/7", Priority = 3 },
                new() { Text = "ENG2023 Fri 11:00", Context = "Recent", Url = "/Admin/ManageSession/8", Priority = 2 }
            };

            return Task.FromResult(
                suggestions
                    .Where(s => s.Text.ToLower().Contains(searchTerm) || s.Context.ToLower().Contains(searchTerm))
                    .Take(maxResults)
                    .ToList()
            );
        }

        public async Task<List<SearchResult>> SearchAsync(string searchTerm, int maxResults)
        {
            var suggestions = await GetSuggestionsAsync(searchTerm, maxResults);

            return suggestions.Select(s => new SearchResult
            {
                Title = s.Text,
                Description = $"{s.Context} - {s.Text}",
                Url = s.Url,
                Context = "Sessions",
                IconPath = "/svgs/QRCode_Icon.svg",
                Relevance = s.Priority / 10.0
            }).ToList();
        }
    }

    private class UsersSearchProvider : ISearchContextProvider
    {
        private readonly IUserStorageService _userService;
        private readonly NavigationManager _navigationManager;

        public string Context => "Users";

        public UsersSearchProvider(IUserStorageService userService, NavigationManager navigationManager)
        {
            _userService = userService;
            _navigationManager = navigationManager;
        }

        public async Task<List<SearchSuggestion>> GetSuggestionsAsync(string searchTerm, int maxResults)
        {
            searchTerm = searchTerm.ToLower();
            var suggestions = new List<SearchSuggestion>
            {
                new() { Text = "Add User", Context = "Action", Url = "/Admin/Users/Add", Priority = 10 },
                new() { Text = "Import Users", Context = "Action", Url = "/Admin/Users/Import", Priority = 9 }
            };

            // Get users from service (or use sample
        }
    }
    */
}