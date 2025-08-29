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
// Modal Portal Management System (bokan nijutsu)
let portalElements = new Map();
let portalRoot = null;

// Initialize portal root when DOM is ready
document.addEventListener('DOMContentLoaded', function() {
    initializePortalRoot();
});

function initializePortalRoot() {
    portalRoot = document.getElementById('modal-portal-root');
    if (!portalRoot) {
        portalRoot = document.createElement('div');
        portalRoot.id = 'modal-portal-root';
        portalRoot.className = 'modal-portal-root';
        document.body.appendChild(portalRoot);
    }
}

window.moveElementToPortal = (elementSelector) => {
    try {
        // Ensure portal root exists
        if (!portalRoot) {
            initializePortalRoot();
        }

        const element = document.querySelector(elementSelector);

        if (element && portalRoot && !portalElements.has(elementSelector)) {
            // Store original parent for restoration
            const originalParent = element.parentElement;
            const originalNextSibling = element.nextElementSibling;
            const originalStyles = {
                position: element.style.position,
                top: element.style.top,
                left: element.style.left,
                width: element.style.width,
                height: element.style.height,
                zIndex: element.style.zIndex,
                transform: element.style.transform
            };

            portalElements.set(elementSelector, {
                originalParent,
                originalNextSibling,
                originalStyles
            });

            // Move to portal
            portalRoot.appendChild(element);

            // Force proper modal styling
            element.style.position = 'fixed';
            element.style.top = '0';
            element.style.left = '0';
            element.style.width = '100vw';
            element.style.height = '100vh';
            element.style.zIndex = '10000';
            element.style.transform = 'none';

            console.log(`Moved element ${elementSelector} to portal`);
        }
    } catch (error) {
        console.error('Error moving element to portal:', error);
    }
};

window.returnElementFromPortal = (elementSelector) => {
    try {
        const element = document.querySelector(elementSelector);
        const portalInfo = portalElements.get(elementSelector);

        if (element && portalInfo) {
            const { originalParent, originalNextSibling, originalStyles } = portalInfo;

            // Restore to original location
            if (originalParent) {
                if (originalNextSibling && originalNextSibling.parentElement === originalParent) {
                    originalParent.insertBefore(element, originalNextSibling);
                } else {
                    originalParent.appendChild(element);
                }
            }

            // Restore original styles
            Object.keys(originalStyles).forEach(key => {
                element.style[key] = originalStyles[key] || '';
            });

            // Remove from tracking
            portalElements.delete(elementSelector);

            console.log(`Returned element ${elementSelector} from portal`);
        }
    } catch (error) {
        console.error('Error returning element from portal:', error);
    }
};

// Clean up orphaned elements on page unload
window.addEventListener('beforeunload', function() {
    portalElements.forEach((info, selector) => {
        window.returnElementFromPortal(selector);
    });
});