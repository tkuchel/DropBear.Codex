/**
 * @fileoverview Resize manager for handling window resize events
 * @module resize-manager
 */

import { DOMOperationQueue, EventEmitter, CircuitBreaker } from './DropBearCore.module.js';
import { DropBearUtils } from './DropBearUtils.module.js';

const logger = DropBearUtils.createLogger('DropBearResizeManager');
const circuitBreaker = new CircuitBreaker({ failureThreshold: 3, resetTimeout: 30000 });
let isInitialized = false;
const moduleName = 'DropBearResizeManager';

/** @type {Object} Resize configuration constants */
const RESIZE_CONFIG = {
  MIN_RESIZE_INTERVAL: 100, // Minimum time between resize events (ms)
  DIMENSION_THRESHOLD: 5,   // Minimum pixel change to trigger resize
  MAX_DIMENSION: 10000,     // Maximum supported dimension
  DEFAULT_SCALE: 1          // Default device pixel ratio
};

/**
 * Manager for handling window resize events
 * @implements {IResizeManager}
 */
class ResizeManager {
  /**
   * @param {Object} dotNetReference - .NET reference for Blazor interop
   * @param {Object} [options={}] - Configuration options
   */
  constructor(dotNetReference, options = {}) {
    if (!dotNetReference) {
      throw new Error('dotNetRef is required');
    }

    /** @type {Object|null} */
    this.dotNetReference = dotNetReference;

    /** @type {ResizeObserver|null} */
    this.resizeObserver = null;

    /** @type {boolean} */
    this.isDisposed = false;

    /** @type {{width: number, height: number, scale: number}|null} */
    this.lastDimensions = null;

    /** @type {Object} */
    this.options = {
      minResizeInterval: RESIZE_CONFIG.MIN_RESIZE_INTERVAL,
      dimensionThreshold: RESIZE_CONFIG.DIMENSION_THRESHOLD,
      ...options
    };

    // Removed unused debounceTimeout

    this._initializeResizeObserver();

    // Emit created event with a generated ID (using crypto.randomUUID)
    EventEmitter.emit(
      this,
      'created',
      DropBearUtils.createEvent(crypto.randomUUID(), 'created', {
        timestamp: Date.now(),
        options: this.options
      })
    );

    logger.debug('ResizeManager created');
  }

  /**
   * Initialize the ResizeObserver to observe the document body.
   * @private
   */
  _initializeResizeObserver() {
    try {
      // Create a debounced resize handler
      this.resizeObserver = new ResizeObserver(
        this._createResizeHandler()
      );
      // Observe the document body (adjust if a different element is desired)
      this.resizeObserver.observe(document.body);
      logger.debug('ResizeObserver initialized');
    } catch (error) {
      logger.error('Failed to initialize ResizeObserver:', error);
      throw error;
    }
  }

  /**
   * Create a debounced resize handler using DropBearUtils.debounce.
   * @private
   * @returns {Function} Debounced handler
   */
  _createResizeHandler() {
    return DropBearUtils.debounce(async () => {
      if (this.isDisposed) return;
      await this._handleResize();
    }, this.options.minResizeInterval);
  }

  /**
   * Handle resize events by comparing current dimensions to the last known dimensions.
   * If the change is significant, notify the .NET side.
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

      // Use the circuit breaker to invoke the .NET method
      await circuitBreaker.execute(() =>
        this.dotNetReference.invokeMethodAsync(
          'SetMaxWidthBasedOnWindowSize',
          dimensions
        )
      );

      EventEmitter.emit(
        this,
        'resized',
        DropBearUtils.createEvent(crypto.randomUUID(), 'resized', {
          dimensions,
          timestamp: Date.now()
        })
      );

      logger.debug('Window resized:', dimensions);
    } catch (error) {
      logger.error('Error handling resize:', error);
      throw error;
    }
  }

  /**
   * Get the current window dimensions, constrained by MAX_DIMENSION.
   * @private
   * @returns {{ width: number, height: number, scale: number }}
   */
  _getCurrentDimensions() {
    const width = Math.min(window.innerWidth, RESIZE_CONFIG.MAX_DIMENSION);
    const height = Math.min(window.innerHeight, RESIZE_CONFIG.MAX_DIMENSION);
    const scale = window.devicePixelRatio || RESIZE_CONFIG.DEFAULT_SCALE;

    return { width, height, scale };
  }

  /**
   * Compare two dimension objects to see if they are effectively equal.
   * @private
   * @param {Object} dim1 - First dimensions object
   * @param {Object} dim2 - Second dimensions object
   * @returns {boolean} True if dimensions are effectively the same
   */
  _dimensionsEqual(dim1, dim2) {
    if (!dim1 || !dim2) return false;

    return Math.abs(dim1.width - dim2.width) < this.options.dimensionThreshold &&
      Math.abs(dim1.height - dim2.height) < this.options.dimensionThreshold &&
      dim1.scale === dim2.scale;
  }

