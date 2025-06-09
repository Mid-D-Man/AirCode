using Microsoft.AspNetCore.Components;
using System.Text;
using System.Security.Cryptography;
using AirCode.Components.SharedPrefabs.Cards;
using AirCode.Services.Firebase;
using AirCode.Domain.Enums;

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
    
    private const string STUDENT_ID = "students";
    private const string COURSEREP_ID = "coursereps";
    private const string LECTURER_ID = "lecturers";

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
    private void OnUserTypeChangedWithBinding(ChangeEventArgs e)
    {
        newUserType = e.Value?.ToString() ?? "Student";
        OnUserTypeChanged(e); // Call existing logic
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
        if (string.IsNullOrWhiteSpace(newMatricNumber))
            throw new ArgumentException("Matriculation number is required");
            
        var matricNumber = newMatricNumber.ToUpper().Replace(" ", "").Replace("_", "");
        
        // Check if course rep already exists
        if (courseReps.Any(cr => cr.AdminInfo.MatricNumber.Equals(matricNumber, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Course rep with this matriculation number already exists");
        
        // Create student entry first
        await CreateStudentSkeleton();
        
        // Create admin entry
        var adminId = GenerateAdminId("CourseRep");
        
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
            await Task.Run(() => {
                // For Blazor WebAssembly, you might need to use JavaScript interop
                // This is a placeholder implementation
            });
            notificationComponent?.ShowSuccess("Copied to clipboard!");
        }
        catch (Exception ex)
        {
            notificationComponent?.ShowError($"Error copying to clipboard: {ex.Message}");
        }
    }
    
    // Helper methods for generating IDs
    private string GenerateAdminId(string type)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var randomBytes = new byte[4];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(randomBytes);
        }
        var randomString = Convert.ToHexString(randomBytes).ToLower();
        return $"{type.ToLower()}_{timestamp}_{randomString}";
    }
    
    private string GenerateLecturerId()
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var randomBytes = new byte[3];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(randomBytes);
        }
        var randomString = Convert.ToHexString(randomBytes).ToLower();
        return $"LEC_{timestamp}_{randomString}";
    }
}

// Domain Models
public class StudentSkeletonUser
{
    public string CurrentUserId { get; set; } = "";
    public bool IsCurrentlyInUse { get; set; } = false;
    public string Level { get; set; } = "";
    public string MatricNumber { get; set; } = "";
}

public class LecturerSkeletonUser
{
    public string AdminId { get; set; } = "";
    public string LecturerId { get; set; } = "";
    public int CurrentUsage { get; set; } = 0;
    public int MaxUsage { get; set; } = 1;
    public List<string> UserIds { get; set; } = new();
}

public class CourseRepSkeletonUser
{
    public CourseRepAdminInfo AdminInfo { get; set; } = new();
    public StudentSkeletonUser StudentInfo { get; set; } = new();
}

public class CourseRepAdminInfo
{
    public string AdminId { get; set; } = "";
    public string MatricNumber { get; set; } = "";
    public int CurrentUsage { get; set; } = 0;
    public int MaxUsage { get; set; } = 2;
    public List<string> UserIds { get; set; } = new();
}

// Firebase Document Models
public class StudentLevelDocument
{
    public List<StudentSkeletonUser> ValidStudentMatricNumbers { get; set; } = new();
}

public class LecturerAdminDocument
{
    public List<LecturerAdminInfo> Ids { get; set; } = new();
}

public class LecturerAdminInfo
{
    public string AdminId { get; set; } = "";
    public string LecturerId { get; set; } = "";
    public int CurrentUsage { get; set; } = 0;
    public int MaxUsage { get; set; } = 1;
    public List<string> UserIds { get; set; } = new();
}

public class CourseRepAdminDocument
{
    public List<CourseRepAdminInfo> Ids { get; set; } = new();
}