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