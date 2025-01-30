/**
 * @fileoverview Snackbar component module for displaying temporary notifications
 * @module snackbar
 */

import {CircuitBreaker, DOMOperationQueue, EventEmitter} from './core.module.js';
import {DropBearUtils} from './utils.module.js';
import {ModuleManager} from './module-manager.module.js';

const logger = DropBearUtils.createLogger('DropBearSnackbar');
const circuitBreaker = new CircuitBreaker({failureThreshold: 3, resetTimeout: 30000});

/**
 * Manager for individual snackbar instances
 * @implements {ISnackbarManager}
 */
class SnackbarManager {
  /**
   * @param {string} id - Unique identifier for the snackbar
   */
  constructor(id) {
    DropBearUtils.validateArgs([id], ['string'], 'SnackbarManager');

    this.id = id;
    this.element = document.getElementById(id);
    this.isDisposed = false;
    this.progressTimeout = null;
    this.animationFrame = null;
    this.dotNetRef = null;

    if (!DropBearUtils.isElement(this.element)) {
      throw new TypeError('Invalid element provided to SnackbarManager');
    }

    this.progressBar = this.element.querySelector('.progress-bar');
    this.scopedAttribute = Object.keys(this.element.attributes)
      .map(key => this.element.attributes[key])
      .find(attr => attr.name.startsWith('b-'))?.name;

    this._setupEventListeners();
    EventEmitter.emit(
      this.element,
      'created',
      DropBearUtils.createEvent(this.id, 'created', {timestamp: Date.now()})
    );
  }

  /**
   * Set .NET reference for Blazor interop
   * @param {Object} dotNetRef - .NET reference object
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
   * Set up mouse event listeners for progress bar
   * @private
   */
  _setupEventListeners() {
    this.handleMouseEnter = () => this._pauseProgress();
    this.handleMouseLeave = () => this._resumeProgress();

    this.element.addEventListener('mouseenter', this.handleMouseEnter);
    this.element.addEventListener('mouseleave', this.handleMouseLeave);
  }

  /**
   * Show the snackbar
   * @returns {Promise<boolean>} Success status
   */
  show() {
    if (this.isDisposed) return Promise.resolve(false);

    return circuitBreaker.execute(async () => {
      logger.debug(`Showing snackbar ${this.id}`);
      DOMOperationQueue.add(() => {
        this.element.classList.remove('hide');
        requestAnimationFrame(() => this.element.classList.add('show'));
      });
      return true;
    });
  }

  /**
   * Start progress bar animation
   * @param {number} duration - Duration in milliseconds
   */
  startProgress(duration) {
    if (this.isDisposed || !duration || !this.progressBar) {
      logger.debug(`Cannot start progress for snackbar ${this.id} - invalid state`);
      return;
    }

    logger.debug(`Starting progress for snackbar ${this.id} with duration ${duration}ms`);

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
            logger.debug(`Progress complete for snackbar ${this.id}`);
            if (this.dotNetRef) {
              await this.dotNetRef.invokeMethodAsync('OnProgressComplete');
            }
          } catch (error) {
            logger.error(`Error notifying progress completion for snackbar ${this.id}:`, error);
          }
        }, duration);
      });
    });
  }

  /**
   * Pause progress bar animation
   * @private
   */
  _pauseProgress() {
    if (this.isDisposed || !this.progressBar) return;

    logger.debug(`Pausing progress for snackbar ${this.id}`);
    clearTimeout(this.progressTimeout);
    const computedStyle = window.getComputedStyle(this.progressBar);

    DOMOperationQueue.add(() => {
      this.progressBar.style.transition = 'none';
      this.progressBar.style.transform = computedStyle.transform;
    });
  }

  /**
   * Resume progress bar animation
   * @private
   */
  _resumeProgress() {
    if (this.isDisposed || !this.progressBar) return;

    const computedStyle = window.getComputedStyle(this.progressBar);
    const duration = parseFloat(this.element.style.getPropertyValue('--duration')) || 5000;
    const currentScale = this._getCurrentScale(computedStyle.transform);
    const remainingTime = duration * currentScale;

    logger.debug(`Resuming progress for snackbar ${this.id} with ${remainingTime}ms remaining`);

    DOMOperationQueue.add(() => {
      this.progressBar.style.transition = `transform ${remainingTime}ms linear`;
      this.progressBar.style.transform = 'scaleX(0)';

      this.progressTimeout = setTimeout(async () => {
        try {
          if (this.dotNetRef) {
            await this.dotNetRef.invokeMethodAsync('OnProgressComplete');
          }
        } catch (error) {
          logger.error(`Error notifying progress completion for snackbar ${this.id}:`, error);
        }
      }, remainingTime);
    });
  }

  /**
   * Get current scale value from transform matrix
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
  hide() {
    if (this.isDisposed) return Promise.resolve(false);

    return circuitBreaker.execute(async () => {
      logger.debug(`Hiding snackbar ${this.id}`);
      DOMOperationQueue.add(() => {
        clearTimeout(this.progressTimeout);
        cancelAnimationFrame(this.animationFrame);
        this.element.classList.remove('show');
        this.element.classList.add('hide');
      });
      return true;
    });
  }

  /**
   * Dispose of the snackbar
   */
  dispose() {
    if (this.isDisposed) return;

    logger.debug(`Disposing snackbar ${this.id}`);
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

    this.dotNetRef = null;

    EventEmitter.emit(
      this.element,
      'disposed',
      DropBearUtils.createEvent(this.id, 'disposed', {timestamp: Date.now()})
    );
  }
}

// Register with ModuleManager
ModuleManager.register('DropBearSnackbar', {
  /** @type {Map<string, SnackbarManager>} */
  snackbars: new Map(),

  /**
   * Initialize the snackbar module
   * @returns {Promise<boolean>} Success status
   */
  async initialize() {
    return circuitBreaker.execute(async () => {
      logger.debug('DropBearSnackbar global module initialized');
      return true;
    });
  },

  /**
   * Create a new snackbar instance
   * @param {string} snackbarId - Unique identifier
   * @returns {Promise<void>}
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
   * @returns {Promise<boolean>} Success status
   */
  show(snackbarId) {
    const manager = this.snackbars.get(snackbarId);
    return manager ? manager.show() : Promise.resolve(false);
  },

  /**
   * Start progress bar for a snackbar
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
   * Dispose of a snackbar
   * @param {string} snackbarId - Snackbar identifier
   */
  dispose(snackbarId) {
    const manager = this.snackbars.get(snackbarId);
    if (manager) {
      manager.dispose();
      this.snackbars.delete(snackbarId);
    }
  }
}, ['DropBearCore']);

// Export for window object
const module = ModuleManager.get('DropBearSnackbar');

window.DropBearSnackbar = {
  initialize: () => module.initialize(),
  createSnackbar: snackbarId => module.createSnackbar(snackbarId),
  setDotNetReference: (snackbarId, dotNetRef) => module.setDotNetReference(snackbarId, dotNetRef),
  show: snackbarId => module.show(snackbarId),
  startProgress: (snackbarId, duration) => module.startProgress(snackbarId, duration),
  hide: snackbarId => module.hide(snackbarId),
  dispose: snackbarId => module.dispose(snackbarId)
};

export {SnackbarManager};
