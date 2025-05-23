using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AirCode.Domain.Entities;
using AirCode.Domain.Enums;
using AirCode.Domain.ValueObjects;
using AirCode.Services.Firebase;
using AirCode.Utilities.HelperScripts;
using AirCode.Utilities.ObjectPooling;
using Newtonsoft.Json;

namespace AirCode.Services.Courses
{
    public class CourseService : ICourseService, IDisposable
    {
        private readonly IFirestoreService _firestoreService;
        private readonly MID_ComponentObjectPool<List<Course>> _courseListPool;
        private readonly MID_ComponentObjectPool<Dictionary<string, Course>> _courseDictPool;
        private readonly string _courseCollection = "COURSES";
        private bool _disposed;
        
        public CourseService(IFirestoreService firestoreService)
        {
            _firestoreService = firestoreService ?? throw new ArgumentNullException(nameof(firestoreService));
            
            // Initialize object pools
            _courseListPool = new MID_ComponentObjectPool<List<Course>>(
                () => new List<Course>(),
                list => list.Clear(),
                maxPoolSize: 50,
                cleanupInterval: TimeSpan.FromMinutes(10)
            );
            
            _courseDictPool = new MID_ComponentObjectPool<Dictionary<string, Course>>(
                () => new Dictionary<string, Course>(),
                dict => dict.Clear(),
                maxPoolSize: 30,
                cleanupInterval: TimeSpan.FromMinutes(10)
            );
        }

        public async Task<List<Course>> GetAllCoursesAsync()
{
    if (_disposed) throw new ObjectDisposedException(nameof(CourseService));
    
    using var pooledList = _courseListPool.GetPooled();
    var allCourses = pooledList.Object;
    
    try
    {
        var levels = new[] { "Courses_100Level", "Courses_200Level", "Courses_300Level", "Courses_400Level", "Courses_500Level" };
        
        foreach (var levelDoc in levels)
        {
            // Get the document as a generic object first
            var documentData = await _firestoreService.GetDocumentAsync<object>(_courseCollection, levelDoc);
            
            if (documentData != null)
            {
                // Parse as JSON and filter out metadata fields
                var jsonString = JsonConvert.SerializeObject(documentData);
                var levelCourses = JsonConvert.DeserializeObject<Dictionary<string, object>>(jsonString);
                
                foreach (var kvp in levelCourses)
                {
                    // Skip document metadata and validate key format
                    if (!MID_HelperFunctions.IsValidString(kvp.Key) || 
                        kvp.Key.StartsWith("Courses_") || 
                        kvp.Value == null) 
                        continue;
                    
                    try
                    {
                        var courseJson = JsonConvert.SerializeObject(kvp.Value);
                        
                        // Additional validation before deserialization
                        if (!MID_HelperFunctions.IsValidString(courseJson) || 
                            courseJson.Length < 10) // Minimum viable course JSON
                            continue;
                            
                        var courseData = JsonConvert.DeserializeObject<CourseFirestoreModel>(courseJson);
                        
                        // Validate critical course properties
                        if (courseData != null && 
                            MID_HelperFunctions.IsValidString(courseData.CourseCode) &&
                            MID_HelperFunctions.IsValidString(courseData.Name))
                        {
                            var course = MapFirestoreModelToEntity(courseData, GetLevelFromDocument(levelDoc));
                            allCourses.Add(course);
                        }
                    }
                    catch (JsonException ex)
                    {
                        Console.WriteLine($"Skipping invalid course data for key {kvp.Key}: {ex.Message}");
                    }
                }
            }
        }
        
        return new List<Course>(allCourses);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error getting all courses: {ex.Message}");
        throw;
    }
}

        public async Task<Course> GetCourseByIdAsync(string courseCode)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(CourseService));
            if (!MID_HelperFunctions.IsValidString(courseCode)) return null;
    
