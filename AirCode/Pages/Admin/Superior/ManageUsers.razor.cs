using Microsoft.AspNetCore.Components;
using System.Text;
using System.Security.Cryptography;
using AirCode.Components.SharedPrefabs.Cards;
using AirCode.Services.Firebase;
using AirCode.Domain.ValueObjects;
using AirCode.Utilities.HelperScripts;
using AirCode.Utilities.ObjectPooling;
using Microsoft.JSInterop;

namespace AirCode.Pages.Admin.Superior;

public partial class ManageUsers : ComponentBase
{
    #region Dependency Injection
    [Inject] private IFirestoreService FirestoreService { get; set; }
    [Inject] private IJSRuntime JSRuntime { get; set; }
    #endregion

    #region Object Pooling
    private static readonly MID_ComponentObjectPool<List<StudentSkeletonUser>> StudentListPool = 
        new(() => new List<StudentSkeletonUser>(), list => list.Clear(), maxPoolSize: 10);
    
    private static readonly MID_ComponentObjectPool<List<LecturerSkeletonUser>> LecturerListPool = 
        new(() => new List<LecturerSkeletonUser>(), list => list.Clear(), maxPoolSize: 10);
    
    private static readonly MID_ComponentObjectPool<List<CourseRepSkeletonUser>> CourseRepListPool = 
        new(() => new List<CourseRepSkeletonUser>(), list => list.Clear(), maxPoolSize: 10);
    
    private static readonly MID_ComponentObjectPool<StringBuilder> StringBuilderPool = 
        new(() => new StringBuilder(), sb => sb.Clear(), maxPoolSize: 20);
    #endregion

    #region Collections & State
    private readonly Collections _collections = new();
    private readonly PaginationState _paginationState = new();
    #endregion

    #region Constants
    private const string STUDENTS_COLLECTION = "STUDENTS_MATRICULATION_NUMBERS";
    private const string ADMIN_IDS_COLLECTION = "VALID_ADMIN_IDS";
    private const string LECTURER_ADMIN_DOC = "LecturerAdminIdsDoc";
    private const string COURSEREP_ADMIN_DOC = "CourseRepAdminIdsDoc";
    private const string STUDENT_ID = "students";
    private const string COURSEREP_ID = "coursereps";
    private const string LECTURER_ID = "lecturers";
    private const int ITEMS_PER_PAGE = 10;
    #endregion

    #region UI State Variables
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
    #endregion

