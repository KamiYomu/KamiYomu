if (!window.familySafeModeInitialized) {

    document.addEventListener('htmx:configRequest', (event) => {
        const returnUrlInput = document.getElementById('family-safe-return-url');
        if (returnUrlInput) {
            returnUrlInput.value = window.location.pathname + window.location.search;
        }
    });

    document.addEventListener('DOMContentLoaded', function () {
        const popoverTriggerList = [].slice.call(document.querySelectorAll('[data-bs-toggle="popover"]'));
        popoverTriggerList.forEach(function (el) {
            new bootstrap.Popover(el);
        });
    });

    window.familySafeModeInitialized = true;
}