  /**
   * Force a resize event by calling the handler directly.
   * @returns {Promise<void>}
   */
  async forceResize() {
    if (this.isDisposed) {
      throw new Error('Cannot force resize on disposed manager');
    }
    await this._handleResize();
  }

  /**
   * Get current dimensions (throws if disposed).
   * @returns {{ width: number, height: number, scale: number }}
   */
  getDimensions() {
    if (this.isDisposed) {
      throw new Error('Cannot get dimensions from disposed manager');
    }
    return this._getCurrentDimensions();
  }

  /**
   * Dispose of the resize manager by disconnecting the observer and clearing references.
   */
  dispose() {
    if (this.isDisposed) return;

    logger.debug('Disposing ResizeManager');
    this.isDisposed = true;

    // Clear any pending debounced calls (if applicable)
    // Note: If DropBearUtils.debounce does not expose a cancel method,
    // ensure that no further calls occur by checking isDisposed in the handler.
    // clearTimeout(this.debounceTimeout); // Removed since debounceTimeout is not used

    if (this.resizeObserver) {
      this.resizeObserver.disconnect();
      this.resizeObserver = null;
    }

    this.lastDimensions = null;
    this.dotNetReference = null;

    EventEmitter.emit(
      this,
      'disposed',
      DropBearUtils.createEvent(crypto.randomUUID(), 'disposed', {
        timestamp: Date.now()
      })
    );
  }
}

// Attach to window first
window[moduleName] = {
  __initialized: false,
  instance: null,

  initialize: async () => {
    if (isInitialized) {
      return;
    }

    try {
      logger.debug('Resize manager module initializing');

      // Initialize dependencies first
      await window.DropBearUtils.initialize();
      await window.DropBearCore.initialize();

      isInitialized = true;
      window[moduleName].__initialized = true;

      logger.debug('Resize manager module initialized');
    } catch (error) {
      logger.error('Resize manager initialization failed:', error);
      throw error;
    }
  },

  createResizeManager: (dotNetRef, options = {}) => {
    try {
      if (!isInitialized) {
        throw new Error('Module not initialized');
      }

      if (window[moduleName].instance) {
        logger.debug('Disposing existing ResizeManager instance');
        window[moduleName].dispose();
      }

      window[moduleName].instance = new ResizeManager(dotNetRef, options);
      logger.debug('New ResizeManager instance created');
    } catch (error) {
      logger.error('Failed to create ResizeManager:', error);
      throw error;
    }
  },

  forceResize: async () => {
    if (!window[moduleName].instance) {
      throw new Error('No ResizeManager instance exists');
    }
    await window[moduleName].instance.forceResize();
  },

  getDimensions: () => {
    if (!window[moduleName].instance) {
      return {
        width: window.innerWidth,
        height: window.innerHeight,
        scale: window.devicePixelRatio || RESIZE_CONFIG.DEFAULT_SCALE
      };
    }
    return window[moduleName].instance.getDimensions();
  },

  isInitialized: () => isInitialized,

  dispose: () => {
    if (window[moduleName].instance) {
      window[moduleName].instance.dispose();
      window[moduleName].instance = null;
    }
    isInitialized = false;
    window[moduleName].__initialized = false;
    logger.debug('Resize manager module disposed');
  }
};

// Register with ModuleManager after window attachment
window.DropBearModuleManager.register(
  moduleName,
  {
    initialize: () => window[moduleName].initialize(),
    isInitialized: () => window[moduleName].isInitialized(),
    dispose: () => window[moduleName].dispose()
  },
  ['DropBearUtils', 'DropBearCore']
);

// Export the API functions under a unique namespace for the resize manager module.
export const DropBearResizeManagerAPI = {
  /**
   * Initializes the resize manager module.
   * @returns {Promise<void>}
   */
  initialize: async () => window[moduleName].initialize(),

  /**
   * Creates a new ResizeManager instance.
   * @param {Object} dotNetRef - The .NET reference for Blazor interop.
   * @param {Object} [options={}] - Optional configuration options.
   * @returns {Promise<void>}
   */
  createResizeManager: async (dotNetRef, options = {}) =>
    window[moduleName].createResizeManager(dotNetRef, options),

  /**
   * Forces a resize event.
   * @returns {Promise<void>}
   */
  forceResize: async () => window[moduleName].forceResize(),

  /**
   * Gets the current dimensions of the window.
   * @returns {{ width: number, height: number, scale: number }}
   */
  getDimensions: () => window[moduleName].getDimensions(),

  /**
   * Checks whether the resize manager module is initialized.
   * @returns {boolean}
   */
  isInitialized: () => window[moduleName].isInitialized(),

  /**
   * Disposes of the resize manager module.
   * @returns {Promise<void>}
   */
  dispose: async () => window[moduleName].dispose()
};

// Also export the ResizeManager class for direct access if needed.
export { ResizeManager };
