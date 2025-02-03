/**
 * @fileoverview Module manager for handling dependencies and initialization of DropBear modules
 * @module module-manager
 */

import {DropBearUtils} from './utils.module.js';

const logger = DropBearUtils.createLogger('DropBearModuleManager');
let isInitialized = false;

/**
 * Module manager for handling module registration, dependencies, and initialization
 * @implements {IModuleManager}
 */
const ModuleManager = {
  /** @type {Map<string, Module>} Map of registered modules */
  modules: new Map(),

  /** @type {Map<string, string[]>} Map of module dependencies */
  dependencies: new Map(),

  /** @type {Set<string>} Set of initialized modules */
  initialized: new Set(),

  /** @type {Map<string, Promise<void>>} Map of ongoing initialization promises */
  initializationPromises: new Map(),

  /**
   * Register a new module with optional dependencies
   * @param {string} name - Module name
   * @param {Module} module - Module implementation
   * @param {string[]} [dependencies=[]] - Array of dependency module names
   * @throws {Error} If module name is invalid or already registered
   */
  register(name, module, dependencies = []) {
    try {
      // Validate inputs
      DropBearUtils.validateArgs([name], ['string'], 'register');
      if (!module || typeof module !== 'object') {
        throw new TypeError('Module must be an object');
      }
      if (!Array.isArray(dependencies)) {
        throw new TypeError('Dependencies must be an array');
      }

      if (this.modules.has(name)) {
        throw new Error(`Module "${name}" is already registered`);
      }

      // Validate each dependency
      dependencies.forEach(dep => {
        if (typeof dep !== 'string' || !dep.trim()) {
          throw new TypeError('Dependency names must be non-empty strings');
        }
      });

      // Set up module state
      const moduleState = {
        ...module,
        __initialized: false,
        __initializing: false
      };

      this.dependencies.set(name, dependencies);
      this.modules.set(name, moduleState);

      // Set up window reference
      if (typeof window !== 'undefined' && !window[name]) {
        window[name] = {__initialized: false};
      }

      logger.debug(`Module "${name}" registered successfully`);
    } catch (error) {
      logger.error(`Failed to register module "${name}":`, error);
      throw error;
    }
  },

  /**
   * Initialize a module and its dependencies
   * @param {string} moduleName - Name of module to initialize
   * @returns {Promise<void>}
   * @throws {Error} If module not found or initialization fails
   */
  async initialize(moduleName) {
    try {
      DropBearUtils.validateArgs([moduleName], ['string'], 'initialize');

      // Return existing promise if initialization is in progress
      if (this.initializationPromises.has(moduleName)) {
        return this.initializationPromises.get(moduleName);
      }

      // Return immediately if already initialized
      if (this.initialized.has(moduleName)) {
        return Promise.resolve();
      }

      const module = this.modules.get(moduleName);
      if (!module) {
        throw new Error(`Module "${moduleName}" not found`);
      }

      // Create initialization promise
      const initPromise = (async () => {
        try {
          // Initialize dependencies first
          const deps = this.dependencies.get(moduleName) || [];
          await Promise.all(deps.map(dep => this.initialize(dep)));

          // Initialize the module
          if (typeof module.initialize === 'function') {
            module.__initializing = true;
            await module.initialize();
          }

          // Mark as initialized
          module.__initialized = true;
          module.__initializing = false;
          this.initialized.add(moduleName);

          // Update window reference
          if (typeof window !== 'undefined' && window[moduleName]) {
            window[moduleName].__initialized = true;
          }

          logger.debug(`Module "${moduleName}" initialized successfully`);
        } catch (error) {
          module.__initializing = false;
          logger.error(`Failed to initialize module "${moduleName}":`, error);
          throw error;
        } finally {
          this.initializationPromises.delete(moduleName);
        }
      })();

      this.initializationPromises.set(moduleName, initPromise);
      return initPromise;
    } catch (error) {
      logger.error(`Failed to initialize module "${moduleName}":`, error);
      throw error;
    }
  },

  /**
   * Wait for module dependencies to be initialized
   * @param {string[]} dependencies - Array of dependency names
   * @returns {Promise<void>}
   */
  async waitForDependencies(dependencies) {
    try {
      if (!Array.isArray(dependencies)) {
        throw new TypeError('Dependencies must be an array');
      }

      await Promise.all(dependencies.map(dep => this.initialize(dep)));
      logger.debug('Dependencies initialized successfully');
    } catch (error) {
      logger.error('Failed to initialize dependencies:', error);
      throw error;
    }
  },

  /**
   * Retrieve a registered module by name
   * @param {string} moduleName - Name of module
   * @returns {Module|undefined} The module if found
   */
  get(moduleName) {
    DropBearUtils.validateArgs([moduleName], ['string'], 'get');
    return this.modules.get(moduleName);
  },

  /**
   * Check if a module is initialized
   * @param {string} moduleName - Name of module
   * @returns {boolean} True if the module is initialized
   */
  isInitialized(moduleName) {
    DropBearUtils.validateArgs([moduleName], ['string'], 'isInitialized');
    const module = this.modules.get(moduleName);
    return module ? module.__initialized : false;
  },

  /**
   * Check if a module is currently initializing
   * @param {string} moduleName - Name of module
   * @returns {boolean} True if the module is initializing
   */
  isInitializing(moduleName) {
    DropBearUtils.validateArgs([moduleName], ['string'], 'isInitializing');
    const module = this.modules.get(moduleName);
    return module ? module.__initializing : false;
  },

  /**
   * Dispose of a module
   * @param {string} moduleName - Name of module
   * @returns {boolean} True if the module was disposed
   */
  dispose(moduleName) {
    try {
      DropBearUtils.validateArgs([moduleName], ['string'], 'dispose');

      const module = this.modules.get(moduleName);
      if (!module) {
        return false;
      }

      // Check if any other modules depend on this one
      for (const [name, deps] of this.dependencies) {
        if (deps.includes(moduleName) && this.initialized.has(name)) {
          throw new Error(`Cannot dispose module "${moduleName}" while module "${name}" depends on it`);
        }
      }

      try {
        if (typeof module.dispose === 'function') {
          module.dispose();
        }

        this.modules.delete(moduleName);
        this.dependencies.delete(moduleName);
        this.initialized.delete(moduleName);
        this.initializationPromises.delete(moduleName);

        // Update window reference
        if (typeof window !== 'undefined' && window[moduleName]) {
          window[moduleName].__initialized = false;
        }

        logger.debug(`Module "${moduleName}" disposed successfully`);
        return true;
      } catch (error) {
        logger.error(`Error disposing module "${moduleName}":`, error);
        throw error;
      }
    } catch (error) {
      logger.error(`Failed to dispose module "${moduleName}":`, error);
      return false;
    }
  },

  /**
   * Clear all modules and reset state
   */
  clear() {
    try {
      // Dispose modules in reverse dependency order
      const disposedModules = new Set();
      const disposeModule = name => {
        if (disposedModules.has(name)) return;

        // First dispose any modules that depend on this one
        for (const [depName, deps] of this.dependencies) {
          if (deps.includes(name)) {
            disposeModule(depName);
          }
        }

        this.dispose(name);
        disposedModules.add(name);
      };

      Array.from(this.modules.keys()).forEach(disposeModule);

      this.modules.clear();
      this.dependencies.clear();
      this.initialized.clear();
      this.initializationPromises.clear();

      logger.debug('ModuleManager cleared successfully');
    } catch (error) {
      logger.error('Failed to clear ModuleManager:', error);
      throw error;
    }
  }
};


