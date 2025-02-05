/**
 * @fileoverview Utility functions for the DropBear framework
 * @module utils
 */

let isInitialized = false;

/**
 * Core utility functions for DropBear.
 */
const DropBearUtils = {
  /**
   * Create a namespaced logger.
   * @param {string} namespace - Logger namespace
   * @returns {ILogger} Logger instance
   */
  createLogger(namespace) {
    if (typeof namespace !== 'string' || !namespace.trim()) {
      throw new TypeError('Namespace must be a non-empty string');
    }
    const prefix = `[${namespace}]`;
    return {
      debug: (message, ...args) => console.debug(`${prefix} ${message}`, ...args),
      info: (message, ...args) => console.log(`${prefix} ${message}`, ...args),
      warn: (message, ...args) => console.warn(`${prefix} ${message}`, ...args),
      error: (message, ...args) => console.error(`${prefix} ${message}`, ...args),
    };
  },

  /**
   * Validate function arguments against expected types.
   * @param {Array<*>} args - Arguments to validate
   * @param {Array<string>} types - Expected types
   * @param {string} functionName - Name of function for error messages
   * @throws {TypeError} If arguments don't match expected types
   */
  validateArgs(args, types, functionName) {
    if (!Array.isArray(args) || !Array.isArray(types)) {
      throw new TypeError('Args and types must be arrays');
    }
    if (typeof functionName !== 'string') {
      throw new TypeError('Function name must be a string');
    }
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

  /**
   * Debounce a function.
   * @param {Function} func - Function to debounce
   * @param {number} wait - Wait time in milliseconds
   * @returns {Function} Debounced function
   */
  debounce(func, wait) {
    if (typeof func !== 'function') {
      throw new TypeError('Expected a function');
    }
    if (typeof wait !== 'number' || wait < 0) {
      throw new TypeError('Wait must be a positive number');
    }
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

  /**
   * Throttle a function.
   * @param {Function} func - Function to throttle
   * @param {number} limit - Time limit in milliseconds
   * @returns {Function} Throttled function
   */
  throttle(func, limit) {
    if (typeof func !== 'function') {
      throw new TypeError('Expected a function');
    }
    if (typeof limit !== 'number' || limit < 0) {
      throw new TypeError('Limit must be a positive number');
    }
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

  /**
   * Check if a value is a DOM element.
   * @param {*} element - Value to check
   * @returns {boolean} True if value is an Element or HTMLDocument
   */
  isElement(element) {
    return element instanceof Element || element instanceof HTMLDocument;
  },

  /**
   * Create a DropBear error object.
   * @param {string} message - Error message
   * @param {string} code - Error code
   * @param {string} [component] - Component name
   * @param {*} [details] - Additional error details
   * @returns {IDropBearError} DropBear error object
   */
  createError(message, code, component, details) {
    if (typeof message !== 'string' || !message.trim()) {
      throw new TypeError('Message must be a non-empty string');
    }
    if (typeof code !== 'string' || !code.trim()) {
      throw new TypeError('Code must be a non-empty string');
    }
    const error = new Error(message);
    error.code = code;
    if (component) error.component = component;
    if (details) error.details = details;
    return error;
  },

  /**
   * Create a DropBear event object.
   * @param {string} id - Event identifier
   * @param {string} type - Event type
   * @param {*} [data] - Event data
   * @returns {IDropBearEvent} DropBear event object
   */
  createEvent(id, type, data) {
    if (typeof id !== 'string' || !id.trim()) {
      throw new TypeError('ID must be a non-empty string');
    }
    if (typeof type !== 'string' || !type.trim()) {
      throw new TypeError('Type must be a non-empty string');
    }
    return {id, type, data};
  },

  /**
   * Clicks a DOM element by its id.
   * @param {string} id - The id of the element to click.
   * @throws {TypeError} If id is not a string.
   * @throws {Error} If no element with the specified id is found.
   */
  clickElementById(id) {
    // Validate that the provided id is a string.
    this.validateArgs([id], ['string'], 'clickElementById');
    const element = document.getElementById(id);
    if (!element) {
      throw this.createError(
        `Element with id "${id}" not found.`,
        'ELEMENT_NOT_FOUND',
        'clickElementById'
      );
    }
    element.click();
  },
};

/**
 * Additional window-related utility functions.
 */
const DropBearUtilities = {
  /**
   * Get current window dimensions.
   * @returns {{width: number, height: number}} Window dimensions
   */
  getWindowDimensions() {
    try {
      return {
        width: window.innerWidth,
        height: window.innerHeight,
      };
    } catch (error) {
      console.error('Error getting window dimensions:', error);
      return {width: 0, height: 0};
    }
  },

  /**
   * Safely execute a function with the window context.
   * @param {Function} func - Function to execute
   * @param {*} defaultValue - Default value if execution fails
   * @returns {*} Function result or default value
   */
  safeWindowOperation(func, defaultValue) {
    try {
      return func();
    } catch (error) {
      console.error('Window operation failed:', error);
      return defaultValue;
    }
  },

  /**
   * Check if an element is visible in the viewport.
   * @param {Element} element - DOM element to check
   * @returns {boolean} True if element is visible
   */
  isElementInViewport(element) {
    if (!DropBearUtils.isElement(element)) {
      throw new TypeError('Parameter must be a DOM element');
    }
    try {
      const rect = element.getBoundingClientRect();
      return rect.top >= 0 &&
        rect.left >= 0 &&
        rect.bottom <= window.innerHeight &&
        rect.right <= window.innerWidth;
    } catch (error) {
      console.error('Error checking element visibility:', error);
      return false;
    }
  },
};

// Attach to window immediately (without ModuleManager registration)
window.DropBearUtils = {
  __initialized: false,
  ...DropBearUtils,
  ...DropBearUtilities,
  initialize: async () => {
    if (isInitialized) return;
    try {
      const logger = DropBearUtils.createLogger('DropBearUtils');
      logger.debug('Utils module initializing');
      isInitialized = true;
      window.DropBearUtils.__initialized = true;
      logger.debug('Utils module initialized');
    } catch (error) {
      console.error('Utils initialization failed:', error);
      throw error;
    }
  },
  isInitialized: () => isInitialized,
  dispose: () => {
    isInitialized = false;
    window.DropBearUtils.__initialized = false;
  }
};

// Export the API functions under a unique namespace for the DropBearUtils module.
export const DropBearUtilsAPI = {
  /**
   * Initializes the DropBearUtils module.
   * @returns {Promise<void>}
   */
  initialize: async () => window.DropBearUtils.initialize(),

  /**
   * Checks whether the DropBearUtils module is initialized.
   * @returns {boolean}
   */
  isInitialized: () => window.DropBearUtils.isInitialized(),

  /**
   * Disposes the DropBearUtils module.
   */
  dispose: () => window.DropBearUtils.dispose(),

  // Utility functions from DropBearUtils:
  createLogger: (...args) => window.DropBearUtils.createLogger(...args),
  validateArgs: (...args) => window.DropBearUtils.validateArgs(...args),
  debounce: (...args) => window.DropBearUtils.debounce(...args),
  throttle: (...args) => window.DropBearUtils.throttle(...args),
  isElement: element => window.DropBearUtils.isElement(element),
  createError: (...args) => window.DropBearUtils.createError(...args),
  createEvent: (...args) => window.DropBearUtils.createEvent(...args),
  clickElementById: id => window.DropBearUtils.clickElementById(id), // New API method

  // Functions from DropBearUtilities:
  getWindowDimensions: () => window.DropBearUtils.getWindowDimensions(),
  safeWindowOperation: (...args) => window.DropBearUtils.safeWindowOperation(...args),
  isElementInViewport: (...args) => window.DropBearUtils.isElementInViewport(...args)
};

// Export modules
export {DropBearUtils, DropBearUtilities};
