using AirCode.Domain.Entities;
using Microsoft.JSInterop;

namespace AirCode.Services.Exports;

public interface IPdfExportService
{
    Task<byte[]> GenerateAttendanceReportPdfAsync(AttendanceReport report);
    Task DownloadPdfAsync(IJSRuntime jsRuntime, byte[] pdfBytes, string filename);
}