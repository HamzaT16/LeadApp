// The only first-party script. Loaded from this origin (CSP script-src 'self').
window.leadApp = {
    copyText: async function (text) {
        try {
            await navigator.clipboard.writeText(text);
            return true;
        } catch {
            return false;
        }
    }
};
