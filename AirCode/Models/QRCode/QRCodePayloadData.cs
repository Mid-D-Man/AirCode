// Models/QRCodeModels.cs
using System.Text.Json;
using AirCode.Domain.Enums;
using AttendanceRecord = AirCode.Models.Supabase.AttendanceRecord;
namespace AirCode.Models.QRCode
{
/// <summary>
/// Partial QR code payload data sent to Supabase Edge Function
/// This is NOT encrypted or compressed, only signed for integrity
/// </summary>
public class QRCodePayloadData
{
    public string SessionId { get; set; } = string.Empty;
    public string CourseCode { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public string TemporalKey { get; set; } = string.Empty;
    public bool UseTemporalKeyRefresh { get; set; } // Renamed from AdvanceSecurityModeEnabled
    public bool AllowOfflineConnectionAndSync { get; set; } = true; // New field
    public AdvancedSecurityFeatures SecurityFeatures { get; set; } = AdvancedSecurityFeatures.Default; // New field

    public override string ToString()
    {
        return JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
    }
}

/// <summary>
/// Complete session data used internally in the app (encrypted in QR codes)
/// </summary>
public class DecodedSessionData
{
    public string SessionId { get; set; } = string.Empty;
    public string CourseCode { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public int Duration { get; set; }
    public DateTime GeneratedTime { get; set; }
    public DateTime ExpirationTime { get; set; }
    public string TemporalKey { get; set; } = string.Empty;
    public bool UseTemporalKeyRefresh { get; set; } // Renamed from AdvanceSecurityModeEnabled
    public bool AllowOfflineConnectionAndSync { get; set; } = true; // New field
    public AdvancedSecurityFeatures SecurityFeatures { get; set; } = AdvancedSecurityFeatures.Default; // New field

    public override string ToString()
    {
        return JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
    }
}


}
