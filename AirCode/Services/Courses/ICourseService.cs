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

                // Student Course Management
                Task<List<StudentCourse>> GetAllStudentCoursesAsync();
                Task<StudentCourse> GetStudentCoursesByMatricAsync(string matricNumber);
                Task<List<StudentCourse>> GetStudentCoursesByLevelAsync(LevelType level);
                Task<bool> AddStudentCourseAsync(StudentCourse studentCourse);
                Task<bool> UpdateStudentCourseAsync(StudentCourse studentCourse);
                Task<bool> DeleteStudentCourseAsync(string matricNumber);

// Course Reference Management
                Task<bool> AddCourseReferenceToStudentAsync(string matricNumber, CourseRefrence courseRef);
                Task<bool> RemoveCourseReferenceFromStudentAsync(string matricNumber, string courseCode);

                Task<bool> UpdateStudentCourseReferenceStatusAsync(string matricNumber, string courseCode,
                        CourseEnrollmentStatus newStatus);

// Bulk Operations
                Task<bool> ClearAllStudentCourseReferencesAsync();
                Task<bool> PromoteAllStudentsToNextLevelAsync();
                
                // Add these methods to ICourseService interface

                #region Distributed Storage Operations

                /// <summary>
                /// Add student course data with automatic document distribution
                /// </summary>
                Task<string> AddStudentCourseDistributedAsync(string matricNumber, object courseData, string level);

                /// <summary>
                /// Get all student courses for a level (across all distributed documents)
                /// </summary>
                Task<Dictionary<string, T>> GetAllStudentCoursesDistributedAsync<T>(string level) where T : class;

                /// <summary>
                /// Get specific student course data (searches across distributed documents)
                /// </summary>
                Task<T> GetStudentCourseDistributedAsync<T>(string matricNumber, string level) where T : class;

                /// <summary>
                /// Update student course data (finds correct distributed document automatically)
                /// </summary>
                Task<bool> UpdateStudentCourseDistributedAsync<T>(string matricNumber, string level, T courseData) where T : class;

                #endregion
        }
}