            try
            {
                var levels = new[] { "Courses_100Level", "Courses_200Level", "Courses_300Level", "Courses_400Level", "Courses_500Level" };
        
                foreach (var levelDoc in levels)
                {
                    var levelCourses = await _firestoreService.GetDocumentAsync<Dictionary<string, CourseFirestoreModel>>(_courseCollection, levelDoc);
            
                    if (levelCourses != null && levelCourses.ContainsKey(courseCode))
                    {
                        var courseData = levelCourses[courseCode];
                        if (MID_HelperFunctions.IsValidString(courseData.CourseCode))
                        {
                            return MapFirestoreModelToEntity(courseData, GetLevelFromDocument(levelDoc));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting course by code {courseCode}: {ex.Message}");
                throw;
            }
    
            return null;
        }

        public async Task<List<Course>> GetCoursesByDepartmentAsync(string departmentId)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(CourseService));
            
            var allCourses = await GetAllCoursesAsync();
            return allCourses.Where(c => c.DepartmentId?.Equals(departmentId, StringComparison.OrdinalIgnoreCase) == true).ToList();
        }

        public async Task<List<Course>> GetCoursesByLevelAsync(LevelType level)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(CourseService));
            
            using var pooledList = _courseListPool.GetPooled();
            var courses = pooledList.Object;
            
            try
            {
                var levelDoc = GetDocumentFromLevel(level);
                var levelCourses = await _firestoreService.GetDocumentAsync<Dictionary<string, CourseFirestoreModel>>(_courseCollection, levelDoc);
                
                if (levelCourses != null)
                {
                    foreach (var courseData in levelCourses.Values)
                    {
                        var course = MapFirestoreModelToEntity(courseData, level);
                        courses.Add(course);
                    }
                }
                
                return new List<Course>(courses);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting courses by level {level}: {ex.Message}");
                throw;
            }
        }

        public async Task<List<Course>> GetCoursesByLecturerAsync(string lecturerId)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(CourseService));
            
            var allCourses = await GetAllCoursesAsync();
            return allCourses.Where(c => c.LecturerIds?.Contains(lecturerId) == true).ToList();
        }

        public async Task<List<Course>> GetCoursesBySemesterAsync(SemesterType semester)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(CourseService));
            
            var allCourses = await GetAllCoursesAsync();
            return allCourses.Where(c => c.Semester == semester).ToList();
        }

        public async Task<bool> AddCourseAsync(Course course)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(CourseService));
            if (course == null) return false;
            
            try
            {
                var levelDoc = GetDocumentFromLevel(course.Level);
                var levelCourses = await _firestoreService.GetDocumentAsync<Dictionary<string, CourseFirestoreModel>>(_courseCollection, levelDoc)
                                  ?? new Dictionary<string, CourseFirestoreModel>();
                
                var firestoreModel = MapEntityToFirestoreModel(course);
                levelCourses[course.CourseCode] = firestoreModel;
                
                return await _firestoreService.UpdateDocumentAsync(_courseCollection, levelDoc, levelCourses);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error adding course: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> UpdateCourseAsync(Course course)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(CourseService));
            if (course == null || string.IsNullOrEmpty(course.CourseCode)) return false;
            
            try
            {
                var levelDoc = GetDocumentFromLevel(course.Level);
                var levelCourses = await _firestoreService.GetDocumentAsync<Dictionary<string, CourseFirestoreModel>>(_courseCollection, levelDoc);
                
                if (levelCourses == null || !levelCourses.ContainsKey(course.CourseCode))
                {
                    return false;
                }
                
                var updatedCourse = course.WithModification("System");
                var firestoreModel = MapEntityToFirestoreModel(updatedCourse);
                levelCourses[course.CourseCode] = firestoreModel;
                
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
            if (_disposed) throw new ObjectDisposedException(nameof(CourseService));
            if (string.IsNullOrEmpty(courseId)) return false;
            
            try
            {
                var levels = new[] { "Courses_100Level", "Courses_200Level", "Courses_300Level", "Courses_400Level", "Courses_500Level" };
                
                foreach (var levelDoc in levels)
                {
                    var levelCourses = await _firestoreService.GetDocumentAsync<Dictionary<string, CourseFirestoreModel>>(_courseCollection, levelDoc);
                    
                    if (levelCourses != null && levelCourses.ContainsKey(courseId))
                    {
                        levelCourses.Remove(courseId);
                        return await _firestoreService.UpdateDocumentAsync(_courseCollection, levelDoc, levelCourses);
                    }
                }
                
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deleting course: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> AssignLecturerToCourseAsync(string courseId, string lecturerId)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(CourseService));
            
            try
            {
                var course = await GetCourseByIdAsync(courseId);
                if (course == null) return false;
                
                var lecturerIds = course.LecturerIds?.ToList() ?? new List<string>();
                if (!lecturerIds.Contains(lecturerId))
                {
                    lecturerIds.Add(lecturerId);
                    var updatedCourse = new Course(
                        course.CourseCode, course.Name,  course.DepartmentId,
                        course.Level, course.Semester, course.CreditUnits, course.Schedule,
                        lecturerIds, DateTime.UtcNow, "System"
                    );
                    
                    return await UpdateCourseAsync(updatedCourse);
                }
                
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error assigning lecturer to course: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> RemoveLecturerFromCourseAsync(string courseId, string lecturerId)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(CourseService));
            
            try
            {
                var course = await GetCourseByIdAsync(courseId);
                if (course == null) return false;
                
                var lecturerIds = course.LecturerIds?.ToList() ?? new List<string>();
                if (lecturerIds.Remove(lecturerId))
                {
                    var updatedCourse = new Course(
                        course.CourseCode, course.Name,  course.DepartmentId,
                        course.Level, course.Semester, course.CreditUnits, course.Schedule,
                        lecturerIds, DateTime.UtcNow, "System"
                    );
                    
                    return await UpdateCourseAsync(updatedCourse);
                }
                
                return true;
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
                LevelType.Level500 => "Courses_500Level",
                _ => "Courses_100Level"
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
                "Courses_500Level" => LevelType.Level500,
                _ => LevelType.Level100
            };
        }
        
        private Course MapFirestoreModelToEntity(CourseFirestoreModel model, LevelType level)
        {
            var timeSlots = new List<TimeSlot>();
            if (model.Schedule != null)
            {
                timeSlots.AddRange(model.Schedule.Select(s => new TimeSlot
                {
                    Day = s.Day,
                    StartTime = s.StartTime,
                    EndTime = s.EndTime,
                    Location = s.Location ?? "TBA"
                }));
            }
    
            var schedule = new CourseSchedule(timeSlots);
    
            return new Course(
                model.CourseCode ?? "",
                model.Name ?? "",
                model.DepartmentId ?? "",
                level,
                model.Semester,
                model.CreditUnits,
                schedule,
                model.LecturerIds?.ToList() ?? new List<string>(),
                model.LastModified,
                model.ModifiedBy ?? "System"
            );
        }
        private CourseFirestoreModel MapEntityToFirestoreModel(Course course)
        {
            var scheduleList = new List<CourseScheduleFirestoreModel>();
            if (course.Schedule.TimeSlots?.Count > 0)
            {
                scheduleList.AddRange(course.Schedule.TimeSlots.Select(slot => new CourseScheduleFirestoreModel
                {
                    Day = slot.Day,
                    StartTime = slot.StartTime,
                    EndTime = slot.EndTime,
                    Location = slot.Location
                }));
            }
    
            return new CourseFirestoreModel
            {
                CourseCode = course.CourseCode,
                Name = course.Name,
                DepartmentId = course.DepartmentId,
                Semester = course.Semester,
                CreditUnits = course.CreditUnits,
                Schedule = scheduleList,
                LecturerIds = course.LecturerIds?.ToList() ?? new List<string>(),
                LastModified = course.LastModified,
                ModifiedBy = course.ModifiedBy
            };
        }
        
        #endregion

        public void Dispose()
        {
            if (!_disposed)
            {
                _courseListPool?.Dispose();
                _courseDictPool?.Dispose();
                _disposed = true;
            }
        }
    }

    // Firestore model for serialization
    public class CourseFirestoreModel
    {
        public string CourseCode { get; set; } // Changed from Id to CourseCode
        public string Name { get; set; }
        public string DepartmentId { get; set; }
        public SemesterType Semester { get; set; }
        public byte CreditUnits { get; set; }
        public List<CourseScheduleFirestoreModel> Schedule { get; set; }
        public List<string> LecturerIds { get; set; }
        public DateTime LastModified { get; set; }
        public string ModifiedBy { get; set; }
    }

    public class CourseScheduleFirestoreModel
    {
        public DayOfWeek Day { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public string Location { get; set; }
    }
}