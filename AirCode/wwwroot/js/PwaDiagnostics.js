// PWA Diagnostic Script - Add to debug.js or run in console
class PWADiagnostics {
    static async runFullDiagnostic() {
        console.log('🔍 Starting PWA Diagnostic...');
        console.log('='.repeat(50));

        const results = {
            environment: this.checkEnvironment(),
            manifest: await this.checkManifest(),
            serviceWorker: await this.checkServiceWorker(),
            cache: await this.checkCache(),
            blazor: await this.checkBlazorFramework(),
            networking: await this.checkNetworking(),
            pwaManager: this.checkPWAManager()
        };

        this.displayResults(results);
        return results;
    }

    static checkEnvironment() {
        const env = {
            isHTTPS: location.protocol === 'https:' || location.hostname === 'localhost',
            userAgent: navigator.userAgent,
            isGitHubPages: location.hostname === 'mid-d-man.github.io',
            basePath: location.hostname === 'mid-d-man.github.io' ? '/AirCode/' : '/',
            displayMode: window.matchMedia('(display-mode: standalone)').matches ? 'standalone' : 'browser',
            online: navigator.onLine,
            serviceWorkerSupport: 'serviceWorker' in navigator,
            manifestSupport: 'manifest' in document.head,
            currentURL: location.href,
            referrer: document.referrer
        };

        console.log('🌍 Environment Check:', env);
        return env;
    }

    static async checkManifest() {
        const manifestLink = document.querySelector('link[rel="manifest"]');
        if (!manifestLink) {
            console.error('❌ No manifest link found');
            return { error: 'No manifest link' };
        }

        try {
            const response = await fetch(manifestLink.href);
            const manifest = await response.json();
            console.log('📱 Manifest loaded:', manifest);
            return { success: true, manifest };
        } catch (error) {
            console.error('❌ Manifest load failed:', error);
            return { error: error.message };
        }
    }

    static async checkServiceWorker() {
        if (!('serviceWorker' in navigator)) {
            return { error: 'Service Worker not supported' };
        }

        try {
            const registration = await navigator.serviceWorker.getRegistration();
            const swInfo = {
                hasRegistration: !!registration,
                scope: registration?.scope,
                state: registration?.active?.state,
                scriptURL: registration?.active?.scriptURL,
                updateViaCache: registration?.updateViaCache,
                waiting: !!registration?.waiting,
                installing: !!registration?.installing
            };

            console.log('🔧 Service Worker Status:', swInfo);

            // Test cache status
            if (registration) {
                const channel = new MessageChannel();
                registration.active?.postMessage({ type: 'GET_CACHE_STATUS' }, [channel.port2]);

                const cacheStatus = await new Promise(resolve => {
                    channel.port1.onmessage = event => resolve(event.data);
                    setTimeout(() => resolve({ timeout: true }), 3000);
                });

                swInfo.cacheStatus = cacheStatus;
            }

            return swInfo;
        } catch (error) {
            console.error('❌ Service Worker check failed:', error);
            return { error: error.message };
        }
    }

    static async checkCache() {
        if (!('caches' in window)) {
            return { error: 'Cache API not supported' };
        }

        try {
            const cacheNames = await caches.keys();
            const cacheInfo = { names: cacheNames };

            if (cacheNames.length > 0) {
                const cache = await caches.open(cacheNames[0]);
                const keys = await cache.keys();
                cacheInfo.entries = keys.length;
                cacheInfo.urls = keys.slice(0, 10).map(req => req.url); // First 10 URLs
            }

            console.log('💾 Cache Status:', cacheInfo);
            return cacheInfo;
        } catch (error) {
            console.error('❌ Cache check failed:', error);
            return { error: error.message };
        }
    }

    static async checkBlazorFramework() {
        const blazorInfo = {
            blazorLoaded: typeof window.Blazor !== 'undefined',
            dotnetLoaded: typeof window.DotNet !== 'undefined',
            blazorScript: !!document.querySelector('script[src*="blazor.webassembly.js"]'),
            authScript: !!document.querySelector('script[src*="AuthenticationService.js"]')
        };

        // Check critical framework files
        const criticalFiles = [
            '_framework/blazor.webassembly.js',
            '_framework/blazor.boot.json',
            '_framework/dotnet.wasm'
        ];

        const fileChecks = await Promise.allSettled(
            criticalFiles.map(async file => {
                try {
                    const response = await fetch(file, { method: 'HEAD' });
                    return { file, status: response.status, ok: response.ok };
                } catch (error) {
                    return { file, error: error.message };
                }
            })
        );

        blazorInfo.criticalFiles = fileChecks.map(result =>
            result.status === 'fulfilled' ? result.value : result.reason
        );

        console.log('⚡ Blazor Framework Check:', blazorInfo);
        return blazorInfo;
    }

