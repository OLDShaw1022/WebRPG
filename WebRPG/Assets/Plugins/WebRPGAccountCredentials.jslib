mergeInto(LibraryManager.library, {
    $WebRPGAccountCredentials: {
        registeredUrls: null,
        installed: false,
        wrap: function (original) {
            return function (resource, options) {
                var address = typeof resource === 'string' ? resource : resource.url;
                var method = options && options.method || resource.method || 'GET';
                if (method.toUpperCase() === 'POST' && WebRPGAccountCredentials.registeredUrls.has(address)) {
                    options = Object.assign({}, options, { credentials: 'include' });
                }
                return original.call(this, resource, options);
            };
        }
    },
    WebRPG_RegisterAccountRequest__deps: ['$UTF8ToString', '$WebRPGAccountCredentials'],
    WebRPG_RegisterAccountRequest__sig: 'vi',
    WebRPG_RegisterAccountRequest: function (urlPointer) {
        var address = new URL(UTF8ToString(urlPointer));
        if ((address.protocol !== 'https:' && address.protocol !== 'http:') ||
            !/^\/api\/accounts\/(login|register|refresh|logout)$/.test(address.pathname)) {
            throw new Error('Expected an HTTP account API endpoint.');
        }
        if (!WebRPGAccountCredentials.registeredUrls) WebRPGAccountCredentials.registeredUrls = new Set();
        if (!WebRPGAccountCredentials.installed) {
            if (typeof Module.fetchWithProgress !== 'function') {
                throw new Error('Unity fetchWithProgress is unavailable.');
            }
            Module.fetchWithProgress = WebRPGAccountCredentials.wrap(Module.fetchWithProgress);
            if (typeof Module.cachedFetch === 'function') {
                Module.cachedFetch = WebRPGAccountCredentials.wrap(Module.cachedFetch);
            }
            WebRPGAccountCredentials.installed = true;
        }
        WebRPGAccountCredentials.registeredUrls.add(address.href);
    }
});
