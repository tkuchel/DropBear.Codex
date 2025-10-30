/**
 * DropBear Theme Manager
 *
 * Manages theme switching with smooth transitions, system preference detection,
 * and persistent storage. Supports automatic and manual theme control.
 *
 * Features:
 * - System preference detection (prefers-color-scheme)
 * - Smooth theme transitions
 * - localStorage persistence
 * - Reduced motion support
 * - Component-level theme override
 * - Event-based notifications
 */

class ThemeManager {
    constructor() {
        this.currentTheme = null;
        this.systemTheme = null;
        this.userPreference = null;
        this.transitionDuration = 300; // ms
        this.listeners = new Set();
        this.initialized = false;

        // Storage key
        this.storageKey = 'dropbear-theme-preference';

        // Theme values
        this.themes = {
            LIGHT: 'light',
            DARK: 'dark',
            AUTO: 'auto'
        };
    }

    /**
     * Initialize the theme manager
     */
    initialize() {
        if (this.initialized) {
            console.warn('ThemeManager already initialized');
            return;
        }

        // Detect system preference
        this.detectSystemPreference();

        // Load user preference from storage
        this.loadUserPreference();

        // Apply initial theme
        this.applyTheme();

        // Listen for system preference changes
        this.watchSystemPreference();

        // Listen for storage changes (multi-tab sync)
        this.watchStorageChanges();

        this.initialized = true;
        console.log('ThemeManager initialized:', {
            currentTheme: this.currentTheme,
            systemTheme: this.systemTheme,
            userPreference: this.userPreference
        });
    }

    /**
     * Detect system color scheme preference
     */
    detectSystemPreference() {
        if (window.matchMedia) {
            const darkModeQuery = window.matchMedia('(prefers-color-scheme: dark)');
            this.systemTheme = darkModeQuery.matches ? this.themes.DARK : this.themes.LIGHT;
        } else {
            this.systemTheme = this.themes.LIGHT; // Fallback
        }
    }

    /**
     * Watch for system preference changes
     */
    watchSystemPreference() {
        if (window.matchMedia) {
            const darkModeQuery = window.matchMedia('(prefers-color-scheme: dark)');

            // Modern browsers
            if (darkModeQuery.addEventListener) {
                darkModeQuery.addEventListener('change', (e) => {
                    this.systemTheme = e.matches ? this.themes.DARK : this.themes.LIGHT;
                    console.log('System theme changed to:', this.systemTheme);

                    // Re-apply theme if user preference is 'auto'
                    if (this.userPreference === this.themes.AUTO || !this.userPreference) {
                        this.applyTheme(true);
                    }
                });
            }
            // Legacy browsers
            else if (darkModeQuery.addListener) {
                darkModeQuery.addListener((e) => {
                    this.systemTheme = e.matches ? this.themes.DARK : this.themes.LIGHT;
                    if (this.userPreference === this.themes.AUTO || !this.userPreference) {
                        this.applyTheme(true);
                    }
                });
            }
        }
    }

    /**
     * Watch for storage changes (multi-tab synchronization)
     */
    watchStorageChanges() {
        window.addEventListener('storage', (e) => {
            if (e.key === this.storageKey && e.newValue !== e.oldValue) {
                console.log('Theme preference changed in another tab');
                this.loadUserPreference();
                this.applyTheme(true);
            }
        });
    }

    /**
     * Load user preference from localStorage
     */
    loadUserPreference() {
        try {
            const stored = localStorage.getItem(this.storageKey);
            if (stored && Object.values(this.themes).includes(stored)) {
                this.userPreference = stored;
            } else {
                this.userPreference = this.themes.AUTO;
            }
        } catch (error) {
            console.warn('Failed to load theme preference:', error);
            this.userPreference = this.themes.AUTO;
        }
    }

    /**
     * Save user preference to localStorage
     */
    saveUserPreference(preference) {
        try {
            localStorage.setItem(this.storageKey, preference);
            this.userPreference = preference;
        } catch (error) {
            console.error('Failed to save theme preference:', error);
        }
    }

    /**
     * Get the effective theme based on user preference and system theme
     */
    getEffectiveTheme() {
        if (this.userPreference === this.themes.AUTO || !this.userPreference) {
            return this.systemTheme;
        }
        return this.userPreference;
    }