// Attach to window
window.DropBearModuleManager = {
  __initialized: false,
  initialize: () => ModuleManager.initialize('DropBearModuleManager'),
  register: ModuleManager.register.bind(ModuleManager),
  waitForDependencies: ModuleManager.waitForDependencies.bind(ModuleManager),
  get: ModuleManager.get.bind(ModuleManager),
  isInitialized: ModuleManager.isInitialized.bind(ModuleManager),
  isInitializing: ModuleManager.isInitializing.bind(ModuleManager),
  dispose: ModuleManager.dispose.bind(ModuleManager),
  clear: ModuleManager.clear.bind(ModuleManager)
};

// Register Utils module after ModuleManager is available
ModuleManager.register('DropBearUtils', {
  initialize: () => window.DropBearUtils.initialize(),
  isInitialized: () => window.DropBearUtils.isInitialized(),
  dispose: () => window.DropBearUtils.dispose()
}, []);

// Register ModuleManager itself
ModuleManager.register('DropBearModuleManager', {
  async initialize() {
    if (isInitialized) return;

    try {
      logger.debug('ModuleManager initializing');
      await window.DropBearUtils.initialize();

      isInitialized = true;
      window.DropBearModuleManager.__initialized = true;

      logger.debug('ModuleManager initialized');
    } catch (error) {
      logger.error('ModuleManager initialization failed:', error);
      throw error;
    }
  },
  isInitialized: () => isInitialized,
  dispose: () => {
    isInitialized = false;
    window.DropBearModuleManager.__initialized = false;
  }
}, []);

// Export ModuleManager
export { ModuleManager };
