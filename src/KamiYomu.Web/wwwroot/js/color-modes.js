/*!
 * Color mode toggler for Bootstrap's docs (https://getbootstrap.com/)
 * Copyright 2011-2025 The Bootstrap Authors
 * Licensed under the Creative Commons Attribution 3.0 Unported License.
 */

(() => {
    'use strict'

    const getStoredTheme = () => localStorage.getItem('theme')
    const setStoredTheme = theme => localStorage.setItem('theme', theme)

    const getPreferredTheme = () => {
        const storedTheme = getStoredTheme()
        if (storedTheme) {
            return storedTheme
        }

        return window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light'
    }

    const setTheme = theme => {
        if (theme === 'auto') {
            document.documentElement.setAttribute('data-bs-theme', (window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light'))
        } else {
            document.documentElement.setAttribute('data-bs-theme', theme)
        }
    }

    setTheme(getPreferredTheme())

    const showActiveTheme = (theme, focus = false) => {
        const themeSwitcher = document.querySelector('#bd-theme');
        if (!themeSwitcher) return;

        const themeSwitcherText = document.querySelector('#bd-theme-text');
        const activeThemeIcon = themeSwitcher.querySelector('.theme-icon-active');
        const btnToActivate = document.querySelector(`[data-bs-theme-value="${theme}"]`);
        const iconOfActiveBtn = btnToActivate.querySelector('i.bi');

        // Reset all theme buttons
        document.querySelectorAll('[data-bs-theme-value]').forEach(btn => {
            btn.classList.remove('active');
            btn.setAttribute('aria-pressed', 'false');
            const checkIcon = btn.querySelector('.bi-check2');
            if (checkIcon) checkIcon.classList.add('d-none');
        });

        // Activate selected button
        btnToActivate.classList.add('active');
        btnToActivate.setAttribute('aria-pressed', 'true');
        const checkIcon = btnToActivate.querySelector('.bi-check2');
        if (checkIcon) checkIcon.classList.remove('d-none');

        // Swap theme icon class
        const newIconClass = Array.from(iconOfActiveBtn.classList).find(cls => cls.startsWith('bi-'));
        activeThemeIcon.className = `bi ${newIconClass} my-1 theme-icon-active`;

        // Update label
        const themeLabel = `${themeSwitcherText.textContent} (${btnToActivate.dataset.bsThemeValue})`;
        themeSwitcher.setAttribute('aria-label', themeLabel);

        if (focus) themeSwitcher.focus();
    };

    window.matchMedia('(prefers-color-scheme: dark)').addEventListener('change', () => {
        const storedTheme = getStoredTheme()
        if (storedTheme !== 'light' && storedTheme !== 'dark') {
            setTheme(getPreferredTheme())
        }
    })

    window.addEventListener('DOMContentLoaded', () => {
        showActiveTheme(getPreferredTheme())

        document.querySelectorAll('[data-bs-theme-value]')
            .forEach(toggle => {
                toggle.addEventListener('click', () => {
                    const theme = toggle.getAttribute('data-bs-theme-value')
                    setStoredTheme(theme)
                    setTheme(theme)
                    showActiveTheme(theme, true)
                })
            })
    })
})()
