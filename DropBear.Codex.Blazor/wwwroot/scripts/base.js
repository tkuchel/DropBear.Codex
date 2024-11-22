/**
 * @file base.js
 * Core utilities and components for DropBear Blazor integration
 */

(() => {
  'use strict';

  // Global state management
  const DropBearState = {
    initialized: false,
    initializationError: null
  };

  // Performance monitoring
  const PerformanceMonitor = {
    timings: new Map(),

    start(operation) {
      this.timings.set(operation, performance.now());
    },

    end(operation) {
      const startTime = this.timings.get(operation);
      if (startTime) {
        const duration = performance.now() - startTime;
        this.timings.delete(operation);
        console.debug(`[Performance] ${operation}: ${duration.toFixed(2)}ms`);
      }
    }
  };

  // Event handling system
  const EventEmitter = {
    events: new Map(),

    on(event, callback) {
      if (!this.events.has(event)) {
        this.events.set(event, new Set());
      }
      this.events.get(event).add(callback);
      return () => this.off(event, callback); // Return cleanup function
    },

    off(event, callback) {
      const callbacks = this.events.get(event);
      if (callbacks) {
        callbacks.delete(callback);
      }
    },

    emit(event, data) {
      const callbacks = this.events.get(event);
      if (callbacks) {
        callbacks.forEach(callback => {
          try {
            callback(data);
          } catch (error) {
            console.error(`Error in event handler for ${event}:`, error);
          }
        });
      }
    }
  };

  // Retry operation utility
  const retryOperation = async (operation, retries = 3, delay = 1000) => {
    let lastError;

    for (let i = 0; i < retries; i++) {
      try {
        return await operation();
      } catch (error) {
        lastError = error;
        console.warn(`Operation failed, attempt ${i + 1} of ${retries}:`, error);
        if (i < retries - 1) {
          await new Promise(resolve => setTimeout(resolve, delay));
        }
      }
    }

    throw lastError;
  };

  /**
   * Enhanced Core utilities namespace
   * @namespace
   */
  const DropBearUtils = {
    /**
     * Enhanced logging utility
     * @param {string} namespace - Module namespace for log prefixing
     * @returns {Object} Logger instance
     */
    createLogger(namespace) {
      const prefix = `[${namespace}]`;
      return {
        log: (message, ...args) => console.log(`${prefix} ${message}`, ...args),
        warn: (message, ...args) => console.warn(`${prefix} ${message}`, ...args),
        error: (message, ...args) => console.error(`${prefix} ${message}`, ...args),
        debug: (message, ...args) => console.debug(`${prefix} ${message}`, ...args)
      };
    },

    validateArgs(args, types, functionName) {
      args.forEach((arg, index) => {
        const expectedType = types[index];
        const actualType = typeof arg;
        if (actualType !== expectedType) {
          throw new TypeError(
            `Invalid argument for ${functionName}: Expected ${expectedType}, got ${actualType}`
          );
        }
      });
    },

    isElement(element) {
      return element instanceof Element || element instanceof HTMLDocument;
    },

    debounce(func, wait) {
      this.validateArgs([func, wait], ['function', 'number'], 'debounce');
      let timeout;
      return function executedFunction(...args) {
        const later = () => {
          clearTimeout(timeout);
          func.apply(this, args);
        };
        clearTimeout(timeout);
        timeout = setTimeout(later, wait);
      };
    },

    throttle(func, limit) {
      this.validateArgs([func, limit], ['function', 'number'], 'throttle');
      let inThrottle;
      let lastRan;
      let lastFunc;

      return function executedFunction(...args) {
        const context = this;
        if (!inThrottle) {
          func.apply(context, args);
          lastRan = Date.now();
          inThrottle = true;
        } else {
          clearTimeout(lastFunc);
          lastFunc = setTimeout(() => {
            if (Date.now() - lastRan >= limit) {
              func.apply(context, args);
              lastRan = Date.now();
            }
          }, Math.max(0, limit - (Date.now() - lastRan)));
        }
      };
    },

    safeQuerySelector(selector, context = document) {
      try {
        if (typeof selector !== 'string') {
          throw new TypeError('Selector must be a string');
        }
        return context.querySelector(selector);
      } catch (error) {
        console.error(`Invalid selector: ${selector}`, error);
        return null;
      }
    },

    createOneTimeListener(element, eventName, handler, timeout) {
      if (!this.isElement(element)) {
        throw new TypeError('Invalid element provided');
      }

      return new Promise((resolve, reject) => {
        const timeoutId = timeout && setTimeout(() => {
          try {
            element.removeEventListener(eventName, wrappedHandler);
          } catch (error) {
            console.warn("Error removing event listener:", error);
          }
          reject(new Error('Event listener timed out'));
        }, timeout);

        const wrappedHandler = (...args) => {
          clearTimeout(timeoutId);
          try {
            element.removeEventListener(eventName, wrappedHandler);
            const result = handler(...args);
            resolve(result);
          } catch (error) {
            reject(error);
          }
        };
        element.addEventListener(eventName, wrappedHandler, {once: true});
      });
    }
  };

  const DropBearSnackbar = (() => {
    const logger = DropBearUtils.createLogger('DropBearSnackbar');
    const snackbars = new Map();
    const ANIMATION_DURATION = 300;
    const CLEANUP_INTERVAL = 60000; // 1 minute

    // Periodic cleanup of inactive snackbars
    const cleanupInactiveSnackbars = () => {
      for (const [id, manager] of snackbars.entries()) {
        if (manager.isDisposed || !document.getElementById(id)) {
          manager.dispose();
          snackbars.delete(id);
        }
      }
    };

    // Start cleanup interval
    const cleanupInterval = setInterval(cleanupInactiveSnackbars, CLEANUP_INTERVAL);

    class SnackbarManager {
      constructor(id, element) {
        DropBearUtils.validateArgs([id, element], ['string', 'object'], 'SnackbarManager');
        if (!DropBearUtils.isElement(element)) {
          throw new TypeError('Invalid element provided to SnackbarManager');
        }

        this.id = id;
        this.element = element;
        this.progressBar = element.querySelector('.snackbar-progress');
        this.timeout = null;
        this.isDisposed = false;
        this.createTime = Date.now();

        EventEmitter.emit('snackbar:created', {id, element});
      }

      async startProgress(duration) {
        if (this.isDisposed || !this.progressBar) return;

        await retryOperation(async () => {
          try {
            PerformanceMonitor.start(`snackbar-progress-${this.id}`);

            this.progressBar.style.transition = 'none';
            this.progressBar.style.width = '100%';
            void this.progressBar.offsetWidth;

            this.progressBar.style.transition = `width ${duration}ms linear`;
            this.progressBar.style.width = '0%';

            clearTimeout(this.timeout);
            this.timeout = setTimeout(() => this.hide(), duration);

            PerformanceMonitor.end(`snackbar-progress-${this.id}`);
            logger.debug(`Progress started: ${this.id}`);
          } catch (error) {
            logger.error(`Progress error: ${error.message}`);
            await this.hide();
            throw error;
          }
        });
      }

      async hide() {
        if (this.isDisposed) return;

        try {
          clearTimeout(this.timeout);
          this.element.classList.add('snackbar-exit');

          await DropBearUtils.createOneTimeListener(
            this.element,
            'animationend',
            () => {
            },
            ANIMATION_DURATION + 500
          );

          this.dispose();
        } catch (error) {
          logger.error(`Hide error: ${error.message}`);
          this.dispose();
        }
      }

      dispose() {
        if (this.isDisposed) return;

        this.isDisposed = true;
        clearTimeout(this.timeout);
        try {
          if (this.element && this.element.parentNode) {
            this.element.parentNode.removeChild(this.element);
          }
        } catch (error) {
          logger.warn("Error disposing snackbar element:", error);
        }
        snackbars.delete(this.id);
        EventEmitter.emit('snackbar:disposed', {id: this.id});
      }
    }

    return {
      async startProgress(snackbarId, duration) {
        PerformanceMonitor.start(`snackbar-operation-${snackbarId}`);

        if (!snackbarId || typeof duration !== 'number' || duration <= 0) {
          throw new Error('Invalid arguments');
        }

        try {
          const element = document.getElementById(snackbarId);
          if (!element) return;

          if (snackbars.has(snackbarId)) {
            await this.disposeSnackbar(snackbarId);
          }

          const manager = new SnackbarManager(snackbarId, element);
          snackbars.set(snackbarId, manager);
          await manager.startProgress(duration);
        } catch (error) {
          logger.error('startProgress error:', error);
          throw error;
        } finally {
          PerformanceMonitor.end(`snackbar-operation-${snackbarId}`);
        }
      },

      async hideSnackbar(snackbarId) {
        try {
          const manager = snackbars.get(snackbarId);
          if (manager) await manager.hide();
        } catch (error) {
          logger.error('hideSnackbar error:', error);
          throw error;
        }
      },

      async disposeSnackbar(snackbarId) {
        const manager = snackbars.get(snackbarId);
        if (manager) await manager.dispose();
      },

      dispose() {
        clearInterval(cleanupInterval);
        snackbars.forEach(manager => manager.dispose());
        snackbars.clear();
      }
    };
  })();

  const DropBearResizeManager = (() => {
    const logger = DropBearUtils.createLogger('DropBearResizeManager');
    let instance = null;

    class ResizeManager {
      constructor(dotNetReference) {
        this.dotNetReference = dotNetReference;
        this.resizeHandler = DropBearUtils.debounce(() =>
            this.handleResize().catch(error =>
              logger.error('SetMaxWidthBasedOnWindowSize failed:', error)
            ),
          300
        );

        window.addEventListener('resize', this.resizeHandler);
        logger.debug('Resize manager initialized');
        EventEmitter.emit('resize-manager:created');
      }

      async handleResize() {
        await retryOperation(async () => await this.dotNetReference.invokeMethodAsync('SetMaxWidthBasedOnWindowSize'));
      }

      dispose() {
        if (this.resizeHandler) {
          window.removeEventListener('resize', this.resizeHandler);
          this.resizeHandler = null;
          logger.debug('Resize manager disposed');
          EventEmitter.emit('resize-manager:disposed');
        }
      }
    }

    return {
      initialize(dotNetRef) {
        if (!instance) {
          instance = new ResizeManager(dotNetRef);
        }
      },

      dispose() {
        if (instance) {
          instance.dispose();
          instance = null;
        }
      }
    };
  })();

  const DropBearNavigationButtons = (() => {
    const logger = DropBearUtils.createLogger('DropBearNavigationButtons');
    let dotNetReference = null;
    let scrollHandler = null;

    return {
      initialize(dotNetRef) {
        if (!dotNetRef) {
          throw new Error('dotNetRef is required');
        }

        this.dispose();
        dotNetReference = dotNetRef;

        scrollHandler = DropBearUtils.throttle(() => {
          const isVisible = window.scrollY > 300;
          dotNetReference.invokeMethodAsync('UpdateVisibility', isVisible)
            .catch(error => logger.error('UpdateVisibility failed:', error));
        }, 250);

        window.addEventListener('scroll', scrollHandler);
        scrollHandler();
        logger.debug('Navigation buttons initialized');
        EventEmitter.emit('navigation:initialized');
      },

      scrollToTop() {
        window.scrollTo({top: 0, behavior: 'smooth'});
        EventEmitter.emit('navigation:scrolled-to-top');
      },

      goBack() {
        window.history.back();
        EventEmitter.emit('navigation:went-back');
      },

      dispose() {
        if (scrollHandler) {
          window.removeEventListener('scroll', scrollHandler);
          scrollHandler = null;
        }
        dotNetReference = null;
        logger.debug('Navigation buttons disposed');
        EventEmitter.emit('navigation:disposed');
      }
    };
  })();

  // Add this to your base.js before the global assignments
  const DropBearUtilities = (() => {
    const logger = DropBearUtils.createLogger('DropBearUtilities');

    return {
      getWindowDimensions() {
        try {
          return {
            width: window.innerWidth,
            height: window.innerHeight
          };
        } catch (error) {
          logger.error('Error getting window dimensions:', error);
          return {width: 0, height: 0};
        }
      }
    };
  })();

// Add this to your window assignments
  window.getWindowDimensions = DropBearUtilities.getWindowDimensions;

  // Initialization sequence
  const initializeDropBear = async () => {
    try {
      // Wait for Blazor to be ready
      await new Promise((resolve, reject) => {
        let attempts = 0;
        const checkBlazor = setInterval(() => {
          attempts++;
          if (window.Blazor) {
            clearInterval(checkBlazor);
            resolve();
          }
          if (attempts > 50) { // 5 second timeout
            clearInterval(checkBlazor);
            reject(new Error('Blazor not initialized after 5 seconds'));
          }
        }, 100);
      });

      PerformanceMonitor.start('dropbear-initialization');

      if (DropBearState.initialized) {
        console.warn('DropBear already initialized');
        return;
      }

      console.log("DropBear initialization starting...");

      // Define immutable global objects
      Object.defineProperties(window, {
        DropBearSnackbar: {
          value: DropBearSnackbar,
          writable: false,
          configurable: false
        },
        DropBearUtils: {
          value: DropBearUtils,
          writable: false,
          configurable: false
        },
        DropBearResizeManager: {
          value: DropBearResizeManager,
          writable: false,
          configurable: false
        },
        DropBearNavigationButtons: {
          value: DropBearNavigationButtons,
          writable: false,
          configurable: false
        }
      });

      DropBearState.initialized = true;
      EventEmitter.emit('dropbear:initialized');

      PerformanceMonitor.end('dropbear-initialization');
      console.log("DropBear initialization complete");
    } catch (error) {
      DropBearState.initializationError = error;
      console.error("DropBear initialization failed:", error);
      throw error;
    }
  };

  // Initialize when DOM is ready
  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', initializeDropBear);
  } else {
    initializeDropBear();
  }

  // Add cleanup handler for page unload
  window.addEventListener('unload', () => {
    try {
      DropBearSnackbar.dispose();
      DropBearResizeManager.dispose();
      DropBearNavigationButtons.dispose();
      console.log("DropBear cleanup complete");
    } catch (error) {
      console.error("Error during DropBear cleanup:", error);
    }
  });

})();
