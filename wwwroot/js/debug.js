// Create this file in wwwroot/js/debug.js
console.log('Debug script loaded');

// Log all network errors
window.addEventListener('error', function(e) {
    if (e && e.target && (e.target.tagName === 'LINK' || e.target.tagName === 'SCRIPT' || e.target.tagName === 'IMG')) {
        console.error('Resource failed to load:', e.target.src || e.target.href);
    }
});

// Check paths when page loads
window.addEventListener('DOMContentLoaded', function() {
    console.log('Current base href:', document.querySelector('base')?.href);
    console.log('Current path:', window.location.pathname);

    // Check if critical files are accessible
    const criticalFiles = [
        '_framework/blazor.webassembly.js',
        'manifest.json',
        'css/app.css',
        'favicon.png'
    ];

    criticalFiles.forEach(file => {
        const img = new Image();
        img.onload = () => console.log(`✅ File accessible: ${file}`);
        img.onerror = () => console.error(`❌ File NOT accessible: ${file}`);
        img.src = file;
    });
});

// Add this script to index.html right after the <body> tag