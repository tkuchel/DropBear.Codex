﻿/**
 * @fileoverview Context menu manager for handling right-click menus
 * @module context-menu
 */

import {CircuitBreaker, DOMOperationQueue, EventEmitter} from './DropBearCore.module.js';
import {DropBearUtils} from './DropBearUtils.module.js';

const logger = DropBearUtils.createLogger('DropBearContextMenu');
const circuitBreaker = new CircuitBreaker({failureThreshold: 3, resetTimeout: 30000});
let isInitialized = false;
const moduleName = 'DropBearContextMenu';

/**
 * Manager for context menu behavior and positioning.
 * @implements {IContextMenuManager}
 */
class ContextMenuManager {
  /**
   * Constructor for the ContextMenuManager
   * @param {string} id - The ID of the menu element
   * @param {Object} dotNetRef - The .NET reference for Blazor interop
   * @throws {TypeError} If arguments are not the correct types
   */
  constructor(id, dotNetRef) {
    DropBearUtils.validateArgs([id, dotNetRef], ['string', 'object'], 'ContextMenuManager');

    /** @type {string} */
    this.id = id;

    /** @type {HTMLElement|null} */
    this.element = document.getElementById(id);

    /** @type {Object|null} */
    this.dotNetRef = dotNetRef;

    /** @type {boolean} */
    this.isDisposed = false;

    /** @type {Function|null} */
    this.clickOutsideHandler = null;

    /** @type {Function|null} */
    this.keyboardHandler = null;

    /** @type {{x: number, y: number}} */
    this.lastPosition = {x: 0, y: 0};

    /** @type {boolean} */
    this.isVisible = false;

    if (!DropBearUtils.isElement(this.element)) {
      throw new TypeError('Invalid element provided to ContextMenuManager');
    }

    // Initialize element styles
    DOMOperationQueue.add(() => {
      const menuContainer = document.getElementById(this.id);
      if (menuContainer) {
        menuContainer.style.position = 'fixed';
        menuContainer.style.visibility = 'hidden';
        menuContainer.style.display = 'none';
      }
    });

    this._setupEventListeners();

    EventEmitter.emit(
      this.element,
      'created',
      DropBearUtils.createEvent(this.id, 'created', null)
    );

    logger.debug(`ContextMenuManager created for ID: ${id}`);
  }

  /**
   * Setup all event listeners needed by the context menu
   * @private
   */
  _setupEventListeners() {
    // Context menu handler
    this.handleContextMenu = this._handleContextMenu.bind(this);
    const triggerElement = document.querySelector(`[id='${this.id}']`).previousElementSibling;
    triggerElement.addEventListener('contextmenu', this.handleContextMenu);

    // Click outside handler
    this.clickOutsideHandler = event => {
      if (!this.element.contains(event.target)) {
        this.hide();
      }
    };

    // Keyboard handler for accessibility
    this.keyboardHandler = event => {
      if (event.key === 'Escape') {
        this.hide();
      }
    };

    // Window resize handler
    this.resizeHandler = DropBearUtils.throttle(() => {
      if (this.isVisible) {
        const position = this._getOptimalPosition(this.lastPosition.x, this.lastPosition.y);
        this._updatePosition(position);
      }
    }, 100);

    window.addEventListener('resize', this.resizeHandler);

    logger.debug('Event listeners initialized');
  }

  /**
   * Internal contextmenu event handler
   * @private
   * @param {MouseEvent} e - The contextmenu event
   */
  async _handleContextMenu(e) {
    e.preventDefault();
    if (this.isDisposed) return;

    document.addEventListener('click', this.clickOutsideHandler);
    document.addEventListener('keydown', this.keyboardHandler);

    await this.show(e.clientX, e.clientY);
  }

  /**
   * Check if menu would be clipped at position
   * @private
   * @param {number} x - X coordinate
   * @param {number} y - Y coordinate
   * @returns {boolean} True if menu would be clipped
   */
  _isMenuClipped(x, y) {
    const rect = this.element.getBoundingClientRect();
    const viewportWidth = window.innerWidth;
    const viewportHeight = window.innerHeight;

    return x + rect.width > viewportWidth ||
      y + rect.height > viewportHeight ||
      x < 0 ||
      y < 0;
  }

