using System.ComponentModel.DataAnnotations;
using AirCode.Domain.Enums;
using AirCode.Domain.ValueObjects;

namespace AirCode.Domain.Entities
{
    /// <summary>
    /// Simplified Course entity without security interface
    /// </summary>
    public class Course
    {
        [Required]
        [StringLength(10, ErrorMessage = "Course code cannot exceed 10 characters")]
        public string CourseCode { get; init; } // Primary identifier: CYB415, CSC484, etc.
        
        [Required]
        [StringLength(100, ErrorMessage = "Course name cannot exceed 100 characters")]
        public string Name { get; init; }
        
        [StringLength(5, ErrorMessage = "Department code cannot exceed 5 characters")]
        public string DepartmentId { get; init; } // CYB, CS, AED, etc.
        
        public LevelType Level { get; init; }
        
        [Required]
        [Range(1, 10, ErrorMessage = "Credit units must be between 1 and 10")]
        public byte CreditUnits { get; init; }

        public SemesterType Semester { get; init; }
        public CourseSchedule Schedule { get; init; }
        public IReadOnlyList<string> LecturerIds { get; init; }
        
        // Basic modification tracking
        public DateTime LastModified { get; init; }
        public string ModifiedBy { get; init; }

        // Constructor to ensure immutability
        public Course(string courseCode, string name, string departmentId, 
            LevelType level, SemesterType semester, byte creditUnits,
            CourseSchedule schedule, List<string> lecturerIds,
            DateTime? lastModified = null, string modifiedBy = "")
        {
            CourseCode = courseCode;
            Name = name;
            DepartmentId = departmentId;
            Level = level;
            Semester = semester;
            CreditUnits = creditUnits;
            Schedule = schedule;
            LecturerIds = lecturerIds?.AsReadOnly() ?? new List<string>().AsReadOnly();
            LastModified = lastModified ?? DateTime.UtcNow;
            ModifiedBy = modifiedBy;
        }
        
        // Factory method for creating new courses
        public static Course Create(string courseCode, string name, string departmentId,
            LevelType level, SemesterType semester, byte creditUnits,
            CourseSchedule schedule, List<string> lecturerIds = null)
        {
            return new Course(courseCode, name, departmentId, level, semester, 
                creditUnits, schedule, lecturerIds, DateTime.UtcNow, "System");
        }
        
        // Method to create updated version
        public Course WithModification(string modifiedBy)
        {
            return new Course(CourseCode, Name, DepartmentId, Level, Semester, 
                CreditUnits, Schedule, LecturerIds.ToList(), DateTime.UtcNow, modifiedBy);
        }
    }

    /// <summary>
    /// Represents a student's course enrollment information
    /// </summary>
    public class StudentCourse
    {
        [Required]
        [StringLength(15, ErrorMessage = "Matric Number cannot exceed 15 characters")]
        public string StudentMatricNumber { get; init; } // Primary identifier format: U21CYS1083, U22CS1009
        
        [Required]
        public LevelType StudentLevel { get; init; }
        
        public IReadOnlyList<CourseRefrence> StudentCoursesRefs { get; init; } // Course references list
        
        // Tracking fields
        public DateTime LastModified { get; init; }
        public string ModifiedBy { get; init; }

        // Constructor for immutability
        public StudentCourse(string studentMatricNumber, LevelType studentLevel, 
            List<CourseRefrence> courseReferences, DateTime? lastModified = null, string modifiedBy = "")
        {
            StudentMatricNumber = studentMatricNumber ?? throw new ArgumentNullException(nameof(studentMatricNumber));
            StudentLevel = studentLevel;
            StudentCoursesRefs = courseReferences?.AsReadOnly() ?? new List<CourseRefrence>().AsReadOnly();
            LastModified = lastModified ?? DateTime.UtcNow;
            ModifiedBy = modifiedBy ?? "System";
        }
        public StudentCourse(string studentMatricNumber, LevelType studentLevel, 
            List<CourseRefrence> studentCoursesRefs = null)
        {
            StudentMatricNumber = studentMatricNumber;
            StudentLevel = studentLevel;
            StudentCoursesRefs = studentCoursesRefs?.AsReadOnly() ?? new List<CourseRefrence>().AsReadOnly();
        }

      

        // Method to create updated version with new course references
        public StudentCourse WithCourseReferences(List<CourseRefrence> newCourseRefs)
        {
            return new StudentCourse(StudentMatricNumber, StudentLevel, newCourseRefs);
        }

        // Method to create updated version with new level
        public StudentCourse WithLevel(LevelType newLevel)
        {
            return new StudentCourse(StudentMatricNumber, newLevel, StudentCoursesRefs.ToList());
        }
        // Factory method for creating new student course records
        public static StudentCourse Create(string studentMatricNumber, LevelType studentLevel, 
            List<CourseRefrence> courseReferences = null)
        {
            return new StudentCourse(studentMatricNumber, studentLevel, courseReferences, DateTime.UtcNow, "System");
        }

