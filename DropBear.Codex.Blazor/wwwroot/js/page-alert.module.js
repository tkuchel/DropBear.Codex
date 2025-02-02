/**
 * @fileoverview PageAlert manager module for displaying page-level alerts
 * @module page-alert
 */

import { CircuitBreaker, DOMOperationQueue, EventEmitter } from './core.module.js';
import { DropBearUtils } from './utils.module.js';
import { ModuleManager } from './module-manager.module.js';

const logger = DropBearUtils.createLogger('DropBearPageAlert');
const circuitBreaker = new CircuitBreaker({ failureThreshold: 3, resetTimeout: 30000 });
let isInitialized = false;

/** @type {Object} Animation configuration constants */
const ANIMATION_CONFIG = {
  DURATION: 300, // Base animation duration in ms
  PROGRESS_UPDATE_INTERVAL: 16, // ~60fps for progress updates
  MIN_DURATION: 1000, // Minimum alert duration
  MAX_DURATION: 10000 // Maximum alert duration
};

/**
 * Manager for page alert behavior and animations
 * @implements {IPageAlertManager}
 */
class PageAlertManager {
  /**
   * @param {string} id - The ID of the alert element
   * @param {boolean} isPermanent - If true, no auto-hide or progress bar
   * @param {Object} [options={}] - Additional configuration options
   */
  constructor(id, isPermanent, options = {}) {
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

    /** @type {number|null} */
    this.hideTimeout = null;

    /** @type {Object} */
    this.options = {
      animationDuration: ANIMATION_CONFIG.DURATION,
      ...options
    };

    if (!DropBearUtils.isElement(this.element)) {
      throw new TypeError('Invalid element provided to PageAlertManager');
    }

    this._cacheElements();
    this._setupEventListeners();

    EventEmitter.emit(
      this.element,
      'created',
      DropBearUtils.createEvent(this.id, 'created', {
        isPermanent,
        options: this.options
      })
    );

    logger.debug('PageAlertManager created:', { id, isPermanent });
  }

  /**
   * Cache required DOM elements
   * @private
   */
  _cacheElements() {
    this.progressBar = this.element.querySelector('.page-alert-progress-bar');
    this.content = this.element.querySelector('.page-alert-content');
    this.closeButton = this.element.querySelector('.page-alert-close');

    // Optional elements that might not exist
    this.icon = this.element.querySelector('.page-alert-icon');
    this.title = this.element.querySelector('.page-alert-title');
  }

  /**
   * Set up event listeners
   * @private
   */
  _setupEventListeners() {
    if (this.isPermanent) return;

    // Mouse enter/leave for progress pause
    this.handleMouseEnter = () => this._pauseProgress();
    this.handleMouseLeave = () => this._resumeProgress();
    this.element.addEventListener('mouseenter', this.handleMouseEnter);
    this.element.addEventListener('mouseleave', this.handleMouseLeave);

    // Close button click handler
    if (this.closeButton) {
      this.handleClose = () => this.hide();
      this.closeButton.addEventListener('click', this.handleClose);
    }

    logger.debug('Event listeners initialized');
  }

