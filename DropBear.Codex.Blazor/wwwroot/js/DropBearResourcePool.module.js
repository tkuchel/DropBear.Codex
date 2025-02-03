/**
 * @fileoverview Resource pool manager for efficient object reuse
 * @module resource-pool
 */

import {DropBearUtils} from './DropBearUtils.module.js';

const logger = DropBearUtils.createLogger('DropBearResourcePool');
let isInitialized = false;

/** @type {Object} Pool configuration constants */
const POOL_CONFIG = {
  DEFAULT_MAX_SIZE: 50,
  DEFAULT_INITIAL_SIZE: 10,
  MIN_POOL_SIZE: 1,
  MAX_POOL_SIZE: 1000,
  CLEANUP_INTERVAL: 60000 // Cleanup interval in ms
};

/**
 * Resource pool manager for efficient object reuse
 * @implements {IResourcePool}
 */
class ResourcePoolManager {
  constructor() {
    /** @type {Map<string, Array<any>>} Map of resource pools */
    this.pools = new Map();

    /** @type {Map<string, PoolOptions>} Map of pool configurations */
    this.poolConfigs = new Map();

    /** @type {number|null} */
    this.cleanupInterval = null;

    /** @type {boolean} */
    this.isDisposed = false;

    // Start cleanup interval
    this._startCleanup();
    logger.debug('ResourcePoolManager created');
  }

  /**
   * Start periodic cleanup of unused resources
   * @private
   */
  _startCleanup() {
    this.cleanupInterval = setInterval(() => this._cleanupUnusedResources(), POOL_CONFIG.CLEANUP_INTERVAL);
  }

  /**
   * Cleanup unused resources from all pools
   * @private
   */
  _cleanupUnusedResources() {
    if (this.isDisposed) return;

    try {
      let totalCleaned = 0;
      this.pools.forEach((pool, type) => {
        const config = this.poolConfigs.get(type);
        if (!config) return;

        // Keep only initial size if pool is larger
        if (pool.length > config.initialSize) {
          const toRemove = pool.length - config.initialSize;
          pool.length = config.initialSize;
          totalCleaned += toRemove;
        }
      });

      if (totalCleaned > 0) {
        logger.debug('Cleaned unused resources:', {totalCleaned});
      }
    } catch (error) {
      logger.error('Error during cleanup:', error);
    }
  }

  /**
   * Create a new resource pool
   * @param {string} type - Unique identifier for the pool
   * @param {() => any} factory - Factory function to create new resources
   * @param {PoolOptions} [options={}] - Pool configuration options
   * @throws {Error} If pool already exists or invalid parameters
   */
  async create(type, factory, options = {}) {
    if (this.isDisposed) {
      throw new Error('ResourcePoolManager is disposed');
    }

    try {
      await circuitBreaker.execute(async () => {
        // Parameter validation
        DropBearUtils.validateArgs([type], ['string'], 'create');
        if (typeof factory !== 'function') {
          throw new TypeError('Factory must be a function');
        }
        if (this.pools.has(type)) {
          throw new Error(`Pool "${type}" already exists`);
        }

        // Process options with defaults
        const config = {
          maxSize: Math.min(
            options.maxSize || POOL_CONFIG.DEFAULT_MAX_SIZE,
            POOL_CONFIG.MAX_POOL_SIZE
          ),
          initialSize: options.initialSize || POOL_CONFIG.DEFAULT_INITIAL_SIZE,
          validate: options.validate || null,
        };

        // Validate options
        if (config.maxSize < POOL_CONFIG.MIN_POOL_SIZE) {
          throw new Error(`maxSize must be at least ${POOL_CONFIG.MIN_POOL_SIZE}`);
        }
        if (config.initialSize < 0) {
          throw new Error('initialSize cannot be negative');
        }
        if (config.initialSize > config.maxSize) {
          throw new Error('initialSize cannot exceed maxSize');
        }
        if (config.validate && typeof config.validate !== 'function') {
          throw new TypeError('validate must be a function');
        }

        // Initialize pool with resources
        const pool = [];
        for (let i = 0; i < config.initialSize; i++) {
          const resource = await Promise.resolve(factory());
          if (config.validate && !config.validate(resource)) {
            throw new Error('Factory produced invalid resource');
          }
          pool.push(resource);
        }

        this.pools.set(type, pool);
        this.poolConfigs.set(type, config);

        EventEmitter.emit(
          this,
          'pool-created',
          DropBearUtils.createEvent(crypto.randomUUID(), 'pool-created', {
            type,
            size: pool.length,
            config
          })
        );

        logger.debug('Pool created:', {type, size: pool.length});
      });
    } catch (error) {
      logger.error(`Failed to create pool "${type}":`, error);
      throw error;
    }
  }

