namespace Aircode.Models;

public class SessionData
{public string CourseName { get; set; } = string.Empty;
    public string CourseId { get; set; } = string.Empty;
    public DateTime StartTime { get; set; } = DateTime.UtcNow;
    public DateTime Date { get; set; } = DateTime.UtcNow;
    public int Duration { get; set; } // Duration in minutes
}