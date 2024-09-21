/**
 * Utility function for debouncing events.
 * Creates a debounced function that delays invoking the provided function until after the specified wait time.
 * @param {Function} func - The function to debounce.
 * @param {number} wait - The number of milliseconds to delay.
 * @returns {Function} A new debounced function.
 */
function debounce(func, wait) {
  if (typeof func !== 'function') {
    console.error('debounce: First argument must be a function');
    throw new TypeError('First argument must be a function');
  }
  if (typeof wait !== 'number') {
    console.error('debounce: Second argument must be a number');
    throw new TypeError('Second argument must be a number');
  }
  let timeout;
  return function (...args) {
    clearTimeout(timeout);
    timeout = setTimeout(() => {
      try {
        func.apply(this, args);
      } catch (error) {
        console.error('debounce: Error executing function:', error);
      }
    }, wait);
  };
}

/**
 * Utility function for throttling events.
 * Creates a throttled function that only invokes the provided function at most once per every limit milliseconds.
 * @param {Function} func - The function to throttle.
 * @param {number} limit - The number of milliseconds to throttle invocations to.
 * @returns {Function} A new throttled function.
 */
function throttle(func, limit) {
  if (typeof func !== 'function') {
    console.error('throttle: First argument must be a function');
    throw new TypeError('First argument must be a function');
  }
  if (typeof limit !== 'number') {
    console.error('throttle: Second argument must be a number');
    throw new TypeError('Second argument must be a number');
  }
  let inThrottle;
  return function (...args) {
    if (!inThrottle) {
      try {
        func.apply(this, args);
      } catch (error) {
        console.error('throttle: Error executing function:', error);
      }
      inThrottle = true;
      setTimeout(() => inThrottle = false, limit);
    }
  };
}

/**
 * DropBearSnackbar module (v1.0.0)
 * Provides functionality to manage snackbars with progress bars.
 */
