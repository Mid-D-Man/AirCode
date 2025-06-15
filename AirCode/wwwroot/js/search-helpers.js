// Search component JavaScript helpers
window.searchHelpers = {
    clickOutsideHandlers: new Map(),
    
    setupClickOutside: (element, dotNetRef) => {
        const handler = (event) => {
            if (element && !element.contains(event.target)) {
                dotNetRef.invokeMethodAsync('HideDropdown');
            }
        };
        
        document.addEventListener('click', handler);
        window.searchHelpers.clickOutsideHandlers.set(element, handler);
    },
    
    cleanupClickOutside: (element) => {
        const handler = window.searchHelpers.clickOutsideHandlers.get(element);
        if (handler) {
            document.removeEventListener('click', handler);
            window.searchHelpers.clickOutsideHandlers.delete(element);
        }
    },
    
    focusSearchInput: (element) => {
        if (element) {
            element.focus();
        }
    }
};

// Global functions for backward compatibility
window.setupClickOutside = window.searchHelpers.setupClickOutside;
window.cleanupClickOutside = window.searchHelpers.cleanupClickOutside;
window.focusSearchInput = window.searchHelpers.focusSearchInput;
