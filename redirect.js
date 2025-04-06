// SPA redirect script for direct navigation to routes
(function() {
  const isGitHubPages = window.location.hostname.includes('github.io');
  if (!isGitHubPages) return;

  const pathSegments = window.location.pathname.split('/');
  const repoName = pathSegments[1];
  const basePath = '/' + repoName + '/';

  // Check if this is a direct navigation to a route
  if (window.location.pathname !== basePath && 
      !window.location.pathname.endsWith('/index.html') && 
      !window.location.pathname.endsWith('/404.html')) {

      const routePath = window.location.pathname.substring(basePath.length - 1);
      sessionStorage.setItem('navigationPath', routePath);
      console.log('Stored navigation path for redirect:', routePath);
  }
})();
