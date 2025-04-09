using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AirCode.Domain.Entities;
using AirCode.Domain.Enums;
using AirCode.Domain.ValueObjects;
using AirCode.Models.Core;
using AirCode.Services.Permissions;
using AirCode.Services.Storage;

namespace AirCode.Services.Courses
{
    public class CourseService : ICourseService
    {
        private readonly IPermissionService _permissionService;
        private readonly IBlazorAppLocalStorageService _blazorAppLocalStorageService;
        
        // Dependency injection
        public CourseService(IPermissionService permissionService, IBlazorAppLocalStorageService blazorAppLocalStorageService)
        {
            _permissionService = permissionService;
            _blazorAppLocalStorageService = blazorAppLocalStorageService;
        }
        
        // Queries - no permission checks needed for read operations
        public async Task<Course> GetCourseAsync(string courseId)
        {
            var courses = await _blazorAppLocalStorageService.GetItemAsync<List<Course>>("courses") ?? new List<Course>();
            return courses.FirstOrDefault(c => c.CourseId == courseId);
        }
        
        public async Task<List<Course>> GetCoursesByDepartmentAsync(string departmentId)
        {
            var courses = await _blazorAppLocalStorageService.GetItemAsync<List<Course>>("courses") ?? new List<Course>();
            return courses.Where(c => c.DepartmentId == departmentId).ToList();
        }
        
        public async Task<List<Course>> GetCoursesByLecturerAsync(string lecturerId)
        {
            var courses = await _blazorAppLocalStorageService.GetItemAsync<List<Course>>("courses") ?? new List<Course>();
            return courses.Where(c => c.LecturerIds.Contains(lecturerId)).ToList();
        }
        
        public async Task<List<Course>> GetCoursesByLevelAsync(string departmentId, LevelType level)
        {
            var courses = await _blazorAppLocalStorageService.GetItemAsync<List<Course>>("courses") ?? new List<Course>();
            return courses.Where(c => c.DepartmentId == departmentId && c.Level == level).ToList();
        }
        
        // Command operations - require permission checks
        public async Task<Course> CreateCourseAsync(string userId, Course course)
        {
            // Check if user can create courses
            if (!await _permissionService.CanEditCourseInfoAsync(userId, null))
                throw new UnauthorizedAccessException("User cannot create courses");
            
            var courses = await _blazorAppLocalStorageService.GetItemAsync<List<Course>>("courses") ?? new List<Course>();
            
            // Ensure course ID is unique
            if (courses.Any(c => c.CourseId == course.CourseId))
                throw new InvalidOperationException("Course ID already exists");
            
            // Add security attributes
            var securedCourse = course with 
            { 
                SecurityToken = Guid.NewGuid().ToString(),
                LastModified = DateTime.UtcNow,
                ModifiedBy = userId 
            };
            
            courses.Add(securedCourse);
            await _blazorAppLocalStorageService.SetItemAsync("courses", courses);
            
            return securedCourse;
        }
        
        public async Task<Course> UpdateCourseNameAsync(string userId, string courseId, string newName)
        {
            // Check permissions
            if (!await _permissionService.CanEditCourseInfoAsync(userId, courseId))
                throw new UnauthorizedAccessException("User cannot edit this course");
                
            var courses = await _blazorAppLocalStorageService.GetItemAsync<List<Course>>("courses") ?? new List<Course>();
            var existingCourse = courses.FirstOrDefault(c => c.CourseId == courseId);
            
            if (existingCourse == null)
                throw new InvalidOperationException("Course not found");
            
            // Create updated course (immutable record with "with" expression)
            var updatedCourse = existingCourse with 
            { 
                Name = newName,
                LastModified = DateTime.UtcNow,
                ModifiedBy = userId 
            };
            
            // Replace in list
            var index = courses.FindIndex(c => c.CourseId == courseId);
            courses[index] = updatedCourse;
            
            // Save updated list
            await _blazorAppLocalStorageService.SetItemAsync("courses", courses);
            
            return updatedCourse;
        }
        
