using System;
using Supabase;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace AirCode.Models.Supabase;

[Table("test_items")]
public class TestItem : BaseModel
{
    [PrimaryKey("id")]
    public int Id { get; set; }
        
    [Column("name")]
    public string Name { get; set; }
        
    [Column("description")]
    public string Description { get; set; }
        
    [Column("created_at")]
    public DateTime CreatedAt { get; set; }
}
[Table("courses")]
public class Course : BaseModel
{
    [PrimaryKey("id")]
    public int Id { get; set; }
        
    [Column("code")]
    public string Code { get; set; } = string.Empty;
        
    [Column("title")]
    public string Title { get; set; } = string.Empty;
        
    [Column("description")]
    public string Description { get; set; } = string.Empty;
        
    [Column("credit_units")]
    public int CreditUnits { get; set; }
        
    [Column("created_at")]
    public DateTime CreatedAt { get; set; }
        
    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }
}