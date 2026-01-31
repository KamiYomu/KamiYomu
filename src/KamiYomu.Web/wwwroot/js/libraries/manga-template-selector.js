function applyMangaSelection(btnElement, name, source, isIcon) {
    // 1. Update the Dropdown Visuals
    const container = document.getElementById('selected-manga-container');
    let visualHtml = isIcon
        ? `<i class="bi ${source} me-2 me-md-3" style="font-size: 1.4rem;"></i>`
        : `<img src="${source}" style="width: 32px; height: 44px; object-fit: cover;" class="me-2 me-md-3 rounded shadow-sm">`;
    container.innerHTML = `${visualHtml} <span class="fw-bold text-truncate">${name}</span>`;

    // 2. Get the template values from the clicked <li>
    const parentLi = btnElement.closest('li');
    const data = {
        path: parentLi.getAttribute('data-file-path'),
        series: parentLi.getAttribute('data-series'),
        title: parentLi.getAttribute('data-title')
    };

    // 3. Find the TARGET elements using the selectors passed to the ViewComponent
    const selectors = {
        path: document.getElementById('selector-filepath').value,
        series: document.getElementById('selector-series').value,
        title: document.getElementById('selector-title').value
    };

    // 4. Update the target inputs
    updateTargetInput(selectors.path, data.path);
    updateTargetInput(selectors.series, data.series);
    updateTargetInput(selectors.title, data.title);
}

function updateTargetInput(selector, value) {
    if (!selector) return;

    // Use querySelector to find the input by the name attribute passed in the VC
    const input = document.querySelector(selector);
    if (input) {
        input.value = value ?? '';

        input.dispatchEvent(new Event('input', { bubbles: true }));

        input.dispatchEvent(new Event('change', { bubbles: true }));
        input.dispatchEvent(new KeyboardEvent('keyup', {
            bubbles: true,
            cancelable: true,
            key: "Enter" // Simulating a key release
        }));

        console.log(`Programmatically triggered HTMX for: ${selector}`);
    }
}
