// FloatingQrCodeWindow.razor.js - Blazor WASM Compatible

const dragManager = {
    components: new Map(),
    dragState: {
        isDragging: false,
        currentX: 0, currentY: 0,
        initialX: 0, initialY: 0,
        activeElement: null,
        activeComponent: null
    }
};

// Initialize global listeners once
let listenersInitialized = false;

function initializeGlobalListeners() {
    if (listenersInitialized) return;

    document.addEventListener('mousemove', handleMouseMove, { passive: false });
    document.addEventListener('mouseup', handleMouseUp);
    document.addEventListener('touchmove', handleTouchMove, { passive: false });
    document.addEventListener('touchend', handleTouchEnd);

    listenersInitialized = true;
}

export function registerComponent(componentRef, windowElement) {
    initializeGlobalListeners();

    const componentId = `floating-qr-${Date.now()}-${Math.random().toString(36).substr(2, 9)}`;
    const headerElement = windowElement.querySelector('.floating-qr-header');

    dragManager.components.set(componentId, {
        componentRef,
        windowElement,
        headerElement,
        isFullscreen: false
    });

    // Setup drag handlers
    headerElement.addEventListener('mousedown', (e) => handleDragStart(e, componentId));
    headerElement.addEventListener('touchstart', (e) => handleDragStart(e, componentId), { passive: false });
    headerElement.style.cursor = 'grab';

    return componentId;
}

export function unregisterComponent(componentId) {
    dragManager.components.delete(componentId);
}

export function setFullscreenState(componentId, isFullscreen) {
    const component = dragManager.components.get(componentId);
    if (component) {
        component.isFullscreen = isFullscreen;
        component.headerElement.style.cursor = isFullscreen ? 'default' : 'grab';
    }
}

function handleDragStart(e, componentId) {
    const component = dragManager.components.get(componentId);
    if (!component || component.isFullscreen || e.target.closest('button')) return;

    e.preventDefault();

    const clientX = e.type === 'touchstart' ? e.touches[0].clientX : e.clientX;
    const clientY = e.type === 'touchstart' ? e.touches[0].clientY : e.clientY;
    const rect = component.windowElement.getBoundingClientRect();

    dragManager.dragState = {
        isDragging: true,
        initialX: clientX - rect.left,
        initialY: clientY - rect.top,
        currentX: rect.left,
        currentY: rect.top,
        activeElement: component.windowElement,
        activeComponent: componentId
    };

    component.windowElement.classList.add('dragging');
    component.headerElement.style.cursor = 'grabbing';
    component.windowElement.style.zIndex = '1002';
}

function handleMouseMove(e) {
    if (!dragManager.dragState.isDragging) return;
    e.preventDefault();
    updateDragPosition(e.clientX, e.clientY);
}

function handleTouchMove(e) {
    if (!dragManager.dragState.isDragging) return;
    e.preventDefault();
    updateDragPosition(e.touches[0].clientX, e.touches[0].clientY);
}

function updateDragPosition(clientX, clientY) {
    const component = dragManager.components.get(dragManager.dragState.activeComponent);
    if (!component) return;

    let newX = clientX - dragManager.dragState.initialX;
    let newY = clientY - dragManager.dragState.initialY;

    // Boundary constraints
    const windowRect = component.windowElement.getBoundingClientRect();
    newX = Math.max(0, Math.min(newX, window.innerWidth - windowRect.width));
    newY = Math.max(0, Math.min(newY, window.innerHeight - windowRect.height));

    dragManager.dragState.currentX = newX;
    dragManager.dragState.currentY = newY;

    component.windowElement.style.transform = `translate(${newX}px, ${newY}px)`;

    // Update Blazor component
    component.componentRef.invokeMethodAsync('UpdatePosition', newX, newY);
}

function handleMouseUp() { endDrag(); }
function handleTouchEnd() { endDrag(); }

function endDrag() {
    if (!dragManager.dragState.isDragging) return;

    const component = dragManager.components.get(dragManager.dragState.activeComponent);
    if (component) {
        component.windowElement.classList.remove('dragging');
        component.headerElement.style.cursor = 'grab';
        component.windowElement.style.zIndex = '1001';
        component.windowElement.style.transform = '';
    }

    dragManager.dragState.isDragging = false;
}