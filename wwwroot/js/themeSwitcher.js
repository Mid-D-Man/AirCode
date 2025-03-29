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

        // Only apply dark mode if explicitly set to 'dark'
        if (savedTheme === 'dark') {
            document.querySelector('body').classList.add('dark-mode');
        } else {
            // Ensure light mode is applied (remove dark-mode if present)
            document.querySelector('body').classList.remove('dark-mode');
            // Optionally save the preference
            if (!savedTheme) localStorage.setItem('theme', 'light');
        }
    }
};

// Initialize theme when page loads
document.addEventListener('DOMContentLoaded', window.themeSwitcher.initTheme);