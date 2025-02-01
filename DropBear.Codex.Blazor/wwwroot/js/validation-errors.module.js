/**
 * @fileoverview Provides validation errors manager for dropbear context
 * @module DropBearValidationErrors
 */

import {CircuitBreaker, DOMOperationQueue, EventEmitter} from './core.module.js';
import {DropBearUtils} from './utils.module.js';
import {ModuleManager} from './module-manager.module.js';

/**
 * Create a logger instance
 */
const logger = DropBearUtils.createLogger('DropBearValidationErrors');

/**
 * Circuit breaker for repeated failures
 */
const circuitBreaker = new CircuitBreaker({failureThreshold: 3, resetTimeout: 30000});

/**
 * Manager for validation errors container
 * @implements {IValidationErrorsManager}
 */
class ValidationErrorsManager {
  /**
   * @param {string} id - The ID of the container element
   */
  constructor(id) {
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

    /** @type {WeakMap<HTMLElement, boolean>|null} */
    this.items = null;

    if (!DropBearUtils.isElement(this.element)) {
      throw new TypeError('Invalid element provided to ValidationErrorsManager');
    }

    this._cacheElements();
    this._setupEventListeners();

    EventEmitter.emit(this.element, 'created', {id});
  }

  /**
   * Cache relevant DOM elements
   * @private
   */
  _cacheElements() {
    this.list = this.element.querySelector('.validation-errors__list');
    this.header = this.element.querySelector('.validation-errors__header');
    this.items = new WeakMap();
  }

  /**
   * Set up any listeners needed
   * @private
   */
  _setupEventListeners() {
    if (!this.header) return;

    this.keyboardHandler = this._handleKeydown.bind(this);
    this.header.addEventListener('keydown', this.keyboardHandler);
  }

  /**
   * Handle keydown on the header (e.g. pressing Enter or Space)
   * @private
   */
  _handleKeydown(event) {
    if (event.key === 'Enter' || event.key === ' ') {
      event.preventDefault();
      DOMOperationQueue.add(() => this.header.click());
    }
  }

  /**
   * Update ARIA attributes (e.g., collapsed/expanded) with circuit-breaker
   * @param {boolean} isCollapsed - Whether the list is collapsed
   * @returns {Promise<void>}
   */
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
      })
    );
  }

  /**
   * Dispose of the manager, remove event listeners
   */
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

// -------------------------------------------
// Register with ModuleManager
// -------------------------------------------
ModuleManager.register(
  'DropBearValidationErrors',
  {
    /** @type {Map<string, ValidationErrorsManager>} */
    validationContainers: new Map(),

    /**
     * Global initialization method, no-arg.
     */
    async initialize() {
      logger.debug('DropBearValidationErrors module init done (no-arg).');
    },

    /**
     * Creates a new validation errors manager for a specific container ID.
     * @param {string} containerId
     * @returns {void}
     */
    createValidationContainer(containerId) {
      DropBearUtils.validateArgs([containerId], ['string'], 'createValidationContainer');

      try {
        if (this.validationContainers.has(containerId)) {
          logger.warn(`Validation container already exists for ${containerId}, disposing old instance`);
          this.dispose(containerId);
        }

        const manager = new ValidationErrorsManager(containerId);
        this.validationContainers.set(containerId, manager);
      } catch (error) {
        logger.error('Validation container creation error:', error);
        throw error;
      }
    },

    /**
     * Update ARIA attributes for a specific container
     * @param {string} containerId
     * @param {boolean} isCollapsed
     */
    async updateAriaAttributes(containerId, isCollapsed) {
      const manager = this.validationContainers.get(containerId);
      if (manager) {
        await manager.updateAriaAttributes(isCollapsed);
      }
    },

    /**
     * Dispose of a specific validation container
     * @param {string} containerId
     */
    dispose(containerId) {
      const manager = this.validationContainers.get(containerId);
      if (manager) {
        manager.dispose();
        this.validationContainers.delete(containerId);
      }
    },

    /**
     * Dispose all containers
     */
    disposeAll() {
      Array.from(this.validationContainers.keys()).forEach(id => this.dispose(id));
      this.validationContainers.clear();
    }
  },
  // Dependencies
  ['DropBearCore']
);

// -------------------------------------------
// Retrieve the registered module
// -------------------------------------------
const validationErrorsModule = ModuleManager.get('DropBearValidationErrors');

// -------------------------------------------
// Attach to window if you want global usage
// -------------------------------------------
window.DropBearValidationErrors = {
  initialize: () => validationErrorsModule.initialize(),
  createValidationContainer: containerId => validationErrorsModule.createValidationContainer(containerId),
  updateAriaAttributes: (containerId, isCollapsed) => validationErrorsModule.updateAriaAttributes(containerId, isCollapsed),
  dispose: containerId => validationErrorsModule.dispose(containerId),
  disposeAll: () => validationErrorsModule.disposeAll()
};

/**
 * Export the manager class for direct imports if needed
 */
export {ValidationErrorsManager};
