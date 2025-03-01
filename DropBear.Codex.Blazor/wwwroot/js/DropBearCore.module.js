﻿/**
 * @fileoverview Core module containing fundamental utilities and classes for the DropBear framework
 * @module core
 */

import {DropBearUtils} from './DropBearUtils.module.js';

const logger = DropBearUtils.createLogger('DropBearCore');
let isInitialized = false;

/**
 * Queue for batching DOM operations to reduce reflows and improve performance
 * @implements {IDOMOperationQueue}
 */
const DOMOperationQueue = {
  /** @type {Set<Function>} */
  queue: new Set(),

  /** @type {boolean} */
  scheduled: false,

  /**
   * Add an operation to the queue
   * @param {Function} operation - The DOM operation to queue
   */
  add(operation) {
    if (typeof operation !== 'function') {
      throw new TypeError('Operation must be a function');
    }

    this.queue.add(operation);

    if (!this.scheduled) {
      this.scheduled = true;
      requestAnimationFrame(() => this.flush());
    }
  },

  /**
   * Execute all queued operations and clear the queue
   */
  flush() {
    this.queue.forEach(operation => {
      try {
        operation();
      } catch (error) {
        logger.error('Error in queued operation:', error);
      }
    });

    this.queue.clear();
    this.scheduled = false;
  }
};

/**
 * Enhanced event emitter using WeakMap for automatic cleanup
 * @implements {IEventEmitter}
 */
const EventEmitter = {
  /** @type {WeakMap<object, Map<string, Set<Function>>>} */
  events: new WeakMap(),

  /**
   * Register an event handler
   * @param {object} target - Target object to attach the event to
   * @param {string} event - Event name
   * @param {Function} callback - Event handler function
   * @returns {Function} Cleanup function to remove the handler
   */
  on(target, event, callback) {
    if (!target || typeof target !== 'object') {
      throw new TypeError('Target must be an object');
    }
    if (typeof event !== 'string' || !event.trim()) {
      throw new TypeError('Event name must be a string');
    }
    if (typeof callback !== 'function') {
      throw new TypeError('Callback must be a function');
    }

    if (!this.events.has(target)) {
      this.events.set(target, new Map());
    }
    const targetEvents = this.events.get(target);
    if (!targetEvents.has(event)) {
      targetEvents.set(event, new Set());
    }
    targetEvents.get(event).add(callback);

    return () => this.off(target, event, callback);
  },

  /**
   * Remove an event handler
   * @param {object} target - Target object
   * @param {string} event - Event name
   * @param {Function} callback - Handler to remove
   */
  off(target, event, callback) {
    const targetEvents = this.events.get(target);
    if (targetEvents?.has(event)) {
      targetEvents.get(event).delete(callback);
    }
  },

  /**
   * Emit an event with data
   * @param {object} target - Target object
   * @param {string} event - Event name
   * @param {*} data - Event data
   */
  emit(target, event, data) {
    const targetEvents = this.events.get(target);
    if (targetEvents?.has(event)) {
      targetEvents.get(event).forEach(callback => {
        try {
          callback(data);
        } catch (error) {
          logger.error(`Error in event handler for ${event}:`, error);
        }
      });
    }
  }
};

/**
 * Circuit breaker for handling failures in operations
 * @implements {ICircuitBreaker}
 */
class CircuitBreaker {
  /**
   * @param {Object} options - Circuit breaker options
   * @param {number} [options.failureThreshold=5] - Number of failures before opening
   * @param {number} [options.resetTimeout=60000] - Time in ms before attempting reset
   */
  constructor(options = {}) {
    this.failureThreshold = options.failureThreshold || 5;
    this.resetTimeout = options.resetTimeout || 60000;
    this.failures = 0;
    this.lastFailureTime = null;
    this.state = 'closed';

    if (this.failureThreshold < 1) {
      throw new Error('Failure threshold must be at least 1');
    }
    if (this.resetTimeout < 1000) {
      throw new Error('Reset timeout must be at least 1000ms');
    }
  }

  /**
   * Execute an operation with circuit breaker protection
   * @param {Function} operation - Async operation to execute
   * @returns {Promise<*>} Operation result
   * @throws {Error} If circuit is open or operation fails
   */
  async execute(operation) {
    if (typeof operation !== 'function') {
      throw new TypeError('Operation must be a function');
    }

    if (this.state === 'open') {
      const timeSinceFailure = Date.now() - this.lastFailureTime;
      if (timeSinceFailure >= this.resetTimeout) {
        this.state = 'half-open';
      } else {
        throw new Error('Circuit breaker is open');
      }
    }

    try {
      const result = await operation();
      if (this.state === 'half-open') {
        this.reset();
      }
      return result;
    } catch (error) {
      this.recordFailure();
      throw error;
    }
  }

  /**
   * Record a failure and potentially open the circuit
   */
  recordFailure() {
    this.failures++;
    this.lastFailureTime = Date.now();
    if (this.failures >= this.failureThreshold) {
      this.state = 'open';
    }
  }

  /**
   * Reset the circuit breaker state
   */
  reset() {
    this.failures = 0;
    this.lastFailureTime = null;
    this.state = 'closed';
  }

  /**
   * Get the current state of the circuit breaker
   * @returns {'open' | 'closed' | 'half-open'} Current state
   */
  getState() {
    return this.state;
  }
}

// Attach to window first
window.DropBearCore = {
  __initialized: false,
  DOMOperationQueue,
  EventEmitter,
  CircuitBreaker,
  initialize: async () => {
    if (isInitialized) return;

    try {
      logger.debug('Core module initializing');
      // Ensure Utils is initialized first
      await window.DropBearUtils.initialize();

      isInitialized = true;
      window.DropBearCore.__initialized = true;

      logger.debug('Core module initialized');
    } catch (error) {
      logger.error('Core initialization failed:', error);
      throw error;
    }
  },
  isInitialized: () => isInitialized,
  dispose: () => {
    isInitialized = false;
    window.DropBearCore.__initialized = false;
  }
};

// Register with ModuleManager after window attachment
window.DropBearModuleManager.register(
  'DropBearCore',
  {
    initialize: () => window.DropBearCore.initialize(),
    isInitialized: () => window.DropBearCore.isInitialized(),
    dispose: () => window.DropBearCore.dispose()
  },
  ['DropBearUtils']
);

// Export everything
export {DOMOperationQueue, EventEmitter, CircuitBreaker};
