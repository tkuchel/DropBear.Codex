/**
 * @fileoverview PageAlert manager module for displaying page-level alerts
 * @module DropBearPageAlert
 */

import {CircuitBreaker, DOMOperationQueue, EventEmitter} from './core.module.js';
import {DropBearUtils} from './utils.module.js';
import {ModuleManager} from './module-manager.module.js';

/**
 * Create a logger instance
 */
const logger = DropBearUtils.createLogger('DropBearPageAlert');

/**
 * Circuit breaker for repeated failures
 */
const circuitBreaker = new CircuitBreaker({failureThreshold: 3, resetTimeout: 30000});

/**
 * Default animation duration in ms
 */
const ANIMATION_DURATION = 300;

/**
 * A PageAlertManager that manages show/hide lifecycle for a page alert element
 * @implements {IPageAlertManager}
 */
class PageAlertManager {
  /**
   * Create a new manager for the specified element ID
   * @param {string} id - Element ID
   * @param {boolean} isPermanent - If true, no auto-hide or progress bar
   */
  constructor(id, isPermanent) {
    /** @type {string} */
    this.id = id;
    /** @type {boolean} */
    this.isPermanent = isPermanent;
    /** @type {boolean} */
    this.isDisposed = false;
    /** @type {HTMLElement|null} */
    this.element = document.getElementById(id);

    /** @type {number} */
    this.progressDuration = 0;
    /** @type {number|null} */
    this.progressTimeout = null;
    /** @type {number|null} */
    this.animationFrame = null;

    if (!this.element) {
      throw new Error(`Element with id ${id} not found`);
    }

    this._cacheElements();
    this._setupEventListeners();

    // Fire an event indicating the alert was created
    EventEmitter.emit(this.element, 'created', {id});
  }

  /**
   * Find needed sub-elements (e.g., progress bar)
   * @private
   */
  _cacheElements() {
    this.progressBar = this.element.querySelector('.page-alert-progress-bar');
  }

  /**
   * Set up event listeners for mouseenter/mouseleave if it's not permanent
   * @private
   */
  _setupEventListeners() {
    if (this.isPermanent) return;

    this.handleMouseEnter = () => this._pauseProgress();
    this.handleMouseLeave = () => this._resumeProgress();

    this.element.addEventListener('mouseenter', this.handleMouseEnter);
    this.element.addEventListener('mouseleave', this.handleMouseLeave);
  }

  /**
   * Show the alert (fade in, etc.)
   * @returns {Promise<boolean>} - Resolves when transition completes
   */
  show() {
    if (this.isDisposed) {
      return Promise.resolve(true);
    }

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

          // Fallback: resolve after an animation
          setTimeout(() => resolve(true), ANIMATION_DURATION + 50);
        });
      })));
  }

  /**
   * Start the progress bar for auto-hide after a given duration (if not permanent)
   * @param {number} duration - Duration in ms
   */
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

  /**
   * Pause the progress bar animation
   * @private
   */
  _pauseProgress() {
    if (this.isDisposed || this.isPermanent || !this.progressBar) return;

    clearTimeout(this.progressTimeout);
    const computedStyle = window.getComputedStyle(this.progressBar);

    DOMOperationQueue.add(() => {
      this.progressBar.style.transition = 'none';
      this.progressBar.style.transform = computedStyle.transform;
    });
  }

  /**
   * Resume the progress bar animation from its current scale
   * @private
   */
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

  /**
   * Parse out the scaleX from a matrix transform
   * @private
   * @param {string} transform
   * @returns {number}
   */
  _getCurrentScale(transform) {
    if (transform === 'none') return 1;
    const match = transform.match(/matrix\(([^)]+)\)/);
    if (!match) return 1;
    // e.g. "matrix(0.65, 0, 0, 1, 0, 0)"
    return parseFloat(match[1].split(',')[0]) || 1;
  }

  /**
   * Hide the alert (fade out), then dispose
   * @returns {Promise<boolean>}
   */
  hide() {
    if (this.isDisposed) {
      return Promise.resolve(true);
    }

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

  /**
   * Dispose of the alert, remove from DOM and mark as done
   */
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

//----------------------------------------------
// Register module with ModuleManager
//----------------------------------------------
ModuleManager.register(
  'DropBearPageAlert',
  {
    /** @type {Map<string, PageAlertManager>} */
    alerts: new Map(),

    /**
     * No-arg init so "ModuleManager.initialize('DropBearPageAlert')" won't fail.
     */
    async initialize() {
      logger.debug('DropBearPageAlert module init done (no-arg).');
    },

    /**
     * Create a new page alert with given parameters
     * @param {string} id - The element ID
     * @param {number} [duration=5000] - Duration in ms
     * @param {boolean} [isPermanent=false] - Whether the alert is permanent
     * @returns {boolean} - True if creation succeeded
     */
    create(id, duration = 5000, isPermanent = false) {
      try {
        DropBearUtils.validateArgs([id], ['string'], 'create');

        // Dispose existing if it exists
        if (this.alerts.has(id)) {
          logger.debug(`Alert ${id} already exists; disposing old instance`);
          this.alerts.get(id).dispose();
        }

        const manager = new PageAlertManager(id, isPermanent);
        this.alerts.set(id, manager);

        // Show immediately
        manager.show().then(() => {
          if (!isPermanent && duration > 0) {
            manager.startProgress(duration);
          }
        });

        return true;
      } catch (error) {
        logger.error('Error creating page alert:', error);
        return false;
      }
    },

    /**
     * Hide a single alert
     * @param {string} id
     * @returns {Promise<boolean>}
     */
    hide(id) {
      const manager = this.alerts.get(id);
      if (!manager) {
        logger.warn(`Cannot hide alert: no manager found for ${id}`);
        return Promise.resolve(false);
      }
      return manager.hide();
    },

    /**
     * Hide all alerts
     * @returns {Promise<boolean[]>} - Array of result booleans
     */
    async hideAll() {
      const promises = [];
      for (const [id, manager] of this.alerts.entries()) {
        promises.push(manager.hide());
      }
      return Promise.all(promises);
    }
  },
  ['DropBearCore']
);

//----------------------------------------------
// Grab the registered module reference
//----------------------------------------------
const dropBearPageAlertModule = ModuleManager.get('DropBearPageAlert');

//----------------------------------------------
// Attach to window if you want global usage
//----------------------------------------------
window.DropBearPageAlert = {
  initialize: () => dropBearPageAlertModule.initialize(),
  create: (id, duration, isPermanent) => dropBearPageAlertModule.create(id, duration, isPermanent),
  hide: id => dropBearPageAlertModule.hide(id),
  hideAll: () => dropBearPageAlertModule.hideAll()
};

/**
 * Export the PageAlertManager class if you need to import it directly.
 */
export {PageAlertManager};
