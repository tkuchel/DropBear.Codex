/**
 * @fileoverview Module manager for handling dependencies and initialization of DropBear modules
 * @module module-manager
 */

/**
 * @typedef {Object} Module
 * @property {Function} [initialize] - Async initialization function
 * @property {Function} [dispose] - Cleanup function
 */

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

  /**
   * Register a new module with optional dependencies.
   * @param {string} name - Module name
   * @param {Module} module - Module implementation
   * @param {string[]} [dependencies=[]] - Array of dependency module names
   * @throws {Error} If module name is invalid or already registered
   */
  register(name, module, dependencies = []) {
    if (typeof name !== 'string' || !name.trim()) {
      throw new TypeError('Module name must be a non-empty string');
    }
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
    dependencies.forEach((dep) => {
      if (typeof dep !== 'string' || !dep.trim()) {
        throw new TypeError('Dependency names must be non-empty strings');
      }
    });

    this.dependencies.set(name, dependencies);
    this.modules.set(name, module);
  },

  /**
   * Initialize a module and its dependencies.
   * @param {string} moduleName - Name of module to initialize
   * @returns {Promise<void>}
   * @throws {Error} If module not found or initialization fails
   */
  async initialize(moduleName) {
    if (typeof moduleName !== 'string' || !moduleName.trim()) {
      throw new TypeError('Module name must be a non-empty string');
    }

    // If already initialized, do nothing
    if (this.initialized.has(moduleName)) {
      return;
    }

    // Check that the module exists
    if (!this.modules.has(moduleName)) {
      throw new Error(`Module "${moduleName}" not found`);
    }

    try {
      // Initialize dependencies first
      const deps = this.dependencies.get(moduleName) || [];
      await Promise.all(deps.map((dep) => this.initialize(dep)));

      // Initialize the module if it has an initialize method
      const module = this.modules.get(moduleName);
      if (typeof module.initialize === 'function') {
        await module.initialize();
      }

      this.initialized.add(moduleName);
    } catch (error) {
      throw new Error(`Failed to initialize module "${moduleName}": ${error.message}`);
    }
  },

  /**
   * Retrieve a registered module by name.
   * @param {string} moduleName - Name of module
   * @returns {Module|undefined} - The module if found
   */
  get(moduleName) {
    if (typeof moduleName !== 'string' || !moduleName.trim()) {
      throw new TypeError('Module name must be a non-empty string');
    }
    return this.modules.get(moduleName);
  },

  /**
   * Check if a module is initialized.
   * @param {string} moduleName - Name of module
   * @returns {boolean} True if the module is initialized
   */
  isInitialized(moduleName) {
    if (typeof moduleName !== 'string' || !moduleName.trim()) {
      throw new TypeError('Module name must be a non-empty string');
    }
    return this.initialized.has(moduleName);
  },

  /**
   * Dispose of a module.
   * @param {string} moduleName - Name of module
   * @returns {boolean} True if the module was disposed
   */
  dispose(moduleName) {
    if (typeof moduleName !== 'string' || !moduleName.trim()) {
      throw new TypeError('Module name must be a non-empty string');
    }

    const module = this.modules.get(moduleName);
    if (!module) {
      return false;
    }

    try {
      if (typeof module.dispose === 'function') {
        module.dispose();
      }
      this.modules.delete(moduleName);
      this.dependencies.delete(moduleName);
      this.initialized.delete(moduleName);
      return true;
    } catch (error) {
      console.error(`Error disposing module "${moduleName}":`, error);
      return false;
    }
  },

  /**
   * Clear all modules and reset state.
   */
  clear() {
    // Dispose modules in reverse registration order (or reverse dependency order if you prefer).
    Array.from(this.modules.keys())
      .reverse()
      .forEach((name) => this.dispose(name));

    this.modules.clear();
    this.dependencies.clear();
    this.initialized.clear();
  },
};

/**
 * Export the ModuleManager at the top level so it’s recognized as an ES module.
 */
export { ModuleManager };
