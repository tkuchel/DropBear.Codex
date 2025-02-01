/**
 * @fileoverview Utility functions for the DropBear framework
 * @module utils
 */

/**
 * @typedef {Object} ILogger
 * @property {Function} debug - Debug level logging
 * @property {Function} info - Info level logging
 * @property {Function} warn - Warning level logging
 * @property {Function} error - Error level logging
 */

/**
 * @typedef {Object} IDropBearError
 * @property {string} message - Error message
 * @property {string} code - Error code
 * @property {string} [component] - Component name
 * @property {*} [details] - Additional error details
 */

/**
 * @typedef {Object} IDropBearEvent
 * @property {string} id - Event identifier
 * @property {string} type - Event type
 * @property {*} [data] - Event data
 */

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
      debug: (message, ...args) => {
        console.debug(`${prefix} ${message}`, ...args);
      },
      info: (message, ...args) => {
        console.log(`${prefix} ${message}`, ...args);
      },
      warn: (message, ...args) => {
        console.warn(`${prefix} ${message}`, ...args);
      },
      error: (message, ...args) => {
        console.error(`${prefix} ${message}`, ...args);
      },
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

    return { id, type, data };
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
      return { width: 0, height: 0 };
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
      return (
        rect.top >= 0 &&
        rect.left >= 0 &&
        rect.bottom <= window.innerHeight &&
        rect.right <= window.innerWidth
      );
    } catch (error) {
      console.error('Error checking element visibility:', error);
      return false;
    }
  },
};

/**
 * Export them together at the top level so they are valid ES module exports.
 */
export { DropBearUtils, DropBearUtilities };
