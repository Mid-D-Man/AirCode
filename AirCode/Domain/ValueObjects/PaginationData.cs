using AirCode.Utilities.ObjectPooling;

namespace AirCode.Domain.ValueObjects;

public class PaginationState
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

public class Collections
{
    public List<StudentSkeletonUser> Students { get; } = new();
    public List<LecturerSkeletonUser> Lecturers { get; } = new();
    public List<CourseRepSkeletonUser> CourseReps { get; } = new();
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