  /**
   * Acquire a resource from the pool
   * @template T
   * @param {string} type - Pool identifier
   * @returns {Promise<T|null>} Resource from pool or null if none available
   */
  async acquire(type) {
    if (this.isDisposed) {
      throw new Error('ResourcePoolManager is disposed');
    }

    try {
      return await circuitBreaker.execute(async () => {
        if (!this.pools.has(type)) {
          throw new Error(`Pool "${type}" does not exist`);
        }

        const pool = this.pools.get(type);
        const config = this.poolConfigs.get(type);
        if (!pool || pool.length === 0) {
          return null;
        }

        const resource = pool.pop();

        // Validate resource before returning
        if (config.validate && !config.validate(resource)) {
          logger.warn(`Invalid resource found in pool "${type}", discarding`);
          return this.acquire(type);
        }

        EventEmitter.emit(
          this,
          'resource-acquired',
          DropBearUtils.createEvent(crypto.randomUUID(), 'resource-acquired', {
            type,
            remainingSize: pool.length
          })
        );

        logger.debug('Resource acquired:', {type, remainingSize: pool.length});
        return resource;
      });
    } catch (error) {
      logger.error(`Failed to acquire resource from pool "${type}":`, error);
      throw error;
    }
  }

  /**
   * Return a resource to the pool
   * @template T
   * @param {string} type - Pool identifier
   * @param {T} resource - Resource to return to pool
   * @returns {Promise<boolean>} True if resource was added back to pool
   */
  async release(type, resource) {
    if (this.isDisposed) {
      throw new Error('ResourcePoolManager is disposed');
    }

    try {
      return await circuitBreaker.execute(async () => {
        if (!this.pools.has(type)) {
          throw new Error(`Pool "${type}" does not exist`);
        }
        if (resource === null || resource === undefined) {
          throw new Error('Cannot release null or undefined resource');
        }

        const pool = this.pools.get(type);
        const config = this.poolConfigs.get(type);

        // Validate resource
        if (config.validate && !config.validate(resource)) {
          throw new Error('Cannot release invalid resource');
        }

        // Only add to pool if under max size
        if (pool.length < config.maxSize) {
          pool.push(resource);

          EventEmitter.emit(
            this,
            'resource-released',
            DropBearUtils.createEvent(crypto.randomUUID(), 'resource-released', {
              type,
              currentSize: pool.length
            })
          );

          logger.debug('Resource released:', {type, currentSize: pool.length});
          return true;
        }

        logger.debug('Resource discarded (pool full):', {type});
        return false;
      });
    } catch (error) {
      logger.error(`Failed to release resource to pool "${type}":`, error);
      throw error;
    }
  }

  /**
   * Get the current size of a pool
   * @param {string} type - Pool identifier
   * @returns {number} Current number of available resources
   */
  getSize(type) {
    if (this.isDisposed) {
      throw new Error('ResourcePoolManager is disposed');
    }

    if (!this.pools.has(type)) {
      throw new Error(`Pool "${type}" does not exist`);
    }

    return this.pools.get(type).length;
  }

  /**
   * Get pool configuration
   * @param {string} type - Pool identifier
   * @returns {PoolOptions} Pool configuration
   */
  getPoolConfig(type) {
    if (this.isDisposed) {
      throw new Error('ResourcePoolManager is disposed');
    }

    if (!this.poolConfigs.has(type)) {
      throw new Error(`Pool "${type}" does not exist`);
    }

    return {...this.poolConfigs.get(type)};
  }

  /**
   * Clear all resources from a specific pool
   * @param {string} type - Pool identifier
   */
  async clear(type) {
    if (this.isDisposed) {
      throw new Error('ResourcePoolManager is disposed');
    }

    try {
      if (!this.pools.has(type)) {
        throw new Error(`Pool "${type}" does not exist`);
      }

      const pool = this.pools.get(type);
      const previousSize = pool.length;
      pool.length = 0;

      EventEmitter.emit(
        this,
        'pool-cleared',
        DropBearUtils.createEvent(crypto.randomUUID(), 'pool-cleared', {
          type,
          previousSize
        })
      );

      logger.debug('Pool cleared:', {type, previousSize});
    } catch (error) {
      logger.error(`Failed to clear pool "${type}":`, error);
      throw error;
    }
  }

  /**
   * Clear all pools
   */
  async clearAll() {
    if (this.isDisposed) {
      throw new Error('ResourcePoolManager is disposed');
    }

    try {
      const poolSizes = new Map();
      this.pools.forEach((pool, type) => {
        poolSizes.set(type, pool.length);
        pool.length = 0;
      });

      EventEmitter.emit(
        this,
        'all-pools-cleared',
        DropBearUtils.createEvent(crypto.randomUUID(), 'all-pools-cleared', {
          poolSizes: Object.fromEntries(poolSizes)
        })
      );

      logger.debug('All pools cleared');
    } catch (error) {
      logger.error('Failed to clear all pools:', error);
      throw error;
    }
  }

