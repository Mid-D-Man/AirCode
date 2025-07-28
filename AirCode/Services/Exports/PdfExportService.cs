using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using AirCode.Domain.Entities;
using Microsoft.JSInterop;

namespace AirCode.Services.Exports;

   public class PdfExportService : IPdfExportService
    {
        private const string SCHOOL_NAME = "University of Technology";

        static PdfExportService()
        {
            QuestPDF.Settings.License = LicenseType.Community;
        }

        public async Task<byte[]> GenerateAttendanceReportPdfAsync(AttendanceReport report)
        {
            return await Task.Run(() =>
            {
                var document = Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4.Landscape());
                        page.Margin(1, Unit.Centimetre);
                        page.PageColor(Colors.White);
                        page.DefaultTextStyle(x => x.FontSize(10));

                        page.Header().Height(80).Background(Colors.Grey.Lighten3).Padding(10)
                            .Column(column =>
                            {
                                column.Item().Text(SCHOOL_NAME).FontSize(18).Bold().AlignCenter();
                                column.Item().Text($"Course: {report.CourseCode} - Level {report.CourseLevel}")
                                    .FontSize(14).Bold().AlignCenter();
                                column.Item().Text($"Generated: {report.GeneratedAt:yyyy-MM-dd HH:mm}")
                                    .FontSize(10).AlignCenter();
                            });

                        page.Content().PaddingVertical(10).Column(column =>
                        {
                            column.Item().Component(new SummaryComponent(report));
                            column.Item().PaddingTop(15);
                            column.Item().Component(new AttendanceTableComponent(report));
                        });

                        page.Footer().AlignCenter().Text(x =>
                        {
                            x.Span("Page ");
                            x.CurrentPageNumber();
                            x.Span(" of ");
                            x.TotalPages();
                        });
                    });
                });

                return document.GeneratePdf();
            });
        }

        public async Task DownloadPdfAsync(IJSRuntime jsRuntime, byte[] pdfBytes, string filename)
        {
            var base64 = Convert.ToBase64String(pdfBytes);
            await jsRuntime.InvokeVoidAsync("window.downloadFile", base64, filename, "application/pdf");
        }
    }

    // Component classes for QuestPDF structure
    public class SummaryComponent : IComponent
    {
        private readonly AttendanceReport _report;

        public SummaryComponent(AttendanceReport report)
        {
            _report = report;
        }

        public void Compose(IContainer container)
        {
            container.Background(Colors.Blue.Lighten4).Padding(10).Row(row =>
            {
                row.RelativeItem().Column(col =>
                {
                    col.Item().Text($"Total Students: {_report.TotalStudentsEnrolled}").Bold();
                    col.Item().Text($"Total Sessions: {_report.TotalSessions}").Bold();
                });
                
                row.RelativeItem().Column(col =>
                {
                    col.Item().Text($"Average Attendance: {_report.AverageAttendancePercentage}%").Bold();
                    col.Item().Text($"Perfect Attendance: {_report.StudentsWithPerfectAttendance}").Bold();
                });
                
                row.RelativeItem().Column(col =>
                {
                    col.Item().Text($"Poor Attendance (<75%): {_report.StudentsWithPoorAttendance}").Bold();
                });
            });
        }
    }

    public class AttendanceTableComponent : IComponent
    {
        private readonly AttendanceReport _report;

        public AttendanceTableComponent(AttendanceReport report)
        {
            _report = report;
        }

        public void Compose(IContainer container)
        {
            var sessions = _report.StudentReports
                .SelectMany(s => s.SessionAttendance)
                .GroupBy(sa => sa.SessionId)
                .OrderBy(g => g.First().SessionDate)
                .Select(g => new { SessionId = g.Key, Date = g.First().SessionDate })
                .ToList();

            container.Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(80);
                    columns.ConstantColumn(40);
                    foreach (var session in sessions)
                    {
                        columns.ConstantColumn(45);
                    }
                    columns.ConstantColumn(50);
                });

                table.Header(header =>
                {
                    header.Cell().Border(1).BorderColor(Colors.Grey.Medium).Padding(2)
                        .AlignCenter().AlignMiddle().Text("Matric No").Bold();
                    header.Cell().Border(1).BorderColor(Colors.Grey.Medium).Padding(2)
                        .AlignCenter().AlignMiddle().Text("Level").Bold();
                    
                    foreach (var session in sessions)
                    {
                        header.Cell().Border(1).BorderColor(Colors.Grey.Medium).Padding(2)
                            .AlignCenter().AlignMiddle()
                            .Text(session.Date.ToString("MM/dd")).FontSize(8).Bold();
                    }
                    
                    header.Cell().Border(1).BorderColor(Colors.Grey.Medium).Padding(2)
                        .AlignCenter().AlignMiddle().Text("Total %").Bold();
                });

                foreach (var student in _report.StudentReports.OrderBy(s => s.MatricNumber))
                {
                    table.Cell().Border(1).BorderColor(Colors.Grey.Medium).Padding(2)
                        .AlignCenter().AlignMiddle().Text(student.MatricNumber).FontSize(9);
                    table.Cell().Border(1).BorderColor(Colors.Grey.Medium).Padding(2)
                        .AlignCenter().AlignMiddle().Text(student.StudentLevel.ToString()).FontSize(8);
                    
                    foreach (var session in sessions)
                    {
                        var attendance = student.SessionAttendance
                            .FirstOrDefault(sa => sa.SessionId == session.SessionId);
                        
                        var symbol = attendance?.IsPresent == true ? "✓" : 
                                   attendance?.IsPresent == false ? "✗" : "-";
                        
                        var color = attendance?.IsPresent == true ? Colors.Green.Medium :
                                  attendance?.IsPresent == false ? Colors.Red.Medium : Colors.Grey.Medium;
                        
                        table.Cell().Border(1).BorderColor(Colors.Grey.Medium).Padding(2)
                            .AlignCenter().AlignMiddle()
                            .Text(symbol).FontColor(color).FontSize(12).Bold();
                    }
                    
                    table.Cell().Border(1).BorderColor(Colors.Grey.Medium).Padding(2)
                        .AlignCenter().AlignMiddle()
                        .Text($"{student.AttendancePercentage:F1}%").FontSize(9).Bold();
                }
            });
        }
    }
    