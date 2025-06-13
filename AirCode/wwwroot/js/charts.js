
    // Check if Chart.js is loaded
    if (typeof Chart === 'undefined') {
    console.error('Chart.js is not loaded. Please include Chart.js before this script.');
}

    // Enrollment Chart Functions
    let enrollmentChart = null;

    window.initializeEnrollmentChart = function() {
    if (typeof Chart === 'undefined') {
    console.error('Chart.js is not available');
    return;
}

    const ctx = document.getElementById('enrollmentChart');
    if (!ctx) {
    console.error('Canvas element with id "enrollmentChart" not found');
    return;
}

    // Destroy existing chart if it exists
    if (enrollmentChart) {
    enrollmentChart.destroy();
}

    const enrollmentData = {
    labels: ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun'],
    datasets: [{
    label: 'Total Enrollment',
    data: [120, 135, 128, 145, 132, 150],
    borderColor: 'rgb(75, 192, 192)',
    backgroundColor: 'rgba(75, 192, 192, 0.2)',
    tension: 0.4
}]
};

    enrollmentChart = new Chart(ctx, {
    type: 'line',
    data: enrollmentData,
    options: {
    responsive: true,
    maintainAspectRatio: false,
    plugins: {
    legend: {
    display: true,
    position: 'top'
}
},
    scales: {
    y: {
    beginAtZero: true
}
}
}
});
};

    window.updateEnrollmentChart = function(selectedLevel) {
    if (!enrollmentChart) {
    console.error('Enrollment chart not initialized');
    return;
}

    // Update chart data based on selected level
    // Add your level-specific data logic here
    enrollmentChart.update();
};

    // Existing attendance chart function
    window.initializeAttendanceChart = (canvasId, data, type) => {
    const ctx = document.getElementById(canvasId).getContext('2d');
    new Chart(ctx, {
    type: type,
    data: data,
    options: {
    responsive: true,
    maintainAspectRatio: false,
    plugins: {
    legend: {
    display: false
}
},
    scales: {
    y: {
    beginAtZero: true,
    max: 100,
    ticks: {
    callback: function(value) {
    return value + '%';
}
}
}
}
}
});
};