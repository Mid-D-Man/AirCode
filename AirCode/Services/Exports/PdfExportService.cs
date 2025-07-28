using Microsoft.JSInterop;
using AirCode.Domain.Entities;

namespace AirCode.Services.Exports
{
    public class PdfExportService : IPdfExportService
    {
        private readonly IJSRuntime _jsRuntime;
      
        public PdfExportService(IJSRuntime jsRuntime)
        {
            _jsRuntime = jsRuntime;
        }

        public async Task<bool> GenerateAttendanceReportPdfAsync(AttendanceReport report, string schoolName = "Federal University of Technology Minna")
        {
            try
            {
              
                var fileName = $"Attendance_Report_{report.CourseCode}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
                var reportData = MapToReportData(report);
                
                return await _jsRuntime.InvokeAsync<bool>("PdfService.generateAttendanceReport", reportData, schoolName, fileName);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"PDF Generation Error: {ex.Message}");
                return false;
            }
        }

        private object MapToReportData(AttendanceReport report)
        {
            return new
            {
                courseCode = report.CourseCode,
                courseLevel = report.CourseLevel.ToString(),
                totalStudentsEnrolled = report.TotalStudentsEnrolled,
                totalSessions = report.TotalSessions,
                averageAttendancePercentage = report.AverageAttendancePercentage,
                studentsWithPerfectAttendance = report.StudentsWithPerfectAttendance,
                studentsWithPoorAttendance = report.StudentsWithPoorAttendance,
                studentReports = report.StudentReports.Select(s => new
                {
                    matricNumber = s.MatricNumber,
                    studentLevel = s.StudentLevel,
                    totalPresent = s.TotalPresent,
                    totalAbsent = s.TotalAbsent,
                    attendancePercentage = s.AttendancePercentage,
                    sessionAttendance = s.SessionAttendance.Select(sa => new
                    {
                        isPresent = sa.IsPresent,
                        sessionDate = sa.SessionDate,
                        hasRecord = sa.ScanTime.HasValue || sa.DeviceGUID != null, // Student has an attendance record
                        scanTime = sa.ScanTime
                    }).ToArray()
                }).ToArray()
            };
        }

