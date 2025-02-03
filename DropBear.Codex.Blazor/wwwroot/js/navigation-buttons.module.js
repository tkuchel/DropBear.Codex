/**
 * @fileoverview Navigation buttons manager for handling scroll-to-top and back navigation
 * @module navigation-buttons
 */

import {CircuitBreaker, DOMOperationQueue, EventEmitter} from './core.module.js';
import {DropBearUtils} from './utils.module.js';

const logger = DropBearUtils.createLogger('DropBearNavigationButtons');
const circuitBreaker = new CircuitBreaker({failureThreshold: 3, resetTimeout: 30000});
let isInitialized = false;

/**
 * Manager for navigation button behavior and visibility
 * @implements {INavigationManager}
 */
class NavigationManager {
  /**
   * @param {Object} dotNetRef - .NET reference for Blazor interop
   */
  constructor(dotNetRef) {
    if (!dotNetRef) {
      throw new Error('dotNetRef is required');
    }

    /** @type {Object|null} */
    this.dotNetRef = dotNetRef;

    /** @type {boolean} */
    this.isDisposed = false;

    /** @type {IntersectionObserver|null} */
    this.intersectionObserver = null;

    /** @type {number|null} */
    this.scrollThrottleTimeout = null;

    /** @type {number} */
    this.scrollThrottleDelay = 250; // ms between scroll checks

    this._setupScrollObserver();

    // Emit initialization event
    EventEmitter.emit(
      this,
      'initialized',
      DropBearUtils.createEvent(crypto.randomUUID(), 'initialized', {
        timestamp: Date.now(),
      })
    );

    logger.debug('NavigationManager instance created');
  }

  /**
   * Set up the intersection observer for scroll position monitoring
   * @private
   */
  _setupScrollObserver() {
    const options = {
      threshold: [0, 0.5, 1],
      rootMargin: '300px',
    };

    try {
      this.intersectionObserver = new IntersectionObserver(
        DropBearUtils.throttle(entries => {
          if (this.isDisposed) return;

          const isVisible = entries.some(entry => entry.intersectionRatio > 0);
          this._updateVisibility(!isVisible);
        }, this.scrollThrottleDelay),
        options
      );

      const sentinel = document.createElement('div');
      sentinel.style.cssText = 'height: 1px; pointer-events: none; opacity: 0;';
      document.body.prepend(sentinel);

      this.intersectionObserver.observe(sentinel);
      logger.debug('Scroll observer initialized');
    } catch (error) {
      logger.error('Failed to initialize scroll observer:', error);
      throw error;
    }
  }

  /**
   * Update navigation buttons' visibility state
   * @private
   * @param {boolean} isVisible - Whether navigation should be visible
   * @returns {Promise<void>}
   */
  async _updateVisibility(isVisible) {
    if (this.isDisposed) return;

    try {
      await circuitBreaker.execute(() =>
        this.dotNetRef.invokeMethodAsync('UpdateVisibility', isVisible)
      );

      EventEmitter.emit(
        this,
        'visibility-changed',
        DropBearUtils.createEvent(crypto.randomUUID(), 'visibility-changed', {
          isVisible,
          timestamp: Date.now(),
        })
      );

      logger.debug(`Visibility updated: ${isVisible}`);
    } catch (error) {
      logger.error('Failed to update visibility:', error);
      throw error;
    }
  }

  /**
   * Scroll to the top of the page
   * @public
   */
  scrollToTop() {
    if (this.isDisposed) {
      logger.warn('Attempted to scroll while disposed');
      return;
    }

    DOMOperationQueue.add(() => {
      try {
        window.scrollTo({
          top: 0,
          behavior: 'smooth',
        });

        EventEmitter.emit(
          this,
          'scrolled-to-top',
          DropBearUtils.createEvent(crypto.randomUUID(), 'scrolled-to-top', {
            timestamp: Date.now(),
          })
        );

        logger.debug('Scrolled to top');
      } catch (error) {
        logger.error('Error scrolling to top:', error);
        // Fallback to instant scroll
        window.scrollTo(0, 0);
      }
    });
  }

