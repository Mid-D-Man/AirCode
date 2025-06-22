using Microsoft.AspNetCore.Components;
using System.Text;
using System.Security.Cryptography;
using AirCode.Components.SharedPrefabs.Cards;
using AirCode.Services.Firebase;
using AirCode.Domain.ValueObjects;
using Microsoft.JSInterop;

namespace AirCode.Pages.Admin.Superior;

public partial class ManageUsers : ComponentBase
{
    [Inject] private IFirestoreService FirestoreService { get; set; }
    [Inject] private IJSRuntime JSRuntime { get; set; }
    
    // Collections - simplified without pooling
    private readonly Collections _collections = new();
    
    // Constants
    private const string STUDENTS_COLLECTION = "STUDENTS_MATRICULATION_NUMBERS";
    private const string ADMIN_IDS_COLLECTION = "VALID_ADMIN_IDS";
    private const string LECTURER_ADMIN_DOC = "LecturerAdminIdsDoc";
    private const string COURSEREP_ADMIN_DOC = "CourseRepAdminIdsDoc";
    private const string STUDENT_ID = "students";
    private const string COURSEREP_ID = "coursereps";
    private const string LECTURER_ID = "lecturers";
    
    // Pagination Constants
    private const int ITEMS_PER_PAGE = 10;
    
    // UI State
    private bool loading = true;
    private string activeTab = "students";
    
    // Pagination State
    private readonly PaginationState _paginationState = new();
    
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
    
    // Rendering control
    private bool _isUpdatingCollections = false;
    
    protected override async Task OnInitializedAsync()
    {
        await LoadAllUsers();
    }
    
    private async Task LoadAllUsers()
    {
        try
        {
            loading = true;
            _isUpdatingCollections = true;
            StateHasChanged();
        
            // Load sequentially to prevent concurrent modification
            await LoadStudents();
            await LoadLecturers();
            await LoadCourseReps();
        
            _paginationState.ResetAllPages();
        }
        catch (Exception ex)
        {
            notificationComponent?.ShowError($"Error loading users: {ex.Message}");
        }
        finally
        {
            _isUpdatingCollections = false;
            loading = false;
            StateHasChanged();
        }
    }

    private string GetStatusClass(StudentSkeletonUser student)
    {
        return student?.IsCurrentlyInUse == true ? "user-card in-use" : "user-card";
    }
    
    private async Task LoadStudents()
    {
        var tempStudents = new List<StudentSkeletonUser>();
        
        try
        {
            // Load from all student level documents
            for (int level = 100; level <= 500; level += 100)
            {
                var docName = $"StudentLevel{level}";
                var levelData = await FirestoreService.GetDocumentAsync<StudentLevelDocument>(STUDENTS_COLLECTION, docName);
                
                if (levelData?.ValidStudentMatricNumbers != null)
                {
                    // Filter out course reps to prevent them showing in students tab
                    var courseRepMatricNumbers = _collections.CourseReps.Select(cr => cr.AdminInfo.MatricNumber).ToHashSet();
                    
                    foreach (var student in levelData.ValidStudentMatricNumbers)
                    {
                        // Only add if not a course rep
                        if (!courseRepMatricNumbers.Contains(student.MatricNumber))
                        {
                            tempStudents.Add(student);
                        }
                    }
                }
            }
            
            // Atomic update
            _collections.Students.Clear();
            _collections.Students.AddRange(tempStudents);
        }
        catch (Exception ex)
        {
            notificationComponent?.ShowError($"Error loading students: {ex.Message}");
        }
    }
    
