function initPopovers() {
    const popoverTriggerList = document.querySelectorAll('[data-bs-toggle="popover"]');
    [...popoverTriggerList].map(popoverTriggerEl => new bootstrap.Popover(popoverTriggerEl));
}

if (!window.familySafeModeInitialized) {

    document.addEventListener('htmx:configRequest', (event) => {
        const returnUrlInput = document.getElementById('family-safe-return-url');
        if (returnUrlInput) {
            returnUrlInput.value = window.location.pathname + window.location.search;
        }
    });

    document.addEventListener('DOMContentLoaded', function () {
        initPopovers();
    });

    document.body.addEventListener('htmx:afterSwap', function (e) {
        initPopovers();
    });

    window.familySafeModeInitialized = true;
}
