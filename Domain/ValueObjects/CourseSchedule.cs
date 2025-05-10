using System;
using System.Collections.Generic;

namespace AirCode.Domain.ValueObjects
{
    //implment as a struct because scedule can indeed change
    // Implemented as a record for immutability and value-based equality
    public struct CourseSchedule
    {
        public List<TimeSlot> TimeSlots { get; internal set; }
        
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