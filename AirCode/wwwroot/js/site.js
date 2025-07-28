window.triggerAutoNav = () => {
    const navLink = document.getElementById('autoNav');
    if (navLink) {
        navLink.click();
    }
};
window.downloadFile = function(base64, filename, contentType) {
    const blob = new Blob([Uint8Array.from(atob(base64), c => c.charCodeAt(0))], { type: contentType });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = filename;
    a.click();
    URL.revokeObjectURL(url);
};