window.DropBearSnackbar = (function () {
  const snackbars = new Map();

  /**
   * Logs a message to the console with a specific log type.
   * @param {string} message - The message to log.
   * @param {string} [type='log'] - The console method to use ('log', 'warn', 'error', etc.).
   */
  function log(message, type = 'log') {
    console[type](`[DropBearSnackbar] ${message}`);
  }

  /**
   * Retrieves the snackbar DOM element by its ID.
   * @param {string} snackbarId - The ID of the snackbar element.
   * @returns {HTMLElement|null} The snackbar element or null if not found.
   */
  function getSnackbarElement(snackbarId) {
    const snackbar = document.getElementById(snackbarId);
    if (!snackbar) {
      log(`Snackbar ${snackbarId} not found in DOM`, 'warn');
    }
    return snackbar;
  }

  /**
   * Retrieves the progress bar element within a snackbar.
   * @param {HTMLElement} snackbar - The snackbar element.
   * @returns {HTMLElement|null} The progress bar element or null if not found.
   */
  function getProgressBarElement(snackbar) {
    if (!snackbar) {
      log('Snackbar element is null or undefined', 'error');
      return null;
    }
    const progressBar = snackbar.querySelector('.snackbar-progress');
    if (!progressBar) {
      log('Progress bar not found', 'error');
    }
    return progressBar;
  }

  /**
   * Animates the progress bar of a snackbar.
   * @param {HTMLElement} progressBar - The progress bar element.
   * @param {number} duration - The duration of the animation in milliseconds.
   */
  function animateProgressBar(progressBar, duration) {
    if (!progressBar) {
      log('animateProgressBar: progressBar is null or undefined', 'error');
      return;
    }
    if (typeof duration !== 'number' || duration <= 0) {
      log('animateProgressBar: duration must be a positive number', 'error');
      return;
    }
    progressBar.style.transition = 'none';
    progressBar.style.width = '100%';
    progressBar.style.backgroundColor = getComputedStyle(progressBar).getPropertyValue('color');

    setTimeout(() => {
      progressBar.style.transition = `width ${duration}ms linear`;
      progressBar.style.width = '0%';
    }, 10);
  }

  /**
   * Removes a snackbar element from the DOM and the active snackbars map.
   * @param {string} snackbarId - The ID of the snackbar to remove.
   */
  function removeSnackbar(snackbarId) {
    log(`Attempting to remove snackbar ${snackbarId}`);
    const snackbar = getSnackbarElement(snackbarId);
    if (snackbar) {
      snackbar.addEventListener(
        'animationend',
        () => {
          snackbar.remove();
          snackbars.delete(snackbarId);
          log(`Snackbar ${snackbarId} removed from DOM and active snackbars`);
        },
        {once: true}
      );
      snackbar.style.animation = 'slideOutDown 0.3s ease-out forwards';
    } else {
      snackbars.delete(snackbarId);
    }
  }

  return {
    /**
     * Starts the progress animation for a snackbar.
     * @param {string} snackbarId - The ID of the snackbar.
     * @param {number} duration - The duration of the progress animation in milliseconds.
     */
    startProgress(snackbarId, duration) {
      if (typeof snackbarId !== 'string' || snackbarId.trim() === '') {
        log('startProgress: snackbarId must be a non-empty string', 'error');
        return;
      }
      if (typeof duration !== 'number' || duration <= 0) {
        log('startProgress: duration must be a positive number', 'error');
        return;
      }
      log(`Starting progress for snackbar ${snackbarId} with duration ${duration}`);

      const snackbar = getSnackbarElement(snackbarId);
      if (!snackbar) {
        log(`Retrying to find snackbar ${snackbarId} in 50ms`);
        setTimeout(() => this.startProgress(snackbarId, duration), 50);
        return;
      }

      const progressBar = getProgressBarElement(snackbar);
      if (!progressBar) return;

      animateProgressBar(progressBar, duration);

      if (snackbars.has(snackbarId)) {
        clearTimeout(snackbars.get(snackbarId));
      }

      snackbars.set(
        snackbarId,
        setTimeout(() => this.hideSnackbar(snackbarId), duration)
      );
      log(`Snackbar ${snackbarId} added to active snackbars`);
    },

    /**
     * Hides the snackbar by removing it from the DOM and the active snackbars map.
     * @param {string} snackbarId - The ID of the snackbar to hide.
     */
    hideSnackbar(snackbarId) {
      if (typeof snackbarId !== 'string' || snackbarId.trim() === '') {
        log('hideSnackbar: snackbarId must be a non-empty string', 'error');
        return;
      }
      log(`Attempting to hide snackbar ${snackbarId}`);
      if (snackbars.has(snackbarId)) {
        clearTimeout(snackbars.get(snackbarId));
        removeSnackbar(snackbarId);
      } else {
        log(`Snackbar ${snackbarId} not found in active snackbars`, 'warn');
      }
    },

    /**
     * Disposes of a snackbar by hiding it.
     * @param {string} snackbarId - The ID of the snackbar to dispose.
     */
    disposeSnackbar(snackbarId) {
      if (typeof snackbarId !== 'string' || snackbarId.trim() === '') {
        log('disposeSnackbar: snackbarId must be a non-empty string', 'error');
        return;
      }
      log(`Disposing snackbar ${snackbarId}`);
      this.hideSnackbar(snackbarId);
    },

    /**
     * Checks if a snackbar is currently active.
     * @param {string} snackbarId - The ID of the snackbar to check.
     * @returns {boolean} True if the snackbar is active, false otherwise.
     */
    isSnackbarActive(snackbarId) {
      if (typeof snackbarId !== 'string' || snackbarId.trim() === '') {
        log('isSnackbarActive: snackbarId must be a non-empty string', 'error');
        return false;
      }
      const isActive = snackbars.has(snackbarId);
      log(`Checking if snackbar ${snackbarId} is active: ${isActive}`);
      return isActive;
    }
  };
})();

