/**
 * FloatingQrCodeWindow JavaScript Module
 * Handles drag functionality and window management for the floating QR code component
 */

export class FloatingQrCodeWindow {
    constructor() {
        this.registeredComponents = new Map();
        this.dragState = {
            isDragging: false,
            currentX: 0,
            currentY: 0,
            initialX: 0,
            initialY: 0,
            xOffset: 0,
            yOffset: 0,
            activeElement: null,
            activeComponent: null
        };

        this.initializeEventListeners();
    }

    /**
     * Register a FloatingQR component for drag management
     * @param {DotNetObjectReference} componentRef - Blazor component reference
     * @param {HTMLElement} windowElement - The floating window element
     */
    registerComponent(componentRef, windowElement) {
        const componentId = this.generateComponentId();

        this.registeredComponents.set(componentId, {
            componentRef,
            windowElement,
            headerElement: windowElement.querySelector('.floating-qr-header'),
            isFullscreen: false,
            lastPosition: { x: 0, y: 0 }
        });

        this.setupDragHandlers(componentId);
        return componentId;
    }

    /**
     * Unregister a component (cleanup on disposal)
     * @param {string} componentId - Component identifier
     */
    unregisterComponent(componentId) {
        if (this.registeredComponents.has(componentId)) {
            this.registeredComponents.delete(componentId);
        }
    }

    /**
     * Initialize global drag event listeners
     */
    initializeEventListeners() {
        // Global mouse events for drag handling
        document.addEventListener('mousemove', (e) => this.handleMouseMove(e), { passive: false });
        document.addEventListener('mouseup', () => this.handleMouseUp());

        // Global touch events for mobile drag support
        document.addEventListener('touchmove', (e) => this.handleTouchMove(e), { passive: false });
        document.addEventListener('touchend', () => this.handleTouchEnd());

        // Prevent context menu during drag
        document.addEventListener('contextmenu', (e) => {
            if (this.dragState.isDragging) {
                e.preventDefault();
            }
        });
    }

    /**
     * Setup drag handlers for a specific component
     * @param {string} componentId - Component identifier
     */
    setupDragHandlers(componentId) {
        const component = this.registeredComponents.get(componentId);
        if (!component || !component.headerElement) return;

        const { headerElement, windowElement } = component;

        // Mouse drag handlers
        headerElement.addEventListener('mousedown', (e) => {
            this.handleDragStart(e, componentId);
        });

        // Touch drag handlers for mobile
        headerElement.addEventListener('touchstart', (e) => {
            this.handleDragStart(e, componentId);
        }, { passive: false });

        // Prevent text selection during drag
        headerElement.addEventListener('selectstart', (e) => e.preventDefault());

        // Visual feedback
        headerElement.style.cursor = 'grab';
    }

    /**
     * Handle drag start (mouse or touch)
     * @param {MouseEvent|TouchEvent} e - Event object
     * @param {string} componentId - Component identifier
     */
    handleDragStart(e, componentId) {
        const component = this.registeredComponents.get(componentId);
        if (!component || component.isFullscreen) return;

        // Ignore if clicking on buttons
        if (e.target.closest('button')) return;

        e.preventDefault();

        const clientX = e.type === 'touchstart' ? e.touches[0].clientX : e.clientX;
        const clientY = e.type === 'touchstart' ? e.touches[0].clientY : e.clientY;

        // Get current window position
        const rect = component.windowElement.getBoundingClientRect();

        this.dragState = {
            isDragging: true,
            initialX: clientX - rect.left,
            initialY: clientY - rect.top,
            currentX: rect.left,
            currentY: rect.top,
            xOffset: rect.left,
            yOffset: rect.top,
            activeElement: component.windowElement,
            activeComponent: componentId
        };

        // Visual feedback
        component.windowElement.classList.add('dragging');
        component.headerElement.style.cursor = 'grabbing';

        // Bring to front
        component.windowElement.style.zIndex = '1002';
    }

    /**
     * Handle mouse move during drag
     * @param {MouseEvent} e - Mouse event
     */
    handleMouseMove(e) {
        if (!this.dragState.isDragging) return;

        e.preventDefault();
        this.updateDragPosition(e.clientX, e.clientY);
    }