        public async Task<Course> UpdateCourseLevelAsync(string userId, string courseId, LevelType newLevel)
        {
            if (!await _permissionService.CanEditCourseInfoAsync(userId, courseId))
                throw new UnauthorizedAccessException("User cannot edit this course");
                
            var courses = await _blazorAppLocalStorageService.GetItemAsync<List<Course>>("courses") ?? new List<Course>();
            var existingCourse = courses.FirstOrDefault(c => c.CourseId == courseId);
            
            if (existingCourse == null)
                throw new InvalidOperationException("Course not found");
            
            var updatedCourse = existingCourse with 
            { 
                Level = newLevel,
                LastModified = DateTime.UtcNow,
                ModifiedBy = userId 
            };
            
            var index = courses.FindIndex(c => c.CourseId == courseId);
            courses[index] = updatedCourse;
            
            await _blazorAppLocalStorageService.SetItemAsync("courses", courses);
            
            return updatedCourse;
        }
        
        public async Task<Course> UpdateCourseSemesterAsync(string userId, string courseId, SemesterType newSemester)
        {
            if (!await _permissionService.CanEditCourseInfoAsync(userId, courseId))
                throw new UnauthorizedAccessException("User cannot edit this course");
                
            var courses = await _blazorAppLocalStorageService.GetItemAsync<List<Course>>("courses") ?? new List<Course>();
            var existingCourse = courses.FirstOrDefault(c => c.CourseId == courseId);
            
            if (existingCourse == null)
                throw new InvalidOperationException("Course not found");
            
            var updatedCourse = existingCourse with 
            { 
                Semester = newSemester,
                LastModified = DateTime.UtcNow,
                ModifiedBy = userId 
            };
            
            var index = courses.FindIndex(c => c.CourseId == courseId);
            courses[index] = updatedCourse;
            
            await _blazorAppLocalStorageService.SetItemAsync("courses", courses);
            
            return updatedCourse;
        }
        
        public async Task<Course> AddLecturerToCourseAsync(string userId, string courseId, string lecturerId)
        {
            if (!await _permissionService.CanEditCourseInfoAsync(userId, courseId))
                throw new UnauthorizedAccessException("User cannot edit this course");
                
            var courses = await _blazorAppLocalStorageService.GetItemAsync<List<Course>>("courses") ?? new List<Course>();
            var existingCourse = courses.FirstOrDefault(c => c.CourseId == courseId);
            
            if (existingCourse == null)
                throw new InvalidOperationException("Course not found");
                
            // Check if lecturer already assigned
            if (existingCourse.LecturerIds.Contains(lecturerId))
                return existingCourse; // Already assigned, no change needed
            
            // Create new lecturer list with added ID
            var updatedLecturerIds = new List<string>(existingCourse.LecturerIds) { lecturerId };
            
            var updatedCourse = existingCourse with 
            { 
                LecturerIds = updatedLecturerIds,
                LastModified = DateTime.UtcNow,
                ModifiedBy = userId 
            };
            
            var index = courses.FindIndex(c => c.CourseId == courseId);
            courses[index] = updatedCourse;
            
            await _blazorAppLocalStorageService.SetItemAsync("courses", courses);
            
            return updatedCourse;
        }
        
        public async Task<Course> RemoveLecturerFromCourseAsync(string userId, string courseId, string lecturerId)
        {
            if (!await _permissionService.CanEditCourseInfoAsync(userId, courseId))
                throw new UnauthorizedAccessException("User cannot edit this course");
                
            var courses = await _blazorAppLocalStorageService.GetItemAsync<List<Course>>("courses") ?? new List<Course>();
            var existingCourse = courses.FirstOrDefault(c => c.CourseId == courseId);
            
            if (existingCourse == null)
                throw new InvalidOperationException("Course not found");
                
            // If lecturer not in list, no change needed
            if (!existingCourse.LecturerIds.Contains(lecturerId))
                return existingCourse;
            
            // Create new lecturer list without the ID
            var updatedLecturerIds = new List<string>(existingCourse.LecturerIds);
            updatedLecturerIds.Remove(lecturerId);
            
            var updatedCourse = existingCourse with 
            { 
                LecturerIds = updatedLecturerIds,
                LastModified = DateTime.UtcNow,
                ModifiedBy = userId 
            };
            
            var index = courses.FindIndex(c => c.CourseId == courseId);
            courses[index] = updatedCourse;
            
            await _blazorAppLocalStorageService.SetItemAsync("courses", courses);
            
            return updatedCourse;
        }
        