  /**
   * Delete a specific pool
   * @param {string} type - Pool identifier
   * @returns {Promise<boolean>} True if pool was deleted
   */
  async deletePool(type) {
    if (this.isDisposed) {
      throw new Error('ResourcePoolManager is disposed');
    }

    try {
      if (!this.pools.has(type)) {
        return false;
      }

      await this.clear(type);
      this.pools.delete(type);
      this.poolConfigs.delete(type);

      EventEmitter.emit(
        this,
        'pool-deleted',
        DropBearUtils.createEvent(crypto.randomUUID(), 'pool-deleted', {
          type
        })
      );

      logger.debug('Pool deleted:', {type});
      return true;
    } catch (error) {
      logger.error(`Failed to delete pool "${type}":`, error);
      throw error;
    }
  }

  /**
   * Check if a pool exists
   * @param {string} type - Pool identifier
   * @returns {boolean} True if pool exists
   */
  hasPool(type) {
    if (this.isDisposed) {
      throw new Error('ResourcePoolManager is disposed');
    }

    return this.pools.has(type);
  }

  /**
   * Get all pool statistics
   * @returns {Object} Pool statistics
   */
  getStats() {
    if (this.isDisposed) {
      throw new Error('ResourcePoolManager is disposed');
    }

    const stats = {
      totalPools: this.pools.size,
      pools: {}
    };

    this.pools.forEach((pool, type) => {
      const config = this.poolConfigs.get(type);
      stats.pools[type] = {
        currentSize: pool.length,
        maxSize: config.maxSize,
        utilization: (pool.length / config.maxSize) * 100
      };
    });

    return stats;
  }

  /**
   * Dispose the resource pool manager
   */
  dispose() {
    if (this.isDisposed) return;

    logger.debug('Disposing ResourcePoolManager');
    this.isDisposed = true;

    clearInterval(this.cleanupInterval);
    this.clearAll();
    this.pools.clear();
    this.poolConfigs.clear();

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
window.DropBearResourcePool = {
  __initialized: false,
  instance: null,

  initialize: async () => {
    if (isInitialized) {
      return;
    }

    try {
      logger.debug('Resource pool module initializing');

      // Initialize dependencies
      await window.DropBearUtils.initialize();

      window.DropBearResourcePool.instance = new ResourcePoolManager();

      isInitialized = true;
      window.DropBearResourcePool.__initialized = true;

      logger.debug('Resource pool module initialized');
    } catch (error) {
      logger.error('Resource pool initialization failed:', error);
      throw error;
    }
  },

  createPool: async (type, factory, options = {}) => {
    if (!isInitialized) {
      throw new Error('Module not initialized');
    }
    await window.DropBearResourcePool.instance.create(type, factory, options);
  },

  acquire: async type => {
    if (!isInitialized) {
      throw new Error('Module not initialized');
    }
    return window.DropBearResourcePool.instance.acquire(type);
  },

  release: async (type, resource) => {
    if (!isInitialized) {
      throw new Error('Module not initialized');
    }
    return window.DropBearResourcePool.instance.release(type, resource);
  },

  getSize: type => {
    if (!isInitialized) {
      throw new Error('Module not initialized');
    }
    return window.DropBearResourcePool.instance.getSize(type);
  },

  hasPool: type => {
    if (!isInitialized) {
      throw new Error('Module not initialized');
    }
    return window.DropBearResourcePool.instance.hasPool(type);
  },

  getPoolConfig: type => {
    if (!isInitialized) {
      throw new Error('Module not initialized');
    }
    return window.DropBearResourcePool.instance.getPoolConfig(type);
  },

  clear: async type => {
    if (!isInitialized) {
      throw new Error('Module not initialized');
    }
    await window.DropBearResourcePool.instance.clear(type);
  },

  clearAll: async () => {
    if (!isInitialized) {
      throw new Error('Module not initialized');
    }
    await window.DropBearResourcePool.instance.clearAll();
  },

  deletePool: async type => {
    if (!isInitialized) {
      throw new Error('Module not initialized');
    }
    return window.DropBearResourcePool.instance.deletePool(type);
  },

  isInitialized: () => isInitialized,

  dispose: () => {
    if (window.DropBearResourcePool.instance) {
      window.DropBearResourcePool.instance.dispose();
      window.DropBearResourcePool.instance = null;
    }
    isInitialized = false;
    window.DropBearResourcePool.__initialized = false;
    logger.debug('Resource pool module disposed');
  }
};

// Register with ModuleManager after window attachment
window.DropBearModuleManager.register(
  'DropBearResourcePool',
  {
    initialize: () => window.DropBearResourcePool.initialize(),
    isInitialized: () => window.DropBearResourcePool.isInitialized(),
    dispose: () => window.DropBearResourcePool.dispose()
  },
  ['DropBearUtils']
);

// Export ResourcePoolManager class
export {ResourcePoolManager};
