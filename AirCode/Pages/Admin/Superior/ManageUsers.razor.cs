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
using AirCode.Utilities.ObjectPooling;
using Microsoft.JSInterop;

namespace AirCode.Pages.Admin.Superior;

public partial class ManageUsers : ComponentBase
{
    [Inject] private IFirestoreService FirestoreService { get; set; }
    [Inject] private IJSRuntime JSRuntime { get; set; }
    // Object Pools
    private static readonly MID_ComponentObjectPool<List<StudentSkeletonUser>> StudentListPool = 
        new(() => new List<StudentSkeletonUser>(), list => list.Clear(), maxPoolSize: 10);
    
    private static readonly MID_ComponentObjectPool<List<LecturerSkeletonUser>> LecturerListPool = 
        new(() => new List<LecturerSkeletonUser>(), list => list.Clear(), maxPoolSize: 10);
    
    private static readonly MID_ComponentObjectPool<List<CourseRepSkeletonUser>> CourseRepListPool = 
        new(() => new List<CourseRepSkeletonUser>(), list => list.Clear(), maxPoolSize: 10);
    
    private static readonly MID_ComponentObjectPool<StringBuilder> StringBuilderPool = 
        new(() => new StringBuilder(), sb => sb.Clear(), maxPoolSize: 20);
    
    // Collections - now using pooled objects
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
    
    
    protected override async Task OnInitializedAsync()
    {
        await LoadAllUsers();
    }
    
    private async Task LoadAllUsers()
    {
        try
        {
            loading = true;
            StateHasChanged(); // Immediate UI feedback
        
            // Sequential loading to prevent DOM conflicts
            await LoadStudentsPooled().Task;
            await LoadLecturersPooled().Task;
            await LoadCourseRepsPooled().Task;
        
            _paginationState.ResetAllPages();
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

    
    private PooledTaskWrapper<List<StudentSkeletonUser>> LoadStudentsPooled()
    {
        return new PooledTaskWrapper<List<StudentSkeletonUser>>(
            StudentListPool,
            async (pooledList) =>
            {
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
                                    pooledList.Add(student);
                                }
                            }
                        }
                    }
                    
                    _collections.Students.Clear();
                    _collections.Students.AddRange(pooledList);
                }
                catch (Exception ex)
                {
                    notificationComponent?.ShowError($"Error loading students: {ex.Message}");
                }
                
                return pooledList;
            }
        );
    }
    private PooledTaskWrapper<List<LecturerSkeletonUser>> LoadLecturersPooled()
    {
        return new PooledTaskWrapper<List<LecturerSkeletonUser>>(
            LecturerListPool,
            async (pooledList) =>
            {
                try
                {
                    var lecturerDoc = await FirestoreService.GetDocumentAsync<LecturerAdminDocument>(ADMIN_IDS_COLLECTION, LECTURER_ADMIN_DOC);
                    if (lecturerDoc?.Ids != null)
                    {
                        foreach (var adminData in lecturerDoc.Ids)
                        {
                            pooledList.Add(new LecturerSkeletonUser
                            {
                                AdminId = adminData.AdminId,
                                LecturerId = adminData.LecturerId,
                                CurrentUsage = adminData.CurrentUsage,
                                MaxUsage = adminData.MaxUsage,
                                UserIds = adminData.UserIds
                            });
                        }
                    }
                    
                    _collections.Lecturers.Clear();
                    _collections.Lecturers.AddRange(pooledList);
                }
                catch (Exception ex)
                {
                    notificationComponent?.ShowError($"Error loading lecturers: {ex.Message}");
                }
                
                return pooledList;
            }
        );
    }

    
   private PooledTaskWrapper<List<CourseRepSkeletonUser>> LoadCourseRepsPooled()
    {
        return new PooledTaskWrapper<List<CourseRepSkeletonUser>>(
            CourseRepListPool,
            async (pooledList) =>
            {
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
                                pooledList.Add(new CourseRepSkeletonUser
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
                    
                    _collections.CourseReps.Clear();
                    _collections.CourseReps.AddRange(pooledList);
                }
                catch (Exception ex)
                {
                    notificationComponent?.ShowError($"Error loading course reps: {ex.Message}");
                }
                
                return pooledList;
            }
        );
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
        var totalPages = GetTotalPages(items.Count);
        var safePage = Math.Max(1, Math.Min(currentPage, Math.Max(1, totalPages)));
    
        return items
            .Skip((safePage - 1) * ITEMS_PER_PAGE)
            .Take(ITEMS_PER_PAGE);
    }
    
    private void NavigateToPage(string tabType, int page)
    {
        switch (tabType)
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
    
    private int GetTotalPages(int itemCount) => (int)Math.Ceiling(itemCount / (double)ITEMS_PER_PAGE);
    
    // Generate ID methods using pooled StringBuilder
    private string GenerateAdminId()
    {
        using var sbWrapper = StringBuilderPool.GetPooled();
        var sb = sbWrapper.Object;

        var saltBytes = new byte[2];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(saltBytes);
        }
        var saltString = Convert.ToHexString(saltBytes).ToUpper();

        var randomBytes = new byte[12];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(randomBytes);
        }
    
        // Fix: Use URL-safe base64 encoding for HTML attribute compatibility
        var base64String = Convert.ToBase64String(randomBytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .Replace("=", "");

        sb.Append("AIRCODE-")
            .Append(saltString)
            .Append('-')
            .Append(base64String);
      
        return sb.ToString();
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
    
    protected override bool ShouldRender()
    {
        // Enhanced rendering control
        return !loading && _collections != null && 
               (_collections.Students != null || _collections.Lecturers != null || _collections.CourseReps != null);
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

        // Critical: Ensure state consistency before re-render
        InvokeAsync(() => {
            StateHasChanged();
            return Task.CompletedTask;
        });
    }
    // Replace both methods with single implementation
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
            loading = true;
            StateHasChanged(); // Ensure UI reflects loading state
        
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

    // Create admin entry with updated ID generation
    var adminId = GenerateAdminId(); // Updated call

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
    var adminId = GenerateAdminId(); // Updated call
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
        //remember update document dosent work for some reason
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
    private class Collections
    {
        public List<StudentSkeletonUser> Students { get; } = new();
        public List<LecturerSkeletonUser> Lecturers { get; } = new();
        public List<CourseRepSkeletonUser> CourseReps { get; } = new();
    }
    
    private class PaginationState
    {
        public int StudentsCurrentPage { get; set; } = 1;
        public int LecturersCurrentPage { get; set; } = 1;
        public int CourseRepsCurrentPage { get; set; } = 1;
        
        public void ResetAllPages()
        {
            StudentsCurrentPage = 1;
            LecturersCurrentPage = 1;
            CourseRepsCurrentPage = 1;
        }
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
// Pooled Task Wrapper for async operations
public class PooledTaskWrapper<T> : IDisposable where T : class
{
    private readonly MID_ComponentObjectPool<T> _pool;
    private readonly T _pooledObject;
    public Task<T> Task { get; }
    
    public PooledTaskWrapper(MID_ComponentObjectPool<T> pool, Func<T, Task<T>> asyncOperation)
    {
        _pool = pool;
        _pooledObject = pool.Get();
        Task = asyncOperation(_pooledObject);
    }
    
    public void Dispose()
    {
        _pool.Return(_pooledObject);
    }
}
