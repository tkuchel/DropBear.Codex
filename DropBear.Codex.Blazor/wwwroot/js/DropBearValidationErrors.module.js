/**
 * @fileoverview Validation errors manager for dropbear context
 * @module validation-errors
 */

import { CircuitBreaker, DOMOperationQueue, EventEmitter } from './DropBearCore.module.js';
import { DropBearUtils } from './DropBearUtils.module.js';

const logger = DropBearUtils.createLogger('DropBearValidationErrors');
const circuitBreaker = new CircuitBreaker({ failureThreshold: 3, resetTimeout: 30000 });
let isInitialized = false;
const moduleName = 'DropBearValidationErrors';

/** @type {Object} Validation configuration constants */
const VALIDATION_CONFIG = {
  ANIMATION_DURATION: 300,
  MAX_ERRORS: 100,
  ERROR_DISPLAY_TIMEOUT: 5000
};

/**
 * Manager for validation errors container
 * @implements {IValidationErrorsManager}
 */
class ValidationErrorsManager {
  /**
   * @param {string} id - The ID of the container element
   * @param {Object} [options={}] - Configuration options
   */
  constructor(id, options = {}) {
    DropBearUtils.validateArgs([id], ['string'], 'ValidationErrorsManager');

    /** @type {string} */
    this.id = id;

    /** @type {HTMLElement|null} */
    this.element = document.getElementById(id);

    /** @type {boolean} */
    this.isDisposed = false;

    /** @type {HTMLElement|null} */
    this.list = null;

    /** @type {HTMLElement|null} */
    this.header = null;

    /** @type {HTMLElement|null} */
    this.summary = null;

    /** @type {HTMLElement|null} */
    this.clearButton = null;

    /** @type {Function|null} */
    this.keyboardHandler = null;

    /** @type {Function|null} */
    this.handleClear = null;

    /** @type {WeakMap<HTMLElement, boolean>} */
    this.items = new WeakMap();

    /** @type {number|null} */
    this.autoHideTimeout = null;

    /** @type {Object} */
    this.options = {
      animationDuration: VALIDATION_CONFIG.ANIMATION_DURATION,
      autoHide: false,
      // Use ERROR_DISPLAY_TIMEOUT as the default auto-hide delay
      autoHideDelay: VALIDATION_CONFIG.ERROR_DISPLAY_TIMEOUT,
      ...options
    };

    if (!DropBearUtils.isElement(this.element)) {
      throw new TypeError('Invalid element provided to ValidationErrorsManager');
    }

    this._cacheElements();
    this._setupEventListeners();

    EventEmitter.emit(
      this.element,
      'created',
      DropBearUtils.createEvent(this.id, 'created', {
        timestamp: Date.now(),
        options: this.options
      })
    );

    logger.debug('ValidationErrorsManager created:', { id });
  }

  /**
   * Cache required DOM elements
   * @private
   */
  _cacheElements() {
    this.list = this.element.querySelector('.validation-errors__list');
    this.header = this.element.querySelector('.validation-errors__header');
    this.summary = this.element.querySelector('.validation-errors__summary');
    this.clearButton = this.element.querySelector('.validation-errors__clear');

    logger.debug('DOM elements cached');
  }

  /**
   * Set up event listeners
   * @private
   */
  _setupEventListeners() {
    if (!this.header) return;

    // Keyboard handler for accessibility
    this.keyboardHandler = this._handleKeydown.bind(this);
    this.header.addEventListener('keydown', this.keyboardHandler);

    // Clear button handler
    if (this.clearButton) {
      this.handleClear = () => this.clearErrors();
      this.clearButton.addEventListener('click', this.handleClear);
    }

    // Auto-hide functionality
    if (this.options.autoHide) {
      this.autoHideTimeout = setTimeout(() => this.hide(), this.options.autoHideDelay);
    }

    logger.debug('Event listeners initialized');
  }

  /**
   * Handle keydown events
   * @private
   * @param {KeyboardEvent} event - Keyboard event
   */
  _handleKeydown(event) {
    if (event.key === 'Enter' || event.key === ' ') {
      event.preventDefault();
      DOMOperationQueue.add(() => this.header.click());
    }
  }

