using Microsoft.AspNetCore.Components;
using System.Text;
using System.Security.Cryptography;
using AirCode.Components.SharedPrefabs.Cards;
using AirCode.Services.Firebase;
using AirCode.Domain.Enums;

namespace AirCode.Pages.Admin.Superior;

public partial class ManageUsers : ComponentBase
{
    [Inject] private IFirestoreService FirestoreService { get; set; }
        
        // Collections
        private const string STUDENTS_COLLECTION = "STUDENTS_MATRICULATION_NUMBERS";
        private const string ADMIN_IDS_COLLECTION = "VALID_ADMIN_IDS";
        
        // Document names
        private const string LECTURER_ADMIN_DOC = "LecturerAdminIdsDoc";
        private const string COURSEREP_ADMIN_DOC = "CourseRepAdminIdsDoc";
        
        // Data lists
        private List<StudentSkeletonUser> students = new();
        private List<LecturerSkeletonUser> lecturers = new();
        private List<CourseRepSkeletonUser> courseReps = new();
        
        // UI State
        private bool loading = true;
        private string activeTab = "students";
        
        // Modal states
        private bool showCreateModal = false;
        private bool showMaxUsageModal = false;
        private bool showDeleteModal = false;
        private bool showRemoveUserModal = false;
        
        // Form fields
        private string newUserType = "Student";
        private string newMatricNumber = "";
        private string newLevel = "100";
        private int newMaxUsage = 1;
        private int updateMaxUsage = 1;
        
        // Selected items
        private object selectedUser;
        private string selectedUserType;
        private string selectedAssignedUserId;
        
        // Reference to notification component
        private NotificationComponent notificationComponent;
        
        protected override async Task OnInitializedAsync()
        {
            await LoadAllUsers();
        }
        
        private async Task LoadAllUsers()
        {
            try
            {
                loading = true;
                await Task.WhenAll(
                    LoadStudents(),
                    LoadLecturers(),
                    LoadCourseReps()
                );
            }
            catch (Exception ex)
            {
                notificationComponent?.ShowError($"Error loading users: {ex.Message}");
            }
            finally
            {
                loading = false;
                StateHasChanged();
            }
        }
        
