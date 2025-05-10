using System.Collections.Generic;
using System.Threading.Tasks;
using AirCode.Domain.Entities;
using AirCode.Domain.Enums;
using AirCode.Domain.ValueObjects;
using AirCode.Models.Core;
/*
namespace AirCode.Services.Courses
{
    public interface ICourseService
    {
        // Queries
        Task<Course> GetCourseAsync(string courseId);
        Task<List<Course>> GetCoursesByDepartmentAsync(string departmentId);
        Task<List<Course>> GetCoursesByLecturerAsync(string lecturerId);
        Task<List<Course>> GetCoursesByLevelAsync(string departmentId, LevelType level);
        
        // Command operations (require permission checks)
        Task<Course> CreateCourseAsync(string userId, Course course);
        Task<Course> UpdateCourseNameAsync(string userId, string courseId, string newName);
        Task<Course> UpdateCourseLevelAsync(string userId, string courseId, LevelType newLevel);
        Task<Course> UpdateCourseSemesterAsync(string userId, string courseId, SemesterType newSemester);
        Task<Course> AddLecturerToCourseAsync(string userId, string courseId, string lecturerId);
        Task<Course> RemoveLecturerFromCourseAsync(string userId, string courseId, string lecturerId);
        Task<Course> UpdateCourseScheduleAsync(string userId, string courseId, CourseSchedule schedule);
        Task<bool> DeleteCourseAsync(string userId, string courseId);
        
        // Mapping methods
        CourseDto MapToDto(Course course);
        Course MapFromDto(CourseDto courseDto);
    }
}
*/