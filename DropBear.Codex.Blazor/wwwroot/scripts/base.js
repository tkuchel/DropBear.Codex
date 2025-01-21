/// <reference types="./base" />
// @ts-check

/**
 * @typedef {import('./base').ILogger} ILogger
 * @typedef {import('./base').IDisposable} IDisposable
 * @typedef {import('./base').IEventEmitter} IEventEmitter
 * @typedef {import('./base').IDOMOperationQueue} IDOMOperationQueue
 * @typedef {import('./base').IResourcePool} IResourcePool
 * @typedef {import('./base').IModuleManager} IModuleManager
 * @typedef {import('./base').ICircuitBreaker} ICircuitBreaker
 * @typedef {import('./base').ISnackbarManager} ISnackbarManager
 * @typedef {import('./base').IResizeManager} IResizeManager
 * @typedef {import('./base').INavigationManager} INavigationManager
 * @typedef {import('./base').IContextMenuManager} IContextMenuManager
 * @typedef {import('./base').IValidationErrorsManager} IValidationErrorsManager
 * @typedef {import('./base').IProgressBarManager} IProgressBarManager
 * @typedef {import('./base').IDotNetReference} IDotNetReference
 * @typedef {import('./base').IDropBearError} IDropBearError
 * @typedef {import('./base').IDropBearEvent} IDropBearEvent
 */

