/**
 * @fileoverview Navigation buttons manager for handling scroll-to-top and back navigation
 * @module navigation-buttons
 */

import {CircuitBreaker, DOMOperationQueue, EventEmitter} from './core.module.js';
import {DropBearUtils} from './utils.module.js';
import {ModuleManager} from './module-manager.module.js';

/**
 * Create a logger instance for this module
 */
const logger = DropBearUtils.createLogger('DropBearNavigationButtons');

/**
 * Create a circuit breaker to handle repeated failures
 */
const circuitBreaker = new CircuitBreaker({failureThreshold: 3, resetTimeout: 30000});

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

    // Set up the intersection observer on creation
    this._setupScrollObserver();

    // Emit an event indicating the navigation manager was initialized
    EventEmitter.emit(
      this,
      'initialized',
      DropBearUtils.createEvent(crypto.randomUUID(), 'initialized', {
        timestamp: Date.now(),
      })
    );
  }

  /**
   * Set up the intersection observer for scroll position monitoring.
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

          // Determine if any observed element is in view
          const isVisible = entries.some(entry => entry.intersectionRatio > 0);
          this._updateVisibility(!isVisible);
        }, this.scrollThrottleDelay),
        options
      );

      // Create a sentinel element for the observer
      const sentinel = document.createElement('div');
      sentinel.style.cssText = 'height: 1px; pointer-events: none; opacity: 0;';
      document.body.prepend(sentinel);

      // Begin observing
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
    try {
      // Use circuit breaker to protect asynchronous call to Blazor .NET code
      await circuitBreaker.execute(() =>
        this.dotNetRef.invokeMethodAsync('UpdateVisibility', isVisible)
      );

      // Emit an event indicating the visibility changed
      EventEmitter.emit(
        this,
        'visibility-changed',
        DropBearUtils.createEvent(crypto.randomUUID(), 'visibility-changed', {
          isVisible,
          timestamp: Date.now(),
        })
      );
    } catch (error) {
      logger.error('Failed to update visibility:', error);
      throw error;
    }
  }

  /**
   * Scroll to the top of the page
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
      } catch (error) {
        logger.error('Error scrolling to top:', error);
        // Fallback to instant scroll
        window.scrollTo(0, 0);
      }
    });
  }

  /**
   * Navigate back in the browser history
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
    } catch (error) {
      logger.error('Error navigating back:', error);
      throw error;
    }
  }

  /**
   * Force a visibility update
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

// Register with ModuleManager
ModuleManager.register(
  'DropBearNavigationButtons',
  {
    /** @type {NavigationManager|null} */
    instance: null,

    /**
     * Initialize the navigation module
     * @returns {Promise<void>}
     */
    async initialize() {
      logger.debug('DropBearNavigationButtons module initialized');
    },

    /**
     * Create a new NavigationManager instance
     * @param {Object} dotNetRef - .NET reference
     */
    createNavigationManager(dotNetRef) {
      if (!dotNetRef) {
        throw new Error('dotNetRef is required');
      }

      if (this.instance) {
        logger.debug('Disposing existing NavigationManager instance');
        this.dispose();
      }

      try {
        this.instance = new NavigationManager(dotNetRef);
        logger.debug('New NavigationManager instance created');
      } catch (error) {
        logger.error('Failed to create NavigationManager:', error);
        throw error;
      }
    },

    /**
     * Scroll to the top of the page
     */
    scrollToTop() {
      if (!this.instance) {
        throw new Error('No NavigationManager instance exists');
      }
      this.instance.scrollToTop();
    },

    /**
     * Navigate back in the browser history
     */
    goBack() {
      if (!this.instance) {
        throw new Error('No NavigationManager instance exists');
      }
      this.instance.goBack();
    },

    /**
     * Force visibility update
     * @param {boolean} isVisible - Desired visibility state
     * @returns {Promise<void>}
     */
    async forceVisibilityUpdate(isVisible) {
      if (!this.instance) {
        throw new Error('No NavigationManager instance exists');
      }
      await this.instance.forceVisibilityUpdate(isVisible);
    },

    /**
     * Dispose of the current NavigationManager instance
     */
    dispose() {
      if (this.instance) {
        this.instance.dispose();
        this.instance = null;
      }
    },
  },
  ['DropBearCore']
);

/**
 * Retrieve the registered module from the ModuleManager
 */
const navigationButtonsModule = ModuleManager.get('DropBearNavigationButtons');

/**
 * Attach a global reference for usage in window scope
 */
window.DropBearNavigationButtons = {
  initialize: () => navigationButtonsModule.initialize(),
  createNavigationManager: dotNetRef => navigationButtonsModule.createNavigationManager(dotNetRef),
  scrollToTop: () => navigationButtonsModule.scrollToTop(),
  goBack: () => navigationButtonsModule.goBack(),
  forceVisibilityUpdate: isVisible => navigationButtonsModule.forceVisibilityUpdate(isVisible),
  dispose: () => navigationButtonsModule.dispose(),
};

/**
 * Export the NavigationManager at the top level for direct module imports.
 */
export {NavigationManager};
