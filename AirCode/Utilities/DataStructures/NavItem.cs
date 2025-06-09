namespace AirCode.Utilities.DataStructures;
public class NavItem
    {
        public string IconName { get; set; }
        public string Label { get; set; }
        public string? Path { get; set; }
        public Action? Action { get; set; }

       
        // Constructor for navigation items with path
        public NavItem(string iconName, string label, string path)
        {
            IconName = iconName;
            Label = label;
            Path = path;
        }
        //constructor
        public NavItem(string iconName, string label, string? path = null,bool protect =false)
        {
            IconName = iconName;
            Label = label;
            Path = path;
        }
        // Constructor for action items (like bottom items)
        public NavItem(string iconName, string label, Action action)
        {
            IconName = iconName;
            Label = label;
            Action = action;
        }
    }