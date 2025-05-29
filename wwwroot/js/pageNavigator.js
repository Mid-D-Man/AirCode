/****************************************************************************
 * Page Navigator Script for Blazor WASM
 * This script discovers all routable pages in the Blazor app and creates
 * a navigation panel with buttons to quickly jump to any page.
 ****************************************************************************/

console.log('Page Navigator script loaded');

// Global page navigator functions
window.pageNavigator = {
    pages: [],
    isInitialized: false,

    // Initialize the page navigator
    init: function() {
        if (this.isInitialized) return;

        // Wait for Blazor to be fully loaded
        const initInterval = setInterval(() => {
            if (window.Blazor && window.DotNet) {
                this.discoverPages();
                this.createNavigatorPanel();
                this.isInitialized = true;
                clearInterval(initInterval);
                console.log('✅ Page Navigator initialized');
            }
        }, 500);
    },

    // Discover all pages by examining the Blazor router
    discoverPages: function() {
        try {
            // Method 1: Check for route data in the DOM
            this.scanRouteAttributes();

            // Method 2: Check for navigation manager routes (if accessible)
            this.scanNavigationRoutes();

            // Method 3: Scan for common page patterns
            this.scanCommonPagePatterns();

            // Remove duplicates and sort
            this.pages = [...new Set(this.pages)].sort();

            console.log('📄 Discovered pages:', this.pages);
        } catch (error) {
            console.error('Error discovering pages:', error);
        }
    },

    // Scan DOM for route attributes
    scanRouteAttributes: function() {
        // Look for elements with data-route or similar attributes
        const routeElements = document.querySelectorAll('[data-route], [data-page], [href^="/"]');
        routeElements.forEach(el => {
            const route = el.getAttribute('data-route') ||
                el.getAttribute('data-page') ||
                el.getAttribute('href');
            if (route && route.startsWith('/') && !route.includes('://')) {
                this.pages.push(route);
            }
        });
    },

    // Scan for navigation manager routes (Blazor specific)
    scanNavigationRoutes: function() {
        // Try to access Blazor's navigation manager
        if (window.DotNet) {
            try {
                // Common Blazor pages that might exist
                const commonRoutes = [
                    '/',
                    '/home',
                    '/counter',
                    '/fetchdata',
                    '/weather',
                    '/about',
                    '/contact',
                    '/login',
                    '/register',
                    '/profile',
                    '/settings',
                    '/dashboard',
                    '/admin',
                    '/users',
                    '/reports',
                    "/Client/ScanPage"
                ];

                // Test each route by attempting navigation (non-destructive check)
                commonRoutes.forEach(route => {
                    if (!this.pages.includes(route)) {
                        this.pages.push(route);
                    }
                });
            } catch (error) {
                console.warn('Could not access navigation routes:', error);
            }
        }
    },

    // Scan for common page patterns in the current app
    scanCommonPagePatterns: function() {
        // Check the current URL path for patterns
        const currentPath = window.location.pathname;
        const pathSegments = currentPath.split('/').filter(s => s);

        // Add current page if not root
        if (currentPath !== '/' && !this.pages.includes(currentPath)) {
            this.pages.push(currentPath);
        }

        // Add root if not present
        if (!this.pages.includes('/')) {
            this.pages.push('/');
        }

        // Check for navigation links in the DOM
        const navLinks = document.querySelectorAll('a[href^="/"], .nav-link[href^="/"]');
        navLinks.forEach(link => {
            const href = link.getAttribute('href');
            if (href && href.startsWith('/') && !href.includes('://') && !href.includes('#')) {
                // Clean up the href (remove query strings and fragments)
                const cleanHref = href.split('?')[0].split('#')[0];
                if (!this.pages.includes(cleanHref)) {
                    this.pages.push(cleanHref);
                }
            }
        });
    },

    // Navigate to a specific page
    navigateToPage: function(route) {
        try {
            if (window.Blazor && window.DotNet) {
                // Use Blazor's navigation
                window.location.href = route;
            } else {
                // Fallback to standard navigation
                window.location.href = route;
            }
            console.log('🧭 Navigating to:', route);
        } catch (error) {
            console.error('Navigation error:', error);
            // Fallback navigation
            window.location.href = route;
        }
    },

    // Create the navigator panel
    createNavigatorPanel: function() {
        // Remove existing panel if present
        const existingPanel = document.getElementById('page-navigator-panel');
        if (existingPanel) {
            existingPanel.remove();
        }

        const panel = document.createElement('div');
        panel.id = 'page-navigator-panel';
        panel.style.cssText = 'position:fixed; top:10px; left:10px; background:#f8f9fa; padding:10px; ' +
            'border:1px solid #dee2e6; z-index:10000; font-size:12px; max-width:300px; ' +
            'max-height:80vh; overflow:auto; opacity:0.95; border-radius:5px; ' +
            'box-shadow:0 2px 10px rgba(0,0,0,0.1); font-family:system-ui,-apple-system,sans-serif;';

        // Header
        const header = document.createElement('div');
        header.style.cssText = 'display:flex; justify-content:space-between; align-items:center; ' +
            'margin-bottom:10px; padding-bottom:8px; border-bottom:1px solid #dee2e6;';

        const title = document.createElement('div');
        title.textContent = 'Page Navigator';
        title.style.cssText = 'font-weight:bold; color:#212529; font-size:14px;';

        const toggleButton = document.createElement('button');
        toggleButton.textContent = '▼';
        toggleButton.style.cssText = 'background:none; border:none; cursor:pointer; font-size:14px; ' +
            'color:#6c757d; padding:0; width:20px; text-align:center;';

        const content = document.createElement('div');
        content.id = 'page-navigator-content';

        toggleButton.addEventListener('click', () => {
            if (content.style.display === 'none') {
                content.style.display = 'block';
                toggleButton.textContent = '▼';
            } else {
                content.style.display = 'none';
                toggleButton.textContent = '►';
            }
        });

        header.appendChild(title);
        header.appendChild(toggleButton);
        panel.appendChild(header);

        // Controls
        const controls = document.createElement('div');
        controls.style.cssText = 'margin-bottom:10px; display:flex; gap:5px; flex-wrap:wrap;';

        const refreshButton = document.createElement('button');
        refreshButton.textContent = 'Refresh';
        refreshButton.style.cssText = 'padding:4px 8px; background:#007bff; color:white; border:none; ' +
            'border-radius:3px; cursor:pointer; font-size:11px;';
        refreshButton.addEventListener('click', () => {
            this.pages = [];
            this.discoverPages();
            this.updatePageList();
        });

        const addPageButton = document.createElement('button');
        addPageButton.textContent = 'Add Page';
        addPageButton.style.cssText = 'padding:4px 8px; background:#28a745; color:white; border:none; ' +
            'border-radius:3px; cursor:pointer; font-size:11px;';
        addPageButton.addEventListener('click', () => {
            const route = prompt('Enter page route (e.g., /mypage):');
            if (route && route.startsWith('/')) {
                if (!this.pages.includes(route)) {
                    this.pages.push(route);
                    this.pages.sort();
                    this.updatePageList();
                }
            }
        });

        controls.appendChild(refreshButton);
        controls.appendChild(addPageButton);
        content.appendChild(controls);

        // Page list container
        const pageList = document.createElement('div');
        pageList.id = 'page-list-container';
        content.appendChild(pageList);

        panel.appendChild(content);
        document.body.appendChild(panel);

        // Initial page list
        this.updatePageList();

        // Make panel draggable
        this.makeDraggable(panel, header);
    },

    // Update the page list display
    updatePageList: function() {
        const container = document.getElementById('page-list-container');
        if (!container) return;

        container.innerHTML = '';

        if (this.pages.length === 0) {
            const emptyMsg = document.createElement('div');
            emptyMsg.textContent = 'No pages discovered';
            emptyMsg.style.cssText = 'color:#6c757d; font-style:italic; padding:10px; text-align:center;';
            container.appendChild(emptyMsg);
            return;
        }

        // Current page indicator
        const currentPath = window.location.pathname;

        this.pages.forEach(route => {
            const pageItem = document.createElement('div');
            pageItem.style.cssText = 'margin:2px 0; display:flex; align-items:center; gap:5px;';

            const pageButton = document.createElement('button');
            pageButton.textContent = route === '/' ? 'Home' : route;
            pageButton.style.cssText = 'flex:1; padding:6px 10px; background:' +
                (currentPath === route ? '#e3f2fd' : '#ffffff') + '; ' +
                'border:1px solid ' + (currentPath === route ? '#2196f3' : '#dee2e6') + '; ' +
                'border-radius:3px; cursor:pointer; text-align:left; font-size:11px; ' +
                'color:' + (currentPath === route ? '#1976d2' : '#212529') + '; ' +
                'font-weight:' + (currentPath === route ? 'bold' : 'normal') + ';';

            pageButton.addEventListener('click', () => {
                this.navigateToPage(route);
            });

            // Remove button
            const removeButton = document.createElement('button');
            removeButton.textContent = '×';
            removeButton.style.cssText = 'width:20px; height:20px; background:#dc3545; color:white; ' +
                'border:none; border-radius:3px; cursor:pointer; font-size:12px; line-height:1;';
            removeButton.title = 'Remove from list';
            removeButton.addEventListener('click', (e) => {
                e.stopPropagation();
                this.pages = this.pages.filter(p => p !== route);
                this.updatePageList();
            });

            pageItem.appendChild(pageButton);
            pageItem.appendChild(removeButton);
            container.appendChild(pageItem);
        });

        // Page count
        const countInfo = document.createElement('div');
        countInfo.textContent = `${this.pages.length} pages found`;
        countInfo.style.cssText = 'margin-top:8px; font-size:10px; color:#6c757d; text-align:center;';
        container.appendChild(countInfo);
    },

    // Make panel draggable
    makeDraggable: function(panel, header) {
        let isDragging = false;
        let currentX;
        let currentY;
        let initialX;
        let initialY;
        let xOffset = 0;
        let yOffset = 0;

        header.addEventListener('mousedown', (e) => {
            if (e.target.tagName === 'BUTTON') return;

            initialX = e.clientX - xOffset;
            initialY = e.clientY - yOffset;

            if (e.target === header || header.contains(e.target)) {
                isDragging = true;
                header.style.cursor = 'grabbing';
            }
        });

        document.addEventListener('mousemove', (e) => {
            if (isDragging) {
                e.preventDefault();
                currentX = e.clientX - initialX;
                currentY = e.clientY - initialY;
                xOffset = currentX;
                yOffset = currentY;

                panel.style.transform = `translate(${currentX}px, ${currentY}px)`;
            }
        });

        document.addEventListener('mouseup', () => {
            initialX = currentX;
            initialY = currentY;
            isDragging = false;
            header.style.cursor = 'grab';
        });

        header.style.cursor = 'grab';
    }
};

// Initialize when DOM is loaded
if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', () => {
        setTimeout(() => window.pageNavigator.init(), 1000);
    });
} else {
    setTimeout(() => window.pageNavigator.init(), 1000);
}

// Also try to initialize when Blazor is ready
window.addEventListener('load', () => {
    setTimeout(() => window.pageNavigator.init(), 2000);
});

// Re-scan pages when navigation occurs
window.addEventListener('popstate', () => {
    if (window.pageNavigator.isInitialized) {
        setTimeout(() => {
            window.pageNavigator.updatePageList();
        }, 100);
    }
});

console.log('Page Navigator script ready');