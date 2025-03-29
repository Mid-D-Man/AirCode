using System.Collections.Generic;
using System.Threading.Tasks;

namespace AirCode.Services.Search;

public interface ISearchContextService
{
    /// <summary>
    /// Gets the current search context
    /// </summary>
    string CurrentContext { get; }
    
    /// <summary>
    /// Sets the current search context
    /// </summary>
    void SetContext(string context);
    
    /// <summary>
    /// Gets suggestions based on current context and search term
    /// </summary>
    Task<List<SearchSuggestion>> GetSuggestionsAsync(string searchTerm, int maxResults = 10);
    
    /// <summary>
    /// Gets suggestions for a specific context and search term
    /// </summary>
    Task<List<SearchSuggestion>> GetSuggestionsForContextAsync(string context, string searchTerm, int maxResults = 10);
    
    /// <summary>
    /// Performs a search and returns results
    /// </summary>
    Task<List<SearchResult>> SearchAsync(string searchTerm, string context = null, int maxResults = 20);
    
    /// <summary>
    /// Registers a custom search provider for a specific context
    /// </summary>
    void RegisterContextProvider(string context, ISearchContextProvider provider);
}

/// <summary>
/// Represents a search suggestion
/// </summary>
public class SearchSuggestion
{
    public string Text { get; set; }
    public string Context { get; set; }
    public string Url { get; set; }
    public string IconPath { get; set; }
    public int Priority { get; set; } = 0;
}

/// <summary>
/// Represents a search result
/// </summary>
public class SearchResult
{
    public string Title { get; set; }
    public string Description { get; set; }
    public string Url { get; set; }
    public string Context { get; set; }
    public string IconPath { get; set; }
    public double Relevance { get; set; }
}

/// <summary>
/// Provides context-specific search functionality
/// </summary>
public interface ISearchContextProvider
{
    string Context { get; }
    Task<List<SearchSuggestion>> GetSuggestionsAsync(string searchTerm, int maxResults);
    Task<List<SearchResult>> SearchAsync(string searchTerm, int maxResults);
}