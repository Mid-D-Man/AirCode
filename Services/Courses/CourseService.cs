using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AirCode.Domain.Entities;
using AirCode.Domain.Enums;
using AirCode.Domain.ValueObjects;
using AirCode.Models.Core;
using AirCode.Services.Firebase;
using AirCode.Services.Permissions;
using AirCode.Services.Storage;

namespace AirCode.Services.Courses
{
    public class CourseService : ICourseService
    {
        private readonly IFirestoreService _firestoreService;
        private readonly string _courseCollection = "COURSES";
        
        public CourseService(IFirestoreService firestoreService)
        {
            _firestoreService = firestoreService ?? throw new ArgumentNullException(nameof(firestoreService));
        }

        public async Task<List<CourseDto>> GetAllCoursesAsync()
        {
            var allCourses = new List<CourseDto>();
            
            try
            {
                // Get courses from all level documents
                var levels = new[] { "Courses_100Level", "Courses_200Level", "Courses_300Level", "Courses_400Level", "Courses_500Level" };
                
                foreach (var levelDoc in levels)
                {
                    var levelCourses = await _firestoreService.GetDocumentAsync<Dictionary<string, CourseDto>>(_courseCollection, levelDoc);
                    
                    if (levelCourses != null)
                    {
                        foreach (var course in levelCourses.Values)
                        {
                            // Ensure the course has the correct level based on the document
                            course.Level = GetLevelFromDocument(levelDoc);
                            allCourses.Add(course);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting all courses: {ex.Message}");
                throw;
            }
            
            return allCourses;
        }

        public async Task<CourseDto> GetCourseByIdAsync(string courseId)
        {
            try
            {
                // We need to search through all level documents to find the course
                var levels = new[] { "Courses_100Level", "Courses_200Level", "Courses_300Level", "Courses_400Level", "Courses_500Level" };
                
                foreach (var levelDoc in levels)
                {
                    var levelCourses = await _firestoreService.GetDocumentAsync<Dictionary<string, CourseDto>>(_courseCollection, levelDoc);
                    
                    if (levelCourses != null && levelCourses.ContainsKey(courseId))
                    {
                        var course = levelCourses[courseId];
                        course.Level = GetLevelFromDocument(levelDoc);
                        return course;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting course by ID {courseId}: {ex.Message}");
                throw;
            }
            
            return null;
        }

        public async Task<List<CourseDto>> GetCoursesByDepartmentAsync(string departmentId)
        {
            var allCourses = await GetAllCoursesAsync();
            return allCourses.Where(c => c.Department?.Equals(departmentId, StringComparison.OrdinalIgnoreCase) == true).ToList();
        }

        public async Task<List<CourseDto>> GetCoursesByLevelAsync(LevelType level)
        {
            try
            {
                var levelDoc = GetDocumentFromLevel(level);
                var levelCourses = await _firestoreService.GetDocumentAsync<Dictionary<string, CourseDto>>(_courseCollection, levelDoc);
                
                if (levelCourses != null)
                {
                    var courses = levelCourses.Values.ToList();
                    // Ensure all courses have the correct level
                    foreach (var course in courses)
                    {
                        course.Level = level;
                    }
                    return courses;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting courses by level {level}: {ex.Message}");
                throw;
            }
            
            return new List<CourseDto>();
        }

        public async Task<List<CourseDto>> GetCoursesByLecturerAsync(string lecturerId)
        {
            var allCourses = await GetAllCoursesAsync();
            return allCourses.Where(c => c.Lecturers?.Any(l => l.Id == lecturerId) == true).ToList();
        }

        public async Task<List<CourseDto>> GetCoursesBySemesterAsync(SemesterType semester)
        {
            var allCourses = await GetAllCoursesAsync();
            return allCourses.Where(c => c.Semester == semester).ToList();
        }

        public async Task<bool> AddCourseAsync(CourseDto courseDto)
        {
            try
            {
                // Generate ID if not provided
                if (string.IsNullOrEmpty(courseDto.Id))
                {
                    courseDto.Id = Guid.NewGuid().ToString();
                }
                
                // Set timestamps
                courseDto.CreatedAt = DateTime.Now;
                courseDto.UpdatedAt = DateTime.Now;
                
                // Get the appropriate document based on level
                var levelDoc = GetDocumentFromLevel(courseDto.Level);
                
                // Get existing courses for this level
                var levelCourses = await _firestoreService.GetDocumentAsync<Dictionary<string, CourseDto>>(_courseCollection, levelDoc)
                                  ?? new Dictionary<string, CourseDto>();
                
                // Add the new course
                levelCourses[courseDto.Id] = courseDto;
                
                // Update the document
                return await _firestoreService.UpdateDocumentAsync(_courseCollection, levelDoc, levelCourses);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error adding course: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> UpdateCourseAsync(CourseDto courseDto)
        {
            try
            {
                if (string.IsNullOrEmpty(courseDto.Id))
                {
                    return false;
                }
                
                // Update timestamp
                courseDto.UpdatedAt = DateTime.Now;
                
                // Get the appropriate document based on level
                var levelDoc = GetDocumentFromLevel(courseDto.Level);
                
                // Get existing courses for this level
                var levelCourses = await _firestoreService.GetDocumentAsync<Dictionary<string, CourseDto>>(_courseCollection, levelDoc);
                
                if (levelCourses == null || !levelCourses.ContainsKey(courseDto.Id))
                {
                    return false; // Course not found
                }
                
                // Update the course
                levelCourses[courseDto.Id] = courseDto;
                
                // Update the document
                return await _firestoreService.UpdateDocumentAsync(_courseCollection, levelDoc, levelCourses);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating course: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> DeleteCourseAsync(string courseId)
        {
            try
            {
                if (string.IsNullOrEmpty(courseId))
                {
                    return false;
                }
                
                // We need to find which level document contains this course
                var levels = new[] { "Courses_100Level", "Courses_200Level", "Courses_300Level", "Courses_400Level", "Courses_500Level" };
                
                foreach (var levelDoc in levels)
                {
                    var levelCourses = await _firestoreService.GetDocumentAsync<Dictionary<string, CourseDto>>(_courseCollection, levelDoc);
                    
                    if (levelCourses != null && levelCourses.ContainsKey(courseId))
                    {
                        // Remove the course
                        levelCourses.Remove(courseId);
                        
                        // Update the document
                        return await _firestoreService.UpdateDocumentAsync(_courseCollection, levelDoc, levelCourses);
                    }
                }
                
                return false; // Course not found
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deleting course: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> AssignLecturerToCourseAsync(string courseId, string lecturerId)
        {
            try
            {
                var course = await GetCourseByIdAsync(courseId);
                if (course == null)
                {
                    return false;
                }
                
                // Initialize lecturers list if null
                if (course.Lecturers == null)
                {
                    course.Lecturers = new List<SimpleLecturerDto>();
                }
                
                // Check if lecturer is already assigned
                if (course.Lecturers.Any(l => l.Id == lecturerId))
                {
                    return true; // Already assigned
                }
                
                // Add lecturer (in a real implementation, you'd fetch lecturer details from user service)
                course.Lecturers.Add(new SimpleLecturerDto
                {
                    Id = lecturerId,
                    Name = "Lecturer Name" // This would be fetched from a user service
                });
                
                return await UpdateCourseAsync(course);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error assigning lecturer to course: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> RemoveLecturerFromCourseAsync(string courseId, string lecturerId)
        {
            try
            {
                var course = await GetCourseByIdAsync(courseId);
                if (course == null || course.Lecturers == null)
                {
                    return false;
                }
                
                // Remove lecturer
                var lecturerToRemove = course.Lecturers.FirstOrDefault(l => l.Id == lecturerId);
                if (lecturerToRemove != null)
                {
                    course.Lecturers.Remove(lecturerToRemove);
                    return await UpdateCourseAsync(course);
                }
                
                return true; // Lecturer wasn't assigned anyway
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error removing lecturer from course: {ex.Message}");
                return false;
            }
        }

        #region Private Helper Methods
        
        private string GetDocumentFromLevel(LevelType level)
        {
            return level switch
            {
                LevelType.Level100 => "Courses_100Level",
                LevelType.Level200 => "Courses_200Level",
                LevelType.Level300 => "Courses_300Level",
                LevelType.Level400 => "Courses_400Level",
                _ => "Courses_100Level" // Default fallback
            };
        }
        
        private LevelType GetLevelFromDocument(string document)
        {
            return document switch
            {
                "Courses_100Level" => LevelType.Level100,
                "Courses_200Level" => LevelType.Level200,
                "Courses_300Level" => LevelType.Level300,
                "Courses_400Level" => LevelType.Level400,
                _ => LevelType.Level100 // Default fallback
            };
        }
        
        private Course MapDtoToEntity(CourseDto courseDto)
        {
            // Map CourseScheduleDto to CourseSchedule and other properties
            var timeSlots = new List<TimeSlot>();
            
            if (courseDto.Schedule != null)
            {
                foreach (var scheduleDto in courseDto.Schedule)
                {
                    timeSlots.Add(new TimeSlot
                    {
                        Day = scheduleDto.Day,
                        StartTime = scheduleDto.StartTime,
                        EndTime = scheduleDto.EndTime,
                        Location = scheduleDto.Location
                    });
                }
            }
            
            var courseSchedule = new CourseSchedule
            {
                TimeSlots = timeSlots
            };
            
            var lecturerIds = new List<string>();
            if (courseDto.Lecturers != null)
            {
                foreach (var lecturer in courseDto.Lecturers)
                {
                    lecturerIds.Add(lecturer.Id);
                }
            }
            
            return new Course
            (
                courseDto.Id,
                courseDto.Name,
                courseDto.Department,
                courseDto.Level,
                courseDto.Semester,
                courseSchedule,
                lecturerIds
            );
        }

        private CourseDto MapEntityToDto(Course course)
        {
            // Map Course entity to CourseDto
            var scheduleList = new List<CourseScheduleDto>();
            
            if (course.Schedule.TimeSlots.Count >0)
            {
                foreach (var slot in course.Schedule.TimeSlots)
                {
                    scheduleList.Add(new CourseScheduleDto
                    {
                        Day = slot.Day,
                        StartTime = slot.StartTime,
                        EndTime = slot.EndTime,
                        Location = slot.Location
                    });
                }
            }
            
            // In a real implementation, you would fetch lecturer details
            var lecturersList = new List<SimpleLecturerDto>();
            if (course.LecturerIds != null)
            {
                foreach (var id in course.LecturerIds)
                {
                    lecturersList.Add(new SimpleLecturerDto
                    {
                        Id = id,
                        Name = "Lecturer Name" // This would be fetched from a user service
                    });
                }
            }
            
            return new CourseDto
            {
                Id = course.CourseId,
                Name = course.Name,
                Department = course.DepartmentId,
                Level = course.Level,
                Semester = course.Semester,
                CreditUnits = 3, // Default or from course entity
                Schedule = scheduleList,
                Lecturers = lecturersList,
                CreatedAt = DateTime.Now, // Would be from course entity
                UpdatedAt = course.LastModified
            };
        }
        
        private string GenerateSecurityToken()
        {
            // Generate a unique security token
            return Guid.NewGuid().ToString();
        }
        
        #endregion
    }
}
