export class ContactUs {
    static initializeContactAnimations() {
        // Initialize any contact page animations
        const sections = document.querySelectorAll('.contact-section');
        sections.forEach((section, index) => {
            section.style.opacity = '0';
            section.style.transform = 'translateY(20px)';
            setTimeout(() => {
                section.style.transition = 'opacity 0.3s ease, transform 0.3s ease';
                section.style.opacity = '1';
                section.style.transform = 'translateY(0)';
            }, index * 100);
        });

        // Initialize FAQ accordions
        const faqItems = document.querySelectorAll('.faq-item');
        faqItems.forEach(item => {
            item.addEventListener('click', (e) => {
                const content = item.querySelector('.faq-content');
                if (content) {
                    content.style.transition = 'max-height 0.3s ease';
                }
            });
        });
    }

    static navigateToPage(page) {
        // Navigate to specific page
        window.location.href = `/${page}`;
    }

    static checkInternetConnection() {
        if (navigator.onLine) {
            alert('Internet connection is active');
        } else {
            alert('No internet connection detected');
        }
    }

    static clearBrowserCache() {
        if ('caches' in window) {
            caches.keys().then(names => {
                names.forEach(name => {
                    caches.delete(name);
                });
            });
        }
        alert('Cache cleared. Please refresh the page.');
    }

    static openLiveChat() {
        // Open live chat widget or redirect to chat service
        console.log('Opening live chat...');
        // Implement your live chat integration here
    }

    static navigateToGuide(guideId) {
        // Navigate to specific guide
        window.location.href = `/guides/${guideId}`;
    }

    static submitContactForm(formData) {
        // Submit contact form to API
        return fetch('/api/contact', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
            },
            body: JSON.stringify(formData)
        });
    }

    static submitErrorReport(reportData) {
        // Submit error report to API
        return fetch('/api/error-report', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
            },
            body: JSON.stringify(reportData)
        });
    }

    static getUserAgent() {
        return navigator.userAgent;
    }
}

// Global function exports for Blazor interop
window.initializeContactAnimations = ContactUs.initializeContactAnimations;
window.navigateToPage = ContactUs.navigateToPage;
window.checkInternetConnection = ContactUs.checkInternetConnection;
window.clearBrowserCache = ContactUs.clearBrowserCache;
window.openLiveChat = ContactUs.openLiveChat;
window.navigateToGuide = ContactUs.navigateToGuide;
window.submitContactForm = ContactUs.submitContactForm;
window.submitErrorReport = ContactUs.submitErrorReport;
window.getUserAgent = ContactUs.getUserAgent;

window.ContactUs = ContactUs;