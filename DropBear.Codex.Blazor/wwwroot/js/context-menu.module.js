/**
 * @fileoverview Context menu manager for handling right-click menus
 * @module context-menu
 */

import {CircuitBreaker, DOMOperationQueue, EventEmitter} from './core.module.js';
import {DropBearUtils} from './utils.module.js';
import {ModuleManager} from './module-manager.module.js';

/**
 * Creates a logger instance.
 */
const logger = DropBearUtils.createLogger('DropBearContextMenu');

/**
 * Circuit breaker to handle repeated failures.
 */
const circuitBreaker = new CircuitBreaker({failureThreshold: 3, resetTimeout: 30000});

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

    this._setupEventListeners();
    EventEmitter.emit(
      this.element,
      'created',
      DropBearUtils.createEvent(this.id, 'created', null)
    );
  }

  /**
   * Setup all event listeners needed by the context menu
   * @private
   */
  _setupEventListeners() {
    // Context menu handler
    this.handleContextMenu = this._handleContextMenu.bind(this);
    this.element.addEventListener('contextmenu', this.handleContextMenu);

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
  }

  /**
   * Internal contextmenu event handler
   * @private
   * @param {MouseEvent} e - The contextmenu event
   */
  async _handleContextMenu(e) {
    e.preventDefault();
    if (this.isDisposed) return;

    // Add global event listeners
    document.addEventListener('click', this.clickOutsideHandler);
    document.addEventListener('keydown', this.keyboardHandler);

    await this.show(e.pageX, e.pageY);
  }

  /**
   * Calculate menu position considering viewport boundaries
   * @private
   * @param {number} x - Initial X coordinate
   * @param {number} y - Initial Y coordinate
   * @returns {{x: number, y: number}} Adjusted coordinates
   */
  _calculatePosition(x, y) {
    const rect = this.element.getBoundingClientRect();
    const viewportWidth = window.innerWidth;
    const viewportHeight = window.innerHeight;

    // Ensure menu stays within viewport
    const adjustedX = Math.min(x, viewportWidth - rect.width);
    const adjustedY = Math.min(y, viewportHeight - rect.height);

    return {
      x: Math.max(0, adjustedX),
      y: Math.max(0, adjustedY)
    };
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

      const position = this._calculatePosition(x, y);
      this.lastPosition = position;

      DOMOperationQueue.add(() => {
        // Position and show menu
        this.element.style.left = `${position.x}px`;
        this.element.style.top = `${position.y}px`;
        this.element.style.visibility = 'visible';
        this.element.classList.add('show');

        // Focus first focusable element for accessibility
        const firstFocusable = this.element.querySelector(
          'button, [href], input, select, textarea, [tabindex]:not([tabindex="-1"])'
        );
        if (firstFocusable) {
          firstFocusable.focus();
        }
      });

      this.isVisible = true;
      EventEmitter.emit(
        this.element,
        'shown',
        DropBearUtils.createEvent(this.id, 'shown', {position: this.lastPosition})
      );
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
      // Remove global event listeners
      document.removeEventListener('click', this.clickOutsideHandler);
      document.removeEventListener('keydown', this.keyboardHandler);

      // Notify Blazor
      await circuitBreaker.execute(() =>
        this.dotNetRef.invokeMethodAsync('Hide')
      );

      DOMOperationQueue.add(() => {
        this.element.classList.remove('show');
        this.element.style.visibility = 'hidden';
      });

      this.isVisible = false;
      EventEmitter.emit(
        this.element,
        'hidden',
        DropBearUtils.createEvent(this.id, 'hidden', null)
      );
    } catch (error) {
      logger.error('Error hiding context menu:', error);
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

    // Remove event listeners
    this.element.removeEventListener('contextmenu', this.handleContextMenu);
    document.removeEventListener('click', this.clickOutsideHandler);
    document.removeEventListener('keydown', this.keyboardHandler);

    this.dotNetRef = null;

    EventEmitter.emit(
      this.element,
      'disposed',
      DropBearUtils.createEvent(this.id, 'disposed', null)
    );
  }
}