  /**
   * Update ARIA attributes
   * @param {boolean} isCollapsed - Whether the list is collapsed
   * @returns {Promise<void>}
   */
  async updateAriaAttributes(isCollapsed) {
    if (this.isDisposed) return;

    try {
      await circuitBreaker.execute(async () => {
        DOMOperationQueue.add(() => {
          if (this.list) {
            this.list.setAttribute('aria-hidden', isCollapsed.toString());
            const items = this.list.querySelectorAll('.validation-errors__item');
            items.forEach(item => {
              this.items.set(item, true);
              item.setAttribute('tabindex', isCollapsed ? '-1' : '0');
            });
          }

          if (this.header) {
            this.header.setAttribute('aria-expanded', (!isCollapsed).toString());
          }
        });

        EventEmitter.emit(
          this.element,
          'aria-updated',
          DropBearUtils.createEvent(this.id, 'aria-updated', {
            isCollapsed,
            timestamp: Date.now()
          })
        );
      });

      logger.debug('ARIA attributes updated:', { isCollapsed });
    } catch (error) {
      logger.error('Error updating ARIA attributes:', error);
      throw error;
    }
  }

  /**
   * Show validation errors
   * @returns {Promise<void>}
   */
  async show() {
    if (this.isDisposed) return;

    try {
      await new Promise(resolve =>
        DOMOperationQueue.add(() => {
          this.element.classList.remove('hide');
          requestAnimationFrame(() => {
            this.element.classList.add('show');

            const transitionEndHandler = () => {
              this.element.removeEventListener('transitionend', transitionEndHandler);
              resolve();
            };

            this.element.addEventListener('transitionend', transitionEndHandler);

            // Fallback
            setTimeout(resolve, this.options.animationDuration + 50);
          });
        }));

      EventEmitter.emit(
        this.element,
        'shown',
        DropBearUtils.createEvent(this.id, 'shown', {
          timestamp: Date.now()
        })
      );

      logger.debug('Validation errors shown');
    } catch (error) {
      logger.error('Error showing validation errors:', error);
      throw error;
    }
  }

  /**
   * Hide validation errors
   * @returns {Promise<void>}
   */
  async hide() {
    if (this.isDisposed) return;

    try {
      await new Promise(resolve =>
        DOMOperationQueue.add(() => {
          this.element.classList.remove('show');
          this.element.classList.add('hide');

          const transitionEndHandler = () => {
            this.element.removeEventListener('transitionend', transitionEndHandler);
            resolve();
          };

          this.element.addEventListener('transitionend', transitionEndHandler);

          // Fallback
          setTimeout(resolve, this.options.animationDuration + 50);
        }));

      EventEmitter.emit(
        this.element,
        'hidden',
        DropBearUtils.createEvent(this.id, 'hidden', {
          timestamp: Date.now()
        })
      );

      logger.debug('Validation errors hidden');
    } catch (error) {
      logger.error('Error hiding validation errors:', error);
      throw error;
    }
  }

  /**
   * Update error messages
   * @param {string[]} errors - Array of error messages
   * @returns {Promise<void>}
   */
  async updateErrors(errors) {
    if (this.isDisposed || !this.list) return;

    try {
      if (!Array.isArray(errors)) {
        throw new TypeError('Errors must be an array');
      }

      // Limit number of errors to prevent performance issues
      const limitedErrors = errors.slice(0, VALIDATION_CONFIG.MAX_ERRORS);

      await new Promise(resolve =>
        DOMOperationQueue.add(() => {
          // Clear existing errors
          this.list.innerHTML = '';

          // Add new errors
          limitedErrors.forEach(error => {
            const item = document.createElement('li');
            item.className = 'validation-errors__item';
            item.textContent = error;
            item.setAttribute('tabindex', '0');
            this.list.appendChild(item);
            this.items.set(item, true);
          });

          // Update summary if it exists
          if (this.summary) {
            this.summary.textContent = `${limitedErrors.length} validation error${limitedErrors.length === 1 ? '' : 's'}`;
          }

          resolve();
        }));

      EventEmitter.emit(
        this.element,
        'errors-updated',
        DropBearUtils.createEvent(this.id, 'errors-updated', {
          errorCount: limitedErrors.length,
          timestamp: Date.now()
        })
      );

      logger.debug('Validation errors updated:', { count: limitedErrors.length });
    } catch (error) {
      logger.error('Error updating validation errors:', error);
      throw error;
    }
  }

