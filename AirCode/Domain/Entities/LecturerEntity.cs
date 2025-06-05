using System.ComponentModel.DataAnnotations;
using AirCode.Domain.Interfaces;

namespace AirCode.Domain.Entities;

public record Lecturer : ISecureEntity
{
    [Required]
    public string LecturerId { get; init; }
    [Required]
    public string Name { get; init; }
    public string DepartmentId { get; init; }
    public List<string> CourseIds { get; init; }
        
    // Security attributes
    public string SecurityToken { get; init; }
    public DateTime LastModified { get; init; }
    public string ModifiedBy { get; init; }
}
