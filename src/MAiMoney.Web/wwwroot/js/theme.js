// Applies the saved color theme ("light", "dark" or "auto") before the page paints, so there is no flash.
// Loaded in <head>; the account menu changes it through mmTheme.set.
(() => {
    const storageKey = 'mm-theme';
    const media = window.matchMedia('(prefers-color-scheme: dark)');

    const read = () => {
        try {
            return localStorage.getItem(storageKey) || 'auto';
        } catch {
            return 'auto';
        }
    };

    const apply = () => {
        const theme = read();
        const resolved = theme === 'auto' ? (media.matches ? 'dark' : 'light') : theme;
        document.documentElement.setAttribute('data-bs-theme', resolved);
    };

    window.mmTheme = {
        get: read,
        set(theme) {
            try {
                localStorage.setItem(storageKey, theme);
            } catch {
                // Storage can be blocked; the choice then lasts until the next page load.
                document.documentElement.setAttribute('data-bs-theme', theme === 'auto' ? (media.matches ? 'dark' : 'light') : theme);
                return;
            }
            apply();
        },
        apply
    };

    media.addEventListener('change', apply);
    apply();
})();
