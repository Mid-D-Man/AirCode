window.PdfService = {
    generateAttendanceReport: async function(reportData, schoolName, filename) {
        try {
            const { jsPDF } = window.jspdf;
            const doc = new jsPDF('l', 'mm', 'a4');

            const pageWidth = 297;
            const pageHeight = 210;
            const margin = 10;
            let yPos = margin;
            let pageNum = 1;

            // Use correct school name
            const correctedSchoolName = "AirForce Institute of Technology Kaduna";

            // Header
            this._addHeader(doc, correctedSchoolName, reportData, pageWidth, yPos);
            yPos += 25;

            // Summary
            this._addSummary(doc, reportData, margin, yPos);
            yPos += 25;

            // Table
            this._addAttendanceTable(doc, reportData, margin, yPos, pageWidth, pageHeight, pageNum);

            doc.save(filename);
            return true;
        } catch (error) {
            console.error('PDF generation failed:', error);
            return false;
        }
    },

    _addHeader: function(doc, schoolName, reportData, pageWidth, yPos) {
        doc.setFontSize(16);
        doc.setFont(undefined, 'bold');
        doc.text(schoolName, pageWidth/2, yPos, { align: 'center' });

        doc.setFontSize(14);
        doc.text('ATTENDANCE REPORT', pageWidth/2, yPos + 8, { align: 'center' });

        doc.setFontSize(10);
        doc.setFont(undefined, 'normal');
        const courseInfo = `Course: ${reportData.courseCode} | Level: ${reportData.courseLevel} | Generated: ${new Date().toLocaleDateString()}`;
        doc.text(courseInfo, pageWidth/2, yPos + 14, { align: 'center' });
    },

    _addSummary: function(doc, reportData, margin, yPos) {
        const summaryData = [
            ['Total Students', reportData.totalStudentsEnrolled],
            ['Total Sessions', reportData.totalSessions],
            ['Avg Attendance', reportData.averageAttendancePercentage.toFixed(1) + '%'],
            ['Perfect', reportData.studentsWithPerfectAttendance],
            ['Poor (<75%)', reportData.studentsWithPoorAttendance]
        ];

        let xPos = margin;
        summaryData.forEach(([label, value]) => {
            doc.rect(xPos, yPos, 50, 15);
            doc.setFontSize(12);
            doc.setFont(undefined, 'bold');
            doc.text(value.toString(), xPos + 25, yPos + 6, { align: 'center' });
            doc.setFontSize(8);
            doc.setFont(undefined, 'normal');
            doc.text(label, xPos + 25, yPos + 12, { align: 'center' });
            xPos += 55;
        });
    },

    _addAttendanceTable: function(doc, reportData, margin, startY, pageWidth, pageHeight, pageNum) {
        const maxSessionsPerPage = 20;
        const sessionCount = Math.min(reportData.totalSessions, maxSessionsPerPage);
        const sessionWidth = (pageWidth - margin * 2 - 80) / sessionCount;

        let yPos = startY;

        // Table headers
        yPos = this._addTableHeaders(doc, margin, yPos, sessionCount, sessionWidth);

        // Student rows
        doc.setFont(undefined, 'normal');
        doc.setFontSize(8);

        reportData.studentReports.forEach((student, index) => {
            if (yPos > pageHeight - 20) {
                this._addPageNumber(doc, pageNum, pageWidth, pageHeight);
                doc.addPage();
                yPos = margin;
                pageNum++;
                yPos = this._addTableHeaders(doc, margin, yPos, sessionCount, sessionWidth);
            }

            yPos = this._addStudentRow(doc, student, margin, yPos, sessionCount, sessionWidth);
        });

        this._addPageNumber(doc, pageNum, pageWidth, pageHeight);
    },

    _addTableHeaders: function(doc, margin, yPos, sessionCount, sessionWidth) {
        doc.setFontSize(8);
        doc.setFont(undefined, 'bold');

        let currentX = margin;
        const rowHeight = 8;

        // Static headers
        doc.rect(currentX, yPos, 25, rowHeight);
        doc.text('Matric Number', currentX + 12.5, yPos + 5, { align: 'center' });
        currentX += 25;

        doc.rect(currentX, yPos, 15, rowHeight);
        doc.text('Level', currentX + 7.5, yPos + 5, { align: 'center' });
        currentX += 15;

        // Session headers
        for (let i = 0; i < sessionCount; i++) {
            doc.rect(currentX, yPos, sessionWidth, rowHeight);
            doc.text(`S${i+1}`, currentX + sessionWidth/2, yPos + 5, { align: 'center' });
            currentX += sessionWidth;
        }

        // Summary headers
        ['P', 'A', '%'].forEach(header => {
            doc.rect(currentX, yPos, 13, rowHeight);
            doc.text(header, currentX + 6.5, yPos + 5, { align: 'center' });
            currentX += 13;
        });

        return yPos + rowHeight;
    },

    _addStudentRow: function(doc, student, margin, yPos, sessionCount, sessionWidth) {
        const rowHeight = 6;
        let currentX = margin;

        // Student info
        doc.rect(currentX, yPos, 25, rowHeight);
        doc.text(student.matricNumber, currentX + 1, yPos + 4);
        currentX += 25;

        doc.rect(currentX, yPos, 15, rowHeight);
        doc.text(student.studentLevel.toString(), currentX + 7.5, yPos + 4, { align: 'center' });
        currentX += 15;

        // FIXED: Enhanced attendance marks with increased size and better symbols
        doc.setFontSize(14); // Increased from 11 to 14 for better visibility
        for (let i = 0; i < sessionCount && i < student.sessionAttendance.length; i++) {
            doc.rect(currentX, yPos, sessionWidth, rowHeight);

            const session = student.sessionAttendance[i];
            let mark, color;

            if (session.hasRecord) {
                if (session.isPresent === true) {
                    mark = '^'; // Using ^ for present (more reliable than checkmark)
                    color = [0, 128, 0]; // Dark green
                } else {
                    mark = 'X'; // Using capital X for absent
                    color = [180, 0, 0]; // Dark red
                }
            } else {
                mark = '-'; // Dash for no record
                color = [100, 100, 100]; // Gray
            }

            doc.setTextColor(color[0], color[1], color[2]);
            doc.setFont(undefined, 'bold');
            doc.text(mark, currentX + sessionWidth/2, yPos + 4.5, { align: 'center' });
            doc.setFont(undefined, 'normal');
            doc.setTextColor(0, 0, 0); // Reset to black
            currentX += sessionWidth;
        }

        doc.setFontSize(8); // Reset font size for summary columns

        // Summary columns
        [student.totalPresent, student.totalAbsent, student.attendancePercentage.toFixed(1) + '%'].forEach(value => {
            doc.rect(currentX, yPos, 13, rowHeight);
            doc.text(value.toString(), currentX + 6.5, yPos + 4, { align: 'center' });
            currentX += 13;
        });

        return yPos + rowHeight;
    },

    _addPageNumber: function(doc, pageNum, pageWidth, pageHeight) {
        doc.setFontSize(10);
        doc.setTextColor(128, 128, 128);
        doc.text(`Page ${pageNum}`, pageWidth - 20, pageHeight - 5);
        doc.setTextColor(0, 0, 0);
    }
};