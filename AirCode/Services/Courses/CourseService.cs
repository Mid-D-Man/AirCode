using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AirCode.Domain.Entities;
using AirCode.Domain.Enums;
using AirCode.Domain.ValueObjects;
using AirCode.Services.Firebase;
using AirCode.Services.Storage;
using AirCode.Utilities.HelperScripts;
using AirCode.Utilities.ObjectPooling;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace AirCode.Services.Courses
{
    public class CourseService : ICourseService, IDisposable
    {
        private readonly IFirestoreService _firestoreService;
        private readonly IBlazorAppLocalStorageService _localStorage;

        private readonly MID_ComponentObjectPool<List<Course>> _courseListPool;
        private readonly MID_ComponentObjectPool<Dictionary<string, Course>> _courseDictPool;
        private readonly string _courseCollection = "COURSES";
        private readonly string _studentCourseCollection = "STUDENT_COURSES";
        private readonly MID_ComponentObjectPool<List<StudentCourse>> _studentCourseListPool;
       // Storage key constants
        private const string ALL_COURSES_KEY = "AllCourses";
        private const string STUDENT_COURSES_KEY = "AllStudentCourses";
        private const string USER_COURSES_PREFIX = "UserCourses_";

        private bool _disposed;
        
        public CourseService(IFirestoreService firestoreService, IBlazorAppLocalStorageService localStorage)
        {
            _firestoreService = firestoreService ?? throw new ArgumentNullException(nameof(firestoreService));
      
            _localStorage = localStorage?? throw new ArgumentNullException(nameof(localStorage));
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
            _studentCourseListPool = new MID_ComponentObjectPool<List<StudentCourse>>(
                () => new List<StudentCourse>(),
                list => list.Clear(),
                maxPoolSize: 50,
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
            var documentData = await _firestoreService.GetDocumentAsync<object>(_courseCollection, levelDoc);
            
            if (documentData != null)
            {
                var jsonString = JsonConvert.SerializeObject(documentData);
                var levelCourses = JsonConvert.DeserializeObject<Dictionary<string, object>>(jsonString);
                
                foreach (var kvp in levelCourses)
                {
                    if (!MID_HelperFunctions.IsValidString(kvp.Key) || 
                        kvp.Key.StartsWith("Courses_") || 
                        kvp.Key.Equals("id", StringComparison.OrdinalIgnoreCase) ||
                        kvp.Value == null) 
                        continue;
                    
                    try
                    {
                        var courseJson = JsonConvert.SerializeObject(kvp.Value);
                        
                        if (!MID_HelperFunctions.IsValidString(courseJson) || 
                            courseJson.Length < 10)
                            continue;
                            
                        var courseData = JsonConvert.DeserializeObject<CourseFirestoreModel>(courseJson);
                        
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
        
        var coursesList = new List<Course>(allCourses);
        
        // Cache the courses locally
        await SaveCoursesToLocalStorageAsync(coursesList);
        
        return coursesList;
    }
    catch (Exception ex)
    {
       await MID_HelperFunctions.DebugMessageAsync($"Error getting all courses: {ex.Message}",DebugClass.Exception);
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
                    var courseData = await _firestoreService.GetFieldAsync<CourseFirestoreModel>(_courseCollection, levelDoc, courseCode);
                    
                    if (courseData != null && MID_HelperFunctions.IsValidString(courseData.CourseCode))
                    {
                        return MapFirestoreModelToEntity(courseData, GetLevelFromDocument(levelDoc));
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
                var documentData = await _firestoreService.GetDocumentAsync<object>(_courseCollection, levelDoc);
                
                if (documentData != null)
                {
                    var jsonString = JsonConvert.SerializeObject(documentData);
                    var levelCourses = JsonConvert.DeserializeObject<Dictionary<string, object>>(jsonString);
                    
                    foreach (var kvp in levelCourses)
                    {
                        if (!MID_HelperFunctions.IsValidString(kvp.Key) || 
                            kvp.Key.StartsWith("Courses_") || 
                            kvp.Key.Equals("id", StringComparison.OrdinalIgnoreCase) ||
                            kvp.Value == null) 
                            continue;
                        
                        try
                        {
                            var courseJson = JsonConvert.SerializeObject(kvp.Value);
                            var courseData = JsonConvert.DeserializeObject<CourseFirestoreModel>(courseJson);
                            
                            if (courseData != null && MID_HelperFunctions.IsValidString(courseData.CourseCode))
                            {
                                var course = MapFirestoreModelToEntity(courseData, level);
                                courses.Add(course);
                            }
                        }
                        catch (JsonException ex)
                        {
                            Console.WriteLine($"Skipping invalid course data for key {kvp.Key}: {ex.Message}");
                        }
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
            var lecturerCourses = allCourses.Where(c => c.LecturerIds?.Contains(lecturerId) == true).ToList();
    
            // Cache lecturer-specific courses
            await SaveUserCoursesToLocalStorageAsync(lecturerId, lecturerCourses);
    
            return lecturerCourses;
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
                var firestoreModel = MapEntityToFirestoreModel(course);
                
                return await _firestoreService.AddOrUpdateFieldAsync(_courseCollection, levelDoc, course.CourseCode, firestoreModel);
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
                
                // Check if course exists
                var existingCourse = await _firestoreService.GetFieldAsync<CourseFirestoreModel>(_courseCollection, levelDoc, course.CourseCode);
                if (existingCourse == null) return false;
                
                // Create updated course with proper timestamp
                var updatedCourse = course.WithModification("System");
                var firestoreModel = MapEntityToFirestoreModel(updatedCourse);
        
                return await _firestoreService.AddOrUpdateFieldAsync(_courseCollection, levelDoc, course.CourseCode, firestoreModel);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating course {course.CourseCode}: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> DeleteCourseAsync(string courseId)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(CourseService));
            if (!MID_HelperFunctions.IsValidString(courseId)) 
            {
                Console.WriteLine($"DeleteCourse: Invalid courseId provided: '{courseId}'");
                return false;
            }

            try
            {
                Console.WriteLine($"DeleteCourse: Starting deletion process for course: '{courseId}'");
                
                var levels = new[] { "Courses_100Level", "Courses_200Level", "Courses_300Level", "Courses_400Level", "Courses_500Level" };

                foreach (var levelDoc in levels)
                {
                    Console.WriteLine($"DeleteCourse: Checking document: '{levelDoc}' for field: '{courseId}'");
                    
                    // Check if the field exists in this document
                    var existingCourse = await _firestoreService.GetFieldAsync<CourseFirestoreModel>(_courseCollection, levelDoc, courseId);
                    
                    if (existingCourse != null)
                    {
                        Console.WriteLine($"DeleteCourse: Found course '{courseId}' in document '{levelDoc}', attempting field removal");
                        
                        var deleteResult = await _firestoreService.RemoveFieldAsync(_courseCollection, levelDoc, courseId);
                        
                        if (deleteResult)
                        {
                            Console.WriteLine($"DeleteCourse: Successfully deleted course field '{courseId}' from '{levelDoc}'");
                            return true;
                        }
                        else
                        {
                            Console.WriteLine($"DeleteCourse: Failed to delete course field '{courseId}' from '{levelDoc}'");
                            return false;
                        }
                    }
                }

                Console.WriteLine($"DeleteCourse: Course '{courseId}' not found in any level document");
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"DeleteCourse: Exception occurred while deleting course '{courseId}': {ex.Message}");
                Console.WriteLine($"DeleteCourse: Stack trace: {ex.StackTrace}");
                return false;
            }
        }

        // Remove the other delete methods as they're no longer needed
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
                        course.CourseCode, course.Name, course.DepartmentId,
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
                        course.CourseCode, course.Name, course.DepartmentId,
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

     #region Student Course Management

public async Task<List<StudentCourse>> GetAllStudentCoursesAsync()
{
    if (_disposed) throw new ObjectDisposedException(nameof(CourseService));
    
    using var pooledList = _studentCourseListPool.GetPooled();
    var allStudentCourses = pooledList.Object;
    
    try
    {
        var levels = new[] { "StudentCourses_100Level", "StudentCourses_200Level", "StudentCourses_300Level", "StudentCourses_400Level", "StudentCourses_500Level", "StudentCourses_ExtraLevel" };
        
        foreach (var levelDoc in levels)
        {
            var documentData = await _firestoreService.GetDocumentAsync<object>(_studentCourseCollection, levelDoc);
            
            if (documentData != null)
            {
                var jsonString = JsonConvert.SerializeObject(documentData);
                var levelStudentCourses = JsonConvert.DeserializeObject<Dictionary<string, object>>(jsonString);
                
                foreach (var kvp in levelStudentCourses)
                {
                    if (!MID_HelperFunctions.IsValidString(kvp.Key) || 
                        kvp.Key.StartsWith("StudentCourses_") || 
                        kvp.Key.Equals("id", StringComparison.OrdinalIgnoreCase) ||
                        kvp.Value == null) 
                        continue;
                    
                    try
                    {
                        var studentCourseJson = JsonConvert.SerializeObject(kvp.Value);
                        
                        if (!MID_HelperFunctions.IsValidString(studentCourseJson) || 
                            studentCourseJson.Length < 10)
                            continue;
                            
                        var studentCourseData = JsonConvert.DeserializeObject<StudentCourseFirestoreModel>(studentCourseJson);
                        
                        if (studentCourseData != null && 
                            MID_HelperFunctions.IsValidString(studentCourseData.StudentMatricNumber))
                        {
                            var studentCourse = MapFirestoreModelToStudentEntity(studentCourseData, GetLevelFromStudentDocument(levelDoc));
                            allStudentCourses.Add(studentCourse);
                        }
                    }
                    catch (JsonException ex)
                    {
                        Console.WriteLine($"Skipping invalid student course data for key {kvp.Key}: {ex.Message}");
                    }
                }
            }
        }
        
        var studentCoursesList = new List<StudentCourse>(allStudentCourses);
        
        // Cache the student courses locally
        await SaveStudentCoursesToLocalStorageAsync(studentCoursesList);
        
        return studentCoursesList;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error getting all student courses: {ex.Message}");
        throw;
    }
}

public async Task<StudentCourse> GetStudentCoursesByMatricAsync(string matricNumber)
{
    if (_disposed) throw new ObjectDisposedException(nameof(CourseService));
    if (!MID_HelperFunctions.IsValidString(matricNumber)) return null;

    try
    {
        var levels = new[] { "StudentCourses_100Level", "StudentCourses_200Level", "StudentCourses_300Level", "StudentCourses_400Level", "StudentCourses_500Level", "StudentCourses_ExtraLevel" };

        foreach (var levelDoc in levels)
        {
            var studentCourseData = await _firestoreService.GetFieldAsync<StudentCourseFirestoreModel>(_studentCourseCollection, levelDoc, matricNumber);
            
            if (studentCourseData != null && MID_HelperFunctions.IsValidString(studentCourseData.StudentMatricNumber))
            {
                var studentCourse = MapFirestoreModelToStudentEntity(studentCourseData, GetLevelFromStudentDocument(levelDoc));
                
                // Cache user-specific courses
                await SaveUserCoursesToLocalStorageAsync(matricNumber, studentCourse);
                
                return studentCourse;
            }
        }

        // Handle new student case - create default record
        var defaultLevel = DetermineStudentLevel(matricNumber);
        var newStudentCourse = new StudentCourse(
            matricNumber,
            defaultLevel,
            new List<CourseRefrence>(),
            DateTime.UtcNow,
            "System"
        );

        // Auto-create the student record
        var creationSuccess = await AddStudentCourseAsync(newStudentCourse);
        
        if (creationSuccess)
        {
            // Cache the new student course
            await SaveUserCoursesToLocalStorageAsync(matricNumber, newStudentCourse);
            return newStudentCourse;
        }
        
        return null;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error getting student courses by matric {matricNumber}: {ex.Message}");
        throw;
    }
}

// New method: GetStudentCoursesByLevelAsync with caching
public async Task<List<StudentCourse>> GetStudentCoursesByLevelAsync(LevelType level)
{
    if (_disposed) throw new ObjectDisposedException(nameof(CourseService));
    
    using var pooledList = _studentCourseListPool.GetPooled();
    var studentCourses = pooledList.Object;
    
    try
    {
        var levelDoc = GetStudentDocumentFromLevel(level);
        var documentData = await _firestoreService.GetDocumentAsync<object>(_studentCourseCollection, levelDoc);
        
        if (documentData != null)
        {
            var jsonString = JsonConvert.SerializeObject(documentData);
            var levelStudentCourses = JsonConvert.DeserializeObject<Dictionary<string, object>>(jsonString);
            
            foreach (var kvp in levelStudentCourses)
            {
                if (!MID_HelperFunctions.IsValidString(kvp.Key) || 
                    kvp.Key.StartsWith("StudentCourses_") || 
                    kvp.Key.Equals("id", StringComparison.OrdinalIgnoreCase) ||
                    kvp.Value == null) 
                    continue;
                
                try
                {
                    var studentCourseJson = JsonConvert.SerializeObject(kvp.Value);
                    var studentCourseData = JsonConvert.DeserializeObject<StudentCourseFirestoreModel>(studentCourseJson);
                    
                    if (studentCourseData != null && MID_HelperFunctions.IsValidString(studentCourseData.StudentMatricNumber))
                    {
                        var studentCourse = MapFirestoreModelToStudentEntity(studentCourseData, level);
                        studentCourses.Add(studentCourse);
                    }
                }
                catch (JsonException ex)
                {
                    Console.WriteLine($"Skipping invalid student course data for key {kvp.Key}: {ex.Message}");
                }
            }
        }
        
        var studentCoursesList = new List<StudentCourse>(studentCourses);
        
        // Cache level-specific student courses
        await SaveLevelStudentCoursesToLocalStorageAsync(level, studentCoursesList);
        
        return studentCoursesList;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error getting student courses by level {level}: {ex.Message}");
        throw;
    }
}
private LevelType DetermineStudentLevel(string matricNumber)
{
    if (string.IsNullOrEmpty(matricNumber)) return LevelType.Level100;
    
    // Enhanced pattern for format: U{YY}{DEPT}{NNNN}
    // Example: U21CYS1099, U22CS1009
    var matricPattern = @"^U(\d{2})([A-Z]{2,4})(\d{4})$";
    var match = System.Text.RegularExpressions.Regex.Match(matricNumber.ToUpper(), matricPattern);
    
    if (!match.Success) return LevelType.Level100;
    
    var yearSuffix = int.Parse(match.Groups[1].Value);
    var departmentCode = match.Groups[2].Value;
    var studentNumber = match.Groups[3].Value;
    
    // Convert 2-digit year to full year
    var admissionYear = 2000 + yearSuffix;
    var currentYear = DateTime.Now.Year;
    
    // Calculate academic level based on years since admission
    var academicYearsPassed = currentYear - admissionYear;
    
    return academicYearsPassed switch
    {
        0 => LevelType.Level100,  // Freshman year
        1 => LevelType.Level200,  // Sophomore year
        2 => LevelType.Level300,  // Junior year
        3 => LevelType.Level400,  // Senior year
        4 => LevelType.Level500,  // Fifth year/Graduate
        _ when academicYearsPassed > 4 => LevelType.LevelExtra,  // Extended program
        _ => LevelType.Level100   // Negative values (future admission) default to 100
    };
}

// Optional: Add validation method for matric number format
public bool IsValidMatricNumber(string matricNumber)
{
    if (string.IsNullOrEmpty(matricNumber)) return false;
    
    var matricPattern = @"^U(\d{2})([A-Z]{2,4})(\d{4})$";
    var match = System.Text.RegularExpressions.Regex.Match(matricNumber.ToUpper(), matricPattern);
    
    if (!match.Success) return false;
    
    var yearSuffix = int.Parse(match.Groups[1].Value);
    var studentNumber = int.Parse(match.Groups[3].Value);
    
    // Validate year range (assuming 2020-2030 admission window)
    var admissionYear = 2000 + yearSuffix;
    if (admissionYear < 2020 || admissionYear > 2040) return false;
    
    // Validate student number range (1001-1999 as typical range)
    if (studentNumber < 1001 || studentNumber > 1999) return false;
    
    return true;
}

public async Task<bool> AddStudentCourseAsync(StudentCourse studentCourse)
{
    if (_disposed) throw new ObjectDisposedException(nameof(CourseService));
    if (studentCourse == null) return false;
    
    try
    {
        var levelDoc = GetStudentDocumentFromLevel(studentCourse.StudentLevel);
        var firestoreModel = MapStudentEntityToFirestoreModel(studentCourse);
        
        return await _firestoreService.AddOrUpdateFieldAsync(_studentCourseCollection, levelDoc, studentCourse.StudentMatricNumber, firestoreModel);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error adding student course: {ex.Message}");
        return false;
    }
}

public async Task<bool> UpdateStudentCourseAsync(StudentCourse studentCourse)
{
    if (_disposed) throw new ObjectDisposedException(nameof(CourseService));
    if (studentCourse == null || string.IsNullOrEmpty(studentCourse.StudentMatricNumber)) return false;

    try
    {
        var levelDoc = GetStudentDocumentFromLevel(studentCourse.StudentLevel);
        
        // Check if student course exists
        var existingStudentCourse = await _firestoreService.GetFieldAsync<StudentCourseFirestoreModel>(_studentCourseCollection, levelDoc, studentCourse.StudentMatricNumber);
        if (existingStudentCourse == null) return false;
        
        // Create updated student course with proper timestamp
        var updatedStudentCourse = studentCourse.WithModification("System");
        var firestoreModel = MapStudentEntityToFirestoreModel(updatedStudentCourse);

        return await _firestoreService.AddOrUpdateFieldAsync(_studentCourseCollection, levelDoc, studentCourse.StudentMatricNumber, firestoreModel);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error updating student course {studentCourse.StudentMatricNumber}: {ex.Message}");
        return false;
    }
}

public async Task<bool> DeleteStudentCourseAsync(string matricNumber)
{
    if (_disposed) throw new ObjectDisposedException(nameof(CourseService));
    if (!MID_HelperFunctions.IsValidString(matricNumber)) 
    {
        Console.WriteLine($"DeleteStudentCourse: Invalid matricNumber provided: '{matricNumber}'");
        return false;
    }

    try
    {
        Console.WriteLine($"DeleteStudentCourse: Starting deletion process for student: '{matricNumber}'");
        
        var levels = new[] { "StudentCourses_100Level", "StudentCourses_200Level", "StudentCourses_300Level", "StudentCourses_400Level", "StudentCourses_500Level", "StudentCourses_ExtraLevel" };

        foreach (var levelDoc in levels)
        {
            Console.WriteLine($"DeleteStudentCourse: Checking document: '{levelDoc}' for field: '{matricNumber}'");
            
            // Check if the field exists in this document
            var existingStudentCourse = await _firestoreService.GetFieldAsync<StudentCourseFirestoreModel>(_studentCourseCollection, levelDoc, matricNumber);
            
            if (existingStudentCourse != null)
            {
                Console.WriteLine($"DeleteStudentCourse: Found student '{matricNumber}' in document '{levelDoc}', attempting field removal");
                
                var deleteResult = await _firestoreService.RemoveFieldAsync(_studentCourseCollection, levelDoc, matricNumber);
                
                if (deleteResult)
                {
                    Console.WriteLine($"DeleteStudentCourse: Successfully deleted student field '{matricNumber}' from '{levelDoc}'");
                    return true;
                }
                else
                {
                    Console.WriteLine($"DeleteStudentCourse: Failed to delete student field '{matricNumber}' from '{levelDoc}'");
                    return false;
                }
            }
        }

        Console.WriteLine($"DeleteStudentCourse: Student '{matricNumber}' not found in any level document");
        return false;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"DeleteStudentCourse: Exception occurred while deleting student '{matricNumber}': {ex.Message}");
        Console.WriteLine($"DeleteStudentCourse: Stack trace: {ex.StackTrace}");
        return false;
    }
}

// Course Reference Management
        public async Task<bool> AddCourseReferenceToStudentAsync(string matricNumber, CourseRefrence courseRef)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(CourseService));
            if (!MID_HelperFunctions.IsValidString(matricNumber) || courseRef == null) return false;

            try
            {
                // UPDATED: This will now auto-create student if they don't exist
                var existingStudent = await GetStudentCoursesByMatricAsync(matricNumber);
                if (existingStudent == null) return false; // Only fail if creation also failed

                var updatedStudent = existingStudent.WithAddedCourse(courseRef, "System");
                return await UpdateStudentCourseAsync(updatedStudent);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error adding course reference to student {matricNumber}: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> UpdateStudentCourseReferenceStatusAsync(string matricNumber, string courseCode, CourseEnrollmentStatus newStatus)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(CourseService));
            if (!MID_HelperFunctions.IsValidString(matricNumber) || !MID_HelperFunctions.IsValidString(courseCode)) return false;

            try
            {
                // UPDATED: This will now auto-create student if they don't exist
                var existingStudent = await GetStudentCoursesByMatricAsync(matricNumber);
                if (existingStudent == null) return false; // Only fail if creation also failed

                var updatedStudent = existingStudent.WithUpdatedCourseStatus(courseCode, newStatus, "System");
                return await UpdateStudentCourseAsync(updatedStudent);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating course status for student {matricNumber}: {ex.Message}");
                return false;
            }
        }
public async Task<bool> RemoveCourseReferenceFromStudentAsync(string matricNumber, string courseCode)
{
    if (_disposed) throw new ObjectDisposedException(nameof(CourseService));
    if (!MID_HelperFunctions.IsValidString(matricNumber) || !MID_HelperFunctions.IsValidString(courseCode)) return false;

    try
    {
        var existingStudent = await GetStudentCoursesByMatricAsync(matricNumber);
        if (existingStudent == null) return false;

        var updatedStudent = existingStudent.WithRemovedCourse(courseCode, "System");
        return await UpdateStudentCourseAsync(updatedStudent);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error removing course reference from student {matricNumber}: {ex.Message}");
        return false;
    }
}



// Bulk Operations
public async Task<bool> ClearAllStudentCourseReferencesAsync()
{
    if (_disposed) throw new ObjectDisposedException(nameof(CourseService));

    try
    {
        var allStudents = await GetAllStudentCoursesAsync();
        var tasks = allStudents.Select(async student =>
        {
            var clearedStudent = student.WithCourseReferences(new List<CourseRefrence>());
            return await UpdateStudentCourseAsync(clearedStudent);
        });

        var results = await Task.WhenAll(tasks);
        return results.All(result => result);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error clearing all student course references: {ex.Message}");
        return false;
    }
}

public async Task<bool> PromoteAllStudentsToNextLevelAsync()
{
    if (_disposed) throw new ObjectDisposedException(nameof(CourseService));

    try
    {
        var allStudents = await GetAllStudentCoursesAsync();
        var promotionTasks = new List<Task<bool>>();

        foreach (var student in allStudents)
        {
            var nextLevel = GetNextLevel(student.StudentLevel);
            if (nextLevel != student.StudentLevel) // Only promote if there's a next level
            {
                // Delete from current level
                var deleteTask = DeleteStudentCourseAsync(student.StudentMatricNumber);
                
                // Create in next level
                var promotedStudent = student.WithLevel(nextLevel);
                var addTask = AddStudentCourseAsync(promotedStudent);
                
                promotionTasks.Add(Task.Run(async () =>
                {
                    var deleteResult = await deleteTask;
                    var addResult = await addTask;
                    return deleteResult && addResult;
                }));
            }
        }

        if (promotionTasks.Any())
        {
            var results = await Task.WhenAll(promotionTasks);
            return results.All(result => result);
        }

        return true;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error promoting all students: {ex.Message}");
        return false;
    }
}

#endregion

     #region Student Course Helper Methods

private string GetStudentDocumentFromLevel(LevelType level)
{
    return level switch
    {
        LevelType.Level100 => "StudentCourses_100Level",
        LevelType.Level200 => "StudentCourses_200Level",
        LevelType.Level300 => "StudentCourses_300Level",
        LevelType.Level400 => "StudentCourses_400Level",
        LevelType.Level500 => "StudentCourses_500Level",
        LevelType.LevelExtra => "StudentCourses_ExtraLevel",
        _ => "StudentCourses_100Level"
    };
}

private LevelType GetLevelFromStudentDocument(string document)
{
    return document switch
    {
        "StudentCourses_100Level" => LevelType.Level100,
        "StudentCourses_200Level" => LevelType.Level200,
        "StudentCourses_300Level" => LevelType.Level300,
        "StudentCourses_400Level" => LevelType.Level400,
        "StudentCourses_500Level" => LevelType.Level500,
        "StudentCourses_ExtraLevel" => LevelType.LevelExtra,
        _ => LevelType.Level100
    };
}

private LevelType GetNextLevel(LevelType currentLevel)
{
    return currentLevel switch
    {
        LevelType.Level100 => LevelType.Level200,
        LevelType.Level200 => LevelType.Level300,
        LevelType.Level300 => LevelType.Level400,
        LevelType.Level400 => LevelType.Level500,
        LevelType.Level500 => LevelType.LevelExtra,
        LevelType.LevelExtra => LevelType.LevelExtra, // No promotion from Extra
        _ => currentLevel
    };
}

private StudentCourse MapFirestoreModelToStudentEntity(StudentCourseFirestoreModel model, LevelType level)
{
    var courseReferences = new List<CourseRefrence>();
    if (model.StudentCoursesRefs != null)
    {
        courseReferences.AddRange(model.StudentCoursesRefs.Select(cr => new CourseRefrence(
            cr.CourseCode,
            cr.CourseEnrollmentStatus,
            cr.EnrollmentDate,
            cr.LastStatusChange
        )));
    }

    return new StudentCourse(
        model.StudentMatricNumber ?? "",
        level,
        courseReferences,
        model.LastModified,
        model.ModifiedBy ?? "System"
    );
}

private StudentCourseFirestoreModel MapStudentEntityToFirestoreModel(StudentCourse studentCourse)
{
    var courseRefsList = new List<CourseRefrenceFirestoreModel>();
    if (studentCourse.StudentCoursesRefs?.Count > 0)
    {
        courseRefsList.AddRange(studentCourse.StudentCoursesRefs.Select(cr => new CourseRefrenceFirestoreModel
        {
            CourseCode = cr.CourseCode,
            CourseEnrollmentStatus = cr.CourseEnrollmentStatus,
            EnrollmentDate = cr.EnrollmentDate,
            LastStatusChange = cr.LastStatusChange
        }));
    }

    return new StudentCourseFirestoreModel
    {
        StudentMatricNumber = studentCourse.StudentMatricNumber,
        StudentCoursesRefs = courseRefsList,
        LastModified = studentCourse.LastModified,
        ModifiedBy = studentCourse.ModifiedBy
    };
}

#region Local Storage Functions

private async Task SaveCoursesToLocalStorageAsync(List<Course> courses)
{
    try
    {
        await _localStorage.SetItemAsync(ALL_COURSES_KEY, courses);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error saving courses to local storage: {ex.Message}");
    }
}

private async Task SaveStudentCoursesToLocalStorageAsync(List<StudentCourse> studentCourses)
{
    try
    {
        await _localStorage.SetItemAsync(STUDENT_COURSES_KEY, studentCourses);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error saving student courses to local storage: {ex.Message}");
    }
}

private async Task SaveUserCoursesToLocalStorageAsync<T>(string userId, T userCourses)
{
    try
    {
        var key = $"{USER_COURSES_PREFIX}{userId}";
        await _localStorage.SetItemAsync(key, userCourses);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error saving user courses to local storage for {userId}: {ex.Message}");
    }
}

private async Task SaveLevelStudentCoursesToLocalStorageAsync(LevelType level, List<StudentCourse> studentCourses)
{
    try
    {
        var key = $"StudentCourses_{level}";
        await _localStorage.SetItemAsync(key, studentCourses);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error saving level student courses to local storage for {level}: {ex.Message}");
    }
}

// Optional: Methods to retrieve from local storage (for offline scenarios)
private async Task<List<Course>> GetCoursesFromLocalStorageAsync()
{
    try
    {
        return await _localStorage.GetItemAsync<List<Course>>(ALL_COURSES_KEY) ?? new List<Course>();
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error retrieving courses from local storage: {ex.Message}");
        return new List<Course>();
    }
}

private async Task<List<StudentCourse>> GetStudentCoursesFromLocalStorageAsync()
{
    try
    {
        return await _localStorage.GetItemAsync<List<StudentCourse>>(STUDENT_COURSES_KEY) ?? new List<StudentCourse>();
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error retrieving student courses from local storage: {ex.Message}");
        return new List<StudentCourse>();
    }
}

private async Task<T> GetUserCoursesFromLocalStorageAsync<T>(string userId)
{
    try
    {
        var key = $"{USER_COURSES_PREFIX}{userId}";
        return await _localStorage.GetItemAsync<T>(key);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error retrieving user courses from local storage for {userId}: {ex.Message}");
        return default(T);
    }
}

#endregion

#endregion



public class StudentCourseFirestoreModel
{
    [JsonProperty("studentMatricNumber")]
    public string StudentMatricNumber { get; set; }

    [JsonProperty("studentCoursesRefs")]
    public List<CourseRefrenceFirestoreModel> StudentCoursesRefs { get; set; }

    [JsonProperty("lastModified")]
    [JsonConverter(typeof(IsoDateTimeConverter))]
    public DateTime LastModified { get; set; }

    [JsonProperty("modifiedBy")]
    public string ModifiedBy { get; set; }
}

public class CourseRefrenceFirestoreModel
{
    [JsonProperty("courseCode")]
    public string CourseCode { get; set; }

    [JsonProperty("courseEnrollmentStatus")]
    [JsonConverter(typeof(StringEnumConverter))]
    public CourseEnrollmentStatus CourseEnrollmentStatus { get; set; }

    [JsonProperty("enrollmentDate")]
    [JsonConverter(typeof(IsoDateTimeConverter))]
    public DateTime EnrollmentDate { get; set; }

    [JsonProperty("lastStatusChange")]
    [JsonConverter(typeof(IsoDateTimeConverter))]
    public DateTime LastStatusChange { get; set; }
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
                    StartTime = TimeSpan.TryParse(s.StartTime, out var start) ? start : TimeSpan.Zero,
                    EndTime = TimeSpan.TryParse(s.EndTime, out var end) ? end : TimeSpan.Zero,
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
                    StartTime = slot.StartTime.ToString(@"hh\:mm"),
                    EndTime = slot.EndTime.ToString(@"hh\:mm"),
                    Location = slot.Location ?? "TBA"
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
        [JsonProperty("courseCode")]
        public string CourseCode { get; set; }
    
        [JsonProperty("name")]
        public string Name { get; set; }
    
        [JsonProperty("departmentId")]
        public string DepartmentId { get; set; }
    
        [JsonProperty("semester")]
        [JsonConverter(typeof(StringEnumConverter))]
        public SemesterType Semester { get; set; }
    
        [JsonProperty("creditUnits")]
        public byte CreditUnits { get; set; }
    
        [JsonProperty("schedule")]
        public List<CourseScheduleFirestoreModel> Schedule { get; set; }
    
        [JsonProperty("lecturerIds")]
        public List<string> LecturerIds { get; set; }
    
        [JsonProperty("lastModified")]
        [JsonConverter(typeof(IsoDateTimeConverter))]
        public DateTime LastModified { get; set; }
    
        [JsonProperty("modifiedBy")]
        public string ModifiedBy { get; set; }
    }

    public class CourseScheduleFirestoreModel
    {
        [JsonProperty("day")]
        [JsonConverter(typeof(StringEnumConverter))]
        public DayOfWeek Day { get; set; }
    
        [JsonProperty("startTime")]
        public string StartTime { get; set; }
    
        [JsonProperty("endTime")]
        public string EndTime { get; set; }
    
        [JsonProperty("location")]
        public string Location { get; set; }
    }
   
}