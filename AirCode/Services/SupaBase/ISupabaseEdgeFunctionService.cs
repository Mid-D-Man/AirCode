
namespace AirCode.Services.SupaBase;

public interface ISupabaseEdgeFunctionService
{
    Task<AttendanceProcessingResult> ProcessAttendanceAsync(string qrCodePayload, AttendanceRecord attendanceData);
    Task<QRValidationResult> ValidateQRCodeAsync(string qrCodePayload);
    Task<string> GetRandomCatImageAsync();
}