    static async checkNetworking() {
        const networkInfo = {
            online: navigator.onLine,
            connection: navigator.connection ? {
                effectiveType: navigator.connection.effectiveType,
                downlink: navigator.connection.downlink,
                rtt: navigator.connection.rtt
            } : 'Not available'
        };

        // Test network connectivity with key resources
        const testUrls = [
            '',  // Base URL
            'manifest.json',
            '_framework/blazor.boot.json'
        ];

        const connectivityTests = await Promise.allSettled(
            testUrls.map(async url => {
                const startTime = performance.now();
                try {
                    const response = await fetch(url, {
                        method: 'HEAD',
                        cache: 'no-cache',
                        signal: AbortSignal.timeout(5000)
                    });
                    const duration = performance.now() - startTime;
                    return { url: url || '/', status: response.status, duration: Math.round(duration) };
                } catch (error) {
                    const duration = performance.now() - startTime;
                    return { url: url || '/', error: error.message, duration: Math.round(duration) };
                }
            })
        );

        networkInfo.connectivity = connectivityTests.map(result =>
            result.status === 'fulfilled' ? result.value : result.reason
        );

        console.log('🌐 Network Check:', networkInfo);
        return networkInfo;
    }

    static checkPWAManager() {
        const pwaInfo = {
            pwaManagerExists: typeof window.pwaManager !== 'undefined',
            pwaManagerClass: typeof window.UnifiedPWAManager !== 'undefined',
            getPWAManager: typeof window.getPWAManager === 'function',
            setupPWAMonitoring: typeof window.setupPWAMonitoring === 'function'
        };

        if (window.pwaManager) {
            pwaInfo.status = window.pwaManager.getStatus?.();
            pwaInfo.initialized = window.pwaManager.initialized;
        }

        console.log('📱 PWA Manager Check:', pwaInfo);
        return pwaInfo;
    }

    static displayResults(results) {
        console.log('\n' + '='.repeat(50));
        console.log('📊 DIAGNOSTIC SUMMARY');
        console.log('='.repeat(50));

        // Environment
        const env = results.environment;
        console.log(`🌍 Environment: ${env.isHTTPS ? '✅' : '❌'} HTTPS | ${env.serviceWorkerSupport ? '✅' : '❌'} SW Support`);
        console.log(`   Display Mode: ${env.displayMode} | Online: ${env.online ? '✅' : '❌'}`);

        // Service Worker
        const sw = results.serviceWorker;
        if (sw.error) {
            console.log(`🔧 Service Worker: ❌ ${sw.error}`);
        } else {
            console.log(`🔧 Service Worker: ${sw.hasRegistration ? '✅' : '❌'} Registered | State: ${sw.state || 'Unknown'}`);
            if (sw.cacheStatus && !sw.cacheStatus.timeout) {
                console.log(`   Cache: ${sw.cacheStatus.criticalCached}/${sw.cacheStatus.criticalTotal} critical assets`);
            }
        }

        // Blazor
        const blazor = results.blazor;
        console.log(`⚡ Blazor: ${blazor.blazorLoaded ? '✅' : '❌'} Loaded | ${blazor.dotnetLoaded ? '✅' : '❌'} .NET`);

        const criticalOk = blazor.criticalFiles?.filter(f => f.ok).length || 0;
        const criticalTotal = blazor.criticalFiles?.length || 0;
        console.log(`   Critical Files: ${criticalOk}/${criticalTotal} available`);

        // PWA Manager
        const pwa = results.pwaManager;
        console.log(`📱 PWA Manager: ${pwa.pwaManagerExists ? '✅' : '❌'} Loaded | ${pwa.initialized ? '✅' : '❌'} Initialized`);

        // Issues Summary
        console.log('\n🔍 POTENTIAL ISSUES:');
        const issues = [];

        if (!env.isHTTPS) issues.push('Not served over HTTPS');
        if (!env.serviceWorkerSupport) issues.push('Service Worker not supported');
        if (!sw.hasRegistration && !sw.error) issues.push('Service Worker not registered');
        if (!blazor.blazorLoaded) issues.push('Blazor framework not loaded');
        if (criticalOk < criticalTotal) issues.push(`Missing ${criticalTotal - criticalOk} critical Blazor files`);
        if (!pwa.pwaManagerExists) issues.push('PWA Manager not loaded');

        if (issues.length === 0) {
            console.log('✅ No obvious issues detected');
        } else {
            issues.forEach(issue => console.log(`❌ ${issue}`));
        }

        console.log('\n💡 Run PWADiagnostics.runFullDiagnostic() again to re-check');
    }

    // Quick fix attempts
    static async attemptFixes() {
        console.log('🔧 Attempting automatic fixes...');

        // Force service worker update
        if ('serviceWorker' in navigator) {
            try {
                const reg = await navigator.serviceWorker.getRegistration();
                if (reg) {
                    await reg.update();
                    console.log('✅ Service Worker update triggered');
                }
            } catch (error) {
                console.log('❌ Service Worker update failed:', error.message);
            }
        }

        // Clear caches and reload
        if ('caches' in window) {
            try {
                const names = await caches.keys();
                await Promise.all(names.map(name => caches.delete(name)));
                console.log('✅ Caches cleared');

                console.log('🔄 Reloading page in 2 seconds...');
                setTimeout(() => location.reload(), 2000);
            } catch (error) {
                console.log('❌ Cache clear failed:', error.message);
            }
        }
    }
}

// Auto-run diagnostic if there are obvious issues
window.addEventListener('load', () => {
    setTimeout(() => {
        const hasBlazor = typeof window.Blazor !== 'undefined';
        const hasPWAManager = typeof window.pwaManager !== 'undefined';

        if (!hasBlazor || !hasPWAManager) {
            console.log('🚨 Potential initialization issues detected. Run PWADiagnostics.runFullDiagnostic() for details.');
        }
    }, 3000);
});

// Make available globally
window.PWADiagnostics = PWADiagnostics;