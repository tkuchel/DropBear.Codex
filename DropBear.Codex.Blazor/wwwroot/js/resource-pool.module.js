/**
 * @fileoverview Resource pool manager for efficient object reuse
 * @module resource-pool
 */

/**
 * @typedef {Object} PoolOptions
 * @property {number} [maxSize=50] - Maximum size of the pool
 * @property {number} [initialSize=10] - Initial size of the pool
 * @property {Function} [validate] - Optional validation function for resources
 */

/**
 * Resource pool manager for efficient object reuse
 * @implements {IResourcePool}
 */
const ResourcePool = {
  /** @type {Map<string, Array<any>>} Map of resource pools */
  pools: new Map(),

  /** @type {Map<string, PoolOptions>} Map of pool configurations */
  poolConfigs: new Map(),

  /**
   * Create a new resource pool.
   * @param {string} type - Unique identifier for the pool
   * @param {() => any} factory - Factory function to create new resources
   * @param {PoolOptions} [options={}] - Pool configuration options
   * @throws {Error} If pool already exists or invalid parameters
   */
  create(type, factory, options = {}) {
    // Parameter validation
    if (typeof type !== 'string' || !type.trim()) {
      throw new TypeError('Pool type must be a non-empty string');
    }
    if (typeof factory !== 'function') {
      throw new TypeError('Factory must be a function');
    }
    if (this.pools.has(type)) {
      throw new Error(`Pool "${type}" already exists`);
    }

    // Process options with defaults
    const config = {
      maxSize: options.maxSize || 50,
      initialSize: options.initialSize || 10,
      validate: options.validate || null,
    };

    // Validate options
    if (config.maxSize < 1) {
      throw new Error('maxSize must be at least 1');
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

    try {
      // Initialize pool with resources
      const pool = [];
      for (let i = 0; i < config.initialSize; i++) {
        const resource = factory();
        if (config.validate && !config.validate(resource)) {
          throw new Error('Factory produced invalid resource');
        }
        pool.push(resource);
      }

      this.pools.set(type, pool);
      this.poolConfigs.set(type, config);
    } catch (error) {
      throw new Error(`Failed to create pool "${type}": ${error.message}`);
    }
  },

  /**
   * Acquire a resource from the pool.
   * @template T
   * @param {string} type - Pool identifier
   * @returns {T|null} Resource from pool or null if none available
   * @throws {Error} If pool doesn't exist
   */
  acquire(type) {
    if (!this.pools.has(type)) {
      throw new Error(`Pool "${type}" does not exist`);
    }

    const pool = this.pools.get(type);
    const config = this.poolConfigs.get(type);
    if (!pool || pool.length === 0) {
      return null;
    }

    const resource = pool.pop();

    // Validate resource before returning if validation function exists
    if (config.validate && !config.validate(resource)) {
      console.warn(`Invalid resource found in pool "${type}", discarding`);
      return this.acquire(type); // Recursively try to get valid resource
    }

    return resource;
  },

  /**
   * Return a resource to the pool.
   * @template T
   * @param {string} type - Pool identifier
   * @param {T} resource - Resource to return to pool
   * @returns {boolean} True if resource was added back to pool
   * @throws {Error} If pool doesn't exist
   */
  release(type, resource) {
    if (!this.pools.has(type)) {
      throw new Error(`Pool "${type}" does not exist`);
    }
    if (resource === null || resource === undefined) {
      throw new Error('Cannot release null or undefined resource');
    }

    const pool = this.pools.get(type);
    const config = this.poolConfigs.get(type);

    // Validate resource if validation function exists
    if (config.validate && !config.validate(resource)) {
      throw new Error('Cannot release invalid resource');
    }

    // Only add to pool if under max size
    if (pool.length < config.maxSize) {
      pool.push(resource);
      return true;
    }
    return false;
  },

  /**
   * Get the current size of a pool.
   * @param {string} type - Pool identifier
   * @returns {number} Current number of available resources
   * @throws {Error} If pool doesn't exist
   */
  getSize(type) {
    if (!this.pools.has(type)) {
      throw new Error(`Pool "${type}" does not exist`);
    }
    return this.pools.get(type).length;
  },

  /**
   * Clear all resources from a specific pool.
   * @param {string} type - Pool identifier
   * @throws {Error} If pool doesn't exist
   */
  clear(type) {
    if (!this.pools.has(type)) {
      throw new Error(`Pool "${type}" does not exist`);
    }
    this.pools.get(type).length = 0;
  },

  /**
   * Clear all pools.
   */
  clearAll() {
    this.pools.forEach(pool => {
      pool.length = 0;
    });
  },

  /**
   * Delete a specific pool.
   * @param {string} type - Pool identifier
   * @returns {boolean} True if pool was deleted
   */
  deletePool(type) {
    if (!this.pools.has(type)) {
      return false;
    }
    this.clear(type);
    this.pools.delete(type);
    this.poolConfigs.delete(type);
    return true;
  },

  /**
   * Check if a pool exists.
   * @param {string} type - Pool identifier
   * @returns {boolean} True if pool exists
   */
  hasPool(type) {
    return this.pools.has(type);
  },
};

/**
 * Export the ResourcePool at the top level to ensure valid ES module syntax.
 */
export {ResourcePool};
