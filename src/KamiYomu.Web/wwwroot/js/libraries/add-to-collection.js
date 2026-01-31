function resetfilePathTemplate() {
    const input = document.getElementById('file-path-template-input');
    const def = input.getAttribute('data-default');

    if (!input || !def) return;

    input.value = def;
    input.dispatchEvent(new Event('input', { bubbles: true }));

    input.dispatchEvent(new Event('change', { bubbles: true }));
    input.dispatchEvent(new KeyboardEvent('keyup', {
        bubbles: true,
        cancelable: true,
        key: "Enter"
    }));
}

function resetComicInfoTitleTemplate() {
    const input = document.getElementById('comic-info-title-template-input');
    const def = input.getAttribute('data-default');
    if (!input || !def) return;

    input.value = def;
    input.dispatchEvent(new Event('input', { bubbles: true }));

    input.dispatchEvent(new Event('change', { bubbles: true }));
    input.dispatchEvent(new KeyboardEvent('keyup', {
        bubbles: true,
        cancelable: true,
        key: "Enter" 
    }));
}

function resetComicInfoSeriesTemplate() {
    const input = document.getElementById('comic-info-series-template-input');
    const def = input.getAttribute('data-default');

    if (!input || !def) return;

    input.value = def;
    htmx.trigger(input, 'change');
}