  /**
   * Show the alert with animation
   * @param {number} [duration] - Duration to show alert (for non-permanent alerts)
   * @returns {Promise<boolean>} Success status
   */
  async show(duration) {
    if (this.isDisposed) return false;

    try {
      await new Promise(resolve => {
        DOMOperationQueue.add(() => {
          cancelAnimationFrame(this.animationFrame);
          this.element.classList.remove('hide');

          this.animationFrame = requestAnimationFrame(() => {
            this.element.classList.add('show');

            const transitionEndHandler = () => {
              this.element.removeEventListener('transitionend', transitionEndHandler);
              resolve();
            };

            this.element.addEventListener('transitionend', transitionEndHandler);

            // Fallback
            setTimeout(resolve, this.options.animationDuration + 50);
          });
        });
      });

      if (!this.isPermanent && duration > 0) {
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

      logger.debug('Alert shown:', { id: this.id, duration });
      return true;
    } catch (error) {
      logger.error('Error showing alert:', error);
      return false;
    }
  }

  /**
   * Start progress bar animation
   * @param {number} duration - Duration in milliseconds
   */
  startProgress(duration) {
    if (this.isDisposed || this.isPermanent || !this.progressBar) return;

    duration = Math.max(
      ANIMATION_CONFIG.MIN_DURATION,
      Math.min(duration, ANIMATION_CONFIG.MAX_DURATION)
    );

    this.progressDuration = duration;

    DOMOperationQueue.add(() => {
      clearTimeout(this.progressTimeout);
      clearTimeout(this.hideTimeout);

      this.progressBar.style.transition = 'none';
      this.progressBar.style.transform = 'scaleX(1)';

      this.animationFrame = requestAnimationFrame(() => {
        this.progressBar.style.transition = `transform ${duration}ms linear`;
        this.progressBar.style.transform = 'scaleX(0)';

        this.progressTimeout = setTimeout(() => {
          this.hide();
        }, duration);
      });
    });

    logger.debug('Progress started:', { duration });
  }

  /**
   * Pause progress bar animation
   * @private
   */
  _pauseProgress() {
    if (this.isDisposed || this.isPermanent || !this.progressBar) return;

    clearTimeout(this.progressTimeout);
    clearTimeout(this.hideTimeout);

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
    if (this.isDisposed || this.isPermanent || !this.progressBar) return;

    const computedStyle = window.getComputedStyle(this.progressBar);
    const currentScale = this._getCurrentScale(computedStyle.transform);
    const remainingTime = this.progressDuration * currentScale;

    DOMOperationQueue.add(() => {
      this.progressBar.style.transition = `transform ${remainingTime}ms linear`;
      this.progressBar.style.transform = 'scaleX(0)';

      this.hideTimeout = setTimeout(() => {
        this.hide();
      }, remainingTime);
    });

    logger.debug('Progress resumed:', { remainingTime });
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
   * Hide the alert with animation
   * @returns {Promise<boolean>} Success status
   */
  async hide() {
    if (this.isDisposed) return false;

    try {
      await new Promise(resolve => {
        clearTimeout(this.progressTimeout);
        clearTimeout(this.hideTimeout);
        cancelAnimationFrame(this.animationFrame);

        DOMOperationQueue.add(() => {
          this.element.classList.remove('show');
          this.element.classList.add('hide');

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
      });

      EventEmitter.emit(
        this.element,
        'hidden',
        DropBearUtils.createEvent(this.id, 'hidden', {
          timestamp: Date.now()
        })
      );

      logger.debug('Alert hidden');
      return true;
    } catch (error) {
      logger.error('Error hiding alert:', error);
      return false;
    }
  }

  /**
   * Update alert content
   * @param {string} content - New content HTML
   * @returns {Promise<boolean>} Success status
   */
  async updateContent(content) {
    if (this.isDisposed || !this.content) return false;

    try {
      await new Promise(resolve => {
        DOMOperationQueue.add(() => {
          this.content.innerHTML = content;
          resolve();
        });
      });

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
   * Dispose the alert manager
   */
  dispose() {
    if (this.isDisposed) return;

    logger.debug('Disposing alert manager:', { id: this.id });
    this.isDisposed = true;

    clearTimeout(this.progressTimeout);
    clearTimeout(this.hideTimeout);
    cancelAnimationFrame(this.animationFrame);

    if (!this.isPermanent) {
      this.element.removeEventListener('mouseenter', this.handleMouseEnter);
      this.element.removeEventListener('mouseleave', this.handleMouseLeave);
    }

    if (this.closeButton && this.handleClose) {
      this.closeButton.removeEventListener('click', this.handleClose);
    }

    DOMOperationQueue.add(() => {
      if (this.element?.parentNode) {
        this.element.parentNode.removeChild(this.element);
      }
    });

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
  'DropBearPageAlert',
  {
    /** @type {Map<string, PageAlertManager>} */
    alerts: new Map(),

    /**
     * Initialize the page alert module
     * @returns {Promise<void>}
     */
    async initialize() {
      if (isInitialized) {
        return;
      }

      try {
        logger.debug('Page alert module initializing');

        // Initialize dependencies
        await ModuleManager.waitForDependencies(['DropBearCore']);

        isInitialized = true;
        window.DropBearPageAlert.__initialized = true;

        logger.debug('Page alert module initialized');
      } catch (error) {
        logger.error('Page alert initialization failed:', error);
        throw error;
      }
    },

    /**
     * Create a new page alert
     * @param {string} id - Alert element ID
     * @param {number} [duration=5000] - Duration in ms
     * @param {boolean} [isPermanent=false] - Whether the alert is permanent
     * @param {Object} [options={}] - Additional options
     * @returns {boolean} Success status
     */
    create(id, duration = 5000, isPermanent = false, options = {}) {
      try {
        if (!isInitialized) {
          throw new Error('Module not initialized');
        }

        DropBearUtils.validateArgs([id], ['string'], 'create');

        if (this.alerts.has(id)) {
          logger.debug(`Alert ${id} already exists; disposing old instance`);
          this.alerts.get(id).dispose();
        }

        const manager = new PageAlertManager(id, isPermanent, options);
        this.alerts.set(id, manager);

        // Show immediately
        manager.show(duration);

        logger.debug('Alert created:', { id, duration, isPermanent });
        return true;
      } catch (error) {
        logger.error('Error creating alert:', error);
        return false;
      }
    },

    /**
     * Update alert content
     * @param {string} id - Alert ID
     * @param {string} content - New content
     * @returns {Promise<boolean>} Success status
     */
    async updateContent(id, content) {
      const manager = this.alerts.get(id);
      return manager ? manager.updateContent(content) : false;
    },

    /**
     * Hide a single alert
     * @param {string} id - Alert ID
     * @returns {Promise<boolean>} Success status
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
     * @returns {Promise<boolean[]>} Array of success statuses
     */
    async hideAll() {
      const promises = Array.from(this.alerts.values()).map(manager => manager.hide());
      return Promise.all(promises);
    },

    /**
     * Check if module is initialized
     * @returns {boolean}
     */
    isInitialized() {
      return isInitialized;
    },

    /**
     * Dispose the module
     */
    dispose() {
      Array.from(this.alerts.values()).forEach(manager => manager.dispose());
      this.alerts.clear();
      isInitialized = false;
      window.DropBearPageAlert.__initialized = false;
      logger.debug('Page alert module disposed');
    }
  },
  ['DropBearCore']
);

// Get module reference
const pageAlertModule = ModuleManager.get('DropBearPageAlert');

// Attach to window
window.DropBearPageAlert = {
  __initialized: false,
  initialize: () => pageAlertModule.initialize(),
  create: (id, duration, isPermanent, options) => pageAlertModule.create(id, duration, isPermanent, options),
  updateContent: (id, content) => pageAlertModule.updateContent(id, content),
  hide: id => pageAlertModule.hide(id),
  hideAll: () => pageAlertModule.hideAll(),
  dispose: () => pageAlertModule.dispose()
};

// Export PageAlertManager class
export { PageAlertManager };
