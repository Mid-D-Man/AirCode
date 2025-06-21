
namespace AirCode.Domain.Enums
{
    //removed assistant lecturer,course rep , deemed unessesary , that would cause confusion 
    //if both lec,rep not in class that means it never holds now, also course repcan just borrow assistant their account s
    public enum UserRole
    {
        SuperiorAdmin,  // Full system access, can manage everything
        LecturerAdmin,  // Can manage their courses, view course attendance
        CourseRepAdmin, // Can view course attendance, start attendance events
        Student         // Can view personal attendance, register courses
    }


    public enum ClassRepStatus
    {
        None,
        MainRep
    }

}