    #region Lifecycle Methods
    protected override async Task OnInitializedAsync()
    {
        await LoadAllUsers();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            StateHasChanged();
        }
        await base.OnAfterRenderAsync(firstRender);
    }

    protected override bool ShouldRender()
    {
        return !loading && _collections != null && 
               (_collections.Students != null || _collections.Lecturers != null || _collections.CourseReps != null);
    }
    #endregion

    #region Data Loading Methods
    private async Task LoadAllUsers()
    {
        try
        {
            loading = true;
            StateHasChanged();
        
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
                    for (int level = 100; level <= 500; level += 100)
                    {
                        var docName = $"StudentLevel{level}";
                        var levelData = await FirestoreService.GetDocumentAsync<StudentLevelDocument>(STUDENTS_COLLECTION, docName);
                        
                        if (levelData?.ValidStudentMatricNumbers != null)
                        {
                            var courseRepMatricNumbers = _collections.CourseReps.Select(cr => cr.AdminInfo.MatricNumber).ToHashSet();
                            
                            foreach (var student in levelData.ValidStudentMatricNumbers)
                            {
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
    #endregion

    #region Pagination Methods
    private IEnumerable<T> GetPagedItems<T>(List<T> items, int currentPage)
    {
        if (items == null || !items.Any())
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
    #endregion

    #region UI Event Handlers
    private void SetActiveTab(string tab)
    {
        activeTab = tab;
        StateHasChanged();
    }

    private string GetStatusClass(StudentSkeletonUser student)
    {
        return student?.IsCurrentlyInUse == true ? "user-card in-use" : "user-card";
    }

    private void OnUserTypeChanged(ChangeEventArgs e)
    {
        newUserType = e.Value?.ToString() ?? "Student";
    
        // Updated max usage logic - Course Reps now default to 1
        newMaxUsage = newUserType switch
        {
            "Lecturer" => 1,
            "CourseRep" => 1, // Changed from 2 to 1
            _ => 1
        };
    
        StateHasChanged();
    }

    private void OnUserTypeChangedWithBinding(ChangeEventArgs e)
    {
        newUserType = e.Value?.ToString() ?? "Student";
        OnUserTypeChanged(e);
    }
    #endregion

    #region Modal Management
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
    
        newMatricNumber = "";
        selectedUser = null;
        selectedUserType = null;
        selectedAssignedUserId = null;
    }

    private void ResetCreateForm()
    {
        newUserType = "Student";
        newMatricNumber = "";
        newLevel = "100";
        newMaxUsage = 1;
    }
    #endregion

    #region ID Generation
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

        var chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        var random = new Random();
        var randomString = new string(Enumerable.Repeat(chars, 16)
            .Select(s => s[random.Next(s.Length)]).ToArray());

        sb.Append("AIRCODE")
            .Append(saltString)
            .Append(randomString);

        return sb.ToString();
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
    #endregion

    #region Create Operations
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

        if (_collections.CourseReps.Any(cr => cr.AdminInfo.MatricNumber.Equals(matricNumber, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Course rep with this matriculation number already exists");

        await CreateStudentSkeleton();

        var adminId = GenerateAdminId();

        var newCourseRepAdmin = new CourseRepAdminInfo
        {
            AdminId = adminId,
            MatricNumber = matricNumber,
            CurrentUsage = 0,
            MaxUsage = newMaxUsage,
            UserIds = new List<string>()
        };

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
        
        var existingDoc = await FirestoreService.GetDocumentAsync<LecturerAdminDocument>(ADMIN_IDS_COLLECTION, LECTURER_ADMIN_DOC);
        if (existingDoc == null)
        {
            existingDoc = new LecturerAdminDocument { Ids = new List<LecturerAdminInfo>() };
        }
        
        existingDoc.Ids.Add(newLecturerAdmin);
        
        await FirestoreService.UpdateDocumentAsync(ADMIN_IDS_COLLECTION, LECTURER_ADMIN_DOC, existingDoc);
    }
    #endregion

    #region Update Operations
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
    #endregion

 #region Delete Operations (Updated Implementation)

private async Task DeleteSkeletonUser()
{
    try
    {
        loading = true;
        StateHasChanged();

        bool success = false;
        
        switch (selectedUserType)
        {
            case "student":
                success = await DeleteStudentSkeletonField(selectedUser as StudentSkeletonUser);
                break;
            case "lecturer":
                success = await DeleteLecturerSkeletonField(selectedUser as LecturerSkeletonUser);
                break;
            case "courserep":
                success = await DeleteCourseRepSkeletonField(selectedUser as CourseRepSkeletonUser);
                break;
        }

        if (success)
        {
            notificationComponent?.ShowSuccess("Skeleton user deleted successfully!");
            CloseModals();
            await LoadAllUsers();
        }
        else
        {
            notificationComponent?.ShowError("Failed to delete skeleton user. Please try again.");
        }
    }
    catch (Exception ex)
    {
        notificationComponent?.ShowError($"Error deleting skeleton user: {ex.Message}");
        Console.WriteLine($"DeleteSkeletonUser: Exception occurred: {ex.Message}");
    }
    finally
    {
        loading = false;
        StateHasChanged();
    }
}

private async Task<bool> DeleteStudentSkeletonField(StudentSkeletonUser student)
{
    if (student == null) return false;
    
    try
    {
        var documentId = $"StudentLevel{student.Level}";
        Console.WriteLine($"DeleteStudent: Starting deletion for student '{student.MatricNumber}' in document '{documentId}'");
        
        // Get the current document
        var existingDoc = await FirestoreService.GetDocumentAsync<StudentLevelDocument>(STUDENTS_COLLECTION, documentId);
        if (existingDoc?.ValidStudentMatricNumbers == null)
        {
            Console.WriteLine($"DeleteStudent: Document '{documentId}' not found or has no ValidStudentMatricNumbers");
            return false;
        }

        // Find and remove the student
        var originalCount = existingDoc.ValidStudentMatricNumbers.Count;
        existingDoc.ValidStudentMatricNumbers.RemoveAll(s => 
            s.MatricNumber.Equals(student.MatricNumber, StringComparison.OrdinalIgnoreCase));

        if (existingDoc.ValidStudentMatricNumbers.Count == originalCount)
        {
            Console.WriteLine($"DeleteStudent: Student '{student.MatricNumber}' not found in document");
            return false;
        }

        // Update the field using field-based operation
        var result = await FirestoreService.AddOrUpdateFieldAsync(
            STUDENTS_COLLECTION, 
            documentId, 
            "ValidStudentMatricNumbers", 
            existingDoc.ValidStudentMatricNumbers);
        
        Console.WriteLine($"DeleteStudent: Deletion result for '{student.MatricNumber}': {result}");
        return result;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"DeleteStudent: Exception occurred while deleting '{student?.MatricNumber}': {ex.Message}");
        return false;
    }
}

private async Task<bool> DeleteLecturerSkeletonField(LecturerSkeletonUser lecturer)
{
    if (lecturer == null) return false;
    
    try
    {
        Console.WriteLine($"DeleteLecturer: Starting deletion for lecturer '{lecturer.AdminId}'");
        
        // Get the current document
        var existingDoc = await FirestoreService.GetDocumentAsync<LecturerAdminDocument>(ADMIN_IDS_COLLECTION, LECTURER_ADMIN_DOC);
        if (existingDoc?.Ids == null)
        {
            Console.WriteLine("DeleteLecturer: Document not found or has no Ids");
            return false;
        }

        // Find and remove the lecturer
        var originalCount = existingDoc.Ids.Count;
        existingDoc.Ids.RemoveAll(l => l.AdminId == lecturer.AdminId);

        if (existingDoc.Ids.Count == originalCount)
        {
            Console.WriteLine($"DeleteLecturer: Lecturer '{lecturer.AdminId}' not found in document");
            return false;
        }

        // Update the field using field-based operation
        var result = await FirestoreService.AddOrUpdateFieldAsync(
            ADMIN_IDS_COLLECTION, 
            LECTURER_ADMIN_DOC, 
            "Ids", 
            existingDoc.Ids);
        
        Console.WriteLine($"DeleteLecturer: Deletion result for '{lecturer.AdminId}': {result}");
        return result;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"DeleteLecturer: Exception occurred while deleting '{lecturer?.AdminId}': {ex.Message}");
        return false;
    }
}

private async Task<bool> DeleteCourseRepSkeletonField(CourseRepSkeletonUser courseRep)
{
    if (courseRep == null) return false;
    
    try
    {
        Console.WriteLine($"DeleteCourseRep: Starting deletion for course rep '{courseRep.AdminInfo.AdminId}'");
        
        bool adminSuccess = false;
        bool studentSuccess = false;

        // Delete from admin collection
        var existingDoc = await FirestoreService.GetDocumentAsync<CourseRepAdminDocument>(ADMIN_IDS_COLLECTION, COURSEREP_ADMIN_DOC);
        if (existingDoc?.Ids != null)
        {
            var originalCount = existingDoc.Ids.Count;
            existingDoc.Ids.RemoveAll(cr => cr.AdminId == courseRep.AdminInfo.AdminId);

            if (existingDoc.Ids.Count < originalCount)
            {
                adminSuccess = await FirestoreService.AddOrUpdateFieldAsync(
                    ADMIN_IDS_COLLECTION, 
                    COURSEREP_ADMIN_DOC, 
                    "Ids", 
                    existingDoc.Ids);
                Console.WriteLine($"DeleteCourseRep: Admin deletion result: {adminSuccess}");
            }
        }

        // Delete associated student record
        studentSuccess = await DeleteStudentSkeletonField(courseRep.StudentInfo);

        var overallSuccess = adminSuccess && studentSuccess;
        Console.WriteLine($"DeleteCourseRep: Overall deletion result for '{courseRep.AdminInfo.AdminId}': {overallSuccess}");
        return overallSuccess;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"DeleteCourseRep: Exception occurred while deleting '{courseRep?.AdminInfo?.AdminId}': {ex.Message}");
        return false;
    }
}

#endregion

#region Remove User Operations (Updated Implementation)

private async Task RemoveAssignedUser()
{
    try
    {
        Console.WriteLine($"RemoveAssignedUser: Starting removal of user '{selectedAssignedUserId}' from '{selectedUserType}'");
        
        bool success = false;
        
        if (selectedUser is LecturerSkeletonUser lecturer)
        {
            success = await RemoveUserFromLecturer(lecturer, selectedAssignedUserId);
        }
        else if (selectedUser is CourseRepAdminInfo courseRepAdmin)
        {
            success = await RemoveUserFromCourseRep(courseRepAdmin, selectedAssignedUserId);
        }

        if (success)
        {
            notificationComponent?.ShowSuccess("User removed successfully!");
            CloseModals();
            await LoadAllUsers();
        }
        else
        {
            notificationComponent?.ShowError("Failed to remove user. Please try again.");
        }
    }
    catch (Exception ex)
    {
        notificationComponent?.ShowError($"Error removing user: {ex.Message}");
        Console.WriteLine($"RemoveAssignedUser: Exception occurred: {ex.Message}");
    }
}

private async Task<bool> RemoveUserFromLecturer(LecturerSkeletonUser lecturer, string userId)
{
    if (lecturer == null || string.IsNullOrWhiteSpace(userId)) return false;
    
    try
    {
        Console.WriteLine($"RemoveUserFromLecturer: Removing user '{userId}' from lecturer '{lecturer.AdminId}'");
        
        var lecturerDoc = await FirestoreService.GetDocumentAsync<LecturerAdminDocument>(ADMIN_IDS_COLLECTION, LECTURER_ADMIN_DOC);
        
        if (lecturerDoc?.Ids == null)
        {
            Console.WriteLine("RemoveUserFromLecturer: Document not found or has no Ids");
            return false;
        }

        var lecturerToUpdate = lecturerDoc.Ids.FirstOrDefault(l => l.AdminId == lecturer.AdminId);
        if (lecturerToUpdate == null)
        {
            Console.WriteLine($"RemoveUserFromLecturer: Lecturer '{lecturer.AdminId}' not found");
            return false;
        }

        if (!lecturerToUpdate.UserIds.Contains(userId))
        {
            Console.WriteLine($"RemoveUserFromLecturer: User '{userId}' not found in lecturer's UserIds");
            return false;
        }

        // Remove the user ID and update usage count
        lecturerToUpdate.UserIds.Remove(userId);
        lecturerToUpdate.CurrentUsage = lecturerToUpdate.UserIds.Count;

        // Update the field using field-based operation
        var result = await FirestoreService.AddOrUpdateFieldAsync(
            ADMIN_IDS_COLLECTION, 
            LECTURER_ADMIN_DOC, 
            "Ids", 
            lecturerDoc.Ids);
        
        Console.WriteLine($"RemoveUserFromLecturer: Removal result for user '{userId}': {result}");
        return result;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"RemoveUserFromLecturer: Exception occurred: {ex.Message}");
        return false;
    }
}

private async Task<bool> RemoveUserFromCourseRep(CourseRepAdminInfo courseRepAdmin, string userId)
{
    if (courseRepAdmin == null || string.IsNullOrWhiteSpace(userId)) return false;
    
    try
    {
        Console.WriteLine($"RemoveUserFromCourseRep: Removing user '{userId}' from course rep '{courseRepAdmin.AdminId}'");
        
        var courseRepDoc = await FirestoreService.GetDocumentAsync<CourseRepAdminDocument>(ADMIN_IDS_COLLECTION, COURSEREP_ADMIN_DOC);
        
        if (courseRepDoc?.Ids == null)
        {
            Console.WriteLine("RemoveUserFromCourseRep: Document not found or has no Ids");
            return false;
        }

        var courseRepToUpdate = courseRepDoc.Ids.FirstOrDefault(cr => cr.AdminId == courseRepAdmin.AdminId);
        if (courseRepToUpdate == null)
        {
            Console.WriteLine($"RemoveUserFromCourseRep: Course rep '{courseRepAdmin.AdminId}' not found");
            return false;
        }

        if (!courseRepToUpdate.UserIds.Contains(userId))
        {
            Console.WriteLine($"RemoveUserFromCourseRep: User '{userId}' not found in course rep's UserIds");
            return false;
        }

        // Remove the user ID and update usage count
        courseRepToUpdate.UserIds.Remove(userId);
        courseRepToUpdate.CurrentUsage = courseRepToUpdate.UserIds.Count;

        // Update the field using field-based operation
        var result = await FirestoreService.AddOrUpdateFieldAsync(
            ADMIN_IDS_COLLECTION, 
            COURSEREP_ADMIN_DOC, 
            "Ids", 
            courseRepDoc.Ids);
        
        Console.WriteLine($"RemoveUserFromCourseRep: Removal result for user '{userId}': {result}");
        return result;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"RemoveUserFromCourseRep: Exception occurred: {ex.Message}");
        return false;
    }
}

#endregion
    
    
    #region Utility Methods
    private async Task CopyToClipboard(string text)
    {
        try
        {
            await JSRuntime.InvokeVoidAsync("navigator.clipboard.writeText", text);
            await ShowNotification("Copied to clipboard!");
        }
        catch (Exception ex)
        {
            await ShowNotification($"Error copying to clipboard: {ex.Message}", false);
        }
    }

    private async Task ShowNotification(string message, bool isSuccess = true)
    {
        if (notificationComponent != null)
        {
            if (isSuccess)
                notificationComponent.ShowSuccess(message);
            else
                notificationComponent.ShowError(message);
        
            await InvokeAsync(StateHasChanged);
        }
    }
    #endregion

    #region Disposal
    public void Dispose()
    {
        StudentListPool?.Dispose();
        LecturerListPool?.Dispose();
        CourseRepListPool?.Dispose();
        StringBuilderPool?.Dispose();
    }
    #endregion

    #region Inner Classes
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
    #endregion
}
