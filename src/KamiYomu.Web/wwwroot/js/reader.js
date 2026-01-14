/**
 * Global State
 */
let currentZoom = 1.0;
let currentPageIndex = 0;
let isDown = false;
let startX, startY, scrollLeft, scrollTop;

document.addEventListener("DOMContentLoaded", function () {
    const container = document.getElementById('readerContainer');
    const readerShell = document.getElementById('readerShell');

    // 1. Initial Scroll to Reader Shell
    if (readerShell) {
        setTimeout(() => {
            readerShell.scrollIntoView({
                behavior: 'smooth',
                block: 'end'
            });
        }, 150);
    }

    // 2. Initialize Grab-to-Scroll Logic
    if (container) {
        container.addEventListener('mousedown', (e) => {
            if (e.button !== 0) return; // Only left click
            isDown = true;
            container.classList.add('grabbing');
            startX = e.pageX - container.offsetLeft;
            startY = e.pageY - container.offsetTop;
            scrollLeft = container.scrollLeft;
            scrollTop = container.scrollTop;
        });

        container.addEventListener('mouseleave', () => {
            isDown = false;
            container.classList.remove('grabbing');
        });

        container.addEventListener('mouseup', () => {
            isDown = false;
            container.classList.remove('grabbing');
        });

        container.addEventListener('mousemove', (e) => {
            if (!isDown) return;
            e.preventDefault();
            const x = e.pageX - container.offsetLeft;
            const y = e.pageY - container.offsetTop;
            const walkX = (x - startX) * 2;
            const walkY = (y - startY) * 2;
            container.scrollLeft = scrollLeft - walkX;
            container.scrollTop = scrollTop - walkY;
        });
    }

    // 3. Start watching page positions
    initScrollObserver();
});

/**
 * Intersection Observer: Updates the Page Number based on scroll position
 */
function initScrollObserver() {
    const container = document.getElementById('readerContainer');
    if (!container) return;

    // Use a lower threshold (0.2) so the index updates as soon as a page is partly visible
    const observerOptions = {
        root: container,
        threshold: 0.2,
        rootMargin: '0px 0px -10% 0px'
    };

    const observer = new IntersectionObserver((entries) => {
        entries.forEach(entry => {
            if (entry.isIntersecting) {
                const indexAttr = entry.target.getAttribute('data-page-index');
                if (indexAttr) {
                    const index = parseInt(indexAttr);
                    currentPageIndex = index - 1;
                    updatePageDisplay(index);
                }
            }
        });
    }, observerOptions);

    // Observe all wrappers
    document.querySelectorAll('.manga-page-wrapper').forEach(wrapper => {
        observer.unobserve(wrapper); // Clear previous observation to prevent duplicates
        observer.observe(wrapper);
    });
}

/**
 * Layout & View Modes
 */
function changeMode(mode) {
    const container = document.getElementById('readerContainer');
    if (!container) return;

    // Remove only mode classes, preserve structural classes like flex-grow-1
    container.classList.remove('webtoon-mode', 'paged-mode', 'rtl-mode');
    currentPageIndex = 0;

    if (mode === 'webtoon') {
        container.classList.add('webtoon-mode');
        container.style.display = 'flex'; // Centering requires flex
    } else {
        container.classList.add('paged-mode');
        container.style.display = 'flex';
        if (mode === 'rtl') container.classList.add('rtl-mode');
    }

    // Re-sync UI and Observer for the new layout
    setTimeout(() => {
        initScrollObserver();
        updatePageDisplay(1);
        container.scrollTo(0, 0);
    }, 50);
}

/**
 * Zoom Logic (Webtoon = MaxWidth, Paged = CSS Zoom)
 */
function adjustZoom(delta) {
    currentZoom = Math.min(Math.max(0.3, currentZoom + delta), 2.0);

    const zoomInput = document.getElementById('zoomVal');
    if (zoomInput) zoomInput.value = Math.round(currentZoom * 100) + "%";

    const container = document.getElementById('readerContainer');
    const imgs = container.querySelectorAll('.reader-img');
    const baseWidth = 800;

    imgs.forEach(img => {
        img.style.maxWidth = (baseWidth * currentZoom) + "px";
    });
}

/**
 * Fullscreen API
 */
function toggleFullscreen() {
    const elem = document.getElementById('mainViewer');
    if (!document.fullscreenElement) {
        if (elem.requestFullscreen) elem.requestFullscreen();
        else if (elem.webkitRequestFullscreen) elem.webkitRequestFullscreen();
        else if (elem.msRequestFullscreen) elem.msRequestFullscreen();
    } else {
        if (document.exitFullscreen) document.exitFullscreen();
    }
}

document.addEventListener('fullscreenchange', handleFullscreenUI);

function handleFullscreenUI() {
    const fsBtn = document.querySelector('.bi-fullscreen') || document.querySelector('.bi-fullscreen-exit');
    if (!fsBtn) return;
    if (document.fullscreenElement) fsBtn.classList.replace('bi-fullscreen', 'bi-fullscreen-exit');
    else fsBtn.classList.replace('bi-fullscreen-exit', 'bi-fullscreen');
}

/**
 * Footer & Page Indicators
 */
function updatePageDisplay(index) {
    const display = document.getElementById('currentPageDisplay');
    if (display) display.innerText = index;

    const totalPages = document.querySelectorAll('.manga-page-wrapper').length;
    const percentage = (index / (totalPages || 1)) * 100;

    const progressBar = document.getElementById('readerProgressBar');
    if (progressBar) progressBar.style.width = percentage + "%";
}

/**
 * Navigation Actions
 */
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
