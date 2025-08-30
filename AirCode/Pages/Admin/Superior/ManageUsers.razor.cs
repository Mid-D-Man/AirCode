using Microsoft.AspNetCore.Components;
using System.Text;
using System.Security.Cryptography;
using AirCode.Components.SharedPrefabs.Cards;
using AirCode.Services.Firebase;
using AirCode.Domain.ValueObjects;
using AirCode.Models.EdgeFunction;
using AirCode.Services.SupaBase;
using AirCode.Utilities.HelperScripts;
using AirCode.Utilities.ObjectPooling;
using AirCode.Services.VisualElements;
using Microsoft.JSInterop;
using AirCode.Models.Supabase;

namespace AirCode.Pages.Admin.Superior;

public partial class ManageUsers : ComponentBase
{
    #region Dependency Injection
    [Inject] private IFirestoreService FirestoreService { get; set; }
    [Inject] private ISvgIconService SvgIconService { get; set; }
    [Inject] private IJSRuntime JSRuntime { get; set; }
    
    [Inject] private ISupabaseEdgeFunctionService SupabaseEdgefunctionService { get; set; }
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
    private string settingsIcon = "";
    
    // Modal states
    private bool showCreateModal = false;
    private bool showMaxUsageModal = false;
    private bool showDeleteModal = false;
    private bool showRemoveUserModal = false;
    private bool showUserManagementModal = false;
    
    // User Management Modal sections
    private bool showRemoveUserSection = false;
    private bool showSuspendUserSection = false;
    
    // Form fields
    private string newUserType = "Student";
    private string newMatricNumber = "";
    private string newLevel = "100";
    private int newMaxUsage = 1;
    private int updateMaxUsage = 1;
    