  /**
   * Clear all validation errors
   * @returns {Promise<void>}
   */
  async clearErrors() {
    if (this.isDisposed || !this.list) return;

    try {
      await new Promise(resolve =>
        DOMOperationQueue.add(() => {
          this.list.innerHTML = '';
          if (this.summary) {
            this.summary.textContent = '0 validation errors';
          }
          resolve();
        }));

      EventEmitter.emit(
        this.element,
        'errors-cleared',
        DropBearUtils.createEvent(this.id, 'errors-cleared', {
          timestamp: Date.now()
        })
      );

      logger.debug('Validation errors cleared');
    } catch (error) {
      logger.error('Error clearing validation errors:', error);
      throw error;
    }
  }

  /**
   * Get the number of error items currently displayed.
   * @returns {number}
   */
  getErrorCount() {
    return this.list ? this.list.querySelectorAll('.validation-errors__item').length : 0;
  }

  /**
   * Dispose the validation errors manager
   */
  dispose() {
    if (this.isDisposed) return;

    logger.debug(`Disposing validation errors manager ${this.id}`);
    this.isDisposed = true;

    // Remove event listeners
    if (this.header && this.keyboardHandler) {
      this.header.removeEventListener('keydown', this.keyboardHandler);
    }

    if (this.clearButton && this.handleClear) {
      this.clearButton.removeEventListener('click', this.handleClear);
    }

    clearTimeout(this.autoHideTimeout);

    EventEmitter.emit(
      this.element,
      'disposed',
      DropBearUtils.createEvent(this.id, 'disposed', {
        timestamp: Date.now()
      })
    );
  }
}

// Attach to window first
window[moduleName] = {
  __initialized: false,
  validationContainers: new Map(),

  initialize: async () => {
    if (isInitialized) {
      return;
    }

    try {
      logger.debug('Validation errors module initializing');

      // Initialize dependencies first
      await window.DropBearUtils.initialize();
      await window.DropBearCore.initialize();

      isInitialized = true;
      window[moduleName].__initialized = true;

      logger.debug('Validation errors module initialized');
    } catch (error) {
      logger.error('Validation errors initialization failed:', error);
      throw error;
    }
  },

  createValidationContainer: (containerId, options = {}) => {
    try {
      if (!isInitialized) {
        throw new Error('Module not initialized');
      }

      DropBearUtils.validateArgs([containerId], ['string'], 'createValidationContainer');

      if (window[moduleName].validationContainers.has(containerId)) {
        logger.warn(`Validation container already exists for ${containerId}, disposing old instance`);
        window[moduleName].dispose(containerId);
      }

      const manager = new ValidationErrorsManager(containerId, options);
      window[moduleName].validationContainers.set(containerId, manager);
      logger.debug(`Validation container created for ID: ${containerId}`);
    } catch (error) {
      logger.error('Validation container creation error:', error);
      throw error;
    }
  },

  updateErrors: async (containerId, errors) => {
    const manager = window[moduleName].validationContainers.get(containerId);
    if (manager) {
      await manager.updateErrors(errors);
    }
  },

  updateAriaAttributes: async (containerId, isCollapsed) => {
    const manager = window[moduleName].validationContainers.get(containerId);
    if (manager) {
      await manager.updateAriaAttributes(isCollapsed);
    }
  },

  show: async containerId => {
    const manager = window[moduleName].validationContainers.get(containerId);
    if (manager) {
      await manager.show();
    }
  },

  hide: async containerId => {
    const manager = window[moduleName].validationContainers.get(containerId);
    if (manager) {
      await manager.hide();
    }
  },

  clearErrors: async containerId => {
    const manager = window[moduleName].validationContainers.get(containerId);
    if (manager) {
      await manager.clearErrors();
    }
  },

  getErrorCount: containerId => {
    const manager = window[moduleName].validationContainers.get(containerId);
    return manager ? manager.getErrorCount() : 0;
  },

  isInitialized: () => isInitialized,

  dispose: containerId => {
    const manager = window[moduleName].validationContainers.get(containerId);
    if (manager) {
      manager.dispose();
      window[moduleName].validationContainers.delete(containerId);
      logger.debug(`Validation container disposed for ID: ${containerId}`);
    }
  },

  disposeAll: () => {
    Array.from(window[moduleName].validationContainers.keys()).forEach(id =>
      window[moduleName].dispose(id)
    );
    window[moduleName].validationContainers.clear();
    isInitialized = false;
    window[moduleName].__initialized = false;
    logger.debug('All validation containers disposed');
  }
};

