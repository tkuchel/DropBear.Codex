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
      return () => this.off(event, callback);
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
    for (let i = 0; i < retries; i++) {
      try {
        return await operation();
      } catch (error) {
        console.warn(`Operation failed, attempt ${i + 1} of ${retries}:`, error);
        if (i < retries - 1) {
          await new Promise(resolve => setTimeout(resolve, delay));
        } else {
          throw error;
        }
      }
    }
  };

  /**
   * Enhanced Core utilities namespace
   */
  const DropBearUtils = {
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

    createOneTimeListener(element, eventName, handler, timeout) {
      if (!this.isElement(element)) {
        throw new TypeError('Invalid element provided');
      }

      return new Promise((resolve, reject) => {
        const timeoutId = timeout && setTimeout(() => {
          element.removeEventListener(eventName, wrappedHandler);
          reject(new Error('Event listener timed out'));
        }, timeout);

        const wrappedHandler = (...args) => {
          if (timeoutId) clearTimeout(timeoutId);
          element.removeEventListener(eventName, wrappedHandler);
          try {
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
      constructor(id) {
        DropBearUtils.validateArgs([id], ['string'], 'SnackbarManager');

        this.id = id;
        this.element = document.getElementById(id);
        this.isDisposed = false;
        this.progressTimeout = null;

        if (!DropBearUtils.isElement(this.element)) {
          throw new TypeError('Invalid element provided to SnackbarManager');
        }

        // Add initial hide class
        this.element.classList.add('hide');

        // Setup event listeners
        this._setupEventListeners();

        EventEmitter.emit('snackbar:created', { id });
        logger.debug(`Snackbar initialized: ${id}`);
      }

      _setupEventListeners() {
        this.element.addEventListener('mouseenter', () => this._pauseProgress());
        this.element.addEventListener('mouseleave', () => this._resumeProgress());
        this.element.addEventListener('focusin', () => this._pauseProgress());
        this.element.addEventListener('focusout', () => this._resumeProgress());
      }

      show() {
        if (this.isDisposed) return;

        // Remove any existing classes that might interfere
        this.element.classList.remove('show', 'hide');

        // Force a reflow
        void this.element.offsetWidth;

        // Add show class in next frame
        requestAnimationFrame(() => {
          this.element.classList.add('show');
          logger.debug(`Snackbar shown: ${this.id}`);
        });
      }

      hide() {
        if (this.isDisposed) return;

        return new Promise((resolve) => {
          clearTimeout(this.progressTimeout);
          this.element.classList.add('hide');
          this.element.classList.remove('show');

          const handleTransitionEnd = (event) => {
            if (event.propertyName === 'transform') {
              this.element.removeEventListener('transitionend', handleTransitionEnd);
              this.dispose();
              resolve();
            }
          };

          this.element.addEventListener('transitionend', handleTransitionEnd);

          // Fallback in case transition end doesn't fire
          setTimeout(() => {
            this.element.removeEventListener('transitionend', handleTransitionEnd);
            this.dispose();
            resolve();
          }, ANIMATION_DURATION + 100);
        });
      }

      startProgress(duration) {
        if (this.isDisposed || !duration) return;

        const progressBar = this.element.querySelector('.progress-bar');
        if (progressBar) {
          this.element.style.setProperty('--duration', `${duration}ms`);
          progressBar.style.transform = 'scaleX(1)';
          progressBar.style.transition = `transform ${duration}ms linear`;
          requestAnimationFrame(() => {
            progressBar.style.transform = 'scaleX(0)';
          });

          this.progressTimeout = setTimeout(() => this.hide(), duration);
          logger.debug(`Progress started for: ${this.id}, duration: ${duration}ms`);
        }
      }

      _pauseProgress() {
        if (this.isDisposed) return;

        clearTimeout(this.progressTimeout);
        const progressBar = this.element.querySelector('.progress-bar');
        if (progressBar) {
          const computedStyle = window.getComputedStyle(progressBar);
          progressBar.style.transition = 'none';
          progressBar.style.transform = computedStyle.transform;
          logger.debug(`Progress paused for: ${this.id}`);
        }
      }

      dispose() {
        if (this.isDisposed) return;

        this.isDisposed = true;
        clearTimeout(this.progressTimeout);

        if (this.element?.parentNode) {
          this.element.parentNode.removeChild(this.element);
          logger.debug(`Removed snackbar from DOM: ${this.id}`);
        }

        EventEmitter.emit('snackbar:disposed', { id: this.id });
      }
    }

    return {
      initialize(snackbarId) {
        DropBearUtils.validateArgs([snackbarId], ['string'], 'initialize');

        try {
          if (snackbars.has(snackbarId)) {
            logger.warn(`Snackbar already exists for ${snackbarId}, disposing old instance`);
            this.dispose(snackbarId);
          }

          const manager = new SnackbarManager(snackbarId);
          snackbars.set(snackbarId, manager);
          logger.debug(`Snackbar initialized: ${snackbarId}`);
        } catch (error) {
          logger.error('Snackbar initialization error:', error);
          throw error;
        }
      },

      show(snackbarId) {
        const manager = snackbars.get(snackbarId);
        if (manager) {
          manager.show();
        }
      },

      startProgress(snackbarId, duration) {
        const manager = snackbars.get(snackbarId);
        if (manager) {
          manager.startProgress(duration);
        }
      },

      hide(snackbarId) {
        const manager = snackbars.get(snackbarId);
        if (manager) {
          return manager.hide();
        }
      },

      dispose(snackbarId) {
        try {
          const manager = snackbars.get(snackbarId);
          if (manager) {
            manager.dispose();
            snackbars.delete(snackbarId);
            logger.debug(`Snackbar disposed: ${snackbarId}`);
          }
        } catch (error) {
          logger.error(`Error disposing snackbar ${snackbarId}:`, error);
        }
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
        await retryOperation(async () =>
          await this.dotNetReference.invokeMethodAsync('SetMaxWidthBasedOnWindowSize')
        );
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

  const DropBearContextMenu = (() => {
    const logger = DropBearUtils.createLogger('DropBearContextMenu');
    const menuInstances = new Map();

    class ContextMenuManager {
      constructor(id, dotNetRef) {
        DropBearUtils.validateArgs([id, dotNetRef], ['string', 'object'], 'ContextMenuManager');

        this.id = id;
        this.element = document.getElementById(id);
        this.dotNetRef = dotNetRef;
        this.isDisposed = false;

        if (!DropBearUtils.isElement(this.element)) {
          throw new TypeError('Invalid element provided to ContextMenuManager');
        }

        this.handleContextMenu = this.handleContextMenu.bind(this);
        this.handleDocumentClick = this.handleDocumentClick.bind(this);

        this.initialize();
        EventEmitter.emit('context-menu:created', {id});
        logger.debug(`Context menu initialized: ${id}`);
      }

      initialize() {
        this.element.addEventListener('contextmenu', this.handleContextMenu);
        document.addEventListener('click', this.handleDocumentClick);
      }

      async handleContextMenu(e) {
        e.preventDefault();
        if (this.isDisposed) return;

        const x = e.pageX;
        const y = e.pageY;
        await this.show(x, y);
      }

      async handleDocumentClick() {
        if (this.isDisposed) return;

        try {
          await this.dotNetRef.invokeMethodAsync('Hide');
        } catch (error) {
          if (!error.message.includes('There is no tracked object with id')) {
            logger.error('Error hiding context menu:', error);
          }
        }
      }

      async show(x, y) {
        if (this.isDisposed) return;

        try {
          await this.dotNetRef.invokeMethodAsync('Show', x, y);
        } catch (error) {
          logger.error('Error showing context menu:', error);
          throw error;
        }
      }

      dispose() {
        if (this.isDisposed) return;

        this.isDisposed = true;
        this.element.removeEventListener('contextmenu', this.handleContextMenu);
        document.removeEventListener('click', this.handleDocumentClick);
        this.dotNetRef = null;

        logger.debug(`Context menu disposed: ${this.id}`);
        EventEmitter.emit('context-menu:disposed', {id: this.id});
      }
    }

    return {
      initialize(menuId, dotNetRef) {
        DropBearUtils.validateArgs([menuId, dotNetRef], ['string', 'object'], 'initialize');

        try {
          if (menuInstances.has(menuId)) {
            logger.warn(`Context menu already exists for ${menuId}, disposing old instance`);
            this.dispose(menuId);
          }

          const manager = new ContextMenuManager(menuId, dotNetRef);
          menuInstances.set(menuId, manager);
          logger.debug(`Context menu initialized: ${menuId}`);
        } catch (error) {
          logger.error('Context menu initialization error:', error);
          throw error;
        }
      },

      show(menuId, x, y) {
        const manager = menuInstances.get(menuId);
        if (manager) {
          return manager.show(x, y);
        }
      },

      dispose(menuId) {
        try {
          const manager = menuInstances.get(menuId);
          if (manager) {
            manager.dispose();
            menuInstances.delete(menuId);
            logger.debug(`Context menu disposed: ${menuId}`);
          }
        } catch (error) {
          logger.error(`Error disposing context menu ${menuId}:`, error);
        }
      },

      disposeAll() {
        try {
          const ids = Array.from(menuInstances.keys());
          ids.forEach(id => this.dispose(id));
          menuInstances.clear();
          logger.debug('All context menus disposed');
        } catch (error) {
          logger.error('Error disposing all context menus:', error);
        }
      }
    };
  })();

  const DropBearValidationErrors = (() => {
    const logger = DropBearUtils.createLogger('DropBearValidationErrors');
    const validationContainers = new Map();

    class ValidationErrorsManager {
      constructor(id) {
        DropBearUtils.validateArgs([id], ['string'], 'ValidationErrorsManager');

        this.id = id;
        this.element = document.getElementById(id);
        this.isDisposed = false;
        this.list = this.element?.querySelector('.validation-errors__list');
        this.header = this.element?.querySelector('.validation-errors__header');

        if (!DropBearUtils.isElement(this.element)) {
          throw new TypeError('Invalid element provided to ValidationErrorsManager');
        }

        this.initialize();
        EventEmitter.emit('validation-errors:created', {id});
        logger.debug(`Validation errors container initialized: ${id}`);
      }

      initialize() {
        try {
          this.header?.addEventListener('keydown', this.handleKeydown.bind(this));
          logger.debug(`Validation container ${this.id} initialized`);
        } catch (error) {
          logger.error(`Error initializing validation container: ${error.message}`);
          throw error;
        }
      }

      handleKeydown(event) {
        if (event.key === 'Enter' || event.key === ' ') {
          event.preventDefault();
          this.header.click();
        }
      }

      async updateAriaAttributes(isCollapsed) {
        if (this.isDisposed) return;

        await retryOperation(async () => {
          try {
            PerformanceMonitor.start(`validation-aria-update-${this.id}`);

            if (this.list) {
              this.list.setAttribute('aria-hidden', isCollapsed.toString());

              const items = this.list.querySelectorAll('.validation-errors__item');
              items.forEach(item => item.setAttribute('tabindex', isCollapsed ? '-1' : '0'));
            }

            if (this.header) {
              this.header.setAttribute('aria-expanded', (!isCollapsed).toString());
            }

            logger.debug(`Aria attributes updated for ${this.id}, collapsed: ${isCollapsed}`);
          } catch (error) {
            logger.error(`Error updating aria attributes: ${error.message}`);
            throw error;
          } finally {
            PerformanceMonitor.end(`validation-aria-update-${this.id}`);
          }
        });
      }

      dispose() {
        if (this.isDisposed) return;

        this.isDisposed = true;
        this.header?.removeEventListener('keydown', this.handleKeydown.bind(this));

        logger.debug(`Validation errors container disposed: ${this.id}`);
        EventEmitter.emit('validation-errors:disposed', {id: this.id});
      }
    }

    return {
      initialize(containerId) {
        DropBearUtils.validateArgs([containerId], ['string'], 'initialize');

        try {
          if (validationContainers.has(containerId)) {
            logger.warn(`Validation container already exists for ${containerId}, disposing old instance`);
            this.dispose(containerId);
          }

          const manager = new ValidationErrorsManager(containerId);
          validationContainers.set(containerId, manager);
          logger.debug(`Validation container initialized: ${containerId}`);
        } catch (error) {
          logger.error('Validation container initialization error:', error);
          throw error;
        }
      },

      async updateAriaAttributes(containerId, isCollapsed) {
        const manager = validationContainers.get(containerId);
        if (manager) {
          await manager.updateAriaAttributes(isCollapsed);
        }
      },

      dispose(containerId) {
        try {
          const manager = validationContainers.get(containerId);
          if (manager) {
            manager.dispose();
            validationContainers.delete(containerId);
            logger.debug(`Validation container disposed: ${containerId}`);
          }
        } catch (error) {
          logger.error(`Error disposing validation container ${containerId}:`, error);
        }
      },

      disposeAll() {
        try {
          const ids = Array.from(validationContainers.keys());
          ids.forEach(id => this.dispose(id));
          validationContainers.clear();
          logger.debug('All validation containers disposed');
        } catch (error) {
          logger.error('Error disposing all validation containers:', error);
        }
      }
    };
  })();

  window.getWindowDimensions = DropBearUtilities.getWindowDimensions;

  const initializeDropBear = async () => {
    try {
      await new Promise((resolve, reject) => {
        let attempts = 0;
        const checkBlazor = setInterval(() => {
          attempts++;
          if (window.Blazor) {
            clearInterval(checkBlazor);
            resolve();
          }
          if (attempts > 50) {
            clearInterval(checkBlazor);
            reject(new Error('Blazor not initialized after 5 seconds'));
          }
        }, 100);
      });

      if (DropBearState.initialized) {
        console.warn('DropBear already initialized');
        return;
      }

      PerformanceMonitor.start('dropbear-initialization');

      Object.defineProperties(window, {
        DropBearSnackbar: {value: DropBearSnackbar, writable: false, configurable: false},
        DropBearUtils: {value: DropBearUtils, writable: false, configurable: false},
        DropBearResizeManager: {value: DropBearResizeManager, writable: false, configurable: false},
        DropBearNavigationButtons: {value: DropBearNavigationButtons, writable: false, configurable: false},
        DropBearContextMenu: {value: DropBearContextMenu, writable: false, configurable: false},
        DropBearValidationErrors: {value: DropBearValidationErrors, writable: false, configurable: false}
      });

      // Add after Object.defineProperties in initializeDropBear
      window.validationErrors = {
        updateAriaAttributes: (componentId, isCollapsed) => {
          try {
            return window.DropBearValidationErrors.updateAriaAttributes(componentId, isCollapsed);
          } catch (error) {
            console.error('Error updating validation errors aria attributes:', error);
            throw error;
          }
        }
      };

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

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', () => initializeDropBear().catch(error => console.error("Failed to initialize DropBear:", error)));
  } else {
    initializeDropBear().catch(error => console.error("Failed to initialize DropBear:", error));
  }

  window.addEventListener('unload', () => {
    try {
      DropBearSnackbar.dispose();
      DropBearResizeManager.dispose();
      DropBearNavigationButtons.dispose();
      if (DropBearContextMenu) DropBearContextMenu.disposeAll();
      if (DropBearValidationErrors) DropBearValidationErrors.disposeAll();
      console.log("DropBear cleanup complete");
    } catch (error) {
      console.error("Error during DropBear cleanup:", error);
    }
  });
})();
