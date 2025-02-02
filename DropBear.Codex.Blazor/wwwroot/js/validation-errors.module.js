/**
 * @fileoverview Validation errors manager for dropbear context
 * @module validation-errors
 */

import {CircuitBreaker, DOMOperationQueue, EventEmitter} from './core.module.js';
import {DropBearUtils} from './utils.module.js';
import {ModuleManager} from './module-manager.module.js';

const logger = DropBearUtils.createLogger('DropBearValidationErrors');
const circuitBreaker = new CircuitBreaker({failureThreshold: 3, resetTimeout: 30000});
let isInitialized = false;

/** @type {Object} Validation configuration constants */
const VALIDATION_CONFIG = {
  ANIMATION_DURATION: 300, // Animation duration in ms
  AUTOHIDE_DELAY: 5000, // Auto-hide delay for temporary messages
  MAX_ERRORS: 100 // Maximum number of error items to display
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

    /** @type {Function|null} */
    this.keyboardHandler = null;

    /** @type {WeakMap<HTMLElement, boolean>} */
    this.items = new WeakMap();

    /** @type {Object} */
    this.options = {
      animationDuration: VALIDATION_CONFIG.ANIMATION_DURATION,
      autoHide: false,
      autoHideDelay: VALIDATION_CONFIG.AUTOHIDE_DELAY,
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

    logger.debug('ValidationErrorsManager created:', {id});
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

      logger.debug('ARIA attributes updated:', {isCollapsed});
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

      logger.debug('Validation errors updated:', {count: limitedErrors.length});
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

// Register with ModuleManager
ModuleManager.register(
  'DropBearValidationErrors',
  {
    /** @type {Map<string, ValidationErrorsManager>} */
    validationContainers: new Map(),

    /**
     * Initialize the validation errors module
     * @returns {Promise<void>}
     */
    async initialize() {
      if (isInitialized) {
        return;
      }

      try {
        logger.debug('Validation errors module initializing');

        // Initialize dependencies
        await ModuleManager.waitForDependencies(['DropBearCore']);

        isInitialized = true;
        window.DropBearValidationErrors.__initialized = true;

        logger.debug('Validation errors module initialized');
      } catch (error) {
        logger.error('Validation errors initialization failed:', error);
        throw error;
      }
    },

    /**
     * Create a new validation errors container
     * @param {string} containerId - Container element ID
     * @param {Object} [options={}] - Configuration options
     */
    createValidationContainer(containerId, options = {}) {
      try {
        if (!isInitialized) {
          throw new Error('Module not initialized');
        }

        DropBearUtils.validateArgs([containerId], ['string'], 'createValidationContainer');

        if (this.validationContainers.has(containerId)) {
          logger.warn(`Validation container already exists for ${containerId}, disposing old instance`);
          this.dispose(containerId);
        }

        const manager = new ValidationErrorsManager(containerId, options);
        this.validationContainers.set(containerId, manager);
        logger.debug(`Validation container created for ID: ${containerId}`);
      } catch (error) {
        logger.error('Validation container creation error:', error);
        throw error;
      }
    },

    /**
     * Update validation errors
     * @param {string} containerId - Container ID
     * @param {string[]} errors - Array of error messages
     * @returns {Promise<void>}
     */
    async updateErrors(containerId, errors) {
      const manager = this.validationContainers.get(containerId);
      if (manager) {
        await manager.updateErrors(errors);
      }
    },

    /**
     * Update ARIA attributes
     * @param {string} containerId - Container ID
     * @param {boolean} isCollapsed - Collapsed state
     * @returns {Promise<void>}
     */
    async updateAriaAttributes(containerId, isCollapsed) {
      const manager = this.validationContainers.get(containerId);
      if (manager) {
        await manager.updateAriaAttributes(isCollapsed);
      }
    },

    /**
     * Show validation errors
     * @param {string} containerId - Container ID
     * @returns {Promise<void>}
     */
    async show(containerId) {
      const manager = this.validationContainers.get(containerId);
      if (manager) {
        await manager.show();
      }
    },

    /**
     * Hide validation errors
     * @param {string} containerId - Container ID
     * @returns {Promise<void>}
     */
    async hide(containerId) {
      const manager = this.validationContainers.get(containerId);
      if (manager) {
        await manager.hide();
      }
    },

    /**
     * Clear validation errors
     * @param {string} containerId - Container ID
     * @returns {Promise<void>}
     */
    async clearErrors(containerId) {
      const manager = this.validationContainers.get(containerId);
      if (manager) {
        await manager.clearErrors();
      }
    },

    /**
     * Check if module is initialized
     * @returns {boolean}
     */
    isInitialized() {
      return isInitialized;
    },

    /**
     * Dispose of a validation container
     * @param {string} containerId - Container ID
     */
    dispose(containerId) {
      const manager = this.validationContainers.get(containerId);
      if (manager) {
        manager.dispose();
        this.validationContainers.delete(containerId);
        logger.debug(`Validation container disposed for ID: ${containerId}`);
      }
    },

    /**
     * Dispose all validation containers
     */
    disposeAll() {
      Array.from(this.validationContainers.keys()).forEach(id => this.dispose(id));
      this.validationContainers.clear();
      isInitialized = false;
      window.DropBearValidationErrors.__initialized = false;
      logger.debug('All validation containers disposed');
    }
  },
  ['DropBearCore']
);

// Get module reference
const validationErrorsModule = ModuleManager.get('DropBearValidationErrors');

// Attach to window
window.DropBearValidationErrors = {
  __initialized: false,
  initialize: () => validationErrorsModule.initialize(),
  createValidationContainer: (containerId, options) =>
    validationErrorsModule.createValidationContainer(containerId, options),
  updateErrors: (containerId, errors) =>
    validationErrorsModule.updateErrors(containerId, errors),
  updateAriaAttributes: (containerId, isCollapsed) =>
    validationErrorsModule.updateAriaAttributes(containerId, isCollapsed),
  show: containerId => validationErrorsModule.show(containerId),
  hide: containerId => validationErrorsModule.hide(containerId),
  clearErrors: containerId => validationErrorsModule.clearErrors(containerId),
  dispose: containerId => validationErrorsModule.dispose(containerId),
  disposeAll: () => validationErrorsModule.disposeAll()
};

// Export ValidationErrorsManager class
export {ValidationErrorsManager};
