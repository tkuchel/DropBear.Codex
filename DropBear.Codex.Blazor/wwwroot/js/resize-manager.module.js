/**
 * @fileoverview Resize manager for handling window resize events
 * @module resize-manager
 */

import { DOMOperationQueue, EventEmitter, CircuitBreaker } from './core.module.js';
import { DropBearUtils } from './utils.module.js';
import { ModuleManager } from './module-manager.module.js';

const logger = DropBearUtils.createLogger('DropBearResizeManager');
const circuitBreaker = new CircuitBreaker({ failureThreshold: 3, resetTimeout: 30000 });

/**
 * Manager for handling window resize events and related UI updates
 * @implements {IResizeManager}
 */
class ResizeManager {
  /**
   * @param {Object} dotNetReference - .NET reference for Blazor interop
   */
  constructor(dotNetReference) {
    if (!dotNetReference) {
      throw new Error('dotNetRef is required');
    }

    this.dotNetReference = dotNetReference;
    this.resizeObserver = null;
    this.isDisposed = false;
    this.lastDimensions = null;
    this.minResizeInterval = 100; // Minimum time between resize events

    this._initializeResizeObserver();
    EventEmitter.emit(this, 'created', DropBearUtils.createEvent(
      crypto.randomUUID(),
      'created',
      { timestamp: Date.now() }
    ));
  }

  /**
   * Initialize the ResizeObserver
   * @private
   */
  _initializeResizeObserver() {
    try {
      this.resizeObserver = new ResizeObserver(
        DropBearUtils.debounce(async () => {
          if (this.isDisposed) return;

          await this._handleResize();
        }, this.minResizeInterval)
      );

      this.resizeObserver.observe(document.body);
      logger.debug('ResizeObserver initialized');
    } catch (error) {
      logger.error('Failed to initialize ResizeObserver:', error);
      throw new Error('Failed to initialize resize observer: ' + error.message);
    }
  }

  /**
   * Handle resize events
   * @private
   * @returns {Promise<void>}
   */
  async _handleResize() {
    try {
      const dimensions = this._getCurrentDimensions();

      // Skip if dimensions haven't changed significantly
      if (this._dimensionsEqual(dimensions, this.lastDimensions)) {
        return;
      }

      this.lastDimensions = dimensions;

      await circuitBreaker.execute(() =>
        this.dotNetReference.invokeMethodAsync('SetMaxWidthBasedOnWindowSize', dimensions)
      );

      EventEmitter.emit(this, 'resized', DropBearUtils.createEvent(
        crypto.randomUUID(),
        'resized',
        { dimensions, timestamp: Date.now() }
      ));
    } catch (error) {
      logger.error('Error handling resize:', error);
      throw error;
    }
  }

  /**
   * Get current window dimensions
   * @private
   * @returns {{width: number, height: number, scale: number}}
   */
  _getCurrentDimensions() {
    return {
      width: window.innerWidth,
      height: window.innerHeight,
      scale: window.devicePixelRatio || 1
    };
  }

  /**
   * Compare two dimension objects
   * @private
   * @param {Object} dim1 - First dimensions object
   * @param {Object} dim2 - Second dimensions object
   * @returns {boolean} True if dimensions are effectively equal
   */
  _dimensionsEqual(dim1, dim2) {
    if (!dim1 || !dim2) return false;

    const threshold = 5; // Pixel threshold for considering dimensions different
    return Math.abs(dim1.width - dim2.width) < threshold &&
      Math.abs(dim1.height - dim2.height) < threshold &&
      dim1.scale === dim2.scale;
  }

  /**
   * Force a resize event
   * @returns {Promise<void>}
   */
  async forceResize() {
    if (this.isDisposed) {
      throw new Error('Cannot force resize on disposed manager');
    }

    await this._handleResize();
  }

  /**
   * Dispose of the resize manager
   */
  dispose() {
    if (this.isDisposed) return;

    logger.debug('Disposing ResizeManager');
    this.isDisposed = true;

    if (this.resizeObserver) {
      this.resizeObserver.disconnect();
      this.resizeObserver = null;
    }

    this.lastDimensions = null;
    this.dotNetReference = null;

    EventEmitter.emit(this, 'disposed', DropBearUtils.createEvent(
      crypto.randomUUID(),
      'disposed',
      { timestamp: Date.now() }
    ));
  }
}

// Register with ModuleManager
ModuleManager.register('DropBearResizeManager', {
  /** @type {ResizeManager|null} */
  instance: null,

  /**
   * Initialize the resize manager module
   * @returns {Promise<void>}
   */
  async initialize() {
    logger.debug('DropBearResizeManager module initialized');
  },

  /**
   * Create a new resize manager instance
   * @param {Object} dotNetRef - .NET reference
   * @throws {Error} If dotNetRef is missing or invalid
   */
  createResizeManager(dotNetRef) {
    if (!dotNetRef) {
      throw new Error('dotNetRef is required');
    }

    if (this.instance) {
      logger.debug('Disposing existing ResizeManager instance');
      this.dispose();
    }

    try {
      this.instance = new ResizeManager(dotNetRef);
      logger.debug('New ResizeManager instance created');
    } catch (error) {
      logger.error('Failed to create ResizeManager:', error);
      throw error;
    }
  },

  /**
   * Force a resize event on the current instance
   * @returns {Promise<void>}
   */
  async forceResize() {
    if (!this.instance) {
      throw new Error('No ResizeManager instance exists');
    }
    await this.instance.forceResize();
  },

  /**
   * Get current window dimensions
   * @returns {{width: number, height: number, scale: number}}
   */
  getDimensions() {
    return this.instance?._getCurrentDimensions() ?? {
      width: window.innerWidth,
      height: window.innerHeight,
      scale: window.devicePixelRatio || 1
    };
  },

  /**
   * Dispose of the current instance
   */
  dispose() {
    if (this.instance) {
      this.instance.dispose();
      this.instance = null;
    }
  }
}, ['DropBearCore']);

// Export for window object
const module = ModuleManager.get('DropBearResizeManager');

window.DropBearResizeManager = {
  initialize: () => module.initialize(),
  createResizeManager: (dotNetRef) => module.createResizeManager(dotNetRef),
  forceResize: () => module.forceResize(),
  getDimensions: () => module.getDimensions(),
  dispose: () => module.dispose()
};

export { ResizeManager };
