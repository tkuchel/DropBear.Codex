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
        this.animationFrame = null;

        if (!DropBearUtils.isElement(this.element)) {
          throw new TypeError('Invalid element provided to SnackbarManager');
        }

        // Find the scoped attribute
        this.scopedAttribute = Object.keys(this.element.attributes)
          .map(key => this.element.attributes[key])
          .find(attr => attr.name.startsWith('b-'))?.name;

        this._setupEventListeners();

        EventEmitter.emit('snackbar:created', {id});
        logger.debug(`Snackbar initialized: ${id} with scope ${this.scopedAttribute}`);
      }

      _setupEventListeners() {
        this.mouseEnterHandler = () => this._pauseProgress();
        this.mouseLeaveHandler = () => this._resumeProgress();
        this.element.addEventListener('mouseenter', this.mouseEnterHandler);
        this.element.addEventListener('mouseleave', this.mouseLeaveHandler);
      }

      _cleanupEventListeners() {
        if (this.element) {
          this.element.removeEventListener('mouseenter', this.mouseEnterHandler);
          this.element.removeEventListener('mouseleave', this.mouseLeaveHandler);
        }
      }

      show() {
        if (this.isDisposed) return;

        return retryOperation(async () => {
          cancelAnimationFrame(this.animationFrame);

          // Force layout recalculation
          void this.element.offsetWidth;

          this.element.classList.remove('hide');

          this.animationFrame = requestAnimationFrame(() => {
            this.element.classList.add('show');
            logger.debug(`Snackbar shown: ${this.id}`);
          });
        });
      }

      hide() {
        if (this.isDisposed) return Promise.resolve();

        return new Promise(resolve => {
          clearTimeout(this.progressTimeout);
          cancelAnimationFrame(this.animationFrame);

          const handleTransitionEnd = () => {
            this.element.removeEventListener('transitionend', handleTransitionEnd);
            this.dispose();
            resolve();
          };

          // Force layout recalculation
          void this.element.offsetWidth;

          this.element.classList.remove('show');
          this.element.classList.add('hide');

          // Setup transition end listener
          DropBearUtils.createOneTimeListener(
            this.element,
            'transitionend',
            handleTransitionEnd,
            ANIMATION_DURATION + 100
          ).catch(() => {
            // Fallback if transition doesn't complete
            this.dispose();
            resolve();
          });
        });
      }

      startProgress(duration) {
        if (this.isDisposed || !duration) return;

        try {
          const progressBar = this.element.querySelector('.progress-bar');
          if (!progressBar) return;

          PerformanceMonitor.start(`progress-start-${this.id}`);

          // Reset progress
          clearTimeout(this.progressTimeout);
          progressBar.style.transition = 'none';
          progressBar.style.transform = 'scaleX(1)';

          // Force layout recalculation
          void progressBar.offsetWidth;

          // Start progress animation
          this.element.style.setProperty('--duration', `${duration}ms`);
          progressBar.style.transition = `transform ${duration}ms linear`;

          this.animationFrame = requestAnimationFrame(() => {
            progressBar.style.transform = 'scaleX(0)';
            this.progressTimeout = setTimeout(() => this.hide(), duration);
          });

          logger.debug(`Progress started for: ${this.id}, duration: ${duration}ms`);
        } catch (error) {
          logger.error(`Error starting progress for ${this.id}:`, error);
        } finally {
          PerformanceMonitor.end(`progress-start-${this.id}`);
        }
      }

      _pauseProgress() {
        if (this.isDisposed) return;

        try {
          clearTimeout(this.progressTimeout);
          const progressBar = this.element.querySelector('.progress-bar');
          if (progressBar) {
            const computedStyle = window.getComputedStyle(progressBar);
            const currentTransform = computedStyle.transform;
            progressBar.style.transition = 'none';
            progressBar.style.transform = currentTransform;

            void progressBar.offsetWidth;
            logger.debug(`Progress paused for: ${this.id}`);
          }
        } catch (error) {
          logger.error(`Error pausing progress for ${this.id}:`, error);
        }
      }

      _resumeProgress() {
        if (this.isDisposed) return;

        try {
          const progressBar = this.element.querySelector('.progress-bar');
          if (progressBar) {
            const duration = parseFloat(this.element.style.getPropertyValue('--duration')) || 5000;
            const computedStyle = window.getComputedStyle(progressBar);
            const currentScale = this._getCurrentScale(computedStyle.transform);
            const remainingTime = duration * currentScale;

            progressBar.style.transition = `transform ${remainingTime}ms linear`;

            requestAnimationFrame(() => {
              progressBar.style.transform = 'scaleX(0)';
              this.progressTimeout = setTimeout(() => this.hide(), remainingTime);
            });

            logger.debug(`Progress resumed for: ${this.id}, remaining: ${remainingTime}ms`);
          }
        } catch (error) {
          logger.error(`Error resuming progress for ${this.id}:`, error);
        }
      }

      _getCurrentScale(transform) {
        if (transform === 'none') return 1;
        const match = transform.match(/matrix\\(([^)]+)\\)/);
        return match ? parseFloat(match[1].split(',')[0]) : 1;
      }

      dispose() {
        if (this.isDisposed) return;

        try {
          PerformanceMonitor.start(`snackbar-dispose-${this.id}`);

          this.isDisposed = true;
          clearTimeout(this.progressTimeout);
          cancelAnimationFrame(this.animationFrame);
          this._cleanupEventListeners();

          if (this.element?.parentNode) {
            this.element.parentNode.removeChild(this.element);
            logger.debug(`Removed snackbar from DOM: ${this.id}`);
          }

          EventEmitter.emit('snackbar:disposed', {id: this.id});
        } catch (error) {
          logger.error(`Error disposing snackbar ${this.id}:`, error);
        } finally {
          PerformanceMonitor.end(`snackbar-dispose-${this.id}`);
        }
      }
    }

    return {
      initialize(snackbarId) {
        DropBearUtils.validateArgs([snackbarId], ['string'], 'initialize');

        return retryOperation(async () => {
          try {
            PerformanceMonitor.start(`snackbar-init-${snackbarId}`);

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
          } finally {
            PerformanceMonitor.end(`snackbar-init-${snackbarId}`);
          }
        });
      },

      show(snackbarId) {
        const manager = snackbars.get(snackbarId);
        if (manager) {
          return manager.show();
        }
        return Promise.resolve(false);
      },

      startProgress(snackbarId, duration) {
        const manager = snackbars.get(snackbarId);
        if (manager) {
          manager.startProgress(duration);
          return true;
        }
        return false;
      },

      hide(snackbarId) {
        const manager = snackbars.get(snackbarId);
        if (manager) {
          return manager.hide();
        }
        return Promise.resolve(false);
      },

      dispose(snackbarId) {
        try {
          PerformanceMonitor.start('snackbar-disposal');

          const manager = snackbars.get(snackbarId);
          if (manager) {
            manager.dispose();
            snackbars.delete(snackbarId);
            logger.debug(`Snackbar disposed: ${snackbarId}`);
          }
        } catch (error) {
          logger.error(`Error disposing snackbar ${snackbarId}:`, error);
        } finally {
          PerformanceMonitor.end('snackbar-disposal');
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

  const DropBearFileDownloader = (() => {
    const logger = DropBearUtils.createLogger('DropBearFileDownloader');

    return {
      downloadFileFromStream: async (fileName, content, contentType) => {
        try {
          logger.debug('downloadFileFromStream called with:', {fileName, content, contentType});

          let blob;

          if (content instanceof Blob) {
            logger.debug('Content is a Blob.');
            blob = content;
          } else if (content.arrayBuffer) {
            logger.debug('Content has arrayBuffer method (DotNetStreamReference detected).');
            const arrayBuffer = await content.arrayBuffer();
            logger.debug('ArrayBuffer received, byte length:', arrayBuffer.byteLength);
            blob = new Blob([arrayBuffer], {type: contentType});
          } else if (content instanceof Uint8Array) {
            logger.debug('Content is a Uint8Array.');
            blob = new Blob([content], {type: contentType});
          } else {
            throw new Error('Unsupported content type');
          }

          logger.debug('Blob created, size:', blob.size);

          const url = URL.createObjectURL(blob);
          const anchorElement = document.createElement('a');
          anchorElement.href = url;
          anchorElement.download = fileName || 'download';

          document.body.appendChild(anchorElement);

          setTimeout(() => {
            logger.debug('Triggering download...');
            anchorElement.click();
            document.body.removeChild(anchorElement);
            URL.revokeObjectURL(url);
            logger.debug('Download completed and cleanup done.');
          }, 0);
        } catch (error) {
          logger.error('Error in downloadFileFromStream:', error);
        }
      }
    };
  })();

  const DropBearPageAlert = (() => {
    const logger = DropBearUtils.createLogger('DropBearPageAlert');
    const alerts = new Map();
    const ANIMATION_DURATION = 300;

    class PageAlertManager {
      constructor(id, isPermanent) {
        this.id = id;
        this.isPermanent = isPermanent;
        this.isDisposed = false;
        this.element = document.getElementById(id);
        if (!this.element) {
          throw new Error(`Element with id ${id} not found`);
        }
        this._setupEventListeners();
      }

      _setupEventListeners() {
        this.mouseEnterHandler = () => this._pauseProgress();
        this.mouseLeaveHandler = () => this._resumeProgress();
        this.element.addEventListener('mouseenter', this.mouseEnterHandler);
        this.element.addEventListener('mouseleave', this.mouseLeaveHandler);
      }

      _cleanupEventListeners() {
        if (!this.isPermanent && this.element) {
          this.element.removeEventListener('mouseenter', this.mouseEnterHandler);
          this.element.removeEventListener('mouseleave', this.mouseLeaveHandler);
        }
      }

      show() {
        if (this.isDisposed || !this.element) return Promise.resolve(true);

        return new Promise(resolve => {
          // Store handler reference for cleanup
          this.transitionEndHandler = () => {
            if (this.element) {
              this.element.removeEventListener('transitionend', this.transitionEndHandler);
            }
            this.transitionEndHandler = null;
            resolve(true);
          };

          cancelAnimationFrame(this.animationFrame);
          this.element.classList.remove('hide');

          this.animationFrame = requestAnimationFrame(() => {
            this.element.classList.add('show');
            this.element.addEventListener('transitionend', this.transitionEndHandler, {once: true});
            logger.debug(`PageAlert shown: ${this.id}`);
          });

          // Failsafe cleanup
          setTimeout(() => {
            if (this.transitionEndHandler) {
              this.transitionEndHandler();
            }
          }, ANIMATION_DURATION + 100);
        }).catch(error => {
          logger.error(`Error in show method for ${this.id}:`, error);
          return false;
        });
      }

      hide() {
        if (this.isDisposed) return Promise.resolve(true); // Return true if already disposed

        return new Promise(resolve => {
          if (!this.element) {
            resolve(true); // No element to hide
            return;
          }

          let called = false;
          const handleTransitionEnd = () => {
            if (called) return;
            called = true;
            this.element.removeEventListener('transitionend', handleTransitionEnd);
            this.dispose();
            resolve(true); // Successfully hidden
          };

          clearTimeout(this.progressTimeout);
          cancelAnimationFrame(this.animationFrame);

          this.element.classList.remove('show');
          this.element.classList.add('hide');
          this.element.addEventListener('transitionend', handleTransitionEnd);

          // Failsafe to ensure resolve is called
          setTimeout(() => handleTransitionEnd(), ANIMATION_DURATION + 100);
        }).catch(error => {
          logger.error(`Error in hide method for ${this.id}:`, error);
          return false; // Return false on error
        });
      }

      startProgress(duration) {
        if (this.isDisposed || this.isPermanent) return;
        if (typeof duration !== 'number' || duration <= 0) return;

        this.progressDuration = duration;

        try {
          const progressBar = this.element.querySelector('.page-alert-progress-bar');
          if (!progressBar) {
            logger.error(`Progress bar element not found for ${this.id}`);
            return;
          }

          clearTimeout(this.progressTimeout);
          progressBar.style.transition = 'none';
          progressBar.style.transform = 'scaleX(1)';

          void progressBar.offsetWidth;

          progressBar.style.transition = `transform ${duration}ms linear`;
          this.animationFrame = requestAnimationFrame(() => {
            progressBar.style.transform = 'scaleX(0)';
            this.progressTimeout = setTimeout(() => this.hide(), duration);
          });

          logger.debug(`Progress started for: ${this.id}, duration: ${duration}ms`);
        } catch (error) {
          logger.error(`Error starting progress for ${this.id}:`, error);
        }
      }

      _pauseProgress() {
        if (this.isDisposed || this.isPermanent) return;

        clearTimeout(this.progressTimeout);
        const progressBar = this.element.querySelector('.page-alert-progress-bar');
        if (progressBar) {
          const computedStyle = window.getComputedStyle(progressBar);
          progressBar.style.transition = 'none';
          progressBar.style.transform = computedStyle.transform;
        }
      }

      _resumeProgress() {
        if (this.isDisposed || this.isPermanent) return;

        const progressBar = this.element.querySelector('.page-alert-progress-bar');
        if (progressBar) {
          const computedStyle = window.getComputedStyle(progressBar);
          const currentScale = this._getCurrentScale(computedStyle.transform);
          const remainingTime = this.progressDuration * currentScale;

          progressBar.style.transition = `transform ${remainingTime}ms linear`;
          progressBar.style.transform = 'scaleX(0)';
          this.progressTimeout = setTimeout(() => this.hide(), remainingTime);
        }
      }

      _getCurrentScale(transform) {
        if (transform === 'none') return 1;
        const values = transform.match(/matrix\\(([^)]+)\\)/);
        if (values) {
          const matrixValues = values[1].split(', ');
          return parseFloat(matrixValues[0]);
        }
        return 1;
      }

      dispose() {
        if (this.isDisposed) return;

        try {
          this.isDisposed = true;

          // Clear all timeouts and animations first
          clearTimeout(this.progressTimeout);
          cancelAnimationFrame(this.animationFrame);

          // Clean up event listeners
          this._cleanupEventListeners();

          // Store element reference
          const element = this.element;

          // Clear references
          this.element = null;
          this.transitionEndHandler = null;

          // Remove from DOM last
          if (element?.parentNode) {
            element.parentNode.removeChild(element);
            logger.debug(`Removed page alert from DOM: ${this.id}`);
          }

          alerts.delete(this.id);
        } catch (error) {
          logger.error(`Error disposing page alert ${this.id}:`, error);
        }
      }
    }

    return {
      create(id, duration = 5000, isPermanent = false) {
        try {
          DropBearUtils.validateArgs([id], ['string'], 'create');

          // Check if alert already exists and dispose it
          const existingAlert = alerts.get(id);
          if (existingAlert) {
            logger.debug(`Alert ${id} already exists, disposing old instance`);
            existingAlert.dispose();
          }

          // Create and store new alert
          const manager = new PageAlertManager(id, isPermanent);
          alerts.set(id, manager);

          // Show alert and start progress if applicable
          manager.show().then(() => {
            if (!isPermanent && typeof duration === 'number' && duration > 0) {
              manager.startProgress(duration);
            }
          });

          return true;
        } catch (error) {
          logger.error('Error creating page alert:', error);
          return false;
        }
      },

      hide(id) {
        try {
          DropBearUtils.validateArgs([id], ['string'], 'hide');
          const manager = alerts.get(id);
          if (manager) {
            return manager.hide();
          }
          return Promise.resolve(true); // Return true if no manager exists
        } catch (error) {
          logger.error('Error hiding page alert:', error);
          return Promise.resolve(false); // Return false on error
        }
      },

      hideAll() {
        try {
          const promises = Array.from(alerts.values()).map(manager => manager.hide());
          return Promise.all(promises);
        } catch (error) {
          logger.error('Error hiding all page alerts:', error);
          return Promise.resolve([]); // Return an empty array on error
        }
      }
    };
  })();


  const DropBearProgressBar = (() => {
    const logger = DropBearUtils.createLogger('DropBearProgressBar');
    const progressBars = new Map();
    const ANIMATION_DURATION = 300;

    class ProgressBarManager {
      constructor(id, dotNetRef) {
        DropBearUtils.validateArgs([id, dotNetRef], ['string', 'object'], 'ProgressBarManager');

        this.id = id;
        this.element = document.getElementById(id);
        this.dotNetRef = dotNetRef;
        this.isDisposed = false;
        this.animationFrame = null;

        if (!DropBearUtils.isElement(this.element)) {
          throw new TypeError('Invalid element provided to ProgressBarManager');
        }

        logger.debug(`Progress bar initialized: ${id}`);
      }

      updateProgress(progress) {
        if (this.isDisposed) return;

        try {
          cancelAnimationFrame(this.animationFrame);
          const progressBar = this.element.querySelector('.progress-bar');
          if (progressBar) {
            this.animationFrame = requestAnimationFrame(() => {
              progressBar.style.width = `${progress}%`;
            });
          }
        } catch (error) {
          logger.error(`Error updating progress for ${this.id}:`, error);
        }
      }

      updateStepStatus(stepName, status) {
        if (this.isDisposed) return;

        try {
          const step = Array.from(this.element.querySelectorAll('.step'))
            .find(s => s.querySelector('.step-label').textContent === stepName);

          if (step) {
            step.className = `step ${this.getStepClass(status)}`;
            step.setAttribute('data-status', status);

            // Trigger animation
            void step.offsetWidth; // Force reflow
            step.classList.add('animate-step');
          }
        } catch (error) {
          logger.error(`Error updating step status for ${this.id}:`, error);
        }
      }

      updateStepDisplay(currentIndex, totalSteps) {
        if (this.isDisposed) return false;

        try {
          const stepWindow = this.element.querySelector('.step-window');
          if (!stepWindow) return false;

          cancelAnimationFrame(this.animationFrame);

          this.animationFrame = requestAnimationFrame(() => {
            const steps = stepWindow.querySelectorAll('.step');
            steps.forEach((step, index) => {
              const position = index - currentIndex + 1;

              // Add transition class
              step.classList.add('step-transition');

              if (position >= -1 && position <= 1) {
                step.style.display = 'flex';
                step.style.opacity = position === 0 ? '1' : '0.8';
                step.style.transform = `translateX(${position * 100}%)`;
              } else {
                step.style.display = 'none';
              }
            });

            // Update counter
            const counter = this.element.querySelector('.step-counter');
            if (counter) {
              counter.textContent = `Step ${currentIndex + 1} of ${totalSteps}`;
            }
          });

          return true;
        } catch (error) {
          logger.error(`Error updating step display for ${this.id}:`, error);
          return false;
        }
      }

      getStepClass(status) {
        switch (status) {
          case 'Completed':
            return 'completed success';
          case 'Warning':
            return 'completed warning';
          case 'Error':
            return 'completed error';
          case 'Active':
            return 'active';
          default:
            return '';
        }
      }

      dispose() {
        if (this.isDisposed) return;

        try {
          this.isDisposed = true;
          cancelAnimationFrame(this.animationFrame);
          this.dotNetRef = null;
          logger.debug(`Progress bar disposed: ${this.id}`);
        } catch (error) {
          logger.error(`Error disposing progress bar ${this.id}:`, error);
        }
      }
    }

    return {
      initialize(progressId, dotNetRef) {
        try {
          if (progressBars.has(progressId)) {
            logger.debug(`Progress bar already exists for ${progressId}, disposing old instance`);
            this.dispose(progressId);
          }

          const manager = new ProgressBarManager(progressId, dotNetRef);
          progressBars.set(progressId, manager);
          logger.debug(`Progress bar initialized: ${progressId}`);
          return true;
        } catch (error) {
          logger.error('Progress bar initialization error:', error);
          return false;
        }
      },

      updateProgress(progressId, taskProgress, overallProgress) {
        const manager = progressBars.get(progressId);
        if (!manager) return false;

        manager.updateProgress(taskProgress);

        if (overallProgress !== undefined) {
          const overallBar = manager.element.querySelector('.overall-progress-bar');
          if (overallBar) {
            overallBar.style.width = `${overallProgress}%`;
          }
        }
        return true;
      },

      updateStepStatus(progressId, stepName, status) {
        const manager = progressBars.get(progressId);
        return manager ? manager.updateStepStatus(stepName, status) : false;
      },

      updateStepDisplay(progressId, currentIndex, totalSteps) {
        const manager = progressBars.get(progressId);
        return manager ? manager.updateStepDisplay(currentIndex, totalSteps) : false;
      },

      dispose(progressId) {
        try {
          const manager = progressBars.get(progressId);
          if (manager) {
            manager.dispose();
            progressBars.delete(progressId);
            logger.debug(`Progress bar disposed: ${progressId}`);
          }
        } catch (error) {
          logger.error(`Error disposing progress bar ${progressId}:`, error);
        }
      },

      disposeAll() {
        try {
          Array.from(progressBars.keys()).forEach(id => this.dispose(id));
          logger.debug('All progress bars disposed');
        } catch (error) {
          logger.error('Error disposing all progress bars:', error);
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
        DropBearValidationErrors: {value: DropBearValidationErrors, writable: false, configurable: false},
        DropBearFileDownloader: {value: DropBearFileDownloader, writable: false, configurable: false},
        DropBearPageAlert: {value: DropBearPageAlert, writable: false, configurable: false},
        DropBearProgressBar: {value: DropBearProgressBar, writable: false, configurable: false}
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
      if (DropBearPageAlert) DropBearPageAlert.hideAll();
      if (DropBearProgressBar) DropBearProgressBar.disposeAll();
      console.log("DropBear cleanup complete");
    } catch (error) {
      console.error("Error during DropBear cleanup:", error);
    }
  });
})();
