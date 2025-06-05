using System;
using System.Collections.Generic;

namespace AirCode.Domain.ValueObjects
{
    //implment as a struct because scedule can indeed change
    // Implemented as a record for immutability and value-based equality
    // CourseSchedule.cs
    public readonly record struct CourseSchedule
    {
        public IReadOnlyList<TimeSlot> TimeSlots { get; init; }
    
        public CourseSchedule(List<TimeSlot> timeSlots)
        {
            TimeSlots = timeSlots ?? new List<TimeSlot>();
        }
    
        public string FormatSchedule() => string.Join(", ", 
            TimeSlots.Select(slot => $"{slot.Day}: {slot.StartTime:hh\\:mm} - {slot.EndTime:hh\\:mm} @ {slot.Location}"));
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