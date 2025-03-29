
namespace AirCode.Domain.Enums
{
    public enum UserRole
    {
        SuperiorAdmin, // Full system access, can manage everything
        LecturerAdmin, // Can manage their courses, view course attendance
        CourseRepAdmin, // Can view course attendance, start attendance events
        AssistantLecturer, // Can start attendance events for main lecturer
        AssistantCourseRep, // Can start attendance events for main course rep
        Student // Can view personal attendance, register courses
    }

    public enum ClassRepStatus
    {
        None,
        AssistantRep,
        MainRep
    }

    public enum LecturerStatus
    {
        Main,
        Assistant
    }
}