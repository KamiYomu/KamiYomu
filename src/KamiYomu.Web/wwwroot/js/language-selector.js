if (!window.languageSelectorInitialized) {

    document.addEventListener('htmx:configRequest', (event) => {
        const returnUrlInputs = document.querySelectorAll('.language-return-url');

        returnUrlInputs.forEach((input) => {
            input.value = window.location.pathname + window.location.search;
        });
    });

    window.languageSelectorInitialized = true;
}