  /**
   * Get optimal position for menu
   * @private
   * @param {number} x - Initial X coordinate
   * @param {number} y - Initial Y coordinate
   * @returns {{x: number, y: number}} Optimal position
   */
  _getOptimalPosition(x, y) {
    const rect = this.element.getBoundingClientRect();
    const viewportWidth = window.innerWidth;
    const viewportHeight = window.innerHeight;
    const margin = 10;

    let optimalX = x;
    let optimalY = y;

    // If menu would extend beyond right edge, align to right
    if (x + rect.width > viewportWidth) {
      optimalX = viewportWidth - rect.width - margin;
    }

    // If menu would extend beyond bottom edge, show above click
    if (y + rect.height > viewportHeight) {
      optimalY = viewportHeight - rect.height - margin;
    }

    // Ensure minimum margins
    optimalX = Math.max(margin, optimalX);
    optimalY = Math.max(margin, optimalY);

    return {x: optimalX, y: optimalY};
  }

  /**
   * Update menu position
   * @private
   * @param {{x: number, y: number}} position - New position
   */
  _updatePosition(position) {
    DOMOperationQueue.add(() => {
      this.element.style.left = `${position.x}px`;
      this.element.style.top = `${position.y}px`;
    });
    this.lastPosition = position;
  }

  /**
   * Show the context menu at the specified position
   * @param {number} x - X coordinate
   * @param {number} y - Y coordinate
   * @returns {Promise<void>}
   */
  async show(x, y) {
    if (this.isDisposed) return;

    try {
      // Notify Blazor before showing menu
      await circuitBreaker.execute(() =>
        this.dotNetRef.invokeMethodAsync('Show', x, y)
      );

      // Show menu container first
      DOMOperationQueue.add(() => {
        this.element.style.visibility = 'visible';
        this.element.style.display = 'block';
      });

      const position = this._getOptimalPosition(x, y);
      this.lastPosition = position;

      DOMOperationQueue.add(() => {
        const menuElement = this.element.querySelector('.context-menu');
        if (menuElement) {
          menuElement.style.left = `${position.x}px`;
          menuElement.style.top = `${position.y}px`;
          menuElement.classList.add('active');
        }
      });

      this.isVisible = true;
      EventEmitter.emit(
        this.element,
        'shown',
        DropBearUtils.createEvent(this.id, 'shown', {position: this.lastPosition})
      );

      logger.debug(`Context menu shown at position: ${x}, ${y}`);
    } catch (error) {
      logger.error('Error showing context menu:', error);
      throw error;
    }
  }

  /**
   * Hide the context menu
   * @returns {Promise<void>}
   */
  async hide() {
    if (this.isDisposed) return;


    try {
      DOMOperationQueue.add(() => {
        const menuElement = this.element.querySelector('.context-menu');
        if (menuElement) {
          menuElement.classList.remove('active');
        }
        this.element.style.visibility = 'hidden';
        this.element.style.display = 'none';
      });

      this.isVisible = false;
      EventEmitter.emit(
        this.element,
        'hidden',
        DropBearUtils.createEvent(this.id, 'hidden', null)
      );

      logger.debug('Context menu hidden');
    } catch (error) {
      logger.error('Error hiding context menu:', error);
      throw error;
    }
  }

  /**
   * Update menu items
   * @param {Array} items - New menu items
   * @returns {Promise<void>}
   */
  async updateItems(items) {
    if (this.isDisposed) return;

    if (!Array.isArray(items)) {
      throw new TypeError('Items must be an array');
    }

    try {
      await circuitBreaker.execute(() =>
        this.dotNetRef.invokeMethodAsync('UpdateItems', items)
      );

      EventEmitter.emit(
        this.element,
        'items-updated',
        DropBearUtils.createEvent(this.id, 'items-updated', {itemCount: items.length})
      );

      logger.debug(`Menu items updated, count: ${items.length}`);
    } catch (error) {
      logger.error('Error updating menu items:', error);
      throw error;
    }
  }

  /**
   * Get the current state of the context menu
   * @returns {{isVisible: boolean, position: {x: number, y: number}}}
   */
  getState() {
    return {
      isVisible: this.isVisible,
      position: {...this.lastPosition}
    };
  }

  /**
   * Dispose of the context menu manager
   */
  dispose() {
    if (this.isDisposed) return;

    logger.debug(`Disposing context menu ${this.id}`);
    this.isDisposed = true;

    this.element.removeEventListener('contextmenu', this.handleContextMenu);
    document.removeEventListener('click', this.clickOutsideHandler);
    document.removeEventListener('keydown', this.keyboardHandler);
    window.removeEventListener('resize', this.resizeHandler);

    this.dotNetRef = null;

    EventEmitter.emit(
      this.element,
      'disposed',
      DropBearUtils.createEvent(this.id, 'disposed', null)
    );
  }
}

