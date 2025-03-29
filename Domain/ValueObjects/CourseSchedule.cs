using System;
using System.Collections.Generic;

namespace AirCode.Domain.ValueObjects
{
    // Implemented as a record for immutability and value-based equality
    public record CourseSchedule
    {
        public List<TimeSlot> TimeSlots { get; init; } = new List<TimeSlot>();
        
        // Helper method to create a formatted string representation
        public string FormatSchedule()
        {
            var result = new List<string>();
            foreach (var slot in TimeSlots)
            {
                result.Add($"{slot.Day}: {slot.StartTime.ToString("hh\\:mm")} - {slot.EndTime.ToString("hh\\:mm")} @ {slot.Location}");
            }
            return string.Join(", ", result);
        }
    }
    
    // Small structure for individual time slots - implemented as struct
    public struct TimeSlot
    {
        public DayOfWeek Day { get; init; }
        public TimeSpan StartTime { get; init; }
        public TimeSpan EndTime { get; init; }
        public string Location { get; init; }
    }
}