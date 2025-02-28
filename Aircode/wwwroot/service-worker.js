// In development, always fetch from the network and do not enable offline support.
// This is because caching would make development more difficult (changes would not
// be reflected on the first load after each change).
self.addEventListener('fetch', () => { });
navigator.serviceWorker.register('./service-worker.js', {scope: './'}).then(r => console.log('Service worker registered', r)).catch(err => console.error('Service worker registration failed', err)); 