    private async Task LoadLecturers()
    {
        var tempLecturers = new List<LecturerSkeletonUser>();
        
        try
        {
            var lecturerDoc = await FirestoreService.GetDocumentAsync<LecturerAdminDocument>(ADMIN_IDS_COLLECTION, LECTURER_ADMIN_DOC);
            if (lecturerDoc?.Ids != null)
            {
                foreach (var adminData in lecturerDoc.Ids)
                {
                    tempLecturers.Add(new LecturerSkeletonUser
                    {
                        AdminId = adminData.AdminId,
                        LecturerId = adminData.LecturerId,
                        CurrentUsage = adminData.CurrentUsage,
                        MaxUsage = adminData.MaxUsage,
                        UserIds = new List<string>(adminData.UserIds) // Defensive copy
                    });
                }
            }
            
            // Atomic update
            _collections.Lecturers.Clear();
            _collections.Lecturers.AddRange(tempLecturers);
        }
        catch (Exception ex)
        {
            notificationComponent?.ShowError($"Error loading lecturers: {ex.Message}");
        }
    }
    
   private async Task LoadCourseReps()
    {
        var tempCourseReps = new List<CourseRepSkeletonUser>();
        
        try
        {
            var courseRepDoc = await FirestoreService.GetDocumentAsync<CourseRepAdminDocument>(ADMIN_IDS_COLLECTION, COURSEREP_ADMIN_DOC);
            if (courseRepDoc?.Ids != null)
            {
                foreach (var adminData in courseRepDoc.Ids)
                {
                    // Find corresponding student data
                    var studentInfo = await FindStudentByMatricNumberAsync(adminData.MatricNumber);
                    if (studentInfo != null)
                    {
                        tempCourseReps.Add(new CourseRepSkeletonUser
                        {
                            AdminInfo = new CourseRepAdminInfo
                            {
                                AdminId = adminData.AdminId,
                                MatricNumber = adminData.MatricNumber,
                                CurrentUsage = adminData.CurrentUsage,
                                MaxUsage = adminData.MaxUsage,
                                UserIds = new List<string>(adminData.UserIds) // Defensive copy
                            },
                            StudentInfo = studentInfo
                        });
                    }
                }
            }
            
            // Atomic update
            _collections.CourseReps.Clear();
            _collections.CourseReps.AddRange(tempCourseReps);
        }
        catch (Exception ex)
        {
            notificationComponent?.ShowError($"Error loading course reps: {ex.Message}");
        }
    }
    
    private async Task<StudentSkeletonUser> FindStudentByMatricNumberAsync(string matricNumber)
    {
        // Check all level documents for the student
        for (int level = 100; level <= 500; level += 100)
        {
            var docName = $"StudentLevel{level}";
            var levelData = await FirestoreService.GetDocumentAsync<StudentLevelDocument>(STUDENTS_COLLECTION, docName);
            
            if (levelData?.ValidStudentMatricNumbers != null)
            {
                var student = levelData.ValidStudentMatricNumbers
                    .FirstOrDefault(s => s.MatricNumber.Equals(matricNumber, StringComparison.OrdinalIgnoreCase));
                if (student != null)
                    return student;
            }
        }
        
        return null;
    }
    
    // Pagination Methods
    private IEnumerable<T> GetPagedItems<T>(List<T> items, int currentPage)
    {
        if (items == null || !items.Any() || _isUpdatingCollections)
            return Enumerable.Empty<T>();
    
        return items
            .Skip((currentPage - 1) * ITEMS_PER_PAGE)
            .Take(ITEMS_PER_PAGE);
    }

    private int GetTotalPages(int totalItems)
    {
        return (int)Math.Ceiling((double)totalItems / ITEMS_PER_PAGE);
    }

    private void NavigateToPage(string userType, int page)
    {
        if (_isUpdatingCollections) return;
        
        switch (userType)
        {
            case STUDENT_ID:
                _paginationState.StudentsCurrentPage = page;
                break;
            case LECTURER_ID:
                _paginationState.LecturersCurrentPage = page;
                break;
            case COURSEREP_ID:
                _paginationState.CourseRepsCurrentPage = page;
                break;
        }
        StateHasChanged();
    }
    