// Register with the DropBear ModuleManager
ModuleManager.register(
  'DropBearContextMenu',
  {
    /** @type {Map<string, ContextMenuManager>} */
    menuInstances: new Map(),

    /**
     * Initialize the context menu module
     * @returns {Promise<void>}
     */
    async initialize() {
      logger.debug('DropBearContextMenu module initialized');
    },

    /**
     * Create a new context menu instance
     * @param {string} menuId - Menu element ID
     * @param {Object} dotNetRef - .NET reference
     */
    createContextMenu(menuId, dotNetRef) {
      try {
        if (this.menuInstances.has(menuId)) {
          logger.warn(`Context menu already exists for ${menuId}, disposing old instance`);
          this.dispose(menuId);
        }

        const manager = new ContextMenuManager(menuId, dotNetRef);
        this.menuInstances.set(menuId, manager);
        logger.debug(`Context menu created for ID: ${menuId}`);
      } catch (error) {
        logger.error('Context menu creation error:', error);
        throw error;
      }
    },

    /**
     * Show context menu
     * @param {string} menuId - Menu ID
     * @param {number} x - X coordinate
     * @param {number} y - Y coordinate
     * @returns {Promise<void>}
     */
    show(menuId, x, y) {
      const manager = this.menuInstances.get(menuId);
      return manager ? manager.show(x, y) : Promise.resolve();
    },

    /**
     * Hide context menu
     * @param {string} menuId - Menu ID
     * @returns {Promise<void>}
     */
    hide(menuId) {
      const manager = this.menuInstances.get(menuId);
      return manager ? manager.hide() : Promise.resolve();
    },

    /**
     * Update menu items
     * @param {string} menuId - Menu ID
     * @param {Array} items - New menu items
     * @returns {Promise<void>}
     */
    updateItems(menuId, items) {
      const manager = this.menuInstances.get(menuId);
      return manager ? manager.updateItems(items) : Promise.resolve();
    },

    /**
     * Get menu state
     * @param {string} menuId - Menu ID
     * @returns {Object|null} Menu state
     */
    getState(menuId) {
      const manager = this.menuInstances.get(menuId);
      return manager ? manager.getState() : null;
    },

    /**
     * Dispose of a context menu
     * @param {string} menuId - Menu ID
     */
    dispose(menuId) {
      const manager = this.menuInstances.get(menuId);
      if (manager) {
        manager.dispose();
        this.menuInstances.delete(menuId);
      }
    },

    /**
     * Dispose of all context menus
     */
    disposeAll() {
      Array.from(this.menuInstances.keys()).forEach(id => this.dispose(id));
      this.menuInstances.clear();
    }
  },
  ['DropBearCore']
);

// Grab the registered module object
const dropBearContextMenuModule = ModuleManager.get('DropBearContextMenu');

/**
 * Attach dropBearContextMenuModule methods to the window for easy global use.
 */
window.DropBearContextMenu = {
  initialize: () => dropBearContextMenuModule.initialize(),
  createContextMenu: (menuId, dotNetRef) => dropBearContextMenuModule.createContextMenu(menuId, dotNetRef),
  show: (menuId, x, y) => dropBearContextMenuModule.show(menuId, x, y),
  hide: menuId => dropBearContextMenuModule.hide(menuId),
  updateItems: (menuId, items) => dropBearContextMenuModule.updateItems(menuId, items),
  getState: menuId => dropBearContextMenuModule.getState(menuId),
  dispose: menuId => dropBearContextMenuModule.dispose(menuId),
  disposeAll: () => dropBearContextMenuModule.disposeAll()
};

/**
 * Exporting the ContextMenuManager class as a named export
 * so it can also be imported directly in other modules.
 */
export {ContextMenuManager};