        private string GenerateAttendanceReportHtml(AttendanceReport report, string schoolName)
        {
            var sessionsHtml = GenerateSessionColumns(report);
            var studentsHtml = GenerateStudentRows(report);

            return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <style>
        body {{ font-family: Arial, sans-serif; margin: 20px; font-size: 12px; }}
        .header {{ text-align: center; margin-bottom: 30px; }}
        .school-name {{ font-size: 18px; font-weight: bold; margin-bottom: 10px; }}
        .report-title {{ font-size: 16px; margin-bottom: 5px; }}
        .report-info {{ font-size: 12px; color: #666; }}
        .summary {{ margin: 20px 0; padding: 15px; background: #f5f5f5; border-radius: 5px; }}
        .summary-grid {{ display: grid; grid-template-columns: repeat(auto-fit, minmax(150px, 1fr)); gap: 10px; }}
        .summary-item {{ text-align: center; }}
        .summary-value {{ font-size: 18px; font-weight: bold; color: #2c5aa0; }}
        .summary-label {{ font-size: 11px; color: #666; margin-top: 3px; }}
        .attendance-table {{ width: 100%; border-collapse: collapse; margin-top: 20px; font-size: 10px; }}
        .attendance-table th, .attendance-table td {{ border: 1px solid #ddd; padding: 4px; text-align: center; }}
        .attendance-table th {{ background-color: #2c5aa0; color: white; font-weight: bold; }}
        .student-info {{ text-align: left !important; padding-left: 8px !important; }}
        .present {{ color: #28a745; font-weight: bold; }}
        .absent {{ color: #dc3545; font-weight: bold; }}
        .percentage {{ font-weight: bold; }}
        .excellent {{ color: #28a745; }}
        .good {{ color: #ffc107; }}
        .poor {{ color: #dc3545; }}
        .critical {{ color: #6c757d; }}
        .page-break {{ page-break-before: always; }}
        @media print {{
            body {{ margin: 0; }}
            .page-break {{ page-break-before: always; }}
        }}
    </style>
</head>
<body>
    <div class='header'>
        <div class='school-name'>{schoolName}</div>
        <div class='report-title'>ATTENDANCE REPORT</div>
        <div class='report-info'>
            Course: {report.CourseCode} | Level: {report.CourseLevel} | Generated: {report.GeneratedAt:MMM dd, yyyy HH:mm}
        </div>
    </div>

    <div class='summary'>
        <div class='summary-grid'>
            <div class='summary-item'>
                <div class='summary-value'>{report.TotalStudentsEnrolled}</div>
                <div class='summary-label'>Total Students</div>
            </div>
            <div class='summary-item'>
                <div class='summary-value'>{report.TotalSessions}</div>
                <div class='summary-label'>Total Sessions</div>
            </div>
            <div class='summary-item'>
                <div class='summary-value'>{report.AverageAttendancePercentage:F1}%</div>
                <div class='summary-label'>Average Attendance</div>
            </div>
            <div class='summary-item'>
                <div class='summary-value'>{report.StudentsWithPerfectAttendance}</div>
                <div class='summary-label'>Perfect Attendance</div>
            </div>
            <div class='summary-item'>
                <div class='summary-value'>{report.StudentsWithPoorAttendance}</div>
                <div class='summary-label'>Poor Attendance (&lt;75%)</div>
            </div>
        </div>
    </div>

    <table class='attendance-table'>
        <thead>
            <tr>
                <th rowspan='2' style='width: 120px;'>Matric Number</th>
                <th rowspan='2' style='width: 60px;'>Level</th>
                <th colspan='{report.TotalSessions}'>Attendance Sessions</th>
                <th rowspan='2' style='width: 50px;'>Present</th>
                <th rowspan='2' style='width: 50px;'>Absent</th>
                <th rowspan='2' style='width: 60px;'>%</th>
                <th rowspan='2' style='width: 70px;'>Status</th>
            </tr>
            <tr>
                {sessionsHtml}
            </tr>
        </thead>
        <tbody>
            {studentsHtml}
        </tbody>
    </table>
</body>
</html>";
        }

        private string GenerateSessionColumns(AttendanceReport report)
        {
            if (!report.StudentReports.Any()) return "";

            var sessions = report.StudentReports.First().SessionAttendance
                .OrderBy(s => s.SessionDate)
                .Select(s => $"<th style='width: 35px; font-size: 9px;'>{s.SessionDate:MM/dd}</th>");

            return string.Join("", sessions);
        }

        private string GenerateStudentRows(AttendanceReport report)
        {
            var rows = report.StudentReports
                .OrderBy(s => s.MatricNumber)
                .Select(student => GenerateStudentRow(student));

            return string.Join("", rows);
        }

        private string GenerateStudentRow(StudentAttendanceReport student)
        {
            var attendanceMarks = student.SessionAttendance
                .OrderBy(s => s.SessionDate)
                .Select(s => s.IsPresent ? "<td class='present'>✓</td>" : "<td class='absent'>✗</td>");

            var statusClass = student.AttendancePercentage switch
            {
                >= 90 => "excellent",
                >= 75 => "good", 
                >= 50 => "poor",
                _ => "critical"
            };

            var statusText = student.AttendancePercentage switch
            {
                >= 90 => "Excellent",
                >= 75 => "Good",
                >= 50 => "Poor", 
                _ => "Critical"
            };

            return $@"
                <tr>
                    <td class='student-info'>{student.MatricNumber}</td>
                    <td>{student.StudentLevel}</td>
                    {string.Join("", attendanceMarks)}
                    <td class='present'>{student.TotalPresent}</td>
                    <td class='absent'>{student.TotalAbsent}</td>
                    <td class='percentage {statusClass}'>{student.AttendancePercentage:F1}%</td>
                    <td class='{statusClass}'>{statusText}</td>
                </tr>";
        }
    }
}