  /**
   * Navigate back in the browser history
   * @public
   */
  goBack() {
    if (this.isDisposed) {
      logger.warn('Attempted to navigate while disposed');
      return;
    }

    try {
      window.history.back();
      EventEmitter.emit(
        this,
        'went-back',
        DropBearUtils.createEvent(crypto.randomUUID(), 'went-back', {
          timestamp: Date.now(),
        })
      );

      logger.debug('Navigated back');
    } catch (error) {
      logger.error('Error navigating back:', error);
      throw error;
    }
  }

  /**
   * Force a visibility update
   * @public
   * @param {boolean} isVisible - Desired visibility state
   * @returns {Promise<void>}
   */
  async forceVisibilityUpdate(isVisible) {
    if (this.isDisposed) {
      throw new Error('Cannot update visibility on a disposed manager');
    }
    await this._updateVisibility(isVisible);
  }

  /**
   * Clean up resources
   * @public
   */
  dispose() {
    if (this.isDisposed) return;

    logger.debug('Disposing NavigationManager');
    this.isDisposed = true;

    if (this.intersectionObserver) {
      this.intersectionObserver.disconnect();
      this.intersectionObserver = null;
    }

    clearTimeout(this.scrollThrottleTimeout);
    this.scrollThrottleTimeout = null;
    this.dotNetRef = null;

    EventEmitter.emit(
      this,
      'disposed',
      DropBearUtils.createEvent(crypto.randomUUID(), 'disposed', {
        timestamp: Date.now(),
      })
    );
  }
}


// Attach to window first
window.DropBearNavigationButtons = {
  __initialized: false,
  instance: null,

  initialize: async () => {
    if (isInitialized) {
      return;
    }

    try {
      logger.debug('Navigation buttons module initializing');

      // Initialize dependencies first
      await window.DropBearUtils.initialize();
      await window.DropBearCore.initialize();

      isInitialized = true;
      window.DropBearNavigationButtons.__initialized = true;

      logger.debug('Navigation buttons module initialized');
    } catch (error) {
      logger.error('Navigation buttons initialization failed:', error);
      throw error;
    }
  },

  createNavigationManager: dotNetRef => {
    try {
      if (!isInitialized) {
        throw new Error('Module not initialized');
      }

      if (window.DropBearNavigationButtons.instance) {
        logger.debug('Disposing existing NavigationManager instance');
        window.DropBearNavigationButtons.dispose();
      }

      window.DropBearNavigationButtons.instance = new NavigationManager(dotNetRef);
      logger.debug('New NavigationManager instance created');
    } catch (error) {
      logger.error('Failed to create NavigationManager:', error);
      throw error;
    }
  },

  scrollToTop: () => {
    if (!window.DropBearNavigationButtons.instance) {
      throw new Error('No NavigationManager instance exists');
    }
    window.DropBearNavigationButtons.instance.scrollToTop();
  },

  goBack: () => {
    if (!window.DropBearNavigationButtons.instance) {
      throw new Error('No NavigationManager instance exists');
    }
    window.DropBearNavigationButtons.instance.goBack();
  },

  forceVisibilityUpdate: async isVisible => {
    if (!window.DropBearNavigationButtons.instance) {
      throw new Error('No NavigationManager instance exists');
    }
    await window.DropBearNavigationButtons.instance.forceVisibilityUpdate(isVisible);
  },

  isInitialized: () => isInitialized,

  dispose: () => {
    if (window.DropBearNavigationButtons.instance) {
      window.DropBearNavigationButtons.instance.dispose();
      window.DropBearNavigationButtons.instance = null;
    }
    isInitialized = false;
    window.DropBearNavigationButtons.__initialized = false;
    logger.debug('Navigation buttons module disposed');
  }
};

// Register with ModuleManager after window attachment
window.DropBearModuleManager.register(
  'DropBearNavigationButtons',
  {
    initialize: () => window.DropBearNavigationButtons.initialize(),
    isInitialized: () => window.DropBearNavigationButtons.isInitialized(),
    dispose: () => window.DropBearNavigationButtons.dispose()
  },
  ['DropBearUtils', 'DropBearCore']
);

// Export NavigationManager class
export {NavigationManager};