        private async Task LoadStudents()
        {
            try
            {
                students.Clear();
                
                // Load from all student level documents
                for (int level = 100; level <= 500; level += 100)
                {
                    var docName = $"StudentLevel{level}";
                    var levelData = await FirestoreService.GetDocumentAsync<StudentLevelDocument>(STUDENTS_COLLECTION, docName);
                    
                    if (levelData?.ValidStudentMatricNumbers != null)
                    {
                        foreach (var student in levelData.ValidStudentMatricNumbers)
                        {
                            students.Add(student);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                notificationComponent?.ShowError($"Error loading students: {ex.Message}");
            }
        }
        
        private async Task LoadLecturers()
        {
            try
            {
                lecturers.Clear();
                
                var lecturerDoc = await FirestoreService.GetDocumentAsync<LecturerAdminDocument>(ADMIN_IDS_COLLECTION, LECTURER_ADMIN_DOC);
                if (lecturerDoc?.Ids != null)
                {
                    foreach (var adminData in lecturerDoc.Ids)
                    {
                        lecturers.Add(new LecturerSkeletonUser
                        {
                            AdminId = adminData.AdminId,
                            LecturerId = adminData.LecturerId,
                            CurrentUsage = adminData.CurrentUsage,
                            MaxUsage = adminData.MaxUsage,
                            UserIds = adminData.UserIds
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                notificationComponent?.ShowError($"Error loading lecturers: {ex.Message}");
            }
        }
        
        private async Task LoadCourseReps()
        {
            try
            {
                courseReps.Clear();
                
                var courseRepDoc = await FirestoreService.GetDocumentAsync<CourseRepAdminDocument>(ADMIN_IDS_COLLECTION, COURSEREP_ADMIN_DOC);
                if (courseRepDoc?.Ids != null)
                {
                    foreach (var adminData in courseRepDoc.Ids)
                    {
                        // Find corresponding student data
                        var studentInfo = FindStudentByMatricNumber(adminData.MatricNumber);
                        if (studentInfo != null)
                        {
                            courseReps.Add(new CourseRepSkeletonUser
                            {
                                AdminInfo = new CourseRepAdminInfo
                                {
                                    AdminId = adminData.AdminId,
                                    MatricNumber = adminData.MatricNumber,
                                    CurrentUsage = adminData.CurrentUsage,
                                    MaxUsage = adminData.MaxUsage,
                                    UserIds = adminData.UserIds
                                },
                                StudentInfo = studentInfo
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                notificationComponent?.ShowError($"Error loading course reps: {ex.Message}");
            }
        }
        
        private StudentSkeletonUser FindStudentByMatricNumber(string matricNumber)
        {
            return students.FirstOrDefault(s => s.MatricNumber.Equals(matricNumber, StringComparison.OrdinalIgnoreCase));
        }
        
        private void SetActiveTab(string tab)
        {
            activeTab = tab;
            StateHasChanged();
        }
        
        private void ShowCreateUserModal()
        {
            ResetCreateForm();
            showCreateModal = true;
        }
        
        private void ShowMaxUsageModal(object user, string userType)
        {
            selectedUser = user;
            selectedUserType = userType;
            
            if (user is LecturerSkeletonUser lecturer)
                updateMaxUsage = lecturer.MaxUsage;
            else if (user is CourseRepAdminInfo courseRepAdmin)
                updateMaxUsage = courseRepAdmin.MaxUsage;
                
            showMaxUsageModal = true;
        }
        
        private void ShowDeleteUserModal(object user, string userType)
        {
            selectedUser = user;
            selectedUserType = userType;
            showDeleteModal = true;
        }
        
        private void ShowRemoveUserModal(object user, string userId, string userType)
        {
            selectedUser = user;
            selectedAssignedUserId = userId;
            selectedUserType = userType;
            showRemoveUserModal = true;
        }
        
        private void CloseModals()
        {
            showCreateModal = false;
            showMaxUsageModal = false;
            showDeleteModal = false;
            showRemoveUserModal = false;
            selectedUser = null;
            selectedUserType = null;
            selectedAssignedUserId = null;
        }
        
        private void OnUserTypeChanged(ChangeEventArgs e)
        {
            newUserType = e.Value?.ToString() ?? "Student";
            
            // Reset max usage based on user type
            if (newUserType == "Lecturer")
                newMaxUsage = 1;
            else if (newUserType == "CourseRep")
                newMaxUsage = 2;
                
            StateHasChanged();
        }
        
        private void ResetCreateForm()
        {
            newUserType = "Student";
            newMatricNumber = "";
            newLevel = "100";
            newMaxUsage = 1;
        }
        
        private async Task CreateSkeletonUser()
        {
            try
            {
                switch (newUserType)
                {
                    case "Student":
                        await CreateStudentSkeleton();
                        break;
                    case "Lecturer":
                        await CreateLecturerSkeleton();
                        break;
                    case "CourseRep":
                        await CreateCourseRepSkeleton();
                        break;
                }
                
                notificationComponent?.ShowSuccess("Skeleton user created successfully!");
                CloseModals();
                await LoadAllUsers();
            }
            catch (Exception ex)
            {
                notificationComponent?.ShowError($"Error creating skeleton user: {ex.Message}");
            }
        }
        
        private async Task CreateStudentSkeleton()
        {
            if (string.IsNullOrWhiteSpace(newMatricNumber))
                throw new ArgumentException("Matriculation number is required");
                
            var matricNumber = newMatricNumber.ToUpper().Replace(" ", "").Replace("_", "");
            
            // Check if student already exists
            if (students.Any(s => s.MatricNumber.Equals(matricNumber, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("Student with this matriculation number already exists");
            
            var newStudent = new StudentSkeletonUser
            {
                CurrentUserId = "",
                IsCurrentlyInUse = false,
                Level = newLevel,
                MatricNumber = matricNumber
            };
            
            var docName = $"StudentLevel{newLevel}";
            
            // Get existing document or create new one
            var existingDoc = await FirestoreService.GetDocumentAsync<StudentLevelDocument>(STUDENTS_COLLECTION, docName);
            if (existingDoc == null)
            {
                existingDoc = new StudentLevelDocument { ValidStudentMatricNumbers = new List<StudentSkeletonUser>() };
            }
            
            existingDoc.ValidStudentMatricNumbers.Add(newStudent);
            
            await FirestoreService.UpdateDocumentAsync(STUDENTS_COLLECTION, docName, existingDoc);
        }
        
        private async Task CreateLecturerSkeleton()
        {
            var adminId = GenerateAdminId("Lecturer");
            var lecturerId = GenerateLecturerId();
            
            var newLecturerAdmin = new LecturerAdminInfo
            {
                AdminId = adminId,
                LecturerId = lecturerId,
                CurrentUsage = 0,
                MaxUsage = newMaxUsage,
                UserIds = new List<string>()
            };
            
            // Get existing document or create new one
            var existingDoc = await FirestoreService.GetDocumentAsync<LecturerAdminDocument>(ADMIN_IDS_COLLECTION, LECTURER_ADMIN_DOC);
            if (existingDoc == null)
            {
                existingDoc = new LecturerAdminDocument { Ids = new List<LecturerAdminInfo>() };
            }
            
            existingDoc.Ids.Add(newLecturerAdmin);
            
            await FirestoreService.UpdateDocumentAsync(ADMIN_IDS_COLLECTION, LECTURER_ADMIN_DOC, existingDoc);
        }

        private async Task CreateCourseRepSkeleton()
        {
        }
        }