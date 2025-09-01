// Models/Search/SearchModels.cs

using AirCode.Utilities.HelperScripts;

namespace AirCode.Models.Search
{
    public class SearchSuggestion
    {
        public string Text { get; set; }
        public string Context { get; set; }
        public string Url { get; set; }
        public string IconName { get; set; }
        public int Priority { get; set; } = 0;

        public override string ToString() => 
            MID_HelperFunctions.GetStructOrClassMemberValues(this);
    }

    public class SearchResult
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public string Url { get; set; }
        public string Context { get; set; }
        public double Relevance { get; set; }

        public override string ToString() => 
            MID_HelperFunctions.GetStructOrClassMemberValues(this);
    }
}