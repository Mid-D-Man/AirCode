//used for draging qrcode floating window
window.floatingQrDrag = {
    isDragging: false,
    dragStartX: 0,
    dragStartY: 0,
    windowStartX: 0,
    windowStartY: 0,
    currentWindow: null,

    init: function() {
        document.addEventListener('mousemove', this.handleMouseMove.bind(this));
        document.addEventListener('mouseup', this.handleMouseUp.bind(this));
        document.addEventListener('touchmove', this.handleTouchMove.bind(this));
        document.addEventListener('touchend', this.handleTouchEnd.bind(this));
    },

    startDrag: function(event) {
        this.isDragging = true;

        // Find the floating window element by traversing up from the drag handle
        let windowElement = event.target;
        while (windowElement && !windowElement.classList.contains('floating-qr-window')) {
            windowElement = windowElement.parentElement;
        }

        if (!windowElement) return;

        this.currentWindow = windowElement;

        const clientX = event.touches ? event.touches[0].clientX : event.clientX;
        const clientY = event.touches ? event.touches[0].clientY : event.clientY;

        this.dragStartX = clientX;
        this.dragStartY = clientY;

        const rect = windowElement.getBoundingClientRect();
        this.windowStartX = rect.left;
        this.windowStartY = rect.top;

        event.preventDefault();
    },

    handleMouseMove: function(event) {
        if (!this.isDragging || !this.currentWindow) return;

        const deltaX = event.clientX - this.dragStartX;
        const deltaY = event.clientY - this.dragStartY;

        this.updateWindowPosition(deltaX, deltaY);
    },

    handleTouchMove: function(event) {
        if (!this.isDragging || !this.currentWindow) return;

        const touch = event.touches[0];
        const deltaX = touch.clientX - this.dragStartX;
        const deltaY = touch.clientY - this.dragStartY;

        this.updateWindowPosition(deltaX, deltaY);
        event.preventDefault();
    },

    updateWindowPosition: function(deltaX, deltaY) {
        if (!this.currentWindow) return;

        const newX = Math.max(0, Math.min(
            window.innerWidth - this.currentWindow.offsetWidth,
            this.windowStartX + deltaX
        ));
        const newY = Math.max(0, Math.min(
            window.innerHeight - this.currentWindow.offsetHeight,
            this.windowStartY + deltaY
        ));

        this.currentWindow.style.left = newX + 'px';
        this.currentWindow.style.top = newY + 'px';
    },

    handleMouseUp: function() {
        this.stopDrag();
    },

    handleTouchEnd: function() {
        this.stopDrag();
    },

    stopDrag: function() {
        this.isDragging = false;
        this.currentWindow = null;
    }
};

// Initialize when DOM is loaded
document.addEventListener('DOMContentLoaded', function() {
    window.floatingQrDrag.init();
});