    // Generate ID methods - fixed for HTML attribute safety
    private string GenerateAdminId()
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var randomBytes = new byte[8];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(randomBytes);
        }
        
        // Use only alphanumeric characters for HTML safety
        var randomString = Convert.ToHexString(randomBytes).ToLower();
        return $"AIRCODE-{timestamp}-{randomString}";
    }

    private string GenerateLecturerId()
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var randomBytes = new byte[3];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(randomBytes);
        }
        var randomString = Convert.ToHexString(randomBytes).ToLower();
        return $"LEC-{timestamp}-{randomString}";
    }

    private void SetActiveTab(string tab)
    {
        if (_isUpdatingCollections) return;
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
    
    protected override bool ShouldRender()
    {
        // Prevent rendering during collection updates
        return !_isUpdatingCollections && !loading && _collections != null;
    }

    private void CloseModals()
    {
        showCreateModal = false;
        showMaxUsageModal = false;
        showDeleteModal = false;
        showRemoveUserModal = false;
    
        // Clear form state
        newMatricNumber = "";
        selectedUser = null;
        selectedUserType = null;
        selectedAssignedUserId = null;
    }

    private void OnUserTypeChanged(ChangeEventArgs e)
    {
        newUserType = e.Value?.ToString() ?? "Student";
    
        // Reset max usage based on user type
        newMaxUsage = newUserType switch
        {
            "Lecturer" => 1,
            "CourseRep" => 2,
            _ => 1
        };
    
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
            loading = true;
            StateHasChanged();
        
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
        finally
        {
            loading = false;
            StateHasChanged();
        }
    }
    
    private async Task CreateStudentSkeleton()
    {
        if (string.IsNullOrWhiteSpace(newMatricNumber))
            throw new ArgumentException("Matriculation number is required");

        var matricNumber = newMatricNumber.ToUpper().Replace(" ", "").Replace("_", "");

        // Check if student already exists
        if (_collections.Students.Any(s => s.MatricNumber.Equals(matricNumber, StringComparison.OrdinalIgnoreCase)))
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

    private async Task CreateCourseRepSkeleton()
    {
        if (string.IsNullOrWhiteSpace(newMatricNumber))
            throw new ArgumentException("Matriculation number is required");

        var matricNumber = newMatricNumber.ToUpper().Replace(" ", "").Replace("_", "");

        // Check if course rep already exists
        if (_collections.CourseReps.Any(cr => cr.AdminInfo.MatricNumber.Equals(matricNumber, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Course rep with this matriculation number already exists");

        // Create student entry first
        await CreateStudentSkeleton();

        // Create admin entry
        var adminId = GenerateAdminId();

        var newCourseRepAdmin = new CourseRepAdminInfo
        {
            AdminId = adminId,
            MatricNumber = matricNumber,
            CurrentUsage = 0,
            MaxUsage = newMaxUsage,
            UserIds = new List<string>()
        };

        // Get existing document or create new one
        var existingDoc = await FirestoreService.GetDocumentAsync<CourseRepAdminDocument>(ADMIN_IDS_COLLECTION, COURSEREP_ADMIN_DOC);
        if (existingDoc == null)
        {
            existingDoc = new CourseRepAdminDocument { Ids = new List<CourseRepAdminInfo>() };
        }

        existingDoc.Ids.Add(newCourseRepAdmin);

        await FirestoreService.UpdateDocumentAsync(ADMIN_IDS_COLLECTION, COURSEREP_ADMIN_DOC, existingDoc);
    }
        
    private async Task CreateLecturerSkeleton()
    {
        var adminId = GenerateAdminId();
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
    
    private async Task UpdateMaxUsage()
    {
        try
        {
            if (selectedUser is LecturerSkeletonUser lecturer)
            {
                await UpdateLecturerMaxUsage(lecturer, updateMaxUsage);
            }
            else if (selectedUser is CourseRepAdminInfo courseRepAdmin)
            {
                await UpdateCourseRepMaxUsage(courseRepAdmin, updateMaxUsage);
            }
            
            notificationComponent?.ShowSuccess("Maximum usage updated successfully!");
            CloseModals();
            await LoadAllUsers();
        }
        catch (Exception ex)
        {
            notificationComponent?.ShowError($"Error updating maximum usage: {ex.Message}");
        }
    }
    
    private async Task UpdateLecturerMaxUsage(LecturerSkeletonUser lecturer, int newMaxUsage)
    {
        var lecturerDoc = await FirestoreService.GetDocumentAsync<LecturerAdminDocument>(ADMIN_IDS_COLLECTION, LECTURER_ADMIN_DOC);
        if (lecturerDoc?.Ids != null)
        {
            var lecturerToUpdate = lecturerDoc.Ids.FirstOrDefault(l => l.AdminId == lecturer.AdminId);
            if (lecturerToUpdate != null)
            {
                lecturerToUpdate.MaxUsage = newMaxUsage;
                await FirestoreService.UpdateDocumentAsync(ADMIN_IDS_COLLECTION, LECTURER_ADMIN_DOC, lecturerDoc);
            }
        }
    }
    
    private async Task UpdateCourseRepMaxUsage(CourseRepAdminInfo courseRepAdmin, int newMaxUsage)
    {
        var courseRepDoc = await FirestoreService.GetDocumentAsync<CourseRepAdminDocument>(ADMIN_IDS_COLLECTION, COURSEREP_ADMIN_DOC);
        if (courseRepDoc?.Ids != null)
        {
            var courseRepToUpdate = courseRepDoc.Ids.FirstOrDefault(cr => cr.AdminId == courseRepAdmin.AdminId);
            if (courseRepToUpdate != null)
            {
                courseRepToUpdate.MaxUsage = newMaxUsage;
                await FirestoreService.UpdateDocumentAsync(ADMIN_IDS_COLLECTION, COURSEREP_ADMIN_DOC, courseRepDoc);
            }
        }
    }
    
    private async Task DeleteSkeletonUser()
    {
        try
        {
            switch (selectedUserType)
            {
                case "student":
                    await DeleteStudentSkeleton(selectedUser as StudentSkeletonUser);
                    break;
                case "lecturer":
                    await DeleteLecturerSkeleton(selectedUser as LecturerSkeletonUser);
                    break;
                case "courserep":
                    await DeleteCourseRepSkeleton(selectedUser as CourseRepSkeletonUser);
                    break;
            }
            
            notificationComponent?.ShowSuccess("Skeleton user deleted successfully!");
            CloseModals();
            await LoadAllUsers();
        }
        catch (Exception ex)
        {
            notificationComponent?.ShowError($"Error deleting skeleton user: {ex.Message}");
        }
    }
    
    private async Task DeleteStudentSkeleton(StudentSkeletonUser student)
    {
        var docName = $"StudentLevel{student.Level}";
        var levelDoc = await FirestoreService.GetDocumentAsync<StudentLevelDocument>(STUDENTS_COLLECTION, docName);
        
        if (levelDoc?.ValidStudentMatricNumbers != null)
        {
            levelDoc.ValidStudentMatricNumbers.RemoveAll(s => s.MatricNumber.Equals(student.MatricNumber, StringComparison.OrdinalIgnoreCase));
            await FirestoreService.UpdateDocumentAsync(STUDENTS_COLLECTION, docName, levelDoc);
        }
    }
    
    private async Task DeleteLecturerSkeleton(LecturerSkeletonUser lecturer)
    {
        var lecturerDoc = await FirestoreService.GetDocumentAsync<LecturerAdminDocument>(ADMIN_IDS_COLLECTION, LECTURER_ADMIN_DOC);
        
        if (lecturerDoc?.Ids != null)
        {
            lecturerDoc.Ids.RemoveAll(l => l.AdminId == lecturer.AdminId);
            await FirestoreService.UpdateDocumentAsync(ADMIN_IDS_COLLECTION, LECTURER_ADMIN_DOC, lecturerDoc);
        }
    }
    
    private async Task DeleteCourseRepSkeleton(CourseRepSkeletonUser courseRep)
    {
        // Delete from admin collection
        var courseRepDoc = await FirestoreService.GetDocumentAsync<CourseRepAdminDocument>(ADMIN_IDS_COLLECTION, COURSEREP_ADMIN_DOC);
        if (courseRepDoc?.Ids != null)
        {
            courseRepDoc.Ids.RemoveAll(cr => cr.AdminId == courseRep.AdminInfo.AdminId);
            await FirestoreService.UpdateDocumentAsync(ADMIN_IDS_COLLECTION, COURSEREP_ADMIN_DOC, courseRepDoc);
        }
        
        // Delete from student collection
        await DeleteStudentSkeleton(courseRep.StudentInfo);
    }
    
    private async Task RemoveAssignedUser()
    {
        try
        {
            if (selectedUser is LecturerSkeletonUser lecturer)
            {
                await RemoveUserFromLecturer(lecturer, selectedAssignedUserId);
            }
            else if (selectedUser is CourseRepAdminInfo courseRepAdmin)
            {
                await RemoveUserFromCourseRep(courseRepAdmin, selectedAssignedUserId);
            }
            
            notificationComponent?.ShowSuccess("User removed successfully!");
            CloseModals();
            await LoadAllUsers();
        }
        catch (Exception ex)
        {
            notificationComponent?.ShowError($"Error removing user: {ex.Message}");
        }
    }
    
    private async Task RemoveUserFromLecturer(LecturerSkeletonUser lecturer, string userId)
    {
        var lecturerDoc = await FirestoreService.GetDocumentAsync<LecturerAdminDocument>(ADMIN_IDS_COLLECTION, LECTURER_ADMIN_DOC);
        if (lecturerDoc?.Ids != null)
        {
            var lecturerToUpdate = lecturerDoc.Ids.FirstOrDefault(l => l.AdminId == lecturer.AdminId);
            if (lecturerToUpdate != null)
            {
                lecturerToUpdate.UserIds.Remove(userId);
                lecturerToUpdate.CurrentUsage = lecturerToUpdate.UserIds.Count;
                await FirestoreService.UpdateDocumentAsync(ADMIN_IDS_COLLECTION, LECTURER_ADMIN_DOC, lecturerDoc);
            }
        }
    }
    
    private async Task RemoveUserFromCourseRep(CourseRepAdminInfo courseRepAdmin, string userId)
    {
        var courseRepDoc = await FirestoreService.GetDocumentAsync<CourseRepAdminDocument>(ADMIN_IDS_COLLECTION, COURSEREP_ADMIN_DOC);
        if (courseRepDoc?.Ids != null)
        {
            var courseRepToUpdate = courseRepDoc.Ids.FirstOrDefault(cr => cr.AdminId == courseRepAdmin.AdminId);
            if (courseRepToUpdate != null)
            {
                courseRepToUpdate.UserIds.Remove(userId);
                courseRepToUpdate.CurrentUsage = courseRepToUpdate.UserIds.Count;
                await FirestoreService.UpdateDocumentAsync(ADMIN_IDS_COLLECTION, COURSEREP_ADMIN_DOC, courseRepDoc);
            }
        }
    }
    
    private async Task CopyToClipboard(string text)
    {
        try
        {
            await JSRuntime.InvokeVoidAsync("navigator.clipboard.writeText", text);
            notificationComponent?.ShowSuccess("Copied to clipboard!");
        }
        catch (Exception ex)
        {
            notificationComponent?.ShowError($"Error copying to clipboard: {ex.Message}");
        }
    }
}