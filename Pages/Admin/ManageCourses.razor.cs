using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using AirCode.Domain.Enums;
using AirCode.Models.Core;
using AirCode.Services.Courses;
using AirCode.Services.Firebase;
using Newtonsoft.Json;

namespace AirCode.Pages.Admin
{
    public partial class ManageCourses : ComponentBase
    {
        [Inject]
        private ICourseService CourseService { get; set; }
        
        [Inject]
        private IFirestoreService FirestoreService { get; set; }

        // Firebase connection status
        public bool IsFirebaseInitialized { get; private set; }
        public bool IsFirebaseConnected { get; private set; }
        public string FirebaseConnectionError { get; private set; }

        // Course management properties
        public List<CourseDto> Courses { get; private set; } = new List<CourseDto>();
        public List<CourseDto> FilteredCourses { get; private set; } = new List<CourseDto>();
        public CourseDto SelectedCourse { get; set; } = new CourseDto();
        public bool IsLoading { get; private set; }
        public string ErrorMessage { get; private set; }
        public bool ShowAddEditModal { get; private set; }
        public bool IsEditing { get; private set; }

        // Filters
        public string DepartmentFilter { get; set; }
        public LevelType? LevelFilter { get; set; }
        public SemesterType? SemesterFilter { get; set; }

        // Form model for add/edit
        public CourseFormModel FormModel { get; set; } = new CourseFormModel();

        protected override async Task OnInitializedAsync()
        {
            await CheckFirebaseConnection();
            if (IsFirebaseConnected)
            {
                await LoadCoursesAsync();
            }
        }

        private async Task CheckFirebaseConnection()
        {
            try
            {
                IsFirebaseInitialized = FirestoreService.IsInitialized;
                IsFirebaseConnected = await FirestoreService.IsConnectedAsync();
                
                if (!IsFirebaseConnected)
                {
                    FirebaseConnectionError = "Unable to connect to Firebase. Please check your connection and try again.";
                }
                else
                {
                    FirebaseConnectionError = string.Empty;
                }
            }
            catch (Exception ex)
            {
                IsFirebaseConnected = false;
                FirebaseConnectionError = $"Firebase connection error: {ex.Message}";
            }
            
            StateHasChanged();
        }

        public async Task RetryFirebaseConnection()
        {
            IsLoading = true;
            await CheckFirebaseConnection();
            
            if (IsFirebaseConnected)
            {
                await LoadCoursesAsync();
            }
            
            IsLoading = false;
        }

        public async Task LoadCoursesAsync()
        {
            if (!IsFirebaseConnected)
            {
                ErrorMessage = "Firebase is not connected. Cannot load courses.";
                return;
            }

            try
            {
                IsLoading = true;
                ErrorMessage = string.Empty;

                // Load courses from Firebase via CourseService
                Courses = await CourseService.GetAllCoursesAsync();
                ApplyFilters();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Error loading courses: {ex.Message}";
                Courses = new List<CourseDto>();
                FilteredCourses = new List<CourseDto>();
            }
            finally
            {
                IsLoading = false;
                StateHasChanged();
            }
        }

        public void ApplyFilters()
        {
            FilteredCourses = new List<CourseDto>(Courses);

            if (!string.IsNullOrWhiteSpace(DepartmentFilter))
            {
                FilteredCourses = FilteredCourses.FindAll(c =>
                    c.Department?.Contains(DepartmentFilter, StringComparison.OrdinalIgnoreCase) ?? false);
            }

            if (LevelFilter.HasValue)
            {
                FilteredCourses = FilteredCourses.FindAll(c => c.Level == LevelFilter.Value);
            }

            if (SemesterFilter.HasValue)
            {
                FilteredCourses = FilteredCourses.FindAll(c => c.Semester == SemesterFilter.Value);
            }

            StateHasChanged();
        }

        public async Task ResetFilters()
        {
            DepartmentFilter = string.Empty;
            LevelFilter = null;
            SemesterFilter = null;
            await LoadCoursesAsync();
        }

        public void ShowAddCourse()
        {
            if (!IsFirebaseConnected)
            {
                ErrorMessage = "Cannot add course: Firebase is not connected.";
                return;
            }

            FormModel = new CourseFormModel();
            IsEditing = false;
            ShowAddEditModal = true;
        }

        public void ShowEditCourse(CourseDto course)
        {
            if (!IsFirebaseConnected)
            {
                ErrorMessage = "Cannot edit course: Firebase is not connected.";
                return;
            }

            FormModel = MapCourseToForm(course);
            SelectedCourse = course;
            IsEditing = true;
            ShowAddEditModal = true;
        }

