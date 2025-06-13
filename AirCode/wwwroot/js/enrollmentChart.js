// Chart.js instance storage
let enrollmentChart = null;

// Sample enrollment data - replace with your actual data structure
const enrollmentData = {
    labels: ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun'],
    datasets: [{
        label: 'Enrollment',
        data: [120, 135, 128, 145, 132, 150],
        borderColor: 'rgb(75, 192, 192)',
        backgroundColor: 'rgba(75, 192, 192, 0.2)',
        tension: 0.4
    }]
};

// Level-specific data
const levelData = {
    'Level100': {
        labels: ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun'],
        datasets: [{
            label: '100 Level',
            data: [45, 48, 42, 52, 49, 55],
            borderColor: 'rgb(255, 99, 132)',
            backgroundColor: 'rgba(255, 99, 132, 0.2)',
            tension: 0.4
        }]
    },
    'Level200': {
        labels: ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun'],
        datasets: [{
            label: '200 Level',
            data: [38, 42, 35, 45, 41, 48],
            borderColor: 'rgb(54, 162, 235)',
            backgroundColor: 'rgba(54, 162, 235, 0.2)',
            tension: 0.4
        }]
    },
    'Level300': {
        labels: ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun'],
        datasets: [{
            label: '300 Level',
            data: [25, 28, 32, 30, 28, 32],
            borderColor: 'rgb(255, 205, 86)',
            backgroundColor: 'rgba(255, 205, 86, 0.2)',
            tension: 0.4
        }]
    },
    'Level400': {
        labels: ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun'],
        datasets: [{
            label: '400 Level',
            data: [12, 17, 19, 18, 14, 15],
            borderColor: 'rgb(75, 192, 192)',
            backgroundColor: 'rgba(75, 192, 192, 0.2)',
            tension: 0.4
        }]
    },
    'Level500': {
        labels: ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun'],
        datasets: [{
            label: '500 Level',
            data: [0, 0, 0, 0, 0, 0],
            borderColor: 'rgb(153, 102, 255)',
            backgroundColor: 'rgba(153, 102, 255, 0.2)',
            tension: 0.4
        }]
    }
};

window.initializeEnrollmentChart = function() {
    const ctx = document.getElementById('enrollmentChart');
    if (!ctx) {
        console.error('Canvas element with id "enrollmentChart" not found');
        return;
    }

    // Destroy existing chart if it exists
    if (enrollmentChart) {
        enrollmentChart.destroy();
    }

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
                },
                title: {
                    display: false
                }
            },
            scales: {
                y: {
                    beginAtZero: true,
                    grid: {
                        color: 'rgba(0, 0, 0, 0.1)'
                    },
                    ticks: {
                        color: '#666'
                    }
                },
                x: {
                    grid: {
                        color: 'rgba(0, 0, 0, 0.1)'
                    },
                    ticks: {
                        color: '#666'
                    }
                }
            },
            elements: {
                point: {
                    radius: 4,
                    hoverRadius: 6,
                    backgroundColor: '#fff',
                    borderWidth: 2
                },
                line: {
                    borderWidth: 2
                }
            },
            interaction: {
                intersect: false,
                mode: 'index'
            }
        }
    });
};

window.updateEnrollmentChart = function(selectedLevel) {
    if (!enrollmentChart) {
        console.error('Enrollment chart not initialized');
        return;
    }

    let newData;
    if (!selectedLevel || selectedLevel === '') {
        // Show all levels combined
        newData = enrollmentData;
    } else {
        // Show specific level data
        newData = levelData[selectedLevel] || enrollmentData;
    }

    enrollmentChart.data = newData;
    enrollmentChart.update('active');
};