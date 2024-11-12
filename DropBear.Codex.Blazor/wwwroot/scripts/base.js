/**
 * @file dropbear.js
 * Core utilities and components for DropBear Blazor integration
 */

(() => {
  'use strict';

  /**
   * Core utilities namespace
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

    debounce(func, wait) {
      if (typeof func !== 'function' || typeof wait !== 'number') {
        throw new TypeError('Invalid arguments: Expected (function, number)');
      }
      let timeout;
      return function executedFunction(...args) {
        clearTimeout(timeout);
        timeout = setTimeout(() => func.apply(this, args), wait);
      };
    },

    throttle(func, limit) {
      if (typeof func !== 'function' || typeof limit !== 'number') {
        throw new TypeError('Invalid arguments: Expected (function, number)');
      }
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
        return context.querySelector(selector);
      } catch (error) {
        console.error(`Invalid selector: ${selector}`, error);
        return null;
      }
    },

    createOneTimeListener(element, eventName, handler, timeout) {
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

    class SnackbarManager {
      constructor(id, element) {
        this.id = id;
        this.element = element;
        this.progressBar = element.querySelector('.snackbar-progress');
        this.timeout = null;
        this.isDisposed = false;
      }

      async startProgress(duration) {
        if (this.isDisposed || !this.progressBar) return;

        try {
          this.progressBar.style.transition = 'none';
          this.progressBar.style.width = '100%';
          void this.progressBar.offsetWidth;

          this.progressBar.style.transition = `width ${duration}ms linear`;
          this.progressBar.style.width = '0%';

          clearTimeout(this.timeout);
          this.timeout = setTimeout(() => this.hide(), duration);
          logger.debug(`Progress started: ${this.id}`);
        } catch (error) {
          logger.error(`Progress error: ${error.message}`);
          await this.hide();
        }
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
      }
    }

    return {
      async startProgress(snackbarId, duration) {
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
        }
      },

      async hideSnackbar(snackbarId) {
        try {
          const manager = snackbars.get(snackbarId);
          if (manager) await manager.hide();
        } catch (error) {
          logger.error('hideSnackbar error:', error);
        }
      },

      async disposeSnackbar(snackbarId) {
        const manager = snackbars.get(snackbarId);
        if (manager) await manager.dispose();
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
            dotNetReference.invokeMethodAsync('SetMaxWidthBasedOnWindowSize')
              .catch(error => logger.error('SetMaxWidthBasedOnWindowSize failed:', error)),
          300
        );
        window.addEventListener('resize', this.resizeHandler);
        logger.debug('Resize manager initialized');
      }

      dispose() {
        if (this.resizeHandler) {
          window.removeEventListener('resize', this.resizeHandler);
          this.resizeHandler = null;
          logger.debug('Resize manager disposed');
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
      },

      scrollToTop() {
        window.scrollTo({top: 0, behavior: 'smooth'});
      },

      goBack() {
        window.history.back();
      },

      dispose() {
        if (scrollHandler) {
          window.removeEventListener('scroll', scrollHandler);
          scrollHandler = null;
        }
        dotNetReference = null;
        logger.debug('Navigation buttons disposed');
      }
    };
  })();

  Object.assign(window, {
    DropBearUtils,
    DropBearSnackbar,
    DropBearResizeManager,
    DropBearNavigationButtons,
    downloadFileFromStream,
    getWindowDimensions,
    clickElementById
  });

  document.addEventListener('DOMContentLoaded', () => DropBearFileUploader.initialize());
})();
