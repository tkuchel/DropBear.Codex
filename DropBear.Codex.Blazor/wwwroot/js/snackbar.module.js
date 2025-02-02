/**
 * @fileoverview Snackbar component module for displaying temporary notifications
 * @module snackbar
 */

import {CircuitBreaker, DOMOperationQueue, EventEmitter} from './core.module.js';
import {DropBearUtils} from './utils.module.js';
import {ModuleManager} from './module-manager.module.js';

const logger = DropBearUtils.createLogger('DropBearSnackbar');
const circuitBreaker = new CircuitBreaker({failureThreshold: 3, resetTimeout: 30000});
let isInitialized = false;

/** @type {Object} Snackbar configuration constants */
const SNACKBAR_CONFIG = {
  ANIMATION_DURATION: 300, // Base animation duration in ms
  MIN_DURATION: 2000, // Minimum display duration
  MAX_DURATION: 10000, // Maximum display duration
  DEFAULT_DURATION: 5000, // Default display duration
  PROGRESS_UPDATE_INTERVAL: 16 // ~60fps for progress updates
};

/**
 * Manager for individual snackbar instances
 * @implements {ISnackbarManager}
 */
class SnackbarManager {
  /**
   * @param {string} id - Unique identifier for the snackbar
   * @param {Object} [options={}] - Configuration options
   */
  constructor(id, options = {}) {
    DropBearUtils.validateArgs([id], ['string'], 'SnackbarManager');

    /** @type {string} */
    this.id = id;

    /** @type {HTMLElement|null} */
    this.element = document.getElementById(id);

    /** @type {boolean} */
    this.isDisposed = false;

    /** @type {number|null} */
    this.progressTimeout = null;

    /** @type {number|null} */
    this.animationFrame = null;

    /** @type {Object|null} */
    this.dotNetRef = null;

    /** @type {Object} */
    this.options = {
      animationDuration: SNACKBAR_CONFIG.ANIMATION_DURATION,
      ...options
    };

    if (!DropBearUtils.isElement(this.element)) {
      throw new TypeError('Invalid element provided to SnackbarManager');
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

    logger.debug('SnackbarManager created:', {id});
  }

  /**
   * Cache required DOM elements
   * @private
   */
  _cacheElements() {
    this.progressBar = this.element.querySelector('.progress-bar');
    this.content = this.element.querySelector('.snackbar-content');
    this.actionButton = this.element.querySelector('.snackbar-action');
    this.closeButton = this.element.querySelector('.snackbar-close');

    // Find Blazor-specific scoped attribute if it exists
    this.scopedAttribute = Object.keys(this.element.attributes)
      .map(key => this.element.attributes[key])
      .find(attr => attr.name.startsWith('b-'))?.name;
  }

  /**
   * Set up event listeners
   * @private
   */
  _setupEventListeners() {
    this.handleMouseEnter = () => this._pauseProgress();
    this.handleMouseLeave = () => this._resumeProgress();

    this.element.addEventListener('mouseenter', this.handleMouseEnter);
    this.element.addEventListener('mouseleave', this.handleMouseLeave);

    if (this.actionButton) {
      this.handleAction = () => this.hide();
      this.actionButton.addEventListener('click', this.handleAction);
    }

    if (this.closeButton) {
      this.handleClose = () => this.hide();
      this.closeButton.addEventListener('click', this.handleClose);
    }

    logger.debug('Event listeners initialized');
  }

  /**
   * Set .NET reference for Blazor interop
   * @param {Object} dotNetRef - .NET reference object
   * @returns {Promise<void>}
   */
  async setDotNetReference(dotNetRef) {
    try {
      logger.debug(`Setting .NET reference for snackbar ${this.id}`);
      this.dotNetRef = dotNetRef;
    } catch (error) {
      logger.error(`Error setting .NET reference for snackbar ${this.id}:`, error);
      throw error;
    }
  }

  /**
   * Show the snackbar
   * @param {number} [duration=SNACKBAR_CONFIG.DEFAULT_DURATION] - Display duration
   * @returns {Promise<boolean>} Success status
   */
  async show(duration = SNACKBAR_CONFIG.DEFAULT_DURATION) {
    if (this.isDisposed) return false;

    try {
      duration = Math.max(
        SNACKBAR_CONFIG.MIN_DURATION,
        Math.min(duration, SNACKBAR_CONFIG.MAX_DURATION)
      );

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

      if (duration > 0) {
        this.startProgress(duration);
      }

      EventEmitter.emit(
        this.element,
        'shown',
        DropBearUtils.createEvent(this.id, 'shown', {
          duration,
          timestamp: Date.now()
        })
      );

      logger.debug('Snackbar shown:', {id: this.id, duration});
      return true;
    } catch (error) {
      logger.error('Error showing snackbar:', error);
      return false;
    }
  }

  /**
   * Start progress bar animation
   * @param {number} duration - Duration in milliseconds
   */
  startProgress(duration) {
    if (this.isDisposed || !this.progressBar) return;

    DOMOperationQueue.add(() => {
      clearTimeout(this.progressTimeout);
      this.progressBar.style.transition = 'none';
      this.progressBar.style.transform = 'scaleX(1)';

      requestAnimationFrame(() => {
        this.element.style.setProperty('--duration', `${duration}ms`);
        this.progressBar.style.transition = `transform ${duration}ms linear`;
        this.progressBar.style.transform = 'scaleX(0)';

        this.progressTimeout = setTimeout(async () => {
          try {
            if (this.dotNetRef) {
              await this.dotNetRef.invokeMethodAsync('OnProgressComplete');
            }
            await this.hide();
          } catch (error) {
            logger.error('Error handling progress completion:', error);
          }
        }, duration);
      });
    });

    logger.debug('Progress started:', {duration});
  }

  /**
   * Pause progress bar animation
   * @private
   */
  _pauseProgress() {
    if (this.isDisposed || !this.progressBar) return;

    clearTimeout(this.progressTimeout);

    const computedStyle = window.getComputedStyle(this.progressBar);

    DOMOperationQueue.add(() => {
      this.progressBar.style.transition = 'none';
      this.progressBar.style.transform = computedStyle.transform;
    });

    logger.debug('Progress paused');
  }

  /**
   * Resume progress bar animation
   * @private
   */
  _resumeProgress() {
    if (this.isDisposed || !this.progressBar) return;

    const computedStyle = window.getComputedStyle(this.progressBar);
    const duration = parseFloat(this.element.style.getPropertyValue('--duration')) ||
      SNACKBAR_CONFIG.DEFAULT_DURATION;
    const currentScale = this._getCurrentScale(computedStyle.transform);
    const remainingTime = duration * currentScale;

    DOMOperationQueue.add(() => {
      this.progressBar.style.transition = `transform ${remainingTime}ms linear`;
      this.progressBar.style.transform = 'scaleX(0)';

      this.progressTimeout = setTimeout(async () => {
        try {
          if (this.dotNetRef) {
            await this.dotNetRef.invokeMethodAsync('OnProgressComplete');
          }
          await this.hide();
        } catch (error) {
          logger.error('Error handling progress completion:', error);
        }
      }, remainingTime);
    });

    logger.debug('Progress resumed:', {remainingTime});
  }

  /**
   * Parse scale value from transform matrix
   * @private
   * @param {string} transform - CSS transform value
   * @returns {number} Scale value
   */
  _getCurrentScale(transform) {
    if (transform === 'none') return 1;
    const match = transform.match(/matrix\(([^)]+)\)/);
    return match ? parseFloat(match[1].split(',')[0]) : 1;
  }

