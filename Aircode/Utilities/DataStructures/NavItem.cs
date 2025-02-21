namespace Aircode.Utilities.DataStructures;

public class NavItem
{
    public string IconPath { get; set; }
    public string Label { get; set; }
    public string? Path { get; set; }
    public Action? Action { get; set; }

    // Constructor for navigation items (with Path)
    public NavItem(string iconPath, string label, string path)
    {
        IconPath = iconPath;
        Label = label;
        Path = path;
        Action = null;
    }

    // Constructor for action items (with Action)
    public NavItem(string iconPath, string label, Action action)
    {
        IconPath = iconPath;
        Label = label;
        Path = null;
        Action = action;
    }
}