// Register with ModuleManager after window attachment
window.DropBearModuleManager.register(
  moduleName,
  {
    initialize: () => window[moduleName].initialize(),
    isInitialized: () => window[moduleName].isInitialized(),
    dispose: () => window[moduleName].disposeAll()
  },
  ['DropBearUtils', 'DropBearCore']
);

// Export the API functions under a unique namespace for the validation errors module.
export const DropBearValidationErrorsAPI = {
  /**
   * Initializes the validation errors module.
   * @returns {Promise<void>}
   */
  initialize: async () => window[moduleName].initialize(),

  /**
   * Creates a new validation container (manager) for the specified container ID.
   * @param {string} containerId - The ID of the container element.
   * @param {Object} [options={}] - Additional configuration options.
   */
  createValidationContainer: (containerId, options = {}) =>
    window[moduleName].createValidationContainer(containerId, options),

  /**
   * Updates the error messages in a validation container.
   * @param {string} containerId - The ID of the container element.
   * @param {string[]} errors - An array of error messages.
   * @returns {Promise<void>}
   */
  updateErrors: async (containerId, errors) =>
    window[moduleName].updateErrors(containerId, errors),

  /**
   * Updates the ARIA attributes of a validation container.
   * @param {string} containerId - The ID of the container element.
   * @param {boolean} isCollapsed - Whether the container is collapsed.
   * @returns {Promise<void>}
   */
  updateAriaAttributes: async (containerId, isCollapsed) =>
    window[moduleName].updateAriaAttributes(containerId, isCollapsed),

  /**
   * Shows the validation errors for a given container.
   * @param {string} containerId - The ID of the container element.
   * @returns {Promise<void>}
   */
  show: async containerId => window[moduleName].show(containerId),

  /**
   * Hides the validation errors for a given container.
   * @param {string} containerId - The ID of the container element.
   * @returns {Promise<void>}
   */
  hide: async containerId => window[moduleName].hide(containerId),

  /**
   * Clears all error messages in a validation container.
   * @param {string} containerId - The ID of the container element.
   * @returns {Promise<void>}
   */
  clearErrors: async containerId => window[moduleName].clearErrors(containerId),

  /**
   * Returns the number of errors in a validation container.
   * @param {string} containerId - The ID of the container element.
   * @returns {number}
   */
  getErrorCount: containerId => window[moduleName].getErrorCount(containerId),

  /**
   * Checks whether the validation errors module is initialized.
   * @returns {boolean}
   */
  isInitialized: () => window[moduleName].isInitialized(),

  /**
   * Disposes a specific validation container.
   * @param {string} containerId - The ID of the container element.
   */
  dispose: containerId => window[moduleName].dispose(containerId),

  /**
   * Disposes all validation containers and resets the module.
   * @returns {Promise<void>}
   */
  disposeAll: async () => window[moduleName].disposeAll()
};

// Also export the ValidationErrorsManager class for direct access if needed.
export { ValidationErrorsManager };