    // Suspension fields
    private string suspensionReason = "";
    private int suspensionDurationHours = 24;
    
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
        await LoadSettingsIcon();
        await LoadAllUsers();
    }

    private async Task LoadSettingsIcon()
    {
        try
        {
            settingsIcon = await SvgIconService.GetSettingsIconAsync();
        }
        catch
        {
            settingsIcon = "⚙️"; // Fallback emoji
        }
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

    #region Status Helper Methods
    private string GetUserStatusClass(bool inUse, bool suspended)
    {
        if (suspended) return "text-danger";
        return inUse ? "text-success" : "text-secondary";
    }

    private string GetUserStatusText(bool inUse, bool suspended)
    {
        if (suspended) return "Suspended";
        return inUse ? "In Use" : "Available";
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
                                    student.Suspension.UpdateSuspensionStatus();
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
                            adminData.Suspension.UpdateSuspensionStatus();
                            pooledList.Add(new LecturerSkeletonUser
                            {
                                AdminId = adminData.AdminId,
                                LecturerId = adminData.LecturerId,
                                CurrentUsage = adminData.CurrentUsage,
                                MaxUsage = adminData.MaxUsage,
                                UserIds = adminData.UserIds,
                                Suspension = adminData.Suspension
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
                            adminData.Suspension.UpdateSuspensionStatus();
                            var studentInfo = await FindStudentByMatricNumberAsync(adminData.MatricNumber);
                            if (studentInfo != null)
                            {
                                pooledList.Add(new CourseRepSkeletonUser
                                {
                                    AdminInfo = adminData,
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
        var baseClass = "user-card";
        if (student?.IsCurrentlyInUse == true) baseClass += " in-use";
        if (student?.Suspension?.IsSuspended == true) baseClass += " suspended";
        return baseClass;
    }

    private void OnUserTypeChanged(ChangeEventArgs e)
    {
        newUserType = e.Value?.ToString() ?? "Student";
    
        newMaxUsage = newUserType switch
        {
            "Lecturer" => 1,
            "CourseRep" => 1,
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

    #region User Management Modal Methods
    private void ShowUserManagementModal(object user, string userType)
    {
        selectedUser = user;
        selectedUserType = userType;
        showUserManagementModal = true;
        HideAllSections();
    }

    private bool HasAssignedUsers()
    {
        return selectedUser switch
        {
            StudentSkeletonUser student => !string.IsNullOrEmpty(student.CurrentUserId),
            LecturerSkeletonUser lecturer => lecturer.UserIds.Any(),
            CourseRepSkeletonUser courseRep => !string.IsNullOrEmpty(courseRep.StudentInfo.CurrentUserId) || 
                                               courseRep.AdminInfo.UserIds.Any(),
            _ => false
        };
    }

    private bool IsUserSuspended()
    {
        return selectedUser switch
        {
            StudentSkeletonUser student => student.Suspension.IsSuspended,
            LecturerSkeletonUser lecturer => lecturer.Suspension.IsSuspended,
            CourseRepSkeletonUser courseRep => courseRep.AdminInfo.Suspension.IsSuspended,
            _ => false
        };
    }

    private bool HasAuth0UserId()
    {
        return selectedUser switch
        {
            StudentSkeletonUser student => !string.IsNullOrEmpty(student.CurrentUserId),
            LecturerSkeletonUser lecturer => lecturer.UserIds.Any(),
            CourseRepSkeletonUser courseRep => !string.IsNullOrEmpty(courseRep.StudentInfo.CurrentUserId) || 
                                               courseRep.AdminInfo.UserIds.Any(),
            _ => false
        };
    }

    private void ShowRemoveCurrentUserSection()
    {
        HideAllSections();
        showRemoveUserSection = true;
    }

    private void ShowSuspendUserSection()
    {
        HideAllSections();
        showSuspendUserSection = true;
        
        // Pre-fill current suspension data if exists
        if (IsUserSuspended())
        {
            var activeSuspension = selectedUser switch
            {
                StudentSkeletonUser student => student.Suspension.GetActiveSuspension(),
                LecturerSkeletonUser lecturer => lecturer.Suspension.GetActiveSuspension(),
                CourseRepSkeletonUser courseRep => courseRep.AdminInfo.Suspension.GetActiveSuspension(),
                _ => null
            };
            
            if (activeSuspension != null)
            {
                suspensionReason = activeSuspension.Reason;
                suspensionDurationHours = (int)activeSuspension.Duration.TotalHours;
            }
        }
    }

    private void HideAllSections()
    {
        showRemoveUserSection = false;
        showSuspendUserSection = false;
    }

    private async Task RemoveCurrentUser()
    {
        try
        {
            var result = await RemoveUserFromSelected();
            
            if (result.Success)
            {
                notificationComponent?.ShowSuccess("User removed successfully!");
                CloseModals();
                await LoadAllUsers();
            }
            else
            {
                notificationComponent?.ShowError($"Failed to remove user: {result.ErrorMessage}");
            }
        }
        catch (Exception ex)
        {
            notificationComponent?.ShowError($"Error removing user: {ex.Message}");
        }
    }

    private async Task SuspendUser()
    {
        if (string.IsNullOrWhiteSpace(suspensionReason) || suspensionDurationHours <= 0)
        {
            notificationComponent?.ShowError("Please provide a valid reason and duration.");
            return;
        }

        try
        {
            var violation = new UserViolation
            {
                Reason = suspensionReason.Trim(),
                Duration = TimeSpan.FromHours(suspensionDurationHours),
                IssuedBy = "Admin" // TODO: Get current user ID
            };

            var result = await ApplySuspension(violation);
            
            if (result.Success)
            {
                notificationComponent?.ShowSuccess("User suspension applied successfully!");
                CloseModals();
                await LoadAllUsers();
            }
            else
            {
                notificationComponent?.ShowError($"Failed to suspend user: {result.ErrorMessage}");
            }
        }
        catch (Exception ex)
        {
            notificationComponent?.ShowError($"Error suspending user: {ex.Message}");
        }
    }

    private async Task<OperationResult> RemoveUserFromSelected()
    {
        return selectedUser switch
        {
            StudentSkeletonUser student when !string.IsNullOrEmpty(student.CurrentUserId) 
                => await RemoveUserFromStudent(student, student.CurrentUserId),
            LecturerSkeletonUser lecturer when lecturer.UserIds.Any() 
                => await RemoveUserFromLecturer(lecturer, lecturer.UserIds.First()),
            CourseRepSkeletonUser courseRep when !string.IsNullOrEmpty(courseRep.StudentInfo.CurrentUserId)
                => await RemoveUserFromStudent(courseRep.StudentInfo, courseRep.StudentInfo.CurrentUserId),
            CourseRepSkeletonUser courseRep when courseRep.AdminInfo.UserIds.Any()
                => await RemoveUserFromCourseRep(courseRep.AdminInfo, courseRep.AdminInfo.UserIds.First()),
            _ => new OperationResult { Success = false, ErrorMessage = "No users to remove" }
        };
    }

    private async Task<OperationResult> ApplySuspension(UserViolation violation)
    {
        return selectedUser switch
        {
            StudentSkeletonUser student => await SuspendStudent(student, violation),
            LecturerSkeletonUser lecturer => await SuspendLecturer(lecturer, violation),
            CourseRepSkeletonUser courseRep => await SuspendCourseRep(courseRep.AdminInfo, violation),
            _ => new OperationResult { Success = false, ErrorMessage = "Invalid user type for suspension" }
        };
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

    private void CloseModals()
    {
        showCreateModal = false;
        showMaxUsageModal = false;
        showDeleteModal = false;
        showRemoveUserModal = false;
        showUserManagementModal = false;
    
        HideAllSections();
        newMatricNumber = "";
        suspensionReason = "";
        suspensionDurationHours = 24;
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
    
        var base64String = Convert.ToBase64String(randomBytes)
            .Replace("+", "")
            .Replace("/", "")
            .Replace("=", "")
            .PadRight(16, '0');

        sb.Append("AIRCODE_")
            .Append(saltString)
            .Append("_")
            .Append(base64String);

        return sb.ToString();
    }

    private string GenerateLecturerId()
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var randomBytes = MID_HelperFunctions.GenerateRandomString(3);
        var randomString = randomBytes.ToLower();
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
            MatricNumber = matricNumber,
            Suspension = new UserSuspension()
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
            UserIds = new List<string>(),
            Suspension = new UserSuspension()
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
            UserIds = new List<string>(),
            Suspension = new UserSuspension()
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

    #region Suspension Operations
    private async Task<OperationResult> SuspendStudent(StudentSkeletonUser student, UserViolation violation)
    {
        try
        {
            var docName = $"StudentLevel{student.Level}";
            var levelDoc = await FirestoreService.GetDocumentAsync<StudentLevelDocument>(STUDENTS_COLLECTION, docName);
            
            if (levelDoc?.ValidStudentMatricNumbers == null)
                return new OperationResult { Success = false, ErrorMessage = "Student document not found" };

            var studentToUpdate = levelDoc.ValidStudentMatricNumbers
                .FirstOrDefault(s => s.MatricNumber.Equals(student.MatricNumber, StringComparison.OrdinalIgnoreCase));
            
            if (studentToUpdate == null)
                return new OperationResult { Success = false, ErrorMessage = "Student not found" };

            studentToUpdate.Suspension.AddViolation(violation);
            
            var updateSuccess = await FirestoreService.UpdateDocumentAsync(STUDENTS_COLLECTION, docName, levelDoc);
            return new OperationResult { Success = updateSuccess };
        }
        catch (Exception ex)
        {
            return new OperationResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    private async Task<OperationResult> SuspendLecturer(LecturerSkeletonUser lecturer, UserViolation violation)
    {
        try
        {
            var lecturerDoc = await FirestoreService.GetDocumentAsync<LecturerAdminDocument>(ADMIN_IDS_COLLECTION, LECTURER_ADMIN_DOC);
            if (lecturerDoc?.Ids == null)
                return new OperationResult { Success = false, ErrorMessage = "Lecturer document not found" };

            var lecturerToUpdate = lecturerDoc.Ids.FirstOrDefault(l => l.AdminId == lecturer.AdminId);
            if (lecturerToUpdate == null)
                return new OperationResult { Success = false, ErrorMessage = "Lecturer not found" };

            lecturerToUpdate.Suspension.AddViolation(violation);
            
            var updateSuccess = await FirestoreService.UpdateDocumentAsync(ADMIN_IDS_COLLECTION, LECTURER_ADMIN_DOC, lecturerDoc);
            return new OperationResult { Success = updateSuccess };
        }
        catch (Exception ex)
        {
            return new OperationResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    private async Task<OperationResult> SuspendCourseRep(CourseRepAdminInfo courseRepAdmin, UserViolation violation)
    {
        try
        {
            var courseRepDoc = await FirestoreService.GetDocumentAsync<CourseRepAdminDocument>(ADMIN_IDS_COLLECTION, COURSEREP_ADMIN_DOC);
            if (courseRepDoc?.Ids == null)
                return new OperationResult { Success = false, ErrorMessage = "Course rep document not found" };

            var courseRepToUpdate = courseRepDoc.Ids.FirstOrDefault(cr => cr.AdminId == courseRepAdmin.AdminId);
            if (courseRepToUpdate == null)
                return new OperationResult { Success = false, ErrorMessage = "Course rep not found" };

            courseRepToUpdate.Suspension.AddViolation(violation);
            
            var updateSuccess = await FirestoreService.UpdateDocumentAsync(ADMIN_IDS_COLLECTION, COURSEREP_ADMIN_DOC, courseRepDoc);
            return new OperationResult { Success = updateSuccess };
        }
        catch (Exception ex)
        {
            return new OperationResult { Success = false, ErrorMessage = ex.Message };
        }
    }
    #endregion
    
   #region Delete Operations with Auth0 Integration

    private async Task DeleteSkeletonUser()
    {
        if (selectedUser == null || string.IsNullOrEmpty(selectedUserType))
        {
            notificationComponent?.ShowError("Invalid user selection.");
            return;
        }

        try
        {
            loading = true;
            StateHasChanged();

            // First, handle Auth0 user deletion if there are assigned users
            if (HasAuth0UserId())
            {
                var auth0DeletionResult = await DeleteAuth0UsersForSkeleton();
                if (!auth0DeletionResult.Success)
                {
                    notificationComponent?.ShowError($"Failed to delete Auth0 users: {auth0DeletionResult.ErrorMessage}. Skeleton user deletion cancelled.");
                    return;
                }
            }

            // Then delete the skeleton user from Firestore
            var result = selectedUserType switch
            {
                STUDENT_ID => await DeleteStudentSkeleton(selectedUser as StudentSkeletonUser),
                LECTURER_ID => await DeleteLecturerSkeleton(selectedUser as LecturerSkeletonUser),
                COURSEREP_ID => await DeleteCourseRepSkeleton(selectedUser as CourseRepSkeletonUser),
                _ => new OperationResult { Success = false, ErrorMessage = $"Unknown user type '{selectedUserType}'" }
            };

            if (result.Success)
            {
                notificationComponent?.ShowSuccess("Skeleton user and associated Auth0 accounts deleted successfully!");
                CloseModals();
                await LoadAllUsers();
            }
            else
            {
                notificationComponent?.ShowError($"Failed to delete skeleton user: {result.ErrorMessage}");
            }
        }
        catch (Exception ex)
        {
            notificationComponent?.ShowError($"Error deleting skeleton user: {ex.Message}");
        }
        finally
        {
            loading = false;
            StateHasChanged();
        }
    }

    private async Task<OperationResult> DeleteAuth0UsersForSkeleton()
    {
        try
        {
            var userIdsToDelete = new List<string>();

            // Collect all Auth0 user IDs that need to be deleted
            switch (selectedUser)
            {
                case StudentSkeletonUser student when !string.IsNullOrEmpty(student.CurrentUserId):
                    userIdsToDelete.Add(student.CurrentUserId);
                    break;
                    
                case LecturerSkeletonUser lecturer:
                    userIdsToDelete.AddRange(lecturer.UserIds.Where(id => !string.IsNullOrEmpty(id)));
                    break;
                    
                case CourseRepSkeletonUser courseRep:
                    if (!string.IsNullOrEmpty(courseRep.StudentInfo.CurrentUserId))
                        userIdsToDelete.Add(courseRep.StudentInfo.CurrentUserId);
                    userIdsToDelete.AddRange(courseRep.AdminInfo.UserIds.Where(id => !string.IsNullOrEmpty(id)));
                    break;
            }

            // Delete each Auth0 user
            var allSuccessful = true;
            var errorMessages = new List<string>();

            foreach (var userId in userIdsToDelete)
            {
                try
                {
                    var deleteRequest = new DeleteUserRequest
                    {
                        Auth0UserId = userId,
                        UserEmail = null // We're using Auth0 user ID
                    };

                    var deleteResult = await SupabaseEdgefunctionService.DeleteUserAsync(deleteRequest);
                    
                    if (!deleteResult.Success)
                    {
                        allSuccessful = false;
                        errorMessages.Add($"Failed to delete user {userId}: {deleteResult.Message}");
                        MID_HelperFunctions.DebugMessage($"Auth0 deletion failed for user {userId}: {deleteResult.Message}");
                    }
                    else
                    {
                        MID_HelperFunctions.DebugMessage($"Successfully deleted Auth0 user: {userId}");
                    }
                }
                catch (Exception ex)
                {
                    allSuccessful = false;
                    errorMessages.Add($"Exception deleting user {userId}: {ex.Message}");
                    MID_HelperFunctions.DebugMessage($"Exception during Auth0 deletion for user {userId}: {ex}");
                }
            }

            return new OperationResult
            {
                Success = allSuccessful,
                ErrorMessage = allSuccessful ? null : string.Join("; ", errorMessages)
            };
        }
        catch (Exception ex)
        {
            return new OperationResult
            {
                Success = false,
                ErrorMessage = $"Error during Auth0 user deletion: {ex.Message}"
            };
        }
    }

    private async Task<OperationResult> DeleteStudentSkeleton(StudentSkeletonUser student)
    {
        if (student?.MatricNumber == null || student?.Level == null)
        {
            return new OperationResult { Success = false, ErrorMessage = "Invalid student data" };
        }

        try
        {
            var docName = $"StudentLevel{student.Level}";
            var levelDoc = await FirestoreService.GetDocumentAsync<StudentLevelDocument>(STUDENTS_COLLECTION, docName);
            
            if (levelDoc?.ValidStudentMatricNumbers == null)
            {
                return new OperationResult { Success = false, ErrorMessage = $"Level document for {student.Level} not found" };
            }

            var initialCount = levelDoc.ValidStudentMatricNumbers.Count;
            levelDoc.ValidStudentMatricNumbers.RemoveAll(s => 
                string.Equals(s.MatricNumber, student.MatricNumber, StringComparison.OrdinalIgnoreCase));
            
            if (levelDoc.ValidStudentMatricNumbers.Count >= initialCount)
            {
                return new OperationResult { Success = false, ErrorMessage = $"Student '{student.MatricNumber}' not found in level {student.Level}" };
            }

            var updateSuccess = await FirestoreService.UpdateDocumentAsync(STUDENTS_COLLECTION, docName, levelDoc);
            
            return new OperationResult 
            { 
                Success = updateSuccess, 
                ErrorMessage = updateSuccess ? null : "Failed to update Firestore document" 
            };
        }
        catch (Exception ex)
        {
            return new OperationResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    private async Task<OperationResult> DeleteCourseRepSkeleton(CourseRepSkeletonUser courseRep)
    {
        if (courseRep?.AdminInfo?.AdminId == null || courseRep.StudentInfo == null)
        {
            return new OperationResult { Success = false, ErrorMessage = "Invalid course rep data" };
        }

        try
        {
            // Delete admin record
            var adminResult = await DeleteCourseRepAdmin(courseRep.AdminInfo.AdminId);
            
            // Delete associated student record
            var studentResult = await DeleteStudentSkeleton(courseRep.StudentInfo);

            if (!adminResult.Success || !studentResult.Success)
            {
                var errors = new List<string>();
                if (!adminResult.Success) errors.Add($"Admin deletion: {adminResult.ErrorMessage}");
                if (!studentResult.Success) errors.Add($"Student deletion: {studentResult.ErrorMessage}");
                
                return new OperationResult 
                { 
                    Success = false, 
                    ErrorMessage = string.Join("; ", errors) 
                };
            }

            return new OperationResult { Success = true };
        }
        catch (Exception ex)
        {
            return new OperationResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    private async Task<OperationResult> DeleteCourseRepAdmin(string adminId)
    {
        try
        {
            var existingDoc = await FirestoreService.GetDocumentAsync<CourseRepAdminDocument>(ADMIN_IDS_COLLECTION, COURSEREP_ADMIN_DOC);
            
            if (existingDoc?.Ids == null)
            {
                return new OperationResult { Success = false, ErrorMessage = "Course rep admin document not found" };
            }

            if (!existingDoc.Ids.Any(cr => cr.AdminId == adminId))
            {
                return new OperationResult { Success = true }; // Not found is considered success for deletion
            }

            var updatedList = existingDoc.Ids.Where(cr => cr.AdminId != adminId).ToList();
            var updateSuccess = await FirestoreService.AddOrUpdateFieldAsync(ADMIN_IDS_COLLECTION, COURSEREP_ADMIN_DOC, "Ids", updatedList);

            return new OperationResult 
            { 
                Success = updateSuccess, 
                ErrorMessage = updateSuccess ? null : "Failed to update admin document" 
            };
        }
        catch (Exception ex)
        {
            return new OperationResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    private async Task<OperationResult> DeleteLecturerSkeleton(LecturerSkeletonUser lecturer)
    {
        if (string.IsNullOrEmpty(lecturer?.AdminId))
        {
            return new OperationResult { Success = false, ErrorMessage = "Invalid lecturer data" };
        }

        try
        {
            var existingDoc = await FirestoreService.GetDocumentAsync<LecturerAdminDocument>(ADMIN_IDS_COLLECTION, LECTURER_ADMIN_DOC);
            
            if (existingDoc?.Ids == null)
            {
                return new OperationResult { Success = false, ErrorMessage = "Lecturer document not found" };
            }

            if (!existingDoc.Ids.Any(l => l.AdminId == lecturer.AdminId))
            {
                return new OperationResult { Success = false, ErrorMessage = $"Lecturer '{lecturer.AdminId}' not found" };
            }

            var updatedList = existingDoc.Ids.Where(l => l.AdminId != lecturer.AdminId).ToList();
            var updateSuccess = await FirestoreService.AddOrUpdateFieldAsync(ADMIN_IDS_COLLECTION, LECTURER_ADMIN_DOC, "Ids", updatedList);
            
            return new OperationResult 
            { 
                Success = updateSuccess, 
                ErrorMessage = updateSuccess ? null : "Failed to update lecturer document" 
            };
        }
        catch (Exception ex)
        {
            return new OperationResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    #endregion

    #region Remove User Operations

    private async Task RemoveAssignedUser()
    {
        if (selectedUser == null || string.IsNullOrEmpty(selectedAssignedUserId))
        {
            notificationComponent?.ShowError("Invalid selection for user removal.");
            return;
        }

        try
        {
            var result = selectedUserType switch
            {
                LECTURER_ID when selectedUser is LecturerSkeletonUser lecturer => await RemoveUserFromLecturer(lecturer, selectedAssignedUserId),
                COURSEREP_ID when selectedUser is CourseRepAdminInfo courseRepAdmin => await RemoveUserFromCourseRep(courseRepAdmin, selectedAssignedUserId),
                STUDENT_ID when selectedUser is StudentSkeletonUser student => await RemoveUserFromStudent(student, selectedAssignedUserId),
                _ => new OperationResult { Success = false, ErrorMessage = $"Unsupported user type '{selectedUserType}' for removal" }
            };

            if (result.Success)
            {
                notificationComponent?.ShowSuccess("User removed successfully!");
                CloseModals();
                await LoadAllUsers();
            }
            else
            {
                notificationComponent?.ShowError($"Failed to remove user: {result.ErrorMessage}");
            }
        }
        catch (Exception ex)
        {
            notificationComponent?.ShowError($"Error removing user: {ex.Message}");
        }
    }

    private async Task<OperationResult> RemoveUserFromStudent(StudentSkeletonUser student, string userId)
    {
        if (student?.MatricNumber == null || string.IsNullOrWhiteSpace(userId))
        {
            return new OperationResult { Success = false, ErrorMessage = "Invalid parameters for student user removal" };
        }

        try
        {
            var docName = $"StudentLevel{student.Level}";
            var levelDoc = await FirestoreService.GetDocumentAsync<StudentLevelDocument>(STUDENTS_COLLECTION, docName);
            
            if (levelDoc?.ValidStudentMatricNumbers == null)
            {
                return new OperationResult { Success = false, ErrorMessage = "Student document not found" };
            }

            var studentToUpdate = levelDoc.ValidStudentMatricNumbers
                .FirstOrDefault(s => s.MatricNumber.Equals(student.MatricNumber, StringComparison.OrdinalIgnoreCase));
            
            if (studentToUpdate == null)
            {
                return new OperationResult { Success = false, ErrorMessage = $"Student '{student.MatricNumber}' not found in document" };
            }

            if (studentToUpdate.CurrentUserId != userId)
            {
                return new OperationResult { Success = false, ErrorMessage = $"User '{userId}' not assigned to student" };
            }

            // Update the student's user assignment
            studentToUpdate.CurrentUserId = "";
            studentToUpdate.IsCurrentlyInUse = false;

            var updateSuccess = await FirestoreService.UpdateDocumentAsync(STUDENTS_COLLECTION, docName, levelDoc);
            
            return new OperationResult 
            { 
                Success = updateSuccess, 
                ErrorMessage = updateSuccess ? null : "Failed to update student document" 
            };
        }
        catch (Exception ex)
        {
            return new OperationResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    private async Task<OperationResult> RemoveUserFromLecturer(LecturerSkeletonUser lecturer, string userId)
    {
        if (lecturer?.AdminId == null || string.IsNullOrWhiteSpace(userId))
        {
            return new OperationResult { Success = false, ErrorMessage = "Invalid parameters for lecturer user removal" };
        }

        try
        {
            var lecturerDoc = await FirestoreService.GetDocumentAsync<LecturerAdminDocument>(ADMIN_IDS_COLLECTION, LECTURER_ADMIN_DOC);
            
            if (lecturerDoc?.Ids == null)
            {
                return new OperationResult { Success = false, ErrorMessage = "Lecturer document not found" };
            }

            var lecturerToUpdate = lecturerDoc.Ids.FirstOrDefault(l => l.AdminId == lecturer.AdminId);
            if (lecturerToUpdate == null)
            {
                return new OperationResult { Success = false, ErrorMessage = $"Lecturer '{lecturer.AdminId}' not found in document" };
            }

            if (!lecturerToUpdate.UserIds.Contains(userId))
            {
                return new OperationResult { Success = false, ErrorMessage = $"User '{userId}' not assigned to lecturer" };
            }

            // Update the lecturer's user list
            lecturerToUpdate.UserIds.Remove(userId);
            lecturerToUpdate.CurrentUsage = lecturerToUpdate.UserIds.Count;

            var updateSuccess = await FirestoreService.AddOrUpdateFieldAsync(ADMIN_IDS_COLLECTION, LECTURER_ADMIN_DOC, "Ids", lecturerDoc.Ids);
            
            return new OperationResult 
            { 
                Success = updateSuccess, 
                ErrorMessage = updateSuccess ? null : "Failed to update lecturer document" 
            };
        }
        catch (Exception ex)
        {
            return new OperationResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    private async Task<OperationResult> RemoveUserFromCourseRep(CourseRepAdminInfo courseRepAdmin, string userId)
    {
        if (courseRepAdmin?.AdminId == null || string.IsNullOrWhiteSpace(userId))
        {
            return new OperationResult { Success = false, ErrorMessage = "Invalid parameters for course rep user removal" };
        }

        try
        {
            var courseRepDoc = await FirestoreService.GetDocumentAsync<CourseRepAdminDocument>(ADMIN_IDS_COLLECTION, COURSEREP_ADMIN_DOC);
            
            if (courseRepDoc?.Ids == null)
            {
                return new OperationResult { Success = false, ErrorMessage = "CourseRep document not found" };
            }

            var courseRepToUpdate = courseRepDoc.Ids.FirstOrDefault(cr => cr.AdminId == courseRepAdmin.AdminId);
            if (courseRepToUpdate == null)
            {
                return new OperationResult { Success = false, ErrorMessage = $"CourseRep '{courseRepAdmin.AdminId}' not found in document" };
            }

            if (!courseRepToUpdate.UserIds.Contains(userId))
            {
                return new OperationResult { Success = false, ErrorMessage = $"User '{userId}' not assigned to course rep" };
            }

            // Update the course rep's user list
            courseRepToUpdate.UserIds.Remove(userId);
            courseRepToUpdate.CurrentUsage = courseRepToUpdate.UserIds.Count;

            var updateSuccess = await FirestoreService.AddOrUpdateFieldAsync(ADMIN_IDS_COLLECTION, COURSEREP_ADMIN_DOC, "Ids", courseRepDoc.Ids);
            
            return new OperationResult 
            { 
                Success = updateSuccess, 
                ErrorMessage = updateSuccess ? null : "Failed to update course rep document" 
            };
        }
        catch (Exception ex)
        {
            return new OperationResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    #endregion

    #region Helper Classes

    public class OperationResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
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

}