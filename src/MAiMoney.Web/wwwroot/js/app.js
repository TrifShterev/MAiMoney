// Pressing Enter inside an inline sub-form (such as the quick "new category" fields inside the
// transaction form) should not submit the surrounding form; the component handles Enter itself.
document.addEventListener('keydown', event => {
    if (event.key === 'Enter' && event.target instanceof Element && event.target.closest('[data-no-submit-on-enter]')) {
        event.preventDefault();
    }
});