    /**
     * Apply theme to the document
     */
    applyTheme(animated = false) {
        const newTheme = this.getEffectiveTheme();
        const oldTheme = this.currentTheme;

        if (newTheme === oldTheme && this.currentTheme !== null) {
            return; // No change needed
        }

        console.log('Applying theme:', newTheme, animated ? '(animated)' : '(instant)');

        // Check if user prefers reduced motion
        const prefersReducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
        const shouldAnimate = animated && !prefersReducedMotion;

        if (shouldAnimate) {
            // Add transition class
            document.documentElement.classList.add('theme-transitioning');
        }

        // Set theme attribute
        document.documentElement.setAttribute('data-theme', newTheme);

        // Update current theme
        this.currentTheme = newTheme;

        // Notify listeners
        this.notifyListeners({
            theme: newTheme,
            previousTheme: oldTheme,
            animated: shouldAnimate,
            userPreference: this.userPreference
        });

        if (shouldAnimate) {
            // Remove transition class after animation completes
            setTimeout(() => {
                document.documentElement.classList.remove('theme-transitioning');
            }, this.transitionDuration);
        }

        // Dispatch custom event
        window.dispatchEvent(new CustomEvent('themechange', {
            detail: {
                theme: newTheme,
                previousTheme: oldTheme,
                userPreference: this.userPreference
            }
        }));
    }

    /**
     * Set theme preference (light, dark, or auto)
     */
    setTheme(theme, animated = true) {
        if (!Object.values(this.themes).includes(theme)) {
            console.error('Invalid theme:', theme);
            return false;
        }

        this.saveUserPreference(theme);
        this.applyTheme(animated);
        return true;
    }

    /**
     * Toggle between light and dark themes
     */
    toggleTheme(animated = true) {
        const currentEffective = this.getEffectiveTheme();
        const newTheme = currentEffective === this.themes.DARK
            ? this.themes.LIGHT
            : this.themes.DARK;

        this.setTheme(newTheme, animated);
        return newTheme;
    }

    /**
     * Get current theme information
     */
    getThemeInfo() {
        return {
            current: this.currentTheme,
            effective: this.getEffectiveTheme(),
            userPreference: this.userPreference,
            systemTheme: this.systemTheme,
            isAuto: this.userPreference === this.themes.AUTO,
            isDark: this.currentTheme === this.themes.DARK,
            isLight: this.currentTheme === this.themes.LIGHT
        };
    }

    /**
     * Add theme change listener
     */
    addListener(callback) {
        if (typeof callback === 'function') {
            this.listeners.add(callback);
            return () => this.removeListener(callback);
        }
        return null;
    }

    /**
     * Remove theme change listener
     */
    removeListener(callback) {
        this.listeners.delete(callback);
    }

    /**
     * Notify all listeners of theme change
     */
    notifyListeners(event) {
        this.listeners.forEach(callback => {
            try {
                callback(event);
            } catch (error) {
                console.error('Theme listener error:', error);
            }
        });
    }

    /**
     * Apply theme to a specific element (component-level override)
     */
    applyThemeToElement(element, theme) {
        if (!element || !(element instanceof Element)) {
            console.error('Invalid element provided');
            return false;
        }

        if (!Object.values(this.themes).includes(theme)) {
            console.error('Invalid theme:', theme);
            return false;
        }

        element.setAttribute('data-theme', theme);
        return true;
    }

    /**
     * Remove theme override from element
     */
    removeThemeFromElement(element) {
        if (!element || !(element instanceof Element)) {
            console.error('Invalid element provided');
            return false;
        }

        element.removeAttribute('data-theme');
        return true;
    }

    /**
     * Get theme for element (considering overrides)
     */
    getElementTheme(element) {
        if (!element || !(element instanceof Element)) {
            return this.currentTheme;
        }

        // Check if element has override
        const overrideTheme = element.getAttribute('data-theme');
        if (overrideTheme) {
            return overrideTheme;
        }

        // Check parent hierarchy
        let parent = element.parentElement;
        while (parent) {
            const parentTheme = parent.getAttribute('data-theme');
            if (parentTheme) {
                return parentTheme;
            }
            parent = parent.parentElement;
        }

        // Return global theme
        return this.currentTheme;
    }

    /**
     * Dispose resources
     */
    dispose() {
        this.listeners.clear();
        this.initialized = false;
        console.log('ThemeManager disposed');
    }
}

// Create singleton instance
const themeManager = new ThemeManager();

// Export functions for Blazor interop
export function initialize() {
    themeManager.initialize();
    return themeManager.getThemeInfo();
}

export function setTheme(theme, animated = true) {
    return themeManager.setTheme(theme, animated);
}

export function toggleTheme(animated = true) {
    return themeManager.toggleTheme(animated);
}

export function getThemeInfo() {
    return themeManager.getThemeInfo();
}

export function applyThemeToElement(element, theme) {
    return themeManager.applyThemeToElement(element, theme);
}

export function removeThemeFromElement(element) {
    return themeManager.removeThemeFromElement(element);
}

export function getElementTheme(element) {
    return themeManager.getElementTheme(element);
}

export function addListener(dotNetReference, methodName) {
    const callback = (event) => {
        dotNetReference.invokeMethodAsync(methodName, event);
    };
    return themeManager.addListener(callback);
}

export function dispose() {
    themeManager.dispose();
}

// Auto-initialize on module load
if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', () => {
        themeManager.initialize();
    });
} else {
    themeManager.initialize();
}