        public async Task<Course> UpdateCourseScheduleAsync(string userId, string courseId, CourseSchedule schedule)
        {
            if (!await _permissionService.CanEditCourseInfoAsync(userId, courseId))
                throw new UnauthorizedAccessException("User cannot edit this course");
                
            var courses = await _blazorAppLocalStorageService.GetItemAsync<List<Course>>("courses") ?? new List<Course>();
            var existingCourse = courses.FirstOrDefault(c => c.CourseId == courseId);
            
            if (existingCourse == null)
                throw new InvalidOperationException("Course not found");
            
            var updatedCourse = existingCourse with 
            { 
                Schedule = schedule,
                LastModified = DateTime.UtcNow,
                ModifiedBy = userId 
            };
            
            var index = courses.FindIndex(c => c.CourseId == courseId);
            courses[index] = updatedCourse;
            
            await _blazorAppLocalStorageService.SetItemAsync("courses", courses);
            
            return updatedCourse;
        }
        
        public async Task<bool> DeleteCourseAsync(string userId, string courseId)
        {
            if (!await _permissionService.CanEditCourseInfoAsync(userId, courseId))
                throw new UnauthorizedAccessException("User cannot delete this course");
                
            var courses = await _blazorAppLocalStorageService.GetItemAsync<List<Course>>("courses") ?? new List<Course>();
            var existingCourse = courses.FirstOrDefault(c => c.CourseId == courseId);
            
            if (existingCourse == null)
                return false; // Course doesn't exist
            
            courses.RemoveAll(c => c.CourseId == courseId);
            await _blazorAppLocalStorageService.SetItemAsync("courses", courses);
            
            return true;
        }
        
        // Mapping methods
        public CourseDto MapToDto(Course course)
        {
            if (course == null) return null;
            
            var scheduleItems = new List<CourseScheduleDto>();
            
            if (course.Schedule?.TimeSlots != null)
            {
                foreach (var timeSlot in course.Schedule.TimeSlots)
                {
                    scheduleItems.Add(new CourseScheduleDto
                    {
                        Day = timeSlot.Day,
                        StartTime = timeSlot.StartTime,
                        EndTime = timeSlot.EndTime,
                        Location = timeSlot.Location
                    });
                }
            }
            
            // This would need to be expanded to include lecturer details
            var lecturers = new List<SimpleLecturerDto>();
            
            return new CourseDto
            {
                Id = course.CourseId,
                Name = course.Name,
                Department = course.DepartmentId,
                Level = course.Level,
                Semester = course.Semester,
                Schedule = scheduleItems,
                Lecturers = lecturers,
                CreatedAt = course.LastModified, // Assuming creation date is first modification
                UpdatedAt = course.LastModified
            };
        }
        
        public Course MapFromDto(CourseDto courseDto)
        {
            if (courseDto == null) return null;
            
            var timeSlots = new List<TimeSlot>();
            
            if (courseDto.Schedule != null)
            {
                foreach (var schedule in courseDto.Schedule)
                {
                    timeSlots.Add(new TimeSlot
                    {
                        Day = schedule.Day,
                        StartTime = schedule.StartTime,
                        EndTime = schedule.EndTime,
                        Location = schedule.Location
                    });
                }
            }
            
            var lecturerIds = courseDto.Lecturers?.Select(l => l.Id)?.ToList() ?? new List<string>();
            
            return new Course
            {
                CourseId = courseDto.Id,
                Name = courseDto.Name,
                DepartmentId = courseDto.Department,
                Level = courseDto.Level,
                Semester = courseDto.Semester,
                Schedule = new CourseSchedule { TimeSlots = timeSlots },
                LecturerIds = lecturerIds,
                SecurityToken = Guid.NewGuid().ToString(),
                LastModified = DateTime.UtcNow,
                ModifiedBy = "system" // This should be overridden by actual user ID
            };
        }
    }
}