        public async Task SaveCourseAsync()
        {
            if (!IsFirebaseConnected)
            {
                ErrorMessage = "Cannot save course: Firebase is not connected.";
                return;
            }

            try
            {
                IsLoading = true;
                ErrorMessage = string.Empty;

                var courseDto = MapFormToCourse();

                bool success;
                if (IsEditing)
                {
                    courseDto.Id = SelectedCourse.Id;
                    success = await CourseService.UpdateCourseAsync(courseDto);
                }
                else
                {
                    success = await CourseService.AddCourseAsync(courseDto);
                }

                if (success)
                {
                    ShowAddEditModal = false;
                    await LoadCoursesAsync();
                }
                else
                {
                    ErrorMessage = "Failed to save course. Please try again.";
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Error saving course: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
                StateHasChanged();
            }
        }

        public async Task DeleteCourseAsync(string courseId)
        {
            if (!IsFirebaseConnected)
            {
                ErrorMessage = "Cannot delete course: Firebase is not connected.";
                return;
            }

            try
            {
                IsLoading = true;
                ErrorMessage = string.Empty;

                var success = await CourseService.DeleteCourseAsync(courseId);

                if (success)
                {
                    await LoadCoursesAsync();
                }
                else
                {
                    ErrorMessage = "Failed to delete course. Please try again.";
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Error deleting course: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
                StateHasChanged();
            }
        }

        public void CloseModal()
        {
            ShowAddEditModal = false;
            ErrorMessage = string.Empty;
        }

        private CourseFormModel MapCourseToForm(CourseDto course)
        {
            return new CourseFormModel
            {
                Name = course.Name,
                Department = course.Department,
                Level = course.Level,
                Semester = course.Semester,
                CreditUnits = course.CreditUnits,
                ScheduleItems = course.Schedule ?? new List<CourseScheduleDto>()
            };
        }

        private CourseDto MapFormToCourse()
        {
            return new CourseDto
            {
                Name = FormModel.Name,
                Department = FormModel.Department,
                Level = FormModel.Level,
                Semester = FormModel.Semester,
                CreditUnits = (byte)FormModel.CreditUnits,
                Schedule = FormModel.ScheduleItems ?? new List<CourseScheduleDto>(),
                Lecturers = new List<SimpleLecturerDto>(),
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };
        }

        // Time helper methods for the form
        private void UpdateStartTime(int index, ChangeEventArgs e)
        {
            if (TimeSpan.TryParse((string)e.Value, out var time))
            {
                FormModel.ScheduleItems[index].StartTime = time;
            }
        }

        private void UpdateEndTime(int index, ChangeEventArgs e)
        {
            if (TimeSpan.TryParse((string)e.Value, out var time))
            {
                FormModel.ScheduleItems[index].EndTime = time;
            }
        }
    }

    /// <summary>
    /// Form model for adding/editing courses with validation
    /// </summary>
    public class CourseFormModel
    {
        [Required(ErrorMessage = "Course name is required")]
        [StringLength(100, ErrorMessage = "Course name cannot exceed 100 characters")]
        public string Name { get; set; }

        [Required(ErrorMessage = "Department is required")]
        public string Department { get; set; }

        [Required(ErrorMessage = "Level is required")]
        public LevelType Level { get; set; }

        [Required(ErrorMessage = "Semester is required")]
        public SemesterType Semester { get; set; }

        [Required(ErrorMessage = "Credit units is required")]
        [Range(1, 10, ErrorMessage = "Credit units must be between 1 and 10")]
        public int CreditUnits { get; set; } = 3; // Default to 3 credit units

        public List<CourseScheduleDto> ScheduleItems { get; set; } = new List<CourseScheduleDto>();

        /// <summary>
        /// Add a new schedule item with default values
        /// </summary>
        public void AddScheduleItem()
        {
            ScheduleItems.Add(new CourseScheduleDto
            {
                Day = DayOfWeek.Monday,
                StartTime = new TimeSpan(8, 0, 0),
                EndTime = new TimeSpan(10, 0, 0),
                Location = "TBA"
            });
        }

        /// <summary>
        /// Remove a schedule item at the specified index
        /// </summary>
        /// <param name="index">Index of the item to remove</param>
        public void RemoveScheduleItem(int index)
        {
            if (index >= 0 && index < ScheduleItems.Count)
            {
                ScheduleItems.RemoveAt(index);
            }
        }

        /// <summary>
        /// Validate that end time is after start time for all schedule items
        /// </summary>
        public bool ValidateScheduleTimes()
        {
            foreach (var item in ScheduleItems)
            {
                if (item.EndTime <= item.StartTime)
                {
                    return false;
                }
            }
            return true;
        }
    }
}