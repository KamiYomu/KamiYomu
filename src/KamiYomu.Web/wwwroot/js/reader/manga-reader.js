/**
 * KamiYomu Reader Engine
 * Focus: High-performance, private, self-hosted manga curation.
 */

const getInitialState = () => ({
    mode: 'webtoon', // 'webtoon', 'rtl', 'ltr'
    currentZoom: 1.0,
    currentPageIndex: 0,
    isDown: false,
    startX: 0,
    startY: 0,
    scrollLeft: 0,
    scrollTop: 0,
    baseWidth: 800,
    currentObserver: null,
    isUserScrolling: false // Guard flag to prevent observer firing on load
});

const readerState = {
    ...getInitialState(),

    // --- Helpers & Logic ---
    isWebtoonMode() { return this.mode === 'webtoon'; },
    isPagedRtlMode() { return this.mode === 'rtl'; },
    isPagedLtrMode() { return this.mode === 'ltr'; },

    reset() {
        if (this.currentObserver) this.currentObserver.disconnect();
        Object.assign(this, getInitialState());

        setTimeout(() => {
            this.changeMode(this.mode);
        }, 50);
    },

    // --- Core UI Management ---
    changeMode(mode) {
        const container = document.getElementById('readerContainer');
        if (!container) return;
        this.mode = mode;

        container.classList.remove('webtoon-mode', 'paged-mode', 'rtl-mode', 'ltr-mode');
        this.currentPageIndex = 0;
        this.isUserScrolling = false; // Reset interaction flag on mode change

        if (this.isWebtoonMode()) {
            container.classList.add('webtoon-mode');
        } else {
            container.classList.add('paged-mode');
            if (this.isPagedRtlMode()) container.classList.add('rtl-mode');
            else if (this.isPagedLtrMode()) container.classList.add('ltr-mode');
        }

        setTimeout(() => {
            this.initScrollObserver();
            this.updatePageDisplay(1);
            container.scrollTo(0, 0);
        }, 50);
    },

    // --- Zoom Engine ---
    adjustZoom(delta) {
        this.currentZoom = Math.min(Math.max(0.3, this.currentZoom + delta), 2.0);
        this.applyZoom();
    },

    applyZoom() {
        const container = document.getElementById('readerContainer');
        const imgs = container?.querySelectorAll('.reader-img');
        const zoomInput = document.getElementById('zoomVal');

        if (zoomInput) zoomInput.value = Math.round(this.currentZoom * 100) + "%";

        imgs?.forEach(img => {
            img.style.width = (this.baseWidth * this.currentZoom) + "px";
            img.style.height = "auto";
            img.style.maxWidth = "none";
        });
    },

    setInitialFit() {
        const container = document.getElementById('readerContainer');
        if (!container) return;

        if (container.classList.contains('paged-mode')) {
            const targetHeight = container.clientHeight * 0.95;
            const sampleImg = container.querySelector('.reader-img');
            const referenceHeight = (sampleImg && sampleImg.naturalHeight > 0) ? sampleImg.naturalHeight : 1130;
            this.currentZoom = targetHeight / referenceHeight;
        } else {
            this.currentZoom = container.clientWidth / this.baseWidth;
        }
        this.applyZoom();
    },

    // --- Navigation ---
    nextPage() {
        const pages = document.querySelectorAll('.manga-page-wrapper');
        if (this.currentPageIndex < pages.length - 1) {
            this.isUserScrolling = true; // Allow update
            this.currentPageIndex++;
            this.scrollToPage(this.currentPageIndex);
        }
    },

    prevPage() {
        if (this.currentPageIndex > 0) {
            this.isUserScrolling = true; // Allow update
            this.currentPageIndex--;
            this.scrollToPage(this.currentPageIndex);
        }
    },

    scrollToPage(index) {
        const container = document.getElementById('readerContainer');
        const pages = document.querySelectorAll('.manga-page-wrapper');
        const targetPage = pages[index];

        if (targetPage && container) {
            const isPaged = container.classList.contains('paged-mode');
            const scrollConfig = { behavior: 'smooth' };

            if (isPaged) {
                scrollConfig.left = targetPage.offsetLeft - container.offsetLeft;
            } else {
                scrollConfig.top = targetPage.offsetTop - container.offsetTop;
            }

            container.scrollTo(scrollConfig);
            this.updatePageDisplay(index + 1);
        }
    },

    scrollToStart() {
        const container = document.getElementById("readerContainer");
        if (container) {
            this.isUserScrolling = true;
            container.scrollTo({ top: 0, left: 0, behavior: 'smooth' });
            container.querySelectorAll(".manga-page-wrapper").forEach(el => el.dataset.fired = "false");
        }
    },

    // --- Observers & Feedback ---
    async initScrollObserver() {
        const container = document.getElementById('readerContainer');
        if (!container) return;

        if (this.currentObserver) this.currentObserver.disconnect();

        // 1. Wait for images to stabilize layout
        const images = container.querySelectorAll('.reader-img');
        await this.waitForImages(images);

        // 2. Enable updates only after user interacts
        const enableUpdates = () => { this.isUserScrolling = true; };
        container.addEventListener('scroll', enableUpdates, { once: true });
        container.addEventListener('wheel', enableUpdates, { once: true });
        container.addEventListener('touchstart', enableUpdates, { once: true });

        const isPaged = container.classList.contains('paged-mode');

        this.currentObserver = new IntersectionObserver((entries) => {
            entries.forEach(entry => {
                if (this.isUserScrolling) {
                    if (entry.isIntersecting) {
                        const indexAttr = entry.target.getAttribute('data-page-index');
                        if (indexAttr) {
                            const index = parseInt(indexAttr);
                            this.currentPageIndex = index - 1;
                            this.updatePageDisplay(index);
                        }
                    }
                    else if (entry.boundingClientRect.top < 0) {

                        if (entry.target.dataset.fired === "true") return;

                        entry.target.dataset.fired = "true";
                        htmx.trigger(entry.target, "page-passed");
                    }
                }
                
            });
        }, {
            root: container,
            threshold: isPaged ? 0.5 : 0.1,
            rootMargin: '0px'
        });

        const targets = document.querySelectorAll('.manga-page-wrapper');
        targets.forEach(wrapper => this.currentObserver.observe(wrapper));
    },

    waitForImages(images) {
        const promises = Array.from(images).map(img => {
            if (img.complete) return Promise.resolve();
            return new Promise(resolve => {
                img.onload = resolve;
                img.onerror = resolve;
            });
        });
        return Promise.all(promises);
    },

    updatePageDisplay(index) {
        const display = document.getElementById('currentPageDisplay');
        if (display) display.innerText = index;

        const totalPages = document.querySelectorAll('.manga-page-wrapper').length;
        const progressBar = document.getElementById('readerProgressBar');
        if (progressBar) {
            progressBar.style.width = ((index / (totalPages || 1)) * 100) + "%";
        }
    },

    // --- Input & Initialization ---
    initGrabToScroll(container) {
        container.addEventListener('mousedown', (e) => {
            if (e.button !== 0) return;
            this.isDown = true;
            container.classList.add('grabbing');
            this.startX = e.pageX - container.offsetLeft;
            this.startY = e.pageY - container.offsetTop;
            this.scrollLeft = container.scrollLeft;
            this.scrollTop = container.scrollTop;
        });

        const endGrab = () => { this.isDown = false; container.classList.remove('grabbing'); };
        container.addEventListener('mouseleave', endGrab);
        container.addEventListener('mouseup', endGrab);

        container.addEventListener('mousemove', (e) => {
            if (!this.isDown) return;
            this.isUserScrolling = true; // Trigger guard on grab
            e.preventDefault();
            const walkX = ((e.pageX - container.offsetLeft) - this.startX) * 2;
            const walkY = ((e.pageY - container.offsetTop) - this.startY) * 2;
            container.scrollLeft = this.scrollLeft - walkX;
            container.scrollTop = this.scrollTop - walkY;
        });
    },

    initAtScrollPosition() {
        const container = document.getElementById("readerContainer");
        if (!container) return;

        const page = container.dataset.lastPage;
        const target = document.getElementById(`manga-page-${page}`);
        if (!target) return;

        const images = container.querySelectorAll("img");
        let loaded = 0;

        const scrollToTarget = () => {
            const top = target.offsetTop - container.offsetTop + container.scrollTop;
            container.scrollTo({ top: top, behavior: "auto" });
        };

        if (images.length === 0) return scrollToTarget();

        images.forEach(img => {
            if (img.complete) { loaded++; if (loaded === images.length) scrollToTarget(); }
            else { img.addEventListener("load", () => { loaded++; if (loaded === images.length) scrollToTarget(); }, { once: true }); }
        });
    },

    toggleFullscreen() {
        const elem = document.getElementById('mainViewer');
        if (!document.fullscreenElement) {
            if (elem.requestFullscreen) elem.requestFullscreen();
        } else {
            if (document.exitFullscreen) document.exitFullscreen();
        }
    }
};

// --- Global Event Links ---
window.addEventListener('load', () => readerState.setInitialFit());
window.addEventListener('resize', () => readerState.setInitialFit());

document.addEventListener("DOMContentLoaded", () => {
    const container = document.getElementById('readerContainer');
    const readerShell = document.getElementById('readerShell');

    if (readerShell) {
        setTimeout(() => readerShell.scrollIntoView({ behavior: 'smooth', block: 'end' }), 150);
    }

    if (container) {
        readerState.initGrabToScroll(container);
    }

    // Initialize logic
    readerState.initScrollObserver();
    readerState.initAtScrollPosition();

    // Fullscreen UI handler
    document.addEventListener('fullscreenchange', () => {
        const fsBtns = document.querySelectorAll('.bi-fullscreen, .bi-fullscreen-exit');
        fsBtns.forEach(btn => {
            btn.classList.toggle('bi-fullscreen', !document.fullscreenElement);
            btn.classList.toggle('bi-fullscreen-exit', !!document.fullscreenElement);
        });
    });
});