/**
 * DropBearFileUploader module
 * Handles file drag-and-drop functionality for elements with the class 'file-upload-dropzone'.
 */
window.DropBearFileUploader = (function () {
  let droppedFiles = [];

  /**
   * Converts a file to an ArrayBuffer and returns it as a Uint8Array.
   * @param {File} file - The file to convert.
   * @returns {Promise<Uint8Array>} A promise that resolves to the file's content as a Uint8Array.
   */
  async function readFileAsArrayBuffer(file) {
    return new Promise((resolve, reject) => {
      const reader = new FileReader();
      reader.onload = e => {
        const arrayBuffer = e.target.result;
        resolve(new Uint8Array(arrayBuffer));
      };
      reader.onerror = reject;
      reader.readAsArrayBuffer(file);
    });
  }

  /**
   * Handles the 'drop' event, capturing dropped files and reading their contents.
   * @param {DragEvent} e - The drop event.
   */
  async function handleDrop(e) {
    e.preventDefault();
    e.stopPropagation();

    droppedFiles = [];

    try {
      if (e.dataTransfer.items) {
        for (let i = 0; i < e.dataTransfer.items.length; i++) {
          if (e.dataTransfer.items[i].kind === 'file') {
            const file = e.dataTransfer.items[i].getAsFile();
            const fileData = await readFileAsArrayBuffer(file);
            droppedFiles.push({
              name: file.name,
              size: file.size,
              type: file.type,
              fileData: Array.from(fileData) // Send as array of bytes
            });
          }
        }
      } else {
        for (let i = 0; i < e.dataTransfer.files.length; i++) {
          const file = e.dataTransfer.files[i];
          const fileData = await readFileAsArrayBuffer(file);
          droppedFiles.push({
            name: file.name,
            size: file.size,
            type: file.type,
            fileData: Array.from(fileData)
          });
        }
      }
    } catch (error) {
      console.error('Error handling dropped files:', error);
    }
  }

  /**
   * Initializes the module by adding event listeners for 'drop' and 'dragover' events.
   */
  function init() {
    document.addEventListener('drop', function (e) {
      if (e.target.closest('.file-upload-dropzone')) {
        handleDrop(e);
      }
    });

    document.addEventListener('dragover', function (e) {
      if (e.target.closest('.file-upload-dropzone')) {
        e.preventDefault();
        e.stopPropagation();
      }
    });
  }

  // Initialize when the DOM is ready
  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', init);
  } else {
    init();
  }

  return {
    /**
     * Retrieves the dropped files and clears the internal storage.
     * @returns {Array<Object>} An array of dropped file information.
     */
    getDroppedFiles() {
      const files = droppedFiles;
      droppedFiles = [];
      return files;
    },

    /**
     * Clears the internal storage of dropped files.
     */
    clearDroppedFiles() {
      droppedFiles = [];
    }
  };
})();

/**
 * Utility function for file download.
 * Downloads a file from a content stream or byte array.
 * @param {string} fileName - The name of the file to be downloaded.
 * @param {Uint8Array|DotNetStreamReference|string} content - The content of the file.
 * @param {string} contentType - The MIME type of the file.
 */
window.downloadFileFromStream = async (fileName, content, contentType) => {
  try {
    let blob;

    if (content instanceof Blob) {
      // content is already a Blob
      blob = content;
    } else if (content.arrayBuffer) {
      // content is a DotNetStreamReference
      const arrayBuffer = await content.arrayBuffer();
      blob = new Blob([arrayBuffer], {type: contentType});
    } else if (typeof content === 'string') {
      // content is a Base64 string
      const response = await fetch(`data:${contentType};base64,${content}`);
      blob = await response.blob();
    } else if (content instanceof Uint8Array) {
      // content is a byte array (Uint8Array)
      blob = new Blob([content], {type: contentType});
    } else {
      console.error('downloadFileFromStream: Unsupported content type');
      return;
    }

    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.style.display = 'none';
    a.href = url;
    a.download = fileName ?? '';
    document.body.appendChild(a);
    a.click();
    window.URL.revokeObjectURL(url);
    document.body.removeChild(a);
  } catch (error) {
    console.error('Error in downloadFileFromStream:', error);
  }
};