  /**
   * Hide the snackbar
   * @returns {Promise<boolean>} Success status
   */
  async hide() {
    if (this.isDisposed) return false;

    try {
      DOMOperationQueue.add(() => {
        clearTimeout(this.progressTimeout);
        cancelAnimationFrame(this.animationFrame);
        this.element.classList.remove('show');
        this.element.classList.add('hide');
      });

      await new Promise(resolve => {
        const transitionEndHandler = () => {
          this.element.removeEventListener('transitionend', transitionEndHandler);
          this.dispose();
          resolve();
        };

        this.element.addEventListener('transitionend', transitionEndHandler);

        // Fallback
        setTimeout(() => {
          this.dispose();
          resolve();
        }, this.options.animationDuration + 50);
      });

      EventEmitter.emit(
        this.element,
        'hidden',
        DropBearUtils.createEvent(this.id, 'hidden', {
          timestamp: Date.now()
        })
      );

      logger.debug('Snackbar hidden');
      return true;
    } catch (error) {
      logger.error('Error hiding snackbar:', error);
      return false;
    }
  }

  /**
   * Update snackbar content
   * @param {string} content - New content HTML
   * @returns {Promise<boolean>} Success status
   */
  async updateContent(content) {
    if (this.isDisposed || !this.content) return false;

    try {
      await new Promise(resolve =>
        DOMOperationQueue.add(() => {
          this.content.innerHTML = content;
          resolve();
        }));

      EventEmitter.emit(
        this.element,
        'content-updated',
        DropBearUtils.createEvent(this.id, 'content-updated', {
          timestamp: Date.now()
        })
      );

      logger.debug('Content updated');
      return true;
    } catch (error) {
      logger.error('Error updating content:', error);
      return false;
    }
  }