    /**
     * Handle touch move during drag
     * @param {TouchEvent} e - Touch event
     */
    handleTouchMove(e) {
        if (!this.dragState.isDragging) return;

        e.preventDefault();
        const touch = e.touches[0];
        this.updateDragPosition(touch.clientX, touch.clientY);
    }

    /**
     * Update drag position with boundary constraints
     * @param {number} clientX - Current X coordinate
     * @param {number} clientY - Current Y coordinate
     */
    updateDragPosition(clientX, clientY) {
        const component = this.registeredComponents.get(this.dragState.activeComponent);
        if (!component) return;

        // Calculate new position
        let newX = clientX - this.dragState.initialX;
        let newY = clientY - this.dragState.initialY;

        // Apply boundary constraints
        const windowRect = component.windowElement.getBoundingClientRect();
        const maxX = window.innerWidth - windowRect.width;
        const maxY = window.innerHeight - windowRect.height;

        newX = Math.max(0, Math.min(newX, maxX));
        newY = Math.max(0, Math.min(newY, maxY));

        // Update position
        this.dragState.currentX = newX;
        this.dragState.currentY = newY;

        // Apply transform for smooth dragging
        component.windowElement.style.transform = `translate(${newX}px, ${newY}px)`;

        // Update component position via Blazor
        if (component.componentRef) {
            component.componentRef.invokeMethodAsync('UpdatePosition', newX, newY);
        }
    }

    /**
     * Handle mouse up (end drag)
     */
    handleMouseUp() {
        this.endDrag();
    }

    /**
     * Handle touch end (end drag)
     */
    handleTouchEnd() {
        this.endDrag();
    }

    /**
     * End drag operation and cleanup
     */
    endDrag() {
        if (!this.dragState.isDragging) return;

        const component = this.registeredComponents.get(this.dragState.activeComponent);
        if (component) {
            // Remove visual feedback
            component.windowElement.classList.remove('dragging');
            component.headerElement.style.cursor = 'grab';

            // Reset z-index
            component.windowElement.style.zIndex = '1001';

            // Clear transform and update position styles
            component.windowElement.style.transform = '';

            // Store final position
            component.lastPosition = {
                x: this.dragState.currentX,
                y: this.dragState.currentY
            };
        }

        // Reset drag state
        this.dragState = {
            isDragging: false,
            currentX: 0,
            currentY: 0,
            initialX: 0,
            initialY: 0,
            xOffset: 0,
            yOffset: 0,
            activeElement: null,
            activeComponent: null
        };
    }

    /**
     * Update component fullscreen state
     * @param {string} componentId - Component identifier
     * @param {boolean} isFullscreen - Fullscreen state
     */
    setFullscreenState(componentId, isFullscreen) {
        const component = this.registeredComponents.get(componentId);
        if (component) {
            component.isFullscreen = isFullscreen;

            // Update cursor based on fullscreen state
            if (component.headerElement) {
                component.headerElement.style.cursor = isFullscreen ? 'default' : 'grab';
            }
        }
    }

    /**
     * Get component position
     * @param {string} componentId - Component identifier
     * @returns {Object} Position object {x, y}
     */
    getComponentPosition(componentId) {
        const component = this.registeredComponents.get(componentId);
        return component ? component.lastPosition : { x: 0, y: 0 };
    }

    /**
     * Generate unique component ID
     * @returns {string} Unique identifier
     */
    generateComponentId() {
        return `floating-qr-${Date.now()}-${Math.random().toString(36).substr(2, 9)}`;
    }

    /**
     * Cleanup all components (for disposal)
     */
    dispose() {
        this.registeredComponents.clear();
        this.dragState = {
            isDragging: false,
            currentX: 0,
            currentY: 0,
            initialX: 0,
            initialY: 0,
            xOffset: 0,
            yOffset: 0,
            activeElement: null,
            activeComponent: null
        };
    }
}

// Initialize global instance
window.FloatingQrCodeWindow = new FloatingQrCodeWindow();

// Export for module usage
export default FloatingQrCodeWindow;