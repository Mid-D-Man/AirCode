using AirCode.Domain.Entities;
using Microsoft.JSInterop;

namespace AirCode.Services.Exports;

public interface IPdfExportService
{
    Task<bool> GenerateAttendanceReportPdfAsync(AttendanceReport report, string schoolName = "Federal University of Technology Minna");
}