(() => {
  'use strict';

  // Core optimization utilities
  /** @implements {IDOMOperationQueue} */
  const DOMOperationQueue = {
    queue: new Set(),
    scheduled: false,

    add(operation) {
      this.queue.add(operation);
      if (!this.scheduled) {
        this.scheduled = true;
        requestAnimationFrame(() => this.flush());
      }
    },

    flush() {
      this.queue.forEach(operation => {
        try {
          operation();
        } catch (error) {
          console.error('Error in queued operation:', error);
        }
      });
      this.queue.clear();
      this.scheduled = false;
    }
  };

  // Enhanced Event Emitter with WeakMap
  /** @implements {IEventEmitter} */
  const EventEmitter = {
    events: new WeakMap(),

    on(target, event, callback) {
      if (!this.events.has(target)) {
        this.events.set(target, new Map());
      }
      const targetEvents = this.events.get(target);
      if (!targetEvents.has(event)) {
        targetEvents.set(event, new Set());
      }
      targetEvents.get(event).add(callback);
      return () => this.off(target, event, callback);
    },

    off(target, event, callback) {
      const targetEvents = this.events.get(target);
      if (targetEvents?.has(event)) {
        targetEvents.get(event).delete(callback);
      }
    },

    emit(target, event, data) {
      const targetEvents = this.events.get(target);
      if (targetEvents?.has(event)) {
        targetEvents.get(event).forEach(callback => {
          try {
            callback(data);
          } catch (error) {
            console.error(`Error in event handler for ${event}:`, error);
          }
        });
      }
    }
  };

  // Module Manager for dependency handling
  /** @implements {IModuleManager} */
  const ModuleManager = {
    modules: new Map(),
    dependencies: new Map(),
    initialized: new Set(),

    register(name, module, dependencies = []) {
      this.dependencies.set(name, dependencies);
      this.modules.set(name, module);
    },

    async initialize(moduleName) {
      if (this.initialized.has(moduleName)) return;

      if (!this.modules.has(moduleName)) {
        throw new Error(`Module ${moduleName} not found`);
      }

      const deps = this.dependencies.get(moduleName) || [];
      await Promise.all(deps.map(dep => this.initialize(dep)));

      const module = this.modules.get(moduleName);
      if (typeof module.initialize === 'function') {
        await module.initialize();
      }

      this.initialized.add(moduleName);
    },

    get(moduleName) {
      return this.modules.get(moduleName);
    }
  };

  // Circuit Breaker for resilient operations
  /** @implements {ICircuitBreaker} */
  class CircuitBreaker {
    constructor(options = {}) {
      this.failureThreshold = options.failureThreshold || 5;
      this.resetTimeout = options.resetTimeout || 60000;
      this.failures = 0;
      this.lastFailureTime = null;
      this.state = 'closed';
    }

    async execute(operation) {
      if (this.state === 'open') {
        if (Date.now() - this.lastFailureTime >= this.resetTimeout) {
          this.state = 'half-open';
        } else {
          throw new Error('Circuit breaker is open');
        }
      }

      try {
        const result = await operation();
        if (this.state === 'half-open') {
          this.reset();
        }
        return result;
      } catch (error) {
        this.recordFailure();
        throw error;
      }
    }

    recordFailure() {
      this.failures++;
      this.lastFailureTime = Date.now();
      if (this.failures >= this.failureThreshold) {
        this.state = 'open';
      }
    }

    reset() {
      this.failures = 0;
      this.lastFailureTime = null;
      this.state = 'closed';
    }
  }

  /** @implements {IResourcePool} */
  const ResourcePool = {
    /** @type {Map<string, Array<any>>} */
    pools: new Map(),

    /**
     * @param {string} type
     * @param {() => any} factory
     * @param {number} [initialSize]
     * @returns {void}
     */
    create(type, factory, initialSize = 10) {
      const pool = [];
      for (let i = 0; i < initialSize; i++) {
        pool.push(factory());
      }
      this.pools.set(type, pool);
    },

    /**
     * @template T
     * @param {string} type
     * @returns {T | null}
     */
    acquire(type) {
      const pool = this.pools.get(type);
      if (!pool || pool.length === 0) {
        return null;
      }
      return pool.pop();
    },

    /**
     * @template T
     * @param {string} type
     * @param {T} resource
     * @returns {void}
     */
    release(type, resource) {
      const MAX_POOL_SIZE = 50;
      const pool = this.pools.get(type);
      if (pool && pool.length < MAX_POOL_SIZE) {
        pool.push(resource);
      }
    }
  };

  // Core Utilities

  /**
   * @param {string} message Error message
   * @param {string} code Error code
   * @param {string} [component] Component name
   * @param {any} [details] Additional error details
   * @returns {IDropBearError}
   */
  function createDropBearError(message, code, component, details) {
    /** @type {IDropBearError} */
    const error = new Error(message);
    error.code = code;
    if (component) error.component = component;
    if (details) error.details = details;
    return error;
  }

  /**
   * @param {string} id Component ID
   * @param {string} type Event type
   * @param {any} [data] Additional event data
   * @returns {IDropBearEvent}
   */
  function createDropBearEvent(id, type, data) {
    /** @type {IDropBearEvent} */
    const event = {
      id,
      type,
      data
    };
    return event;
  }

  /**
   * @param {string} id Component ID
   * @param {any} [data] Additional event data
   * @returns {IDropBearEvent}
   */
  function createDisposedEvent(id, data) {
    return createDropBearEvent(id, 'disposed', data);
  }

  const DropBearUtils = {
    /** @type {(namespace: string) => ILogger} */
    createLogger(namespace) {
      const prefix = `[${namespace}]`;
      return {
        debug: (message, ...args) => console.debug(`${prefix} ${message}`, ...args),
        info: (message, ...args) => console.log(`${prefix} ${message}`, ...args),
        warn: (message, ...args) => console.warn(`${prefix} ${message}`, ...args),
        error: (message, ...args) => console.error(`${prefix} ${message}`, ...args)
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

    debounce(func, wait) {
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
      let inThrottle;
      let lastFunc;
      let lastRan;
      return function executedFunction(...args) {
        if (!inThrottle) {
          func.apply(this, args);
          lastRan = Date.now();
          inThrottle = true;
        } else {
          clearTimeout(lastFunc);
          lastFunc = setTimeout(() => {
            if (Date.now() - lastRan >= limit) {
              func.apply(this, args);
              lastRan = Date.now();
            }
          }, Math.max(0, limit - (Date.now() - lastRan)));
        }
      };
    },

    isElement(element) {
      return element instanceof Element || element instanceof HTMLDocument;
    }
  };

  const DropBearUtilities = {
    getWindowDimensions() {
      try {
        return {
          width: window.innerWidth,
          height: window.innerHeight
        };
      } catch (error) {
        console.error('Error getting window dimensions:', error);
        return {width: 0, height: 0};
      }
    }
  };

  // Utility to click elements by ID
  window.clickElementById = function (id) {
    try {
      const element = document.getElementById(id);
      if (element) {
        element.click();
        return true;
      }
      return false;
    } catch (error) {
      console.error(`Error clicking element ${id}:`, error);
      return false;
    }
  };

  // Component Implementations
  const DropBearSnackbar = (() => {
    const logger = DropBearUtils.createLogger('DropBearSnackbar');
    const circuitBreaker = new CircuitBreaker({failureThreshold: 3, resetTimeout: 30000});

    /** @implements {ISnackbarManager} */
    class SnackbarManager {
      constructor(id) {
        // Validate we have a proper string
        DropBearUtils.validateArgs([id], ['string'], 'SnackbarManager');

        this.id = id;
        this.element = document.getElementById(id);
        this.isDisposed = false;
        this.progressTimeout = null;
        this.animationFrame = null;

        if (!DropBearUtils.isElement(this.element)) {
          throw new TypeError('Invalid element provided to SnackbarManager');
        }

        this.progressBar = this.element.querySelector('.progress-bar');
        this.scopedAttribute = Object.keys(this.element.attributes)
          .map(key => this.element.attributes[key])
          .find(attr => attr.name.startsWith('b-'))?.name;

        this._setupEventListeners();
        // Fire a "created" event for hooking into your event system if needed
        EventEmitter.emit(
          this.element,
          'created',
          createDropBearEvent(this.id, 'created', {timestamp: Date.now()})
        );
      }

      _setupEventListeners() {
        this.handleMouseEnter = () => this._pauseProgress();
        this.handleMouseLeave = () => this._resumeProgress();

        this.element.addEventListener('mouseenter', this.handleMouseEnter);
        this.element.addEventListener('mouseleave', this.handleMouseLeave);
      }

      show() {
        if (this.isDisposed) return Promise.resolve(false);

        return circuitBreaker.execute(async () => {
          DOMOperationQueue.add(() => {
            this.element.classList.remove('hide');
            requestAnimationFrame(() => this.element.classList.add('show'));
          });
          return true;
        });
      }

      startProgress(duration) {
        if (this.isDisposed || !duration || !this.progressBar) return;

        DOMOperationQueue.add(() => {
          clearTimeout(this.progressTimeout);
          this.progressBar.style.transition = 'none';
          this.progressBar.style.transform = 'scaleX(1)';

          requestAnimationFrame(() => {
            this.element.style.setProperty('--duration', `${duration}ms`);
            this.progressBar.style.transition = `transform ${duration}ms linear`;
            this.progressBar.style.transform = 'scaleX(0)';
            this.progressTimeout = setTimeout(() => this.hide(), duration);
          });
        });
      }

      _pauseProgress() {
        if (this.isDisposed || !this.progressBar) return;

        clearTimeout(this.progressTimeout);
        const computedStyle = window.getComputedStyle(this.progressBar);

        DOMOperationQueue.add(() => {
          this.progressBar.style.transition = 'none';
          this.progressBar.style.transform = computedStyle.transform;
        });
      }

      _resumeProgress() {
        if (this.isDisposed || !this.progressBar) return;

        const computedStyle = window.getComputedStyle(this.progressBar);
        const duration = parseFloat(
          this.element.style.getPropertyValue('--duration')
        ) || 5000;
        const currentScale = this._getCurrentScale(computedStyle.transform);
        const remainingTime = duration * currentScale;

        DOMOperationQueue.add(() => {
          this.progressBar.style.transition = `transform ${remainingTime}ms linear`;
          this.progressBar.style.transform = 'scaleX(0)';
          this.progressTimeout = setTimeout(() => this.hide(), remainingTime);
        });
      }

      _getCurrentScale(transform) {
        if (transform === 'none') return 1;
        const match = transform.match(/matrix\(([^)]+)\)/);
        return match ? parseFloat(match[1].split(',')[0]) : 1;
      }

      hide() {
        if (this.isDisposed) return Promise.resolve(false);

        return circuitBreaker.execute(async () => {
          DOMOperationQueue.add(() => {
            clearTimeout(this.progressTimeout);
            cancelAnimationFrame(this.animationFrame);
            this.element.classList.remove('show');
            this.element.classList.add('hide');
          });
          return true;
        });
      }

      dispose() {
        if (this.isDisposed) return;

        this.isDisposed = true;
        clearTimeout(this.progressTimeout);
        cancelAnimationFrame(this.animationFrame);

        this.element.removeEventListener('mouseenter', this.handleMouseEnter);
        this.element.removeEventListener('mouseleave', this.handleMouseLeave);

        DOMOperationQueue.add(() => {
          if (this.element?.parentNode) {
            this.element.parentNode.removeChild(this.element);
          }
        });

        EventEmitter.emit(
          this.element,
          'disposed',
          createDropBearEvent(this.id, 'disposed', {timestamp: Date.now()})
        );
      }
    }

    // Register with the ModuleManager
    ModuleManager.register(
      'DropBearSnackbar',
      {
        // A map of { [snackbarId]: SnackbarManager }
        snackbars: new Map(),

        /**
         * Global no-argument initialization called automatically by `initializeDropBear()`.
         * We do not need an ID here, so this method does nothing but log that we’ve loaded.
         */
        async initialize() {
          return circuitBreaker.execute(async () => logger.debug('DropBearSnackbar global module init completed (no ID required).'));
        },

        /**
         * Create a new SnackbarManager instance for a specific ID.
         * This replaces the old `initialize(snackbarId)` which
         * led to "Expected string, got undefined" if called with no argument.
         */
        async createSnackbar(snackbarId) {
          return circuitBreaker.execute(async () => {
            try {
              if (this.snackbars.has(snackbarId)) {
                logger.warn(`Snackbar already exists for ${snackbarId}, disposing old instance`);
                await this.dispose(snackbarId);
              }

              const manager = new SnackbarManager(snackbarId);
              this.snackbars.set(snackbarId, manager);
              logger.debug(`Snackbar created for ID: ${snackbarId}`);
            } catch (error) {
              logger.error('Snackbar creation error:', error);
              throw error;
            }
          });
        },

        show(snackbarId) {
          const manager = this.snackbars.get(snackbarId);
          return manager ? manager.show() : Promise.resolve(false);
        },

        startProgress(snackbarId, duration) {
          const manager = this.snackbars.get(snackbarId);
          if (manager) {
            manager.startProgress(duration);
            return true;
          }
          return false;
        },

        hide(snackbarId) {
          const manager = this.snackbars.get(snackbarId);
          return manager ? manager.hide() : Promise.resolve(false);
        },

        dispose(snackbarId) {
          const manager = this.snackbars.get(snackbarId);
          if (manager) {
            manager.dispose();
            this.snackbars.delete(snackbarId);
          }
        }
      },
      ['DropBearCore']
    );

    return ModuleManager.get('DropBearSnackbar');
  })();

  const DropBearResizeManager = (() => {
    const logger = DropBearUtils.createLogger('DropBearResizeManager');
    const circuitBreaker = new CircuitBreaker({failureThreshold: 3, resetTimeout: 30000});

    /** @implements {IResizeManager} */
    class ResizeManager {
      /**
       * @param {IDotNetReference} dotNetReference
       */
      constructor(dotNetReference) {
        this.dotNetReference = dotNetReference;
        this.resizeObserver = null;
        this.isDisposed = false;

        this._initializeResizeObserver();
        EventEmitter.emit(this, 'created', {timestamp: Date.now()});

      }

      _initializeResizeObserver() {
        this.resizeObserver = new ResizeObserver(
          DropBearUtils.debounce(async () => {
            if (this.isDisposed) return;

            try {
              await circuitBreaker.execute(() =>
                this.dotNetReference.invokeMethodAsync('SetMaxWidthBasedOnWindowSize')
              );
            } catch (error) {
              logger.error('SetMaxWidthBasedOnWindowSize failed:', error);
            }
          }, 300)
        );

        this.resizeObserver.observe(document.body);
      }

      dispose() {
        if (this.isDisposed) return;

        this.isDisposed = true;
        this.resizeObserver?.disconnect();
        this.dotNetReference = null;

        EventEmitter.emit(this, 'disposed', {timestamp: Date.now()});
      }
    }

    ModuleManager.register('DropBearResizeManager', {
      instance: null,

      initialize(dotNetRef) {
        if (!dotNetRef) {
          throw new Error('dotNetRef is required');
        }

        if (this.instance) {
          this.dispose();
        }

        this.instance = new ResizeManager(dotNetRef);
      },

      dispose() {
        if (this.instance) {
          this.instance.dispose();
          this.instance = null;
        }
      }
    }, ['DropBearCore']);

    return ModuleManager.get('DropBearResizeManager');
  })();

  const DropBearNavigationButtons = (() => {
    const logger = DropBearUtils.createLogger('DropBearNavigationButtons');
    const circuitBreaker = new CircuitBreaker({failureThreshold: 3, resetTimeout: 30000});

    /** @implements {INavigationManager} */
    class NavigationManager {
      /**
       * @param {IDotNetReference} dotNetReference
       */
      constructor(dotNetRef) {
        this.dotNetRef = dotNetRef;
        this.isDisposed = false;
        this.intersectionObserver = null;
        this._setupScrollObserver();

        EventEmitter.emit(this, 'initialized', {timestamp: Date.now()});
      }

      _setupScrollObserver() {
        const options = {
          threshold: [0, 0.5, 1],
          rootMargin: '300px'
        };

        this.intersectionObserver = new IntersectionObserver(
          DropBearUtils.throttle(entries => {
            if (this.isDisposed) return;
            const isVisible = entries.some(entry => entry.intersectionRatio > 0);
            this._updateVisibility(!isVisible);
          }, 250),
          options
        );

        const sentinel = document.createElement('div');
        sentinel.style.height = '1px';
        document.body.prepend(sentinel);
        this.intersectionObserver.observe(sentinel);
      }

      async _updateVisibility(isVisible) {
        try {
          await circuitBreaker.execute(() =>
            this.dotNetRef.invokeMethodAsync('UpdateVisibility', isVisible)
          );
        } catch (error) {
          logger.error('UpdateVisibility failed:', error);
        }
      }

      scrollToTop() {
        if (this.isDisposed) return;

        DOMOperationQueue.add(() =>
          window.scrollTo({
            top: 0,
            behavior: 'smooth'
          }));

        EventEmitter.emit(this, 'scrolled-to-top', {timestamp: Date.now()});
      }

      goBack() {
        if (this.isDisposed) return;
        window.history.back();
        EventEmitter.emit(this, 'went-back', {timestamp: Date.now()});
      }

      dispose() {
        if (this.isDisposed) return;

        this.isDisposed = true;
        this.intersectionObserver?.disconnect();
        this.dotNetRef = null;

        EventEmitter.emit(this, 'disposed', {timestamp: Date.now()});
      }
    }

    ModuleManager.register('DropBearNavigationButtons', {
      instance: null,

      initialize(dotNetRef) {
        if (!dotNetRef) {
          throw new Error('dotNetRef is required');
        }

        if (this.instance) {
          this.dispose();
        }

        this.instance = new NavigationManager(dotNetRef);
      },

      scrollToTop() {
        this.instance?.scrollToTop();
      },

      goBack() {
        this.instance?.goBack();
      },

      dispose() {
        if (this.instance) {
          this.instance.dispose();
          this.instance = null;
        }
      }
    }, ['DropBearCore']);

    return ModuleManager.get('DropBearNavigationButtons');
  })();

  const DropBearContextMenu = (() => {
    const logger = DropBearUtils.createLogger('DropBearContextMenu');
    const circuitBreaker = new CircuitBreaker({failureThreshold: 3, resetTimeout: 30000});

    /** @implements {IContextMenuManager} */
    class ContextMenuManager {
      /**
       * @param {IDotNetReference} dotNetReference
       */
      constructor(id, dotNetRef) {
        DropBearUtils.validateArgs([id, dotNetRef], ['string', 'object'], 'ContextMenuManager');

        this.id = id;
        this.element = document.getElementById(id);
        this.dotNetRef = dotNetRef;
        this.isDisposed = false;
        this.clickOutsideHandler = null;

        if (!DropBearUtils.isElement(this.element)) {
          throw new TypeError('Invalid element provided to ContextMenuManager');
        }

        this._setupEventListeners();
        EventEmitter.emit(this.element, 'created', {id});
      }

      _setupEventListeners() {
        this.handleContextMenu = this._handleContextMenu.bind(this);
        this.element.addEventListener('contextmenu', this.handleContextMenu);

        this.clickOutsideHandler = event => {
          if (!this.element.contains(event.target)) {
            this.hide();
          }
        };
      }

      async _handleContextMenu(e) {
        e.preventDefault();
        if (this.isDisposed) return;

        document.addEventListener('click', this.clickOutsideHandler);
        await this.show(e.pageX, e.pageY);
      }

      async show(x, y) {
        if (this.isDisposed) return;

        try {
          await circuitBreaker.execute(() =>
            this.dotNetRef.invokeMethodAsync('Show', x, y)
          );

          DOMOperationQueue.add(() => {
            this.element.style.visibility = 'visible';
            this.element.classList.add('show');
          });
        } catch (error) {
          logger.error('Error showing context menu:', error);
          throw error;
        }
      }

      async hide() {
        if (this.isDisposed) return;

        try {
          document.removeEventListener('click', this.clickOutsideHandler);

          await circuitBreaker.execute(() =>
            this.dotNetRef.invokeMethodAsync('Hide')
          );

          DOMOperationQueue.add(() => {
            this.element.classList.remove('show');
            this.element.style.visibility = 'hidden';
          });
        } catch (error) {
          logger.error('Error hiding context menu:', error);
        }
      }

      dispose() {
        if (this.isDisposed) return;

        this.isDisposed = true;
        this.element.removeEventListener('contextmenu', this.handleContextMenu);
        document.removeEventListener('click', this.clickOutsideHandler);
        this.dotNetRef = null;

        EventEmitter.emit(this.element, 'disposed', {id: this.id});
      }
    }

    ModuleManager.register('DropBearContextMenu', {
      menuInstances: new Map(),

      initialize(menuId, dotNetRef) {
        DropBearUtils.validateArgs([menuId, dotNetRef], ['string', 'object'], 'initialize');

        try {
          if (this.menuInstances.has(menuId)) {
            logger.warn(`Context menu already exists for ${menuId}, disposing old instance`);
            this.dispose(menuId);
          }

          const manager = new ContextMenuManager(menuId, dotNetRef);
          this.menuInstances.set(menuId, manager);
        } catch (error) {
          logger.error('Context menu initialization error:', error);
          throw error;
        }
      },

      show(menuId, x, y) {
        const manager = this.menuInstances.get(menuId);
        return manager ? manager.show(x, y) : Promise.resolve();
      },

      dispose(menuId) {
        const manager = this.menuInstances.get(menuId);
        if (manager) {
          manager.dispose();
          this.menuInstances.delete(menuId);
        }
      },

      disposeAll() {
        Array.from(this.menuInstances.keys()).forEach(id => this.dispose(id));
        this.menuInstances.clear();
      }
    }, ['DropBearCore']);

    return ModuleManager.get('DropBearContextMenu');
  })();

  const DropBearValidationErrors = (() => {
    const logger = DropBearUtils.createLogger('DropBearValidationErrors');
    const circuitBreaker = new CircuitBreaker({failureThreshold: 3, resetTimeout: 30000});

    /** @implements {IValidationErrorsManager} */
    class ValidationErrorsManager {
      constructor(id) {
        DropBearUtils.validateArgs([id], ['string'], 'ValidationErrorsManager');

        this.id = id;
        this.element = document.getElementById(id);
        this.isDisposed = false;
        this.list = null;
        this.header = null;
        this.keyboardHandler = null;

        if (!DropBearUtils.isElement(this.element)) {
          throw new TypeError('Invalid element provided to ValidationErrorsManager');
        }

        this._cacheElements();
        this._setupEventListeners();
        EventEmitter.emit(this.element, 'created', {id});
      }

      _cacheElements() {
        this.list = this.element.querySelector('.validation-errors__list');
        this.header = this.element.querySelector('.validation-errors__header');
        this.items = new WeakMap();
      }

      _setupEventListeners() {
        if (!this.header) return;

        this.keyboardHandler = this._handleKeydown.bind(this);
        this.header.addEventListener('keydown', this.keyboardHandler);
      }

      _handleKeydown(event) {
        if (event.key === 'Enter' || event.key === ' ') {
          event.preventDefault();
          DOMOperationQueue.add(() => this.header.click());
        }
      }

      async updateAriaAttributes(isCollapsed) {
        if (this.isDisposed) return;

        await circuitBreaker.execute(async () =>
          DOMOperationQueue.add(() => {
            if (this.list) {
              this.list.setAttribute('aria-hidden', isCollapsed.toString());
              const items = this.list.querySelectorAll('.validation-errors__item');
              items.forEach(item => {
                if (!this.items.has(item)) {
                  this.items.set(item, true);
                }
                item.setAttribute('tabindex', isCollapsed ? '-1' : '0');
              });
            }

            if (this.header) {
              this.header.setAttribute('aria-expanded', (!isCollapsed).toString());
            }
          }));
      }

      dispose() {
        if (this.isDisposed) return;

        this.isDisposed = true;

        if (this.header && this.keyboardHandler) {
          this.header.removeEventListener('keydown', this.keyboardHandler);
        }

        this.list = null;
        this.header = null;
        this.items = null;

        EventEmitter.emit(this.element, 'disposed', {id: this.id});
      }
    }

    ModuleManager.register('DropBearValidationErrors', {
      validationContainers: new Map(),

      initialize(containerId) {
        DropBearUtils.validateArgs([containerId], ['string'], 'initialize');

        try {
          if (this.validationContainers.has(containerId)) {
            logger.warn(`Validation container already exists for ${containerId}, disposing old instance`);
            this.dispose(containerId);
          }

          const manager = new ValidationErrorsManager(containerId);
          this.validationContainers.set(containerId, manager);
        } catch (error) {
          logger.error('Validation container initialization error:', error);
          throw error;
        }
      },

      async updateAriaAttributes(containerId, isCollapsed) {
        const manager = this.validationContainers.get(containerId);
        if (manager) {
          await manager.updateAriaAttributes(isCollapsed);
        }
      },

      dispose(containerId) {
        const manager = this.validationContainers.get(containerId);
        if (manager) {
          manager.dispose();
          this.validationContainers.delete(containerId);
        }
      },

      disposeAll() {
        Array.from(this.validationContainers.keys()).forEach(id => this.dispose(id));
        this.validationContainers.clear();
      }
    }, ['DropBearCore']);

    return ModuleManager.get('DropBearValidationErrors');
  })();

  const DropBearFileDownloader = (() => {
    const logger = DropBearUtils.createLogger('DropBearFileDownloader');
    const circuitBreaker = new CircuitBreaker({failureThreshold: 3, resetTimeout: 30000});

    /** @implements {IDownloadManager} */
    class DownloadManager {
      constructor() {
        this.activeDownloads = new Set();
      }

      async downloadFileFromStream(fileName, content, contentType) {
        const downloadId = crypto.randomUUID();

        try {
          this.activeDownloads.add(downloadId);
          logger.debug('Starting download:', {fileName, contentType, downloadId});

          const blob = await this._createBlob(content, contentType);
          await this._initiateDownload(blob, fileName);

          logger.debug('Download completed:', {fileName, downloadId});
        } catch (error) {
          logger.error('Download failed:', {fileName, error, downloadId});
          throw error;
        } finally {
          this.activeDownloads.delete(downloadId);
        }
      }

      async _createBlob(content, contentType) {
        return circuitBreaker.execute(async () => {
          let blob;

          if (content instanceof Blob) {
            logger.debug('Content is a Blob');
            blob = content;
          } else if ('arrayBuffer' in content) {
            logger.debug('Processing DotNetStreamReference');
            const arrayBuffer = await content.arrayBuffer();
            blob = new Blob([arrayBuffer], {type: contentType});
          } else if (content instanceof Uint8Array) {
            logger.debug('Processing Uint8Array');
            blob = new Blob([content], {type: contentType});
          } else {
            throw new Error('Unsupported content type');
          }

          logger.debug('Blob created, size:', blob.size);
          return blob;
        });
      }

      async _initiateDownload(blob, fileName) {
        const url = URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = fileName || 'download';

        DOMOperationQueue.add(() => {
          document.body.appendChild(link);
          link.click();
          document.body.removeChild(link);
        });

        // Clean up the URL after a short delay
        setTimeout(() => URL.revokeObjectURL(url), 100);
      }

      dispose() {
        this.activeDownloads.clear();
      }
    }

    ModuleManager.register('DropBearFileDownloader', {
      downloadManager: new DownloadManager(),

      downloadFileFromStream: async (fileName, content, contentType) =>
        await ModuleManager.get('DropBearFileDownloader').downloadManager
          .downloadFileFromStream(fileName, content, contentType),

      dispose() {
        this.downloadManager.dispose();
        this.downloadManager = null;
      }
    }, ['DropBearCore']);

    return ModuleManager.get('DropBearFileDownloader');
  })();

  const DropBearPageAlert = (() => {
    const logger = DropBearUtils.createLogger('DropBearPageAlert');
    const circuitBreaker = new CircuitBreaker({failureThreshold: 3, resetTimeout: 30000});
    const ANIMATION_DURATION = 300;

    /**
     * A PageAlertManager that manages show/hide lifecycle for a page alert element.
     * @implements {IPageAlertManager}
     */
    class PageAlertManager {
      constructor(id, isPermanent) {
        this.id = id;
        this.isPermanent = isPermanent;
        this.isDisposed = false;
        this.element = document.getElementById(id);
        this.progressDuration = 0;
        this.progressTimeout = null;
        this.animationFrame = null;

        if (!this.element) {
          throw new Error(`Element with id ${id} not found`);
        }

        this._cacheElements();
        this._setupEventListeners();

        // Fire an event indicating the alert was created
        EventEmitter.emit(this.element, 'created', {id});
      }

      _cacheElements() {
        this.progressBar = this.element.querySelector('.page-alert-progress-bar');
      }

      _setupEventListeners() {
        if (this.isPermanent) return;

        this.handleMouseEnter = () => this._pauseProgress();
        this.handleMouseLeave = () => this._resumeProgress();

        this.element.addEventListener('mouseenter', this.handleMouseEnter);
        this.element.addEventListener('mouseleave', this.handleMouseLeave);
      }

      // Show the alert element (fade in, etc.)
      show() {
        if (this.isDisposed) return Promise.resolve(true);

        return circuitBreaker.execute(() => new Promise(resolve =>
          DOMOperationQueue.add(() => {
            cancelAnimationFrame(this.animationFrame);
            this.element.classList.remove('hide');

            requestAnimationFrame(() => {
              this.element.classList.add('show');
              const transitionEndHandler = () => {
                this.element.removeEventListener('transitionend', transitionEndHandler);
                resolve(true);
              };
              this.element.addEventListener('transitionend', transitionEndHandler);

              // Fallback: resolve after an animation duration
              setTimeout(() => resolve(true), ANIMATION_DURATION + 50);
            });
          })));
      }

      // Start the progress bar to auto-hide after `duration`
      startProgress(duration) {
        if (this.isDisposed || this.isPermanent || !this.progressBar) return;
        if (typeof duration !== 'number' || duration <= 0) return;

        this.progressDuration = duration;

        DOMOperationQueue.add(() => {
          clearTimeout(this.progressTimeout);
          this.progressBar.style.transition = 'none';
          this.progressBar.style.transform = 'scaleX(1)';

          requestAnimationFrame(() => {
            this.progressBar.style.transition = `transform ${duration}ms linear`;
            this.progressBar.style.transform = 'scaleX(0)';
            this.progressTimeout = setTimeout(() => this.hide(), duration);
          });
        });
      }

      _pauseProgress() {
        if (this.isDisposed || this.isPermanent || !this.progressBar) return;

        clearTimeout(this.progressTimeout);
        const computedStyle = window.getComputedStyle(this.progressBar);

        DOMOperationQueue.add(() => {
          this.progressBar.style.transition = 'none';
          this.progressBar.style.transform = computedStyle.transform;
        });
      }

      _resumeProgress() {
        if (this.isDisposed || this.isPermanent || !this.progressBar) return;

        const computedStyle = window.getComputedStyle(this.progressBar);
        const currentScale = this._getCurrentScale(computedStyle.transform);
        const remainingTime = this.progressDuration * currentScale;

        DOMOperationQueue.add(() => {
          this.progressBar.style.transition = `transform ${remainingTime}ms linear`;
          this.progressBar.style.transform = 'scaleX(0)';
          this.progressTimeout = setTimeout(() => this.hide(), remainingTime);
        });
      }

      _getCurrentScale(transform) {
        if (transform === 'none') return 1;
        const values = transform.match(/matrix\(([^)]+)\)/);
        return values ? parseFloat(values[1].split(', ')[0]) : 1;
      }

      // Hide the alert element (fade out, dispose, etc.)
      hide() {
        if (this.isDisposed) return Promise.resolve(true);

        return circuitBreaker.execute(() => new Promise(resolve => {
          clearTimeout(this.progressTimeout);
          cancelAnimationFrame(this.animationFrame);

          DOMOperationQueue.add(() => {
            this.element.classList.remove('show');
            this.element.classList.add('hide');

            const transitionEndHandler = () => {
              this.element.removeEventListener('transitionend', transitionEndHandler);
              this.dispose();
              resolve(true);
            };

            this.element.addEventListener('transitionend', transitionEndHandler);

            // Fallback: after animation, dispose
            setTimeout(() => {
              this.dispose();
              resolve(true);
            }, ANIMATION_DURATION + 50);
          });
        }));
      }

      // Fully remove the alert from DOM and mark disposed
      dispose() {
        if (this.isDisposed) return;

        this.isDisposed = true;
        clearTimeout(this.progressTimeout);
        cancelAnimationFrame(this.animationFrame);

        if (!this.isPermanent) {
          this.element.removeEventListener('mouseenter', this.handleMouseEnter);
          this.element.removeEventListener('mouseleave', this.handleMouseLeave);
        }

        DOMOperationQueue.add(() => {
          if (this.element?.parentNode) {
            this.element.parentNode.removeChild(this.element);
          }
        });

        EventEmitter.emit(this.element, 'disposed', {id: this.id});
        this.element = null;
        this.progressBar = null;
      }
    }

    /**
     * Register 'DropBearPageAlert' with the ModuleManager.
     * The object below has exactly the methods you want:
     *  - create(id, duration, isPermanent)
     *  - hide(id)
     *  - hideAll()
     */
    ModuleManager.register('DropBearPageAlert', {
      /**
       * Create a new page alert with 3 parameters:
       * @param {string} id          Element ID for the alert
       * @param {number} duration    Duration (ms) before auto-hide
       * @param {boolean} isPermanent If true, the alert won't auto-hide
       */
      create(id, duration, isPermanent) {
        // If any param is missing or undefined, apply your own defaults here
        if (typeof duration !== 'number') {
          duration = 5000; // default to 5s
        }
        if (typeof isPermanent !== 'boolean') {
          isPermanent = false;
        }

        try {
          DropBearUtils.validateArgs([id], ['string'], 'create');

          if (this.alerts.has(id)) {
            logger.debug(`Alert ${id} already exists; disposing old instance`);
            this.alerts.get(id).dispose();
          }

          const manager = new PageAlertManager(id, isPermanent);
          this.alerts.set(id, manager);

          // Show the alert immediately
          manager.show().then(() => {
            // If not permanent, start auto-hide progress
            if (!isPermanent && duration > 0) {
              manager.startProgress(duration);
            }
          });

          return true; // so we match your d.ts: create(...) => boolean
        } catch (error) {
          logger.error('Error creating page alert:', error);
          return false;
        }
      },

      /**
       * Hide an existing page alert by ID
       * @param {string} id The ID of the alert to hide
       * @returns {Promise<boolean>} Promise resolving to true if hide was successful
       */
      hide(id) {
        try {
          DropBearUtils.validateArgs([id], ['string'], 'hide');
          const manager = this.alerts.get(id);

          if (!manager) {
            logger.debug(`No alert found with id ${id}`);
            return Promise.resolve(true);
          }

          return manager.hide().catch(error => {
            logger.error(`Error hiding alert ${id}:`, error);
            return false;
          });
        } catch (error) {
          logger.error('Error in hide method:', error);
          return Promise.resolve(false);
        }
      },

      /**
       * Hide all existing page alerts
       * @returns {Promise<boolean[]>} Promise resolving to array of hide operation results
       */
      hideAll() {
        try {
          const promises = Array.from(this.alerts.values()).map(manager =>
            manager.hide().catch(error => {
              logger.error('Error hiding alert:', error);
              return false;
            })
          );

          return Promise.all(promises);
        } catch (error) {
          logger.error('Error in hideAll method:', error);
          return Promise.resolve([false]);
        }
      }
    }, ['DropBearCore']);

    // We keep a Map of alert managers for each 'id'
    return ModuleManager.get('DropBearPageAlert');
  })();

  const DropBearProgressBar = (() => {
    const logger = DropBearUtils.createLogger('DropBearProgressBar');
    const circuitBreaker = new CircuitBreaker({failureThreshold: 3, resetTimeout: 30000});
    const ANIMATION_DURATION = 300;

    /** @implements {IProgressBarManager} */
    class ProgressBarManager {
      /**
       * @param {IDotNetReference} dotNetReference
       */
      constructor(id, dotNetRef) {
        DropBearUtils.validateArgs([id, dotNetRef], ['string', 'object'], 'ProgressBarManager');

        this.id = id;
        this.element = document.getElementById(id);
        this.dotNetRef = dotNetRef;
        this.isDisposed = false;
        this.animationFrame = null;
        this.resizeObserver = null;
        this.currentAnimation = null;

        if (!DropBearUtils.isElement(this.element)) {
          throw new TypeError('Invalid element provided to ProgressBarManager');
        }

        this._cacheElements();
        this._setupResizeObserver();
        EventEmitter.emit(this.element, 'created', {id});
      }

      _cacheElements() {
        this.elements = {
          overallBar: this.element.querySelector('.progress-bar-fill'),
          stepWindow: this.element.querySelector('.step-window'),
          steps: Array.from(this.element.querySelectorAll('.step'))
        };
      }

      _setupResizeObserver() {
        this.resizeObserver = new ResizeObserver(
          DropBearUtils.throttle(() => this._handleResize(), 100)
        );
        this.resizeObserver.observe(this.element);
      }

      updateProgress(taskProgress, overallProgress) {
        if (this.isDisposed) return false;

        try {
          DOMOperationQueue.add(() => {
            cancelAnimationFrame(this.animationFrame);

            this.animationFrame = requestAnimationFrame(() => {
              const activeStep = this.element.querySelector('.step.active .step-progress-bar');
              if (activeStep) {
                activeStep.style.width = `${Math.min(taskProgress, 100)}%`;
              }

              if (this.elements.overallBar) {
                this.elements.overallBar.style.width = `${Math.min(overallProgress, 100)}%`;
              }
            });
          });

          return true;
        } catch (error) {
          logger.error(`Error updating progress for ${this.id}:`, error);
          return false;
        }
      }

      updateStepDisplay(currentIndex, totalSteps) {
        if (this.isDisposed) return false;

        try {
          if (!this.elements.steps?.length) return false;

          if (this.currentAnimation) {
            this.currentAnimation.cancel();
          }

          const TRANSITION_TIMING = 'cubic-bezier(0.4, 0, 0.2, 1)';

          DOMOperationQueue.add(() =>
            this.elements.steps.forEach((step, index) => {
              const position = index - currentIndex;
              const opacity = position === 0 ? 1 : 0.6;
              const scale = position === 0 ? 1.05 : 1;
              const translate = `${position * 100}%`;

              this.currentAnimation = step.animate([{
                transform: `translateX(${translate}) scale(${scale})`,
                opacity
              }], {
                duration: ANIMATION_DURATION,
                easing: TRANSITION_TIMING,
                fill: 'forwards'
              });

              step.style.visibility = Math.abs(position) <= 1 ? 'visible' : 'hidden';
            }));

          return true;
        } catch (error) {
          logger.error(`Error updating step display for ${this.id}:`, error);
          return false;
        }
      }

      _handleResize() {
        if (this.isDisposed) return;

        try {
          const containerWidth = this.element.offsetWidth;
          const {stepWindow, steps} = this.elements;
          if (!stepWindow || !steps) return;

          DOMOperationQueue.add(() => {
            const minStepWidth = Math.max(containerWidth / 4, 120);

            steps.forEach(step => {
              const label = step.querySelector('.step-label');
              if (label) {
                label.style.maxWidth = `${minStepWidth - 40}px`;
              }
            });

            stepWindow.style.width = '100%';
          });
        } catch (error) {
          logger.error(`Error handling resize for ${this.id}:`, error);
        }
      }

      dispose() {
        if (this.isDisposed) return;

        try {
          this.isDisposed = true;
          cancelAnimationFrame(this.animationFrame);
          this.resizeObserver?.disconnect();

          if (this.currentAnimation) {
            this.currentAnimation.cancel();
          }

          this.elements = null;
          this.dotNetRef = null;

          EventEmitter.emit(this.element, 'disposed', {id: this.id});
        } catch (error) {
          logger.error(`Error disposing progress bar ${this.id}:`, error);
        }
      }
    }

    ModuleManager.register('DropBearProgressBar', {
      progressBars: new Map(),

      initialize(progressId, dotNetRef) {
        try {
          if (this.progressBars.has(progressId)) {
            logger.debug(`Progress bar already exists for ${progressId}, disposing old instance`);
            this.dispose(progressId);
          }

          const manager = new ProgressBarManager(progressId, dotNetRef);
          this.progressBars.set(progressId, manager);
          return true;
        } catch (error) {
          logger.error('Progress bar initialization error:', error);
          return false;
        }
      },

      updateProgress(progressId, taskProgress, overallProgress) {
        const manager = this.progressBars.get(progressId);
        return manager ? manager.updateProgress(taskProgress, overallProgress) : false;
      },

      updateStepDisplay(progressId, currentIndex, totalSteps) {
        const manager = this.progressBars.get(progressId);
        return manager ? manager.updateStepDisplay(currentIndex, totalSteps) : false;
      },

      dispose(progressId) {
        const manager = this.progressBars.get(progressId);
        if (manager) {
          manager.dispose();
          this.progressBars.delete(progressId);
        }
      },

      disposeAll() {
        Array.from(this.progressBars.keys()).forEach(id => this.dispose(id));
        this.progressBars.clear();
      }
    }, ['DropBearCore']);

    return ModuleManager.get('DropBearProgressBar');
  })();

  // Initialize DropBear
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
            reject(createDropBearError('Blazor initialization timeout', 'INIT_TIMEOUT'));
          }
        }, 100);
      });

      // Register core module
      ModuleManager.register('DropBearCore', {
        async initialize() {
          Object.defineProperties(window, {
            DropBearUtils: {value: DropBearUtils, writable: false, configurable: false},
            getWindowDimensions: {
              value: DropBearUtilities.getWindowDimensions,
              writable: false,
              configurable: false
            }
          });
        }
      });

      // Create initial resource pools
      ResourcePool.create('domOperations', () => new Set());
      ResourcePool.create('downloadLinks', () => document.createElement('a'), 5);

      // Initialize modules in dependency order
      await ModuleManager.initialize('DropBearCore');

      // Initialize all modules in parallel
      await Promise.all([
        ModuleManager.initialize('DropBearSnackbar'),
        ModuleManager.initialize('DropBearResizeManager'),
        ModuleManager.initialize('DropBearNavigationButtons'),
        ModuleManager.initialize('DropBearContextMenu'),
        ModuleManager.initialize('DropBearValidationErrors'),
        ModuleManager.initialize('DropBearFileDownloader'),
        ModuleManager.initialize('DropBearPageAlert'),
        ModuleManager.initialize('DropBearProgressBar')
      ]);

    } catch (error) {
      console.error("DropBear initialization failed:", error);
      throw createDropBearError(
        'DropBear initialization failed',
        'INIT_ERROR',
        'DropBearCore',
        {originalError: error}
      );
    }
  };

  // Initialize on load
  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', () =>
      initializeDropBear().catch(error => console.error("Failed to initialize DropBear:", error))
    );
  } else {
    initializeDropBear().catch(error => console.error("Failed to initialize DropBear:", error));
  }

  // Cleanup on unload
  window.addEventListener('unload', () => {
    try {
      ['DropBearSnackbar', 'DropBearResizeManager', 'DropBearNavigationButtons',
        'DropBearContextMenu', 'DropBearValidationErrors', 'DropBearProgressBar',
        'DropBearFileDownloader', 'DropBearPageAlert'].forEach(module => {
        const instance = ModuleManager.get(module);
        if (instance?.dispose) instance.dispose();
      });

      DOMOperationQueue.flush();
    } catch (error) {
      console.error("Error during DropBear cleanup:", error);
    }
  });
})();