// Attach to window first
window[moduleName] = {
  __initialized: false,
  menuInstances: new Map(),

  initialize: async () => {
    if (isInitialized) {
      return;
    }

    try {
      logger.debug('Context menu module initializing');

      // Initialize dependencies first
      await window.DropBearUtils.initialize();
      await window.DropBearCore.initialize();

      isInitialized = true;
      window[moduleName].__initialized = true;

      logger.debug('Context menu module initialized');
    } catch (error) {
      logger.error('Context menu initialization failed:', error);
      throw error;
    }
  },

  createContextMenu: (menuId, dotNetRef) => {
    try {
      if (!isInitialized) {
        throw new Error('Module not initialized');
      }

      if (window[moduleName].menuInstances.has(menuId)) {
        logger.warn(`Context menu already exists for ${menuId}, disposing old instance`);
        window[moduleName].dispose(menuId);
      }

      const manager = new ContextMenuManager(menuId, dotNetRef);
      window[moduleName].menuInstances.set(menuId, manager);
      logger.debug(`Context menu created for ID: ${menuId}`);
    } catch (error) {
      logger.error('Context menu creation error:', error);
      throw error;
    }
  },

  show: (menuId, x, y) => {
    const manager = window[moduleName].menuInstances.get(menuId);
    return manager ? manager.show(x, y) : Promise.resolve();
  },

  hide: menuId => {
    const manager = window[moduleName].menuInstances.get(menuId);
    return manager ? manager.hide() : Promise.resolve();
  },

  updateItems: (menuId, items) => {
    const manager = window[moduleName].menuInstances.get(menuId);
    return manager ? manager.updateItems(items) : Promise.resolve();
  },

  getState: menuId => {
    const manager = window[moduleName].menuInstances.get(menuId);
    return manager ? manager.getState() : null;
  },

  isInitialized: () => isInitialized,

  dispose: menuId => {
    const manager = window[moduleName].menuInstances.get(menuId);
    if (manager) {
      manager.dispose();
      window[moduleName].menuInstances.delete(menuId);
      logger.debug(`Context menu disposed for ID: ${menuId}`);
    }
  },

  disposeAll: () => {
    Array.from(window[moduleName].menuInstances.keys()).forEach(id =>
      window[moduleName].dispose(id)
    );
    window[moduleName].menuInstances.clear();
    isInitialized = false;
    window[moduleName].__initialized = false;
    logger.debug('All context menus disposed');
  }
};

// Register with ModuleManager after window attachment
window.DropBearModuleManager.register(
  moduleName,
  {
    initialize: () => window[moduleName].initialize(),
    isInitialized: () => window[moduleName].isInitialized(),
    dispose: () => window[moduleName].disposeAll()
  },
  ['DropBearUtils', 'DropBearCore']
);

// Export the API functions under a unique namespace
export const DropBearContextMenuAPI = {
  /**
   * Initialize the context menu module.
   * @returns {Promise<void>}
   */
  initialize: async () => window[moduleName].initialize(),

  /**
   * Create a new context menu instance.
   * @param {string} menuId - The ID of the menu element.
   * @param {Object} dotNetRef - The .NET reference for Blazor interop.
   * @returns {Promise<void>}
   */
  createContextMenu: async (menuId, dotNetRef) =>
    window[moduleName].createContextMenu(menuId, dotNetRef),

  /**
   * Show the context menu at the specified coordinates.
   * @param {string} menuId - The ID of the menu element.
   * @param {number} x - The X coordinate.
   * @param {number} y - The Y coordinate.
   * @returns {Promise<void>}
   */
  show: async (menuId, x, y) => window[moduleName].show(menuId, x, y),

  /**
   * Hide the context menu.
   * @param {string} menuId - The ID of the menu element.
   * @returns {Promise<void>}
   */
  hide: async menuId => window[moduleName].hide(menuId),

  /**
   * Update the menu items.
   * @param {string} menuId - The ID of the menu element.
   * @param {Array} items - An array of new menu items.
   * @returns {Promise<void>}
   */
  updateItems: async (menuId, items) => window[moduleName].updateItems(menuId, items),

  /**
   * Get the current state of the context menu.
   * @param {string} menuId - The ID of the menu element.
   * @returns {object|null} The current state or null if not available.
   */
  getState: menuId => window[moduleName].getState(menuId),

  /**
   * Check if the module is initialized.
   * @returns {boolean}
   */
  isInitialized: () => window[moduleName].isInitialized(),

  /**
   * Dispose a specific context menu instance.
   * @param {string} menuId - The ID of the menu element.
   * @returns {Promise<void>}
   */
  dispose: async menuId => window[moduleName].dispose(menuId),

  /**
   * Dispose all context menu instances.
   * @returns {Promise<void>}
   */
  disposeAll: async () => window[moduleName].disposeAll()
};

// Also export the ContextMenuManager class if needed
export {ContextMenuManager};