/**
 * DropBearContextMenu module
 * Manages context menu interactions with Blazor components.
 */
window.DropBearContextMenu = (function () {
  /**
   * Represents a context menu for a specific DOM element.
   */
  class ContextMenu {
    /**
     * Creates a new ContextMenu instance.
     * @param {HTMLElement} element - The DOM element to attach the context menu to.
     * @param {DotNetObjectReference} dotNetReference - The .NET object reference for invoking methods.
     */
    constructor(element, dotNetReference) {
      if (!element || !(element instanceof HTMLElement)) {
        console.error('ContextMenu constructor: element must be a valid HTMLElement');
        throw new TypeError('element must be a valid HTMLElement');
      }
      if (!dotNetReference) {
        console.error('ContextMenu constructor: dotNetReference must not be null or undefined');
        throw new TypeError('dotNetReference must not be null or undefined');
      }
      this.element = element;
      this.dotNetReference = dotNetReference;
      this.isDisposed = false;
      this.initialize();
      console.log(`ContextMenu initialized for element: ${element.id}`);
    }

    /**
     * Initializes the context menu by adding event listeners.
     */
    initialize() {
      this.handleContextMenu = this.handleContextMenu.bind(this);
      this.handleDocumentClick = this.handleDocumentClick.bind(this);

      this.element.addEventListener('contextmenu', this.handleContextMenu);
      document.addEventListener('click', this.handleDocumentClick);
      console.log('Event listeners added');
    }

    /**
     * Handles the 'contextmenu' event on the element.
     * @param {MouseEvent} e - The context menu event.
     */
    handleContextMenu(e) {
      e.preventDefault();
      const x = e.pageX;
      const y = e.pageY;
      console.log(`Context menu triggered at X: ${x}, Y: ${y} (absolute to document)`);
      this.show(x, y);
    }

    /**
     * Handles the 'click' event on the document to hide the context menu.
     */
    handleDocumentClick() {
      if (this.isDisposed) {
        console.log('Context menu instance already disposed, skipping hide invocation.');
        return;
      }

      console.log('Document clicked, hiding context menu');
      this.dotNetReference
        .invokeMethodAsync('Hide')
        .catch(error => {
          if (error.message.includes('There is no tracked object with id')) {
            console.warn(
              'DotNetObjectReference was disposed before hide could be invoked, ignoring error.'
            );
          } else {
            console.error('Error invoking Hide method:', error);
          }
        });
    }

    /**
     * Invokes the 'Show' method on the .NET object reference.
     * @param {number} x - The x-coordinate where the context menu should appear.
     * @param {number} y - The y-coordinate where the context menu should appear.
     */
    show(x, y) {
      console.log(`Showing context menu at X: ${x}, Y: ${y}`);
      this.dotNetReference
        .invokeMethodAsync('Show', x, y)
        .catch(error => console.error('Error invoking Show method:', error));
    }

    /**
     * Disposes of the context menu instance by removing event listeners.
     */
    dispose() {
      this.element.removeEventListener('contextmenu', this.handleContextMenu);
      document.removeEventListener('click', this.handleDocumentClick);
      this.isDisposed = true;
      console.log(`ContextMenu disposed for element: ${this.element.id}`);
    }
  }

  const menuInstances = new Map();

  return {
    /**
     * Initializes a context menu for a specific element.
     * @param {string} elementId - The ID of the DOM element.
     * @param {DotNetObjectReference} dotNetReference - The .NET object reference.
     */
    initialize(elementId, dotNetReference) {
      if (typeof elementId !== 'string' || elementId.trim() === '') {
        console.error('initialize: elementId must be a non-empty string');
        return;
      }
      if (!dotNetReference) {
        console.error('initialize: dotNetReference must not be null or undefined');
        return;
      }
      console.log(`Initializing ContextMenu for element: ${elementId}`);
      const element = document.getElementById(elementId);
      if (!element) {
        console.error(`Element with id '${elementId}' not found.`);
        return;
      }

      if (menuInstances.has(elementId)) {
        console.warn(
          `Context menu for element '${elementId}' already initialized. Disposing old instance.`
        );
        this.dispose(elementId);
      }

      try {
        const menuInstance = new ContextMenu(element, dotNetReference);
        menuInstances.set(elementId, menuInstance);
        console.log(`ContextMenu instance created for element: ${elementId}`);
      } catch (error) {
        console.error(`Error initializing ContextMenu for element '${elementId}':`, error);
      }
    },

    /**
     * Shows the context menu for a specific element at given coordinates.
     * @param {string} elementId - The ID of the element.
     * @param {number} x - The x-coordinate.
     * @param {number} y - The y-coordinate.
     */
    show(elementId, x, y) {
      if (typeof elementId !== 'string' || elementId.trim() === '') {
        console.error('show: elementId must be a non-empty string');
        return;
      }
      if (typeof x !== 'number' || typeof y !== 'number') {
        console.error('show: x and y must be numbers');
        return;
      }
      console.log(`Attempting to show context menu for element: ${elementId}`);
      const menuInstance = menuInstances.get(elementId);
      if (menuInstance) {
        menuInstance.show(x, y);
      } else {
        console.error(`No context menu instance found for element '${elementId}'.`);
      }
    },

    /**
     * Disposes of the context menu for a specific element.
     * @param {string} elementId - The ID of the element.
     */
    dispose(elementId) {
      if (typeof elementId !== 'string' || elementId.trim() === '') {
        console.error('dispose: elementId must be a non-empty string');
        return;
      }
      console.log(`Disposing ContextMenu for element: ${elementId}`);
      const menuInstance = menuInstances.get(elementId);
      if (menuInstance) {
        menuInstance.dispose();
        menuInstances.delete(elementId);
        console.log(`ContextMenu instance removed for element: ${elementId}`);
      } else {
        console.warn(`No ContextMenu instance found to dispose for element: ${elementId}`);
      }
    },

    /**
     * Disposes of all context menu instances.
     */
    disposeAll() {
      console.log('Disposing all ContextMenu instances');
      menuInstances.forEach((instance, elementId) => this.dispose(elementId));
      menuInstances.clear();
      console.log('All ContextMenu instances disposed');
    }
  };
})();