        // Method to add a course reference
        public StudentCourse WithAddedCourse(CourseRefrence courseReference, string modifiedBy = "System")
        {
            if (courseReference == null) throw new ArgumentNullException(nameof(courseReference));
            
            var updatedCourses = StudentCoursesRefs.ToList();
            
            // Check if course already exists and update or add
            var existingIndex = updatedCourses.FindIndex(c => c.CourseCode == courseReference.CourseCode);
            if (existingIndex >= 0)
            {
                updatedCourses[existingIndex] = courseReference;
            }
            else
            {
                updatedCourses.Add(courseReference);
            }

            return new StudentCourse(StudentMatricNumber, StudentLevel, updatedCourses, DateTime.UtcNow, modifiedBy);
        }

        // Method to remove a course reference
        public StudentCourse WithRemovedCourse(string courseCode, string modifiedBy = "System")
        {
            if (string.IsNullOrWhiteSpace(courseCode)) return this;
            
            var updatedCourses = StudentCoursesRefs.Where(c => c.CourseCode != courseCode).ToList();
            return new StudentCourse(StudentMatricNumber, StudentLevel, updatedCourses, DateTime.UtcNow, modifiedBy);
        }

        // Method to update course enrollment status
        public StudentCourse WithUpdatedCourseStatus(string courseCode, CourseEnrollmentStatus newStatus, string modifiedBy = "System")
        {
            if (string.IsNullOrWhiteSpace(courseCode)) return this;
            
            var updatedCourses = StudentCoursesRefs.ToList();
            var courseIndex = updatedCourses.FindIndex(c => c.CourseCode == courseCode);
            
            if (courseIndex >= 0)
            {
                var existingCourse = updatedCourses[courseIndex];
                updatedCourses[courseIndex] = existingCourse.WithStatus(newStatus);
                return new StudentCourse(StudentMatricNumber, StudentLevel, updatedCourses, DateTime.UtcNow, modifiedBy);
            }
            
            return this; // Course not found, return unchanged
        }

        // Method to create updated version with modification tracking
        public StudentCourse WithModification(string modifiedBy)
        {
            return new StudentCourse(StudentMatricNumber, StudentLevel, 
                StudentCoursesRefs.ToList(), DateTime.UtcNow, modifiedBy);
        }

        // Helper method to get enrolled courses only
        public IReadOnlyList<CourseRefrence> GetEnrolledCourses()
        {
            return StudentCoursesRefs.Where(c => c.CourseEnrollmentStatus == CourseEnrollmentStatus.Enrolled).ToList().AsReadOnly();
        }

        // Helper method to get courses by status
        public IReadOnlyList<CourseRefrence> GetCoursesByStatus(CourseEnrollmentStatus status)
        {
            return StudentCoursesRefs.Where(c => c.CourseEnrollmentStatus == status).ToList().AsReadOnly();
        }
    }

    /// <summary>
    /// Represents a reference to a course with enrollment status
    /// </summary>
    public class CourseRefrence
    {
        [Required]
        [StringLength(10, ErrorMessage = "Course code cannot exceed 10 characters")]
        public string CourseCode { get; init; } // Primary identifier: CYB415, CSC484, AED994, etc.
        
        public CourseEnrollmentStatus CourseEnrollmentStatus { get; init; }
        
        // Additional tracking fields
        public DateTime EnrollmentDate { get; init; }
        public DateTime LastStatusChange { get; init; }

        // Constructor
        public CourseRefrence(string courseCode, CourseEnrollmentStatus enrollmentStatus, 
            DateTime? enrollmentDate = null, DateTime? lastStatusChange = null)
        {
            CourseCode = courseCode ?? throw new ArgumentNullException(nameof(courseCode));
            CourseEnrollmentStatus = enrollmentStatus;
            EnrollmentDate = enrollmentDate ?? DateTime.UtcNow;
            LastStatusChange = lastStatusChange ?? DateTime.UtcNow;
        }
        public CourseRefrence(string courseCode, CourseEnrollmentStatus courseEnrollmentStatus)
        {
            CourseCode = courseCode;
            CourseEnrollmentStatus = courseEnrollmentStatus;
        }
        // Factory method for creating new course reference
        public static CourseRefrence Create(string courseCode, CourseEnrollmentStatus enrollmentStatus = CourseEnrollmentStatus.Enrolled)
        {
            return new CourseRefrence(courseCode, enrollmentStatus, DateTime.UtcNow, DateTime.UtcNow);
        }

        // Method to update enrollment status
        public CourseRefrence WithStatus(CourseEnrollmentStatus newStatus)
        {
            return new CourseRefrence(CourseCode, newStatus, EnrollmentDate, DateTime.UtcNow);
        }

        // Override equality for proper comparison
        public override bool Equals(object obj)
        {
            if (obj is CourseRefrence other)
            {
                return CourseCode == other.CourseCode;
            }
            return false;
        }

        public override int GetHashCode()
        {
            return CourseCode?.GetHashCode() ?? 0;
        }
    }
}