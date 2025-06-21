using System;
using AirCode.Domain.Enums;

namespace AirCode.Domain.ValueObjects
{
    //note this is the students own record
    // Implemented as a record for immutability. yeah this no longer neededsupdabse correct impl now
    public record AttendanceRecord
    {
        public string StudentId { get; init; }
        public string CourseCode { get; init; }
        public DateTime Timestamp { get; init; }
        public string Location { get; init; }
        public AttendanceType Type { get; init; }
        public AttendanceVerificationMethod VerificationMethod { get; init; }
        public bool IsManuallyVerified { get; init; }
    }
}