/**
 * DropBearNavigationButtons module
 * Manages navigation buttons like 'scroll to top' and 'go back'.
 */
window.DropBearNavigationButtons = (function () {
  let dotNetReference = null;
  let throttledHandleScroll;

  /**
   * Handles the 'scroll' event to update visibility.
   */
  function handleScroll() {
    if (!dotNetReference) {
      console.error('handleScroll: dotNetReference is null');
      return;
    }
    const isVisible = window.scrollY > 300;
    dotNetReference
      .invokeMethodAsync('UpdateVisibility', isVisible)
      .catch(error => console.error('Error invoking UpdateVisibility method:', error));
  }

  return {
    /**
     * Initializes the navigation buttons with a .NET object reference.
     * @param {DotNetObjectReference} dotNetRef - The .NET object reference.
     */
    initialize(dotNetRef) {
      if (!dotNetRef) {
        console.error('initialize: dotNetRef must not be null or undefined');
        return;
      }
      if (dotNetReference) {
        console.warn('DropBearNavigationButtons already initialized. Disposing previous instance.');
        this.dispose();
      }

      dotNetReference = dotNetRef;
      throttledHandleScroll = throttle(handleScroll, 100);
      window.addEventListener('scroll', throttledHandleScroll);
      console.log('DropBearNavigationButtons initialized');

      // Trigger initial check
      handleScroll();
    },

    /**
     * Scrolls the window to the top.
     */
    scrollToTop() {
      window.scrollTo({
        top: 0,
        behavior: 'smooth'
      });
    },

    /**
     * Navigates back in browser history.
     */
    goBack() {
      window.history.back();
    },

    /**
     * Disposes of the navigation buttons by removing event listeners.
     */
    dispose() {
      if (dotNetReference) {
        window.removeEventListener('scroll', throttledHandleScroll);
        dotNetReference = null;
        console.log('DropBearNavigationButtons disposed');
      }
    }
  };
})();

