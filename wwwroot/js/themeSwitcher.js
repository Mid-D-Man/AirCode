// Add this to your wwwroot/js folder as themeSwitcher.js

window.themeSwitcher = {
    toggleTheme: function () {
        const body = document.querySelector('body');
        if (body.classList.contains('dark-mode')) {
            body.classList.remove('dark-mode');
            localStorage.setItem('theme', 'light');
        } else {
            body.classList.add('dark-mode');
            localStorage.setItem('theme', 'dark');
        }
    },

    initTheme: function () {
        const savedTheme = localStorage.getItem('theme');
        const prefersDark = window.matchMedia('(prefers-color-scheme: dark)').matches;

        if (savedTheme === 'dark' || (savedTheme === null && prefersDark)) {
            document.querySelector('body').classList.add('dark-mode');
        }
    }
};

// Initialize theme when page loads
document.addEventListener('DOMContentLoaded', window.themeSwitcher.initTheme);