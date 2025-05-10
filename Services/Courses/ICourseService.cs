using System.Collections.Generic;
using System.Threading.Tasks;
using AirCode.Domain.Entities;
using AirCode.Domain.Enums;
using AirCode.Domain.ValueObjects;
using AirCode.Models.Core;

namespace AirCode.Services.Courses
{
   
   public interface ICourseService
   {
       Task<List<CourseDto>> GetAllCoursesAsync();
       Task<CourseDto> GetCourseByIdAsync(string courseId);
       Task<List<CourseDto>> GetCoursesByDepartmentAsync(string departmentId);
       Task<List<CourseDto>> GetCoursesByLevelAsync(LevelType level);
       Task<List<CourseDto>> GetCoursesByLecturerAsync(string lecturerId);
       Task<List<CourseDto>> GetCoursesBySemesterAsync(SemesterType semester);
       Task<bool> AddCourseAsync(CourseDto courseDto);
       Task<bool> UpdateCourseAsync(CourseDto courseDto);
       Task<bool> DeleteCourseAsync(string courseId);
       Task<bool> AssignLecturerToCourseAsync(string courseId, string lecturerId);
       Task<bool> RemoveLecturerFromCourseAsync(string courseId, string lecturerId);
   }
}