/**
 * DropBearResizeManager module (v1.0.0)
 * Manages window resize events to adjust component sizing.
 */
window.DropBearResizeManager = (function () {
  let dotNetReference = null;
  let debouncedHandleResize;

  /**
   * Handles the 'resize' event to invoke the .NET method.
   */
  function handleResize() {
    if (dotNetReference) {
      dotNetReference
        .invokeMethodAsync('SetMaxWidthBasedOnWindowSize')
        .catch(error =>
          console.error('Error invoking SetMaxWidthBasedOnWindowSize method:', error)
        );
    }
  }

  return {
    /**
     * Initializes the resize manager with a .NET object reference.
     * @param {DotNetObjectReference} dotNetRef - The .NET object reference.
     */
    initialize(dotNetRef) {
      if (!dotNetRef) {
        console.error('initialize: dotNetRef must not be null or undefined');
        return;
      }
      if (dotNetReference) {
        console.warn('DropBearResizeManager already initialized. Disposing previous instance.');
        this.dispose();
      }

      dotNetReference = dotNetRef;
      debouncedHandleResize = debounce(handleResize, 100);
      window.addEventListener('resize', debouncedHandleResize);
      console.log('DropBearResizeManager initialized');

      // Trigger an initial call to SetMaxWidthBasedOnWindowSize to apply the size on load
      handleResize();
    },

    /**
     * Disposes of the resize manager by removing event listeners.
     */
    dispose() {
      if (dotNetReference) {
        window.removeEventListener('resize', debouncedHandleResize);
        dotNetReference = null;
        console.log('DropBearResizeManager disposed');
      }
    }
  };
})();

/**
 * DropBearThemeManager module (v1.0.1)
 * Manages theme preferences and applies color schemes.
 */
