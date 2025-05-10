
namespace AirCode.Domain.Enums
{
    //removed assistant lecturer, that would cause confusion 
    public enum UserRole
    {
        SuperiorAdmin, // Full system access, can manage everything
        LecturerAdmin, // Can manage their courses, view course attendance
        CourseRepAdmin, // Can view course attendance, start attendance events
        AssistantCourseRep, // Can start attendance events for main course rep
        Student // Can view personal attendance, register courses
    }

    public enum ClassRepStatus
    {
        None,
        AssistantRep,
        MainRep
    }

}