  /**
   * Dispose the snackbar manager
   */
  dispose() {
    if (this.isDisposed) return;

    logger.debug(`Disposing snackbar ${this.id}`);
    this.isDisposed = true;

    clearTimeout(this.progressTimeout);
    cancelAnimationFrame(this.animationFrame);

    this.element.removeEventListener('mouseenter', this.handleMouseEnter);
    this.element.removeEventListener('mouseleave', this.handleMouseLeave);

    if (this.actionButton && this.handleAction) {
      this.actionButton.removeEventListener('click', this.handleAction);
    }

    if (this.closeButton && this.handleClose) {
      this.closeButton.removeEventListener('click', this.handleClose);
    }

    DOMOperationQueue.add(() => {
      if (this.element?.parentNode) {
        this.element.parentNode.removeChild(this.element);
      }
    });

    this.dotNetRef = null;

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
  'DropBearSnackbar',
  {
    /** @type {Map<string, SnackbarManager>} */
    snackbars: new Map(),

    /**
     * Initialize the snackbar module
     * @returns {Promise<void>}
     */
    async initialize() {
      if (isInitialized) {
        return;
      }

      try {
        logger.debug('Snackbar module initializing');

        // Initialize dependencies
        await ModuleManager.waitForDependencies(['DropBearCore']);

        isInitialized = true;
        window.DropBearSnackbar.__initialized = true;

        logger.debug('Snackbar module initialized');
      } catch (error) {
        logger.error('Snackbar initialization failed:', error);
        throw error;
      }
    },

    /**
     * Create a new snackbar instance
     * @param {string} snackbarId - Unique identifier
     * @param {Object} [options={}] - Configuration options
     * @returns {Promise<void>}
     */
    async createSnackbar(snackbarId, options = {}) {
      try {
        if (!isInitialized) {
          throw new Error('Module not initialized');
        }

        if (this.snackbars.has(snackbarId)) {
          logger.warn(`Snackbar already exists for ${snackbarId}, disposing old instance`);
          await this.dispose(snackbarId);
        }

        const manager = new SnackbarManager(snackbarId, options);
        this.snackbars.set(snackbarId, manager);
        logger.debug(`Snackbar created for ID: ${snackbarId}`);
      } catch (error) {
        logger.error('Snackbar creation error:', error);
        throw error;
      }
    },

    /**
     * Set .NET reference for a snackbar
     * @param {string} snackbarId - Snackbar identifier
     * @param {Object} dotNetRef - .NET reference
     * @returns {Promise<boolean>} Success status
     */
    async setDotNetReference(snackbarId, dotNetRef) {
      const manager = this.snackbars.get(snackbarId);
      if (manager) {
        await manager.setDotNetReference(dotNetRef);
        return true;
      }
      logger.warn(`Cannot set .NET reference - no manager found for ${snackbarId}`);
      return false;
    },

    /**
     * Show a snackbar
     * @param {string} snackbarId - Snackbar identifier
     * @param {number} [duration] - Display duration in ms
     * @returns {Promise<boolean>} Success status
     */
    show(snackbarId, duration) {
      const manager = this.snackbars.get(snackbarId);
      return manager ? manager.show(duration) : Promise.resolve(false);
    },

    /**
     * Update snackbar content
     * @param {string} snackbarId - Snackbar identifier
     * @param {string} content - New content
     * @returns {Promise<boolean>} Success status
     */
    async updateContent(snackbarId, content) {
      const manager = this.snackbars.get(snackbarId);
      return manager ? manager.updateContent(content) : false;
    },

    /**
     * Start progress for a snackbar
     * @param {string} snackbarId - Snackbar identifier
     * @param {number} duration - Duration in milliseconds
     * @returns {boolean} Success status
     */
    startProgress(snackbarId, duration) {
      const manager = this.snackbars.get(snackbarId);
      if (manager) {
        manager.startProgress(duration);
        return true;
      }
      return false;
    },

    /**
     * Hide a snackbar
     * @param {string} snackbarId - Snackbar identifier
     * @returns {Promise<boolean>} Success status
     */
    hide(snackbarId) {
      const manager = this.snackbars.get(snackbarId);
      return manager ? manager.hide() : Promise.resolve(false);
    },

    /**
     * Check if module is initialized
     * @returns {boolean}
     */
    isInitialized() {
      return isInitialized;
    },

    /**
     * Dispose of a snackbar
     * @param {string} snackbarId - Snackbar identifier
     */
    dispose(snackbarId) {
      const manager = this.snackbars.get(snackbarId);
      if (manager) {
        manager.dispose();
        this.snackbars.delete(snackbarId);
        logger.debug(`Snackbar disposed for ID: ${snackbarId}`);
      }
    },

    /**
     * Dispose all snackbars and reset module
     */
    disposeAll() {
      Array.from(this.snackbars.keys()).forEach(id => this.dispose(id));
      this.snackbars.clear();
      isInitialized = false;
      window.DropBearSnackbar.__initialized = false;
      logger.debug('All snackbars disposed');
    }
  },
  ['DropBearCore']
);

// Get module reference
const snackbarModule = ModuleManager.get('DropBearSnackbar');

// Attach to window
window.DropBearSnackbar = {
  __initialized: false,
  initialize: () => snackbarModule.initialize(),
  createSnackbar: (snackbarId, options) => snackbarModule.createSnackbar(snackbarId, options),
  setDotNetReference: (snackbarId, dotNetRef) => snackbarModule.setDotNetReference(snackbarId, dotNetRef),
  show: (snackbarId, duration) => snackbarModule.show(snackbarId, duration),
  updateContent: (snackbarId, content) => snackbarModule.updateContent(snackbarId, content),
  startProgress: (snackbarId, duration) => snackbarModule.startProgress(snackbarId, duration),
  hide: snackbarId => snackbarModule.hide(snackbarId),
  dispose: snackbarId => snackbarModule.dispose(snackbarId),
  disposeAll: () => snackbarModule.disposeAll()
};

// Export SnackbarManager class
export {SnackbarManager};