window.DropBearThemeManager = (function () {
  let dotNetReference = null;
  const STORAGE_KEY = 'dropbear-theme-preference';
  let mediaQuery = null;

  /**
   * Logs a message to the console with a specific log type.
   * @param {string} message - The message to log.
   * @param {string} [type='log'] - The console method to use.
   */
  function log(message, type = 'log') {
    console[type](`[DropBearThemeManager] ${message}`);
  }

  /**
   * Sets the color scheme and stores the preference.
   * @param {string} scheme - The color scheme ('auto', 'light', 'dark').
   */
  function setColorScheme(scheme) {
    try {
      document.documentElement.style.setProperty('--color-scheme', scheme);
      localStorage.setItem(STORAGE_KEY, scheme);
      log(`Color scheme set to: ${scheme}`);
    } catch (error) {
      log(`Error setting color scheme: ${error.message}`, 'error');
    }
  }

  /**
   * Retrieves the stored color scheme preference.
   * @returns {string} The stored color scheme.
   */
  function getColorScheme() {
    return localStorage.getItem(STORAGE_KEY) || 'auto';
  }

  /**
   * Applies the specified color scheme.
   * @param {string} scheme - The color scheme to apply.
   */
  function applyColorScheme(scheme) {
    log(`Attempting to apply color scheme: ${scheme}`);

    if (!['auto', 'light', 'dark'].includes(scheme)) {
      log(`Invalid scheme provided: ${scheme}. Falling back to 'auto'.`, 'warn');
      scheme = 'auto';
    }

    let effectiveScheme = scheme;
    if (scheme === 'auto') {
      const prefersDark = window.matchMedia('(prefers-color-scheme: dark)').matches;
      effectiveScheme = prefersDark ? 'dark' : 'light';
      log(`Auto theme detected. System prefers ${effectiveScheme} mode.`);
    }

    try {
      document.documentElement.style.setProperty('--color-scheme', effectiveScheme);
      localStorage.setItem(STORAGE_KEY, scheme); // Store the user's preference, not the effective scheme
      log(`Color scheme applied: ${effectiveScheme} (user preference: ${scheme})`);

      // Update body class for potential CSS hooks
      document.body.classList.remove('theme-light', 'theme-dark');
      document.body.classList.add(`theme-${effectiveScheme}`);

      // Dispatch a custom event for other parts of the application
      window.dispatchEvent(
        new CustomEvent('themeChanged', {
          detail: {scheme: effectiveScheme, preference: scheme}
        })
      );

      if (dotNetReference) {
        dotNetReference
          .invokeMethodAsync('OnThemeChanged', effectiveScheme, scheme)
          .then(() => log('Blazor component notified of theme change'))
          .catch(error => log(`Error invoking OnThemeChanged: ${error.message}`, 'error'));
      } else {
        log('No Blazor reference available to notify of theme change', 'warn');
      }
    } catch (error) {
      log(`Error applying color scheme: ${error.message}`, 'error');
    }
  }

  // Use the existing debounce utility
  const debouncedApplyColorScheme = debounce(applyColorScheme, 50);

  /**
   * Handles system theme changes when in 'auto' mode.
   * @param {MediaQueryListEvent} event - The media query event.
   */
  function handleSystemThemeChange(event) {
    if (getColorScheme() === 'auto') {
      log('System theme change detected');
      debouncedApplyColorScheme('auto');
    }
  }

  return {
    /**
     * Initializes the theme manager with a .NET object reference.
     * @param {DotNetObjectReference} dotNetRef - The .NET object reference.
     */
    initialize(dotNetRef) {
      if (dotNetReference) {
        log('DropBearThemeManager already initialized. Disposing previous instance.', 'warn');
        this.dispose();
      }

      dotNetReference = dotNetRef;
      const storedScheme = getColorScheme();
      setColorScheme(storedScheme);

      mediaQuery = window.matchMedia('(prefers-color-scheme: dark)');
      mediaQuery.addEventListener('change', handleSystemThemeChange);

      if (storedScheme === 'auto') {
        handleSystemThemeChange({matches: mediaQuery.matches});
      }

      log('DropBearThemeManager initialized');
    },

    /**
     * Toggles the theme between 'dark', 'light', and 'auto'.
     */
    toggleTheme() {
      const currentScheme = getColorScheme();
      const newScheme =
        currentScheme === 'dark' ? 'light' : currentScheme === 'light' ? 'auto' : 'dark';
      applyColorScheme(newScheme);
    },

    /**
     * Sets the theme to a specific scheme.
     * @param {string} scheme - The color scheme to set ('auto', 'light', 'dark').
     */
    setTheme(scheme) {
      if (['auto', 'light', 'dark'].includes(scheme)) {
        applyColorScheme(scheme);
      } else {
        log(`Invalid color scheme: ${scheme}`, 'error');
      }
    },

    /**
     * Gets the current theme preference.
     * @returns {string} The current theme preference.
     */
    getCurrentTheme() {
      return getColorScheme();
    },

    /**
     * Disposes of the theme manager by removing event listeners.
     */
    dispose() {
      if (mediaQuery) {
        mediaQuery.removeEventListener('change', handleSystemThemeChange);
        mediaQuery = null;
      }
      if (dotNetReference) {
        dotNetReference = null;
        log('DropBearThemeManager disposed');
      }
    }
  };
})();

/**
 * Utility function for getting the window dimensions.
 * @returns {{width: number, height: number}} An object containing the width and height of the window.
 */
window.getWindowDimensions = function () {
  return {
    width: window.innerWidth,
    height: window.innerHeight
  };
};
