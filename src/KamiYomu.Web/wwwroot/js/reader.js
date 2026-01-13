/**
 * Global State
 */
let currentZoom = 1.0;
let currentPageIndex = 0;

document.addEventListener("DOMContentLoaded", function () {
    // 1. Scroll to the reader shell on load
    const readerShell = document.getElementById('readerShell');
    if (readerShell) {
        setTimeout(() => {
            readerShell.scrollIntoView({
                behavior: 'smooth',
                block: 'end'
            });
        }, 100);
    }

    // 2. Initialize the Scroll Observer for Webtoon Mode
    initScrollObserver();
});

// Add these variables to the top of your reader.js
let isDown = false;
let startX;
let startY;
let scrollLeft;
let scrollTop;

document.addEventListener("DOMContentLoaded", function () {
    const container = document.getElementById('readerContainer');

    // Mouse Down - Start Grabbing
    container.addEventListener('mousedown', (e) => {
        // Only trigger if middle mouse button or left click is used
        if (e.button !== 0) return;

        isDown = true;
        container.classList.add('grabbing'); // UI feedback

        startX = e.pageX - container.offsetLeft;
        startY = e.pageY - container.offsetTop;
        scrollLeft = container.scrollLeft;
        scrollTop = container.scrollTop;
    });

    // Mouse Leave/Up - Stop Grabbing
    container.addEventListener('mouseleave', () => {
        isDown = false;
        container.classList.remove('grabbing');
    });

    container.addEventListener('mouseup', () => {
        isDown = false;
        container.classList.remove('grabbing');
    });

    // Mouse Move - The actual scroll logic
    container.addEventListener('mousemove', (e) => {
        if (!isDown) return;
        e.preventDefault(); // Stop text selection while dragging

        const x = e.pageX - container.offsetLeft;
        const y = e.pageY - container.offsetTop;

        // Adjust the multiplier (2) to make the scroll faster or slower
        const walkX = (x - startX) * 2;
        const walkY = (y - startY) * 2;

        container.scrollLeft = scrollLeft - walkX;
        container.scrollTop = scrollTop - walkY;
    });
});

/**
 * Detects which page is visible during scroll (Webtoon Mode)
 */
function initScrollObserver() {
    const container = document.getElementById('readerContainer');
    const observerOptions = {
        root: container,
        threshold: 0.5 // Trigger when 50% of a page is visible
    };

    const observer = new IntersectionObserver((entries) => {
        // Only track scroll position if we are in webtoon mode
        if (!container.classList.contains('webtoon-mode')) return;

        entries.forEach(entry => {
            if (entry.isIntersecting) {
                const index = parseInt(entry.target.getAttribute('data-page-index'));
                currentPageIndex = index - 1;
                updatePageDisplay(index);
            }
        });
    }, observerOptions);

    document.querySelectorAll('.manga-page-wrapper').forEach(wrapper => {
        observer.observe(wrapper);
    });
}

/**
 * Layout & View Modes
 */
function changeMode(mode) {
    const container = document.getElementById('readerContainer');

    // Instead of className = '', we remove only the "mode" classes
    container.classList.remove('webtoon-mode', 'paged-mode', 'rtl-mode');

    currentPageIndex = 0; // Reset to first page

    if (mode === 'webtoon') {
        container.classList.add('webtoon-mode');
        // Ensure webtoon mode uses vertical block display
        container.style.display = 'block';
        container.scrollTo(0, 0);
    } else {
        container.classList.add('paged-mode');
        // Ensure paged mode uses flex for horizontal alignment
        container.style.display = 'flex';
        if (mode === 'rtl') container.classList.add('rtl-mode');
        updatePagedView();
    }

    // Always sync the UI after a mode change
    updatePageDisplay(1);
}

/**
 * Zoom Logic
 */
function adjustZoom(delta) {
    currentZoom = Math.min(Math.max(0.5, currentZoom + delta), 2.0);
    const zoomInput = document.getElementById('zoomVal');
    if (zoomInput) zoomInput.value = Math.round(currentZoom * 100) + "%";

    const container = document.getElementById('readerContainer');
    if (container.classList.contains('webtoon-mode')) {
        const imgs = container.querySelectorAll('img');
        imgs.forEach(img => img.style.maxWidth = (800 * currentZoom) + "px");
    } else {
        // In paged mode, we use CSS zoom or transform
        container.style.zoom = currentZoom;
    }
}

/**
 * Fullscreen API
 */
function toggleFullscreen() {
    const elem = document.getElementById('mainViewer');

    if (!document.fullscreenElement &&
        !document.webkitFullscreenElement &&
        !document.msFullscreenElement) {

        if (elem.requestFullscreen) {
            elem.requestFullscreen();
        } else if (elem.webkitRequestFullscreen) {
            elem.webkitRequestFullscreen();
        } else if (elem.msRequestFullscreen) {
            elem.msRequestFullscreen();
        }
    } else {
        if (document.exitFullscreen) {
            document.exitFullscreen();
        } else if (document.webkitExitFullscreen) {
            document.webkitExitFullscreen();
        } else if (document.msExitFullscreen) {
            document.msExitFullscreen();
        }
    }
}

document.addEventListener('fullscreenchange', handleFullscreenUI);
document.addEventListener('webkitfullscreenchange', handleFullscreenUI);

function handleFullscreenUI() {
    const fsBtn = document.querySelector('.bi-fullscreen') || document.querySelector('.bi-fullscreen-exit');
    if (!fsBtn) return;

    if (document.fullscreenElement) {
        fsBtn.classList.replace('bi-fullscreen', 'bi-fullscreen-exit');
    } else {
        fsBtn.classList.replace('bi-fullscreen-exit', 'bi-fullscreen');
    }
}

/**
 * Page Navigation logic
 */
function updatePagedView() {
    const container = document.getElementById('readerContainer');
    if (!container.classList.contains('paged-mode')) return;

    const pages = document.querySelectorAll('.manga-page-wrapper');
    pages[currentPageIndex].scrollIntoView({ behavior: 'smooth', block: 'nearest', inline: 'start' });
}

function updatePageDisplay(index) {
    const display = document.getElementById('currentPageDisplay');
    if (display) display.innerText = index;

    // Fixed: Calculate total pages from DOM instead of Razor @Model
    const pages = document.querySelectorAll('.manga-page-wrapper');
    const totalPages = pages.length;
    const percentage = (index / (totalPages || 1)) * 100;

    const progressBar = document.getElementById('readerProgressBar');
    if (progressBar) {
        progressBar.style.width = percentage + "%";
    }
}

function nextPage() {
    const pages = document.querySelectorAll('.manga-page-wrapper');
    if (currentPageIndex < pages.length - 1) {
        currentPageIndex++;
        scrollToPage(currentPageIndex);
    }
}

function prevPage() {
    if (currentPageIndex > 0) {
        currentPageIndex--;
        scrollToPage(currentPageIndex);
    }
}

function scrollToPage(index) {
    const pages = document.querySelectorAll('.manga-page-wrapper');
    const targetPage = pages[index];

    if (targetPage) {
        targetPage.scrollIntoView({
            behavior: 'smooth',
            block: 'start',
            inline: 'center'
        });
        updatePageDisplay(index + 1);
    }
}
