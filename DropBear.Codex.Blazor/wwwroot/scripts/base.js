/**
 * @file dropbear.js
 * Core utilities and components for DropBear Blazor integration
 */

(() => {
  'use strict';

  /**
   * Core utilities namespace
   * @namespace
   */
  const DropBearUtils = {
    /**
     * Enhanced logging utility
     * @param {string} namespace - Module namespace for log prefixing
     * @returns {Object} Logger instance
     */
    createLogger(namespace) {
      const prefix = `[${namespace}]`;
      return {
        log: (message, ...args) => console.log(`${prefix} ${message}`, ...args),
        warn: (message, ...args) => console.warn(`${prefix} ${message}`, ...args),
        error: (message, ...args) => console.error(`${prefix} ${message}`, ...args),
        debug: (message, ...args) => console.debug(`${prefix} ${message}`, ...args)
      };
    },

    /**
     * Enhanced debounce with improved timing accuracy
     * @param {Function} func - Function to debounce
     * @param {number} wait - Milliseconds to wait
     * @returns {Function} Debounced function
     */
    debounce(func, wait) {
      if (typeof func !== 'function' || typeof wait !== 'number') {
        throw new TypeError('Invalid arguments: Expected (function, number)');
      }

      let timeout;
      return function executedFunction(...args) {
        const context = this;
        const later = () => {
          timeout = null;
          func.apply(context, args);
        };

        clearTimeout(timeout);
        timeout = setTimeout(later, wait);
      };
    },

    /**
     * Enhanced throttle with better timing precision
     * @param {Function} func - Function to throttle
     * @param {number} limit - Throttle limit in milliseconds
     * @returns {Function} Throttled function
     */
    throttle(func, limit) {
      if (typeof func !== 'function' || typeof limit !== 'number') {
        throw new TypeError('Invalid arguments: Expected (function, number)');
      }

      let inThrottle;
      let lastRan;
      let lastFunc;

      return function executedFunction(...args) {
        const context = this;

        if (!inThrottle) {
          func.apply(context, args);
          lastRan = Date.now();
          inThrottle = true;
        } else {
          clearTimeout(lastFunc);
          lastFunc = setTimeout(() => {
            if (Date.now() - lastRan >= limit) {
              func.apply(context, args);
              lastRan = Date.now();
            }
          }, Math.max(0, limit - (Date.now() - lastRan)));
        }
      };
    },

    /**
     * Safely query DOM element
     * @param {string} selector - DOM selector
     * @param {Element} [context=document] - Context element
     * @returns {Element|null} Found element or null
     */
    safeQuerySelector(selector, context = document) {
      try {
        return context.querySelector(selector);
      } catch (error) {
        console.error(`Invalid selector: ${selector}`, error);
        return null;
      }
    },

    /**
     * Creates a one-time event listener
     * @param {Element} element - DOM element
     * @param {string} eventName - Event name
     * @param {Function} handler - Event handler
     * @param {number} [timeout] - Optional timeout
     * @returns {Promise} Promise that resolves when event fires
     */
    createOneTimeListener(element, eventName, handler, timeout) {
      return new Promise((resolve, reject) => {
        const timeoutId = timeout && setTimeout(() => {
          element.removeEventListener(eventName, wrappedHandler);
          reject(new Error('Event listener timed out'));
        }, timeout);

        const wrappedHandler = (...args) => {
          element.removeEventListener(eventName, wrappedHandler);
          clearTimeout(timeoutId);
          try {
            const result = handler(...args);
            resolve(result);
          } catch (error) {
            reject(error);
          }
        };

        element.addEventListener(eventName, wrappedHandler, {once: true});
      });
    }
  };

  /**
   * Snackbar management module
   * @namespace
   */
  const DropBearSnackbar = (() => {
    const logger = DropBearUtils.createLogger('DropBearSnackbar');
    const snackbars = new Map();
    const ANIMATION_DURATION = 300;

    class SnackbarManager {
      constructor(id, element) {
        this.id = id;
        this.element = element;
        this.progressBar = element.querySelector('.snackbar-progress');
        this.timeout = null;
        this.isDisposed = false;
      }

      async startProgress(duration) {
        if (this.isDisposed || !this.progressBar) return;

        try {
          // Reset state
          this.progressBar.style.transition = 'none';
          this.progressBar.style.width = '100%';

          // Force reflow
          void this.progressBar.offsetWidth;

          // Start animation
          this.progressBar.style.transition = `width ${duration}ms linear`;
          this.progressBar.style.width = '0%';

          clearTimeout(this.timeout);
          this.timeout = setTimeout(() => this.hide(), duration);

          logger.debug(`Progress started: ${this.id}`);
        } catch (error) {
          logger.error(`Progress error: ${error.message}`);
          await this.hide();
        }
      }

      async hide() {
        if (this.isDisposed) return;

        try {
          clearTimeout(this.timeout);
          this.element.classList.add('snackbar-exit');

          await DropBearUtils.createOneTimeListener(
            this.element,
            'animationend',
            () => {
            },
            ANIMATION_DURATION + 100
          );

          this.dispose();
        } catch (error) {
          logger.error(`Hide error: ${error.message}`);
          this.dispose();
        }
      }

      dispose() {
        if (this.isDisposed) return;

        this.isDisposed = true;
        clearTimeout(this.timeout);
        this.element?.remove();
        snackbars.delete(this.id);
      }
    }

    return {
      async startProgress(snackbarId, duration) {
        if (!snackbarId || typeof duration !== 'number' || duration <= 0) {
          throw new Error('Invalid arguments');
        }

        try {
          const element = document.getElementById(snackbarId);
          if (!element) return;

          if (snackbars.has(snackbarId)) {
            await this.disposeSnackbar(snackbarId);
          }

          const manager = new SnackbarManager(snackbarId, element);
          snackbars.set(snackbarId, manager);
          await manager.startProgress(duration);
        } catch (error) {
          logger.error('startProgress error:', error);
        }
      },

      async hideSnackbar(snackbarId) {
        try {
          const manager = snackbars.get(snackbarId);
          if (manager) await manager.hide();
        } catch (error) {
          logger.error('hideSnackbar error:', error);
        }
      },

      async disposeSnackbar(snackbarId) {
        const manager = snackbars.get(snackbarId);
        if (manager) await manager.dispose();
      }
    };
  })();

  /**
   * File upload module
   * @namespace
   */
  const DropBearFileUploader = (() => {
    const logger = DropBearUtils.createLogger('DropBearFileUploader');

    class FileUploader {
      constructor() {
        this.boundHandlers = {
          dragover: this.handleDragOver.bind(this),
          dragleave: this.handleDragLeave.bind(this),
          drop: this.handleDrop.bind(this)
        };
      }

      initialize() {
        document.addEventListener('dragover', this.boundHandlers.dragover);
        document.addEventListener('dragleave', this.boundHandlers.dragleave);
        document.addEventListener('drop', this.boundHandlers.drop);
        logger.debug('FileUploader initialized');
      }

      handleDragOver(e) {
        const dropZone = e.target.closest('.file-upload-dropzone');
        if (!dropZone) return;

        e.preventDefault();
        e.stopPropagation();
        dropZone.classList.add('dragover');
      }

      handleDragLeave(e) {
        const dropZone = e.target.closest('.file-upload-dropzone');
        if (!dropZone) return;

        e.preventDefault();
        e.stopPropagation();
        dropZone.classList.remove('dragover');
      }

      handleDrop(e) {
        const dropZone = e.target.closest('.file-upload-dropzone');
        if (!dropZone) return;

        e.preventDefault();
        e.stopPropagation();
        dropZone.classList.remove('dragover');

        const fileInput = dropZone.querySelector('input[type="file"]');
        if (!fileInput) {
          logger.error('No file input found in dropzone');
          return;
        }

        this.transferFiles(fileInput, e.dataTransfer.files);
        fileInput.dispatchEvent(new Event('change', {bubbles: true}));
      }

      transferFiles(fileInput, files) {
        const dataTransfer = new DataTransfer();
        Array.from(files).forEach(file => {
          dataTransfer.items.add(file);
          logger.debug(`Added file: ${file.name}`);
        });
        fileInput.files = dataTransfer.files;
      }

      dispose() {
        document.removeEventListener('dragover', this.boundHandlers.dragover);
        document.removeEventListener('dragleave', this.boundHandlers.dragleave);
        document.removeEventListener('drop', this.boundHandlers.drop);
        logger.debug('FileUploader disposed');
      }
    }

    const instance = new FileUploader();

    return {
      initialize() {
        instance.initialize();
      },
      dispose() {
        instance.dispose();
      }
    };
  })();

  /**
   * Navigation buttons module
   * @namespace
   */
  const DropBearNavigationButtons = (() => {
    const logger = DropBearUtils.createLogger('DropBearNavigationButtons');
    let dotNetReference = null;
    let scrollHandler = null;

    return {
      initialize(dotNetRef) {
        if (!dotNetRef) {
          throw new Error('dotNetRef is required');
        }

        this.dispose();
        dotNetReference = dotNetRef;

        scrollHandler = DropBearUtils.throttle(() => {
          const isVisible = window.scrollY > 300;
          dotNetReference.invokeMethodAsync('UpdateVisibility', isVisible)
            .catch(error => logger.error('UpdateVisibility failed:', error));
        }, 100);

        window.addEventListener('scroll', scrollHandler);
        scrollHandler(); // Initial check
        logger.debug('Navigation buttons initialized');
      },

      scrollToTop() {
        window.scrollTo({top: 0, behavior: 'smooth'});
      },

      goBack() {
        window.history.back();
      },

      dispose() {
        if (scrollHandler) {
          window.removeEventListener('scroll', scrollHandler);
          scrollHandler = null;
        }
        dotNetReference = null;
        logger.debug('Navigation buttons disposed');
      }
    };
  })();

  /**
   * Resize manager module
   * @namespace
   */
  const DropBearResizeManager = (() => {
    const logger = DropBearUtils.createLogger('DropBearResizeManager');
    let dotNetReference = null;
    let resizeHandler = null;

    return {
      initialize(dotNetRef) {
        if (!dotNetRef) {
          throw new Error('dotNetRef is required');
        }

        this.dispose();
        dotNetReference = dotNetRef;

        resizeHandler = DropBearUtils.debounce(() =>
          dotNetReference.invokeMethodAsync('SetMaxWidthBasedOnWindowSize')
            .catch(error => logger.error('SetMaxWidthBasedOnWindowSize failed:', error)), 100);

        window.addEventListener('resize', resizeHandler);
        resizeHandler(); // Initial check
        logger.debug('Resize manager initialized');
      },

      dispose() {
        if (resizeHandler) {
          window.removeEventListener('resize', resizeHandler);
          resizeHandler = null;
        }
        dotNetReference = null;
        logger.debug('Resize manager disposed');
      }
    };
  })();

  /**
   * Context menu module
   * @namespace
   */
  const DropBearContextMenu = (() => {
    const logger = DropBearUtils.createLogger('DropBearContextMenu');
    const menuInstances = new Map();

    class ContextMenuManager {
      constructor(element, dotNetReference) {
        this.element = element;
        this.dotNetReference = dotNetReference;
        this.isDisposed = false;
        this.boundHandlers = {
          contextmenu: this.handleContextMenu.bind(this),
          click: this.handleDocumentClick.bind(this)
        };
      }

      initialize() {
        this.element.addEventListener('contextmenu', this.boundHandlers.contextmenu);
        document.addEventListener('click', this.boundHandlers.click);
      }

      handleContextMenu(e) {
        e.preventDefault();
        if (!this.isDisposed) {
          this.show(e.pageX, e.pageY);
        }
      }

      handleDocumentClick() {
        if (!this.isDisposed) {
          this.dotNetReference.invokeMethodAsync('Hide')
            .catch(error => logger.error('Hide failed:', error));
        }
      }

      async show(x, y) {
        try {
          await this.dotNetReference.invokeMethodAsync('Show', x, y);
        } catch (error) {
          logger.error('Show failed:', error);
        }
      }

      dispose() {
        if (this.isDisposed) return;

        this.isDisposed = true;
        this.element.removeEventListener('contextmenu', this.boundHandlers.contextmenu);
        document.removeEventListener('click', this.boundHandlers.click);
      }
    }

    return {
      initialize(elementId, dotNetReference) {
        if (!elementId || !dotNetReference) {
          throw new Error('Invalid arguments');
        }

        const element = document.getElementById(elementId);
        if (!element) {
          logger.error(`Element not found: ${elementId}`);
          return;
        }

        if (menuInstances.has(elementId)) {
          this.dispose(elementId);
        }

        const manager = new ContextMenuManager(element, dotNetReference);
        manager.initialize();
        menuInstances.set(elementId, manager);
        logger.debug(`Context menu initialized: ${elementId}`);
      },

      dispose(elementId) {
        const manager = menuInstances.get(elementId);
        if (manager) {
          manager.dispose();
          menuInstances.delete(elementId);
          logger.debug(`Context menu disposed: ${elementId}`);
        }
      },

      disposeAll() {
        menuInstances.forEach(manager => manager.dispose());
        menuInstances.clear();
        logger.debug('All context menus disposed');
      }
    };
  })();

  /**
   * File download utility
   * @param {string} fileName - Name of the file to download
   * @param {Blob|Uint8Array|DotNetStreamReference} content - File content
   * @param {string} contentType - MIME type of the file
   * @returns {Promise<void>}
   */
  async function downloadFileFromStream(fileName, content, contentType) {
    const logger = DropBearUtils.createLogger('FileDownload');

    try {
      let blob;

      if (content instanceof Blob) {
        blob = content;
      } else if (content.arrayBuffer) {
        const arrayBuffer = await content.arrayBuffer();
        blob = new Blob([arrayBuffer], {type: contentType});
      } else if (content instanceof Uint8Array) {
        blob = new Blob([content], {type: contentType});
      } else {
        throw new Error('Unsupported content type');
      }

      const url = URL.createObjectURL(blob);
      const anchorElement = document.createElement('a');
      anchorElement.href = url;
      anchorElement.download = fileName || 'download';
      document.body.appendChild(anchorElement);

      try {
        anchorElement.click();
      } finally {
        // Cleanup
        requestAnimationFrame(() => {
          document.body.removeChild(anchorElement);
          URL.revokeObjectURL(url);
        });
      }

      logger.debug(`File download initiated: ${fileName}`);
    } catch (error) {
      logger.error('Download failed:', error);
      throw error;
    }
  }

  /**
   * Window dimension utility
   * @returns {{width: number, height: number}}
   */
  function getWindowDimensions() {
    return {
      width: window.innerWidth,
      height: window.innerHeight
    };
  }

  /**
   * Utility to click element by ID
   * @param {string} id - Element ID
   * @returns {boolean} Success status
   */
  function clickElementById(id) {
    const logger = DropBearUtils.createLogger('ElementClick');

    try {
      const element = document.getElementById(id);
      if (!element) {
        logger.warn(`Element not found: ${id}`);
        return false;
      }

      element.click();
      logger.debug(`Clicked element: ${id}`);
      return true;
    } catch (error) {
      logger.error(`Click failed for ${id}:`, error);
      return false;
    }
  }

  // Export all modules and utilities to window object
  Object.assign(window, {
    DropBearUtils,
    DropBearSnackbar,
    DropBearFileUploader,
    DropBearNavigationButtons,
    DropBearResizeManager,
    DropBearContextMenu,
    downloadFileFromStream,
    getWindowDimensions,
    clickElementById
  });

  // Initialize modules that need immediate setup
  document.addEventListener('DOMContentLoaded', () => DropBearFileUploader.initialize());

})();

// Add support for module exports if needed
if (typeof module !== 'undefined' && module.exports) {
  module.exports = {
    DropBearUtils,
    DropBearSnackbar,
    DropBearFileUploader,
    DropBearNavigationButtons,
    DropBearResizeManager,
    DropBearContextMenu
  };
}


