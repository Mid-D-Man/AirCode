using AirCode.Services.Courses;

namespace AirCode.Models.ViewModel;

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using AirCode.Domain.Enums;
using AirCode.Models.Core;
using AirCode.Services;
using Microsoft.AspNetCore.Components;

    public class ManageCoursesViewModel : ComponentBase
    {
        [Inject]
        private ICourseService CourseService { get; set; }
        
        public List<CourseDto> Courses { get; private set; } = new List<CourseDto>();
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
            await LoadCoursesAsync();
        }
        
        public async Task LoadCoursesAsync()
        {
            try
            {
                IsLoading = true;
                ErrorMessage = string.Empty;
                
                Courses = await CourseService.GetAllCoursesAsync();
                
                ApplyFilters();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Error loading courses: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
                StateHasChanged();
            }
        }
        
        public void ApplyFilters()
        {
            var filteredCourses = Courses;
            
            if (!string.IsNullOrWhiteSpace(DepartmentFilter))
            {
                filteredCourses = filteredCourses.FindAll(c => 
                    c.Department?.Contains(DepartmentFilter, StringComparison.OrdinalIgnoreCase) ?? false);
            }
            
            if (LevelFilter.HasValue)
            {
                filteredCourses = filteredCourses.FindAll(c => c.Level == LevelFilter.Value);
            }
            
            if (SemesterFilter.HasValue)
            {
                filteredCourses = filteredCourses.FindAll(c => c.Semester == SemesterFilter.Value);
            }
            
            Courses = filteredCourses;
        }
        
        public void ShowAddCourse()
        {
            FormModel = new CourseFormModel();
            IsEditing = false;
            ShowAddEditModal = true;
        }
        
        public void ShowEditCourse(CourseDto course)
        {
            // Map CourseDto to FormModel
            FormModel = MapCourseToForm(course);
            SelectedCourse = course;
            IsEditing = true;
            ShowAddEditModal = true;
        }
        
        public async Task SaveCourseAsync()
        {
            try
            {
                IsLoading = true;
                
                // Map form to CourseDto
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
            try
            {
                IsLoading = true;
                
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
        }
        
        private CourseFormModel MapCourseToForm(CourseDto course)
        {
            return new CourseFormModel
            {
                Name = course.Name,
                Department = course.Department,
                Level = course.Level,
                Semester = course.Semester,
                ScheduleItems = course.Schedule
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
                Schedule = FormModel.ScheduleItems ?? new List<CourseScheduleDto>(),
                Lecturers = new List<SimpleLecturerDto>(), // Empty initially when creating
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };
        }
    }
    
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
        
        public List<CourseScheduleDto> ScheduleItems { get; set; } = new List<CourseScheduleDto>();
        
        // Method to add a new schedule item
        public void AddScheduleItem()
        {
            ScheduleItems.Add(new CourseScheduleDto
            {
                Day = DayOfWeek.Monday,
                StartTime = new TimeSpan(8, 0, 0),
                EndTime = new TimeSpan(10, 0, 0),
                Location = "Default Location"
            });
        }
        
        // Method to remove a schedule item
        public void RemoveScheduleItem(int index)
        {
            if (index >= 0 && index < ScheduleItems.Count)
            {
                ScheduleItems.RemoveAt(index);
            }
        }
        private void UpdateStartTime(int index, ChangeEventArgs e)
        {
            if (TimeSpan.TryParse((string)e.Value, out var time))
            {
                ScheduleItems[index].StartTime = time;
            }
        }
    }
