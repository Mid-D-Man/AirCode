using System.Collections.Generic;
using System.Threading.Tasks;
using AirCode.Domain.Entities;
using AirCode.Domain.Enums;

namespace AirCode.Services.Courses
{
    public interface ICourseService
    {
        Task<List<Course>> GetAllCoursesAsync();
        Task<Course> GetCourseByIdAsync(string courseId);
        Task<List<Course>> GetCoursesByDepartmentAsync(string departmentId);
        Task<List<Course>> GetCoursesByLevelAsync(LevelType level);
        Task<List<Course>> GetCoursesByLecturerAsync(string lecturerId);
        Task<List<Course>> GetCoursesBySemesterAsync(SemesterType semester);
        Task<bool> AddCourseAsync(Course course);
        Task<bool> UpdateCourseAsync(Course course);
        Task<bool> DeleteCourseAsync(string courseId);
        Task<bool> AssignLecturerToCourseAsync(string courseId, string lecturerId);
        Task<bool> RemoveLecturerFromCourseAsync(string courseId, string lecturerId);
    }
}