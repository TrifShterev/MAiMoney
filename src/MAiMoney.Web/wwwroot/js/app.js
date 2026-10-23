// Pressing Enter inside an inline sub-form (such as the quick "new category" fields inside the
// transaction form) should not submit the surrounding form; the component handles Enter itself.
document.addEventListener('keydown', event => {
    if (event.key === 'Enter' && event.target instanceof Element && event.target.closest('[data-no-submit-on-enter]')) {
        event.preventDefault();
    }
});

// Keyboard shortcuts for the header's quick-add menu. MainLayout registers itself once it is interactive.
window.mmShortcuts = {
    target: null,
    register(dotNetRef) {
        this.target = dotNetRef;
    }
};

document.addEventListener('keydown', event => {
    const target = window.mmShortcuts.target;
    if (!target || event.repeat || event.ctrlKey || event.metaKey || event.altKey || event.key.toLowerCase() !== 'n') {
        return;
    }

    // Leave typing alone, and don't stack a second dialog on an open one.
    const el = event.target instanceof Element ? event.target : null;
    if (el?.closest('input, textarea, select, [contenteditable="true"]') || document.querySelector('.modal')) {
        return;
    }

    event.preventDefault();
    target.invokeMethodAsync('OpenQuickAddExpense').catch(() => { window.mmShortcuts.target = null; });
});

// Enhanced navigation between the account pages swaps the document; keep the chosen theme.
Blazor.addEventListener('enhancedload', () => window.mmTheme?.apply());
