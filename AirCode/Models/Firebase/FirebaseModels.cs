// Models/Firebase/FirebaseModels.cs

using System.Text.Json;
using AirCode.Domain.Enums;
using AirCode.Utilities.HelperScripts;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace AirCode.Models.Firebase
{
    public class FirebaseAttendanceEvent
    {
        public string SessionId { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
        public int Duration { get; set; }
        public string Theme { get; set; } = string.Empty;
        public Dictionary<string, FirebaseAttendanceRecord> AttendanceRecords { get; set; } = new();

        public override string ToString() => 
            MID_HelperFunctions.GetStructOrClassMemberValues(this);
    }

    public class FirebaseAttendanceRecord
    {
        public string MatricNumber { get; set; } = string.Empty;
        public bool HasScannedAttendance { get; set; }
        public DateTime? ScanTime { get; set; }
        public bool IsOnlineScan { get; set; }
        public string? DeviceGUID { get; set; }

        public override string ToString() => 
            MID_HelperFunctions.GetStructOrClassMemberValues(this);
    }

    public class CourseFirestoreModel
    {
        
        [JsonProperty("courseCode")]
        public string CourseCode { get; set; } = string.Empty;
    
        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;
    
        [JsonProperty("departmentId")]
        public string DepartmentId { get; set; } = string.Empty;
    
        [JsonProperty("semester")]
        [System.Text.Json.Serialization.JsonConverter(typeof(StringEnumConverter))]
        public SemesterType Semester { get; set; }
    
        [JsonProperty("creditUnits")]
        public byte CreditUnits { get; set; }
    
        [JsonProperty("schedule")]
        public List<CourseScheduleFirestoreModel> Schedule { get; set; } = new();
    
        [JsonProperty("lecturerIds")]
        public List<string> LecturerIds { get; set; } = new();
    
        [JsonProperty("lastModified")]
        [System.Text.Json.Serialization.JsonConverter(typeof(IsoDateTimeConverter))]
        public DateTime LastModified { get; set; }
    
        [JsonProperty("modifiedBy")]
        public string ModifiedBy { get; set; } = string.Empty;

        public override string ToString() => 
            MID_HelperFunctions.GetStructOrClassMemberValues(this);
    }

    public class CourseScheduleFirestoreModel
    {
        [JsonProperty("day")]
        [System.Text.Json.Serialization.JsonConverter(typeof(StringEnumConverter))]
        public DayOfWeek Day { get; set; }
    
        [JsonProperty("startTime")]
        public string StartTime { get; set; } = string.Empty;
    
        [JsonProperty("endTime")]
        public string EndTime { get; set; } = string.Empty;
    
        [JsonProperty("location")]
        public string Location { get; set; } = string.Empty;

        public override string ToString() => 
            MID_HelperFunctions.GetStructOrClassMemberValues(this);
    }

    public class StudentCourseFirestoreModel
    {
        [JsonProperty("studentMatricNumber")]
        public string StudentMatricNumber { get; set; }

        [JsonProperty("studentCoursesRefs")]
        public List<CourseRefrenceFirestoreModel> StudentCoursesRefs { get; set; }

        [JsonProperty("lastModified")]
        [System.Text.Json.Serialization.JsonConverter(typeof(IsoDateTimeConverter))]
        public DateTime LastModified { get; set; }

        [JsonProperty("modifiedBy")]
        public string ModifiedBy { get; set; }
    }

    public class CourseRefrenceFirestoreModel
    {
        [JsonProperty("courseCode")]
        public string CourseCode { get; set; }

        [JsonProperty("courseEnrollmentStatus")]
        [System.Text.Json.Serialization.JsonConverter(typeof(StringEnumConverter))]
        public CourseEnrollmentStatus CourseEnrollmentStatus { get; set; }

        [JsonProperty("enrollmentDate")]
        [System.Text.Json.Serialization.JsonConverter(typeof(IsoDateTimeConverter))]
        public DateTime EnrollmentDate { get; set; }

        [JsonProperty("lastStatusChange")]
        [System.Text.Json.Serialization.JsonConverter(typeof(IsoDateTimeConverter))]
        public DateTime LastStatusChange { get; set; }
    }
    public class DepartmentsContainer
    {
        public List<Domain.Entities.Department> Departments { get; set; } = new();
        public DateTime LastModified { get; set; }
        public string ModifiedBy { get; set; } = string.Empty;

        public override string ToString() => 
            MID_HelperFunctions.GetStructOrClassMemberValues(this);
    }


    // Data models
    public record FirestoreListener(
        string Id,
        string Type,
        string Collection,
        string? DocumentId,
        string? WhereField,
        object? WhereValue,
        DateTime CreatedAt
    );

    public record DocumentChangeData(
        string ListenerId,
        string Collection,
        string DocumentId,
        bool Exists,
        JsonElement? Data,
        DateTime Timestamp
    );

    public record CollectionChangeData(
        string ListenerId,
        string Collection,
        List<DocumentChange> Changes,
        int Size,
        DateTime Timestamp
    );

    public record DocumentChange(
        string Type,
        JsonElement Document,
        int OldIndex,
        int NewIndex
    );

    public record ListenerErrorData(
        string ListenerId,
        string Error,
        DateTime Timestamp
    );

    // Event argument classes
    public class DocumentChangedEventArgs : EventArgs
    {
        public DocumentChangeData Data { get; }
        public DocumentChangedEventArgs(DocumentChangeData data) => Data = data;
    }

    public class CollectionChangedEventArgs : EventArgs
    {
        public CollectionChangeData Data { get; }
        public CollectionChangedEventArgs(CollectionChangeData data) => Data = data;
    }

    public class AttendanceSessionChangedEventArgs : EventArgs
    {
        public DocumentChangeData Data { get; }
        public AttendanceSessionChangedEventArgs(DocumentChangeData data) => Data = data;
    }

    public class ActiveSessionsChangedEventArgs : EventArgs
    {
        public CollectionChangeData Data { get; }
        public ActiveSessionsChangedEventArgs(CollectionChangeData data) => Data = data;
    }

    public class ListenerErrorEventArgs : EventArgs
    {
        public ListenerErrorData Data { get; }
        public ListenerErrorEventArgs(ListenerErrorData data) => Data = data;
    }

    public class DocumentSizeInfo
    {
        public int EstimatedSize { get; set; }
        public int FieldCount { get; set; }
        public bool Exists { get; set; }

        public override string ToString() => 
            MID_HelperFunctions.GetStructOrClassMemberValues(this);
    }
}