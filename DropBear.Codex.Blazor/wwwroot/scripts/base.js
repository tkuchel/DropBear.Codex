/**
 * Creates a debounced function that delays invoking the provided function until after the specified wait time.
 * @param {Function} func - The function to debounce.
 * @param {number} wait - The number of milliseconds to delay.
 * @returns {Function} A new debounced function.
 */
function debounce(func, wait) {
  if (typeof func !== 'function') {
    throw new TypeError('First argument must be a function');
  }
  if (typeof wait !== 'number') {
    throw new TypeError('Second argument must be a number');
  }
  let timeout;
  return function (...args) {
    clearTimeout(timeout);
    timeout = setTimeout(() => func.apply(this, args), wait);
  };
}

/**
 * Creates a throttled function that only invokes the provided function at most once per every limit milliseconds.
 * @param {Function} func - The function to throttle.
 * @param {number} limit - The number of milliseconds to throttle invocations to.
 * @returns {Function} A new throttled function.
 */
function throttle(func, limit) {
  if (typeof func !== 'function') {
    throw new TypeError('First argument must be a function');
  }
  if (typeof limit !== 'number') {
    throw new TypeError('Second argument must be a number');
  }
  let lastFunc;
  let lastRan;
  return function (...args) {
    if (!lastRan) {
      func.apply(this, args);
      lastRan = Date.now();
    } else {
      clearTimeout(lastFunc);
      lastFunc = setTimeout(() => {
        if (Date.now() - lastRan >= limit) {
          func.apply(this, args);
          lastRan = Date.now();
        }
      }, limit - (Date.now() - lastRan));
    }
  };
}

/**
 * DropBearSnackbar module
 * Provides functionality to manage snackbars with progress bars.
 */
window.DropBearSnackbar = (() => {
  const snackbars = new Map();

  /**
   * Logs a message to the console with a specific log level.
   * @param {string} message - The message to log.
   * @param {'log' | 'warn' | 'error'} [level='log'] - The console method to use.
   */
  function log(message, level = 'log') {
    console[level](`[DropBearSnackbar] ${message}`);
  }

  /**
   * Retrieves the snackbar DOM element by its ID.
   * @param {string} snackbarId - The ID of the snackbar element.
   * @returns {HTMLElement | null} The snackbar element or null if not found.
   */
  function getSnackbarElement(snackbarId) {
    return document.getElementById(snackbarId);
  }

  /**
   * Animates the progress bar of a snackbar.
   * @param {HTMLElement} progressBar - The progress bar element.
   * @param {number} duration - The duration of the animation in milliseconds.
   */
  function animateProgressBar(progressBar, duration) {
    if (!progressBar) return;
    progressBar.style.transition = 'none';
    progressBar.style.width = '100%';

    requestAnimationFrame(() => {
      progressBar.style.transition = `width ${duration}ms linear`;
      progressBar.style.width = '0%';
    });
  }

  /**
   * Removes a snackbar element from the DOM and the active snackbars map.
   * @param {string} snackbarId - The ID of the snackbar to remove.
   */
  function removeSnackbar(snackbarId) {
    const snackbar = getSnackbarElement(snackbarId);
    if (snackbar) {
      snackbar.addEventListener(
        'animationend',
        () => {
          snackbar.remove();
          snackbars.delete(snackbarId);
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
      if (!snackbarId || typeof duration !== 'number' || duration <= 0) {
        throw new Error('Invalid arguments provided to startProgress');
      }

      const snackbar = getSnackbarElement(snackbarId);
      if (!snackbar) {
        setTimeout(() => this.startProgress(snackbarId, duration), 50);
        return;
      }

      const progressBar = snackbar.querySelector('.snackbar-progress');
      if (!progressBar) return;

      animateProgressBar(progressBar, duration);

      if (snackbars.has(snackbarId)) {
        clearTimeout(snackbars.get(snackbarId));
      }

      snackbars.set(
        snackbarId,
        setTimeout(() => this.hideSnackbar(snackbarId), duration)
      );
    },

    /**
     * Hides the snackbar by removing it from the DOM and the active snackbars map.
     * @param {string} snackbarId - The ID of the snackbar to hide.
     */
    hideSnackbar(snackbarId) {
      if (!snackbarId) {
        throw new Error('Invalid snackbarId provided to hideSnackbar');
      }
      if (snackbars.has(snackbarId)) {
        clearTimeout(snackbars.get(snackbarId));
        removeSnackbar(snackbarId);
      }
    },

    /**
     * Disposes of a snackbar by hiding it.
     * @param {string} snackbarId - The ID of the snackbar to dispose.
     */
    disposeSnackbar(snackbarId) {
      this.hideSnackbar(snackbarId);
    },

    /**
     * Checks if a snackbar is currently active.
     * @param {string} snackbarId - The ID of the snackbar to check.
     * @returns {boolean} True if the snackbar is active, false otherwise.
     */
    isSnackbarActive(snackbarId) {
      return snackbars.has(snackbarId);
    },
  };
})();
/**
 * DropBearFileUploader module
 * Handles file drag-and-drop functionality for elements with the class 'file-upload-dropzone'.
 */
window.DropBearFileUploader = (() => {
  let droppedFiles = [];

  /**
   * Reads a file as an ArrayBuffer.
   * @param {File} file - The file to read.
   * @returns {Promise<Uint8Array>} A promise that resolves to a Uint8Array of the file data.
   */
  function readFileAsArrayBuffer(file) {
    return new Promise((resolve, reject) => {
      const reader = new FileReader();
      reader.onload = () => resolve(new Uint8Array(reader.result));
      reader.onerror = () => reject(reader.error);
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
      const items = e.dataTransfer.items || e.dataTransfer.files;
      for (let i = 0; i < items.length; i++) {
        const item = items[i];
        const file = item.getAsFile ? item.getAsFile() : item;
        if (file) {
          const fileData = await readFileAsArrayBuffer(file);
          droppedFiles.push({
            name: file.name,
            size: file.size,
            type: file.type,
            data: fileData,
          });
        }
      }
    } catch (error) {
      console.error('Error processing dropped files:', error);
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
      const files = [...droppedFiles];
      droppedFiles = [];
      return files;
    },

    /**
     * Clears the internal storage of dropped files.
     */
    clearDroppedFiles() {
      droppedFiles = [];
    },
  };
})();
/**
 * Utility function for file download.
 * Downloads a file from a content stream or byte array.
 * @param {string} fileName - The name of the file to be downloaded.
 * @param {Uint8Array | DotNetStreamReference} content - The content of the file.
 * @param {string} contentType - The MIME type of the file.
 */
window.downloadFileFromStream = async (fileName, content, contentType) => {
  try {
    console.log('downloadFileFromStream called with:', {fileName, content, contentType});

    let blob;

    if (content instanceof Blob) {
      console.log('Content is a Blob.');
      blob = content;
    } else if (content.arrayBuffer) {
      console.log('Content has arrayBuffer method (DotNetStreamReference detected).');
      const arrayBuffer = await content.arrayBuffer();
      console.log('ArrayBuffer received, byte length:', arrayBuffer.byteLength);
      blob = new Blob([arrayBuffer], {type: contentType});
    } else if (content instanceof Uint8Array) {
      console.log('Content is a Uint8Array.');
      blob = new Blob([content], {type: contentType});
    } else {
      throw new Error('Unsupported content type');
    }

    console.log('Blob created, size:', blob.size);

    const url = URL.createObjectURL(blob);
    const anchorElement = document.createElement('a');
    anchorElement.href = url;
    anchorElement.download = fileName || 'download';

    document.body.appendChild(anchorElement);

    // Use setTimeout to ensure the click event is not blocked by the browser
    setTimeout(() => {
      console.log('Triggering download...');
      anchorElement.click();
      document.body.removeChild(anchorElement);
      URL.revokeObjectURL(url);
      console.log('Download completed and cleanup done.');
    }, 0);
  } catch (error) {
    console.error('Error in downloadFileFromStream:', error);
  }
};

/**
 * DropBearContextMenu module
 * Manages context menu interactions with Blazor components.
 */
window.DropBearContextMenu = (() => {
  class ContextMenu {
    constructor(element, dotNetReference) {
      if (!(element instanceof HTMLElement)) {
        throw new TypeError('element must be a valid HTMLElement');
      }
      if (!dotNetReference) {
        throw new TypeError('dotNetReference must not be null or undefined');
      }
      this.element = element;
      this.dotNetReference = dotNetReference;
      this.isDisposed = false;
      this.initialize();
    }

    initialize() {
      this.handleContextMenu = this.handleContextMenu.bind(this);
      this.handleDocumentClick = this.handleDocumentClick.bind(this);

      this.element.addEventListener('contextmenu', this.handleContextMenu);
      document.addEventListener('click', this.handleDocumentClick);
    }

    handleContextMenu(e) {
      e.preventDefault();
      const x = e.pageX;
      const y = e.pageY;
      this.show(x, y);
    }

    handleDocumentClick() {
      if (this.isDisposed) return;
      this.dotNetReference.invokeMethodAsync('Hide').catch(() => {
      });
    }

    show(x, y) {
      this.dotNetReference.invokeMethodAsync('Show', x, y).catch(() => {
      });
    }

    dispose() {
      this.element.removeEventListener('contextmenu', this.handleContextMenu);
      document.removeEventListener('click', this.handleDocumentClick);
      this.isDisposed = true;
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
      if (!elementId || !dotNetReference) {
        throw new Error('Invalid arguments provided to initialize');
      }

      const element = document.getElementById(elementId);
      if (!element) {
        console.error(`Element with id '${elementId}' not found.`);
        return;
      }

      if (menuInstances.has(elementId)) {
        this.dispose(elementId);
      }

      const menuInstance = new ContextMenu(element, dotNetReference);
      menuInstances.set(elementId, menuInstance);
    },

    /**
     * Disposes of the context menu for a specific element.
     * @param {string} elementId - The ID of the element.
     */
    dispose(elementId) {
      const menuInstance = menuInstances.get(elementId);
      if (menuInstance) {
        menuInstance.dispose();
        menuInstances.delete(elementId);
      }
    },

    /**
     * Disposes of all context menu instances.
     */
    disposeAll() {
      menuInstances.forEach(instance => instance.dispose());
      menuInstances.clear();
    },
  };
})();
/**
 * DropBearNavigationButtons module
 * Manages navigation buttons like 'scroll to top' and 'go back'.
 */
window.DropBearNavigationButtons = (() => {
  let dotNetReference = null;
  let throttledHandleScroll;

  function handleScroll() {
    if (!dotNetReference) return;
    const isVisible = window.scrollY > 300;
    dotNetReference.invokeMethodAsync('UpdateVisibility', isVisible).catch(() => {
    });
  }

  return {
    /**
     * Initializes the navigation buttons with a .NET object reference.
     * @param {DotNetObjectReference} dotNetRef - The .NET object reference.
     */
    initialize(dotNetRef) {
      if (!dotNetRef) {
        throw new Error('dotNetRef must not be null or undefined');
      }
      if (dotNetReference) {
        this.dispose();
      }

      dotNetReference = dotNetRef;
      throttledHandleScroll = throttle(handleScroll, 100);
      window.addEventListener('scroll', throttledHandleScroll);

      // Trigger initial check
      handleScroll();
    },

    /**
     * Scrolls the window to the top.
     */
    scrollToTop() {
      window.scrollTo({top: 0, behavior: 'smooth'});
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
      }
    },
  };
})();
/**
 * DropBearResizeManager module
 * Manages window resize events to adjust component sizing.
 */
window.DropBearResizeManager = (() => {
  let dotNetReference = null;
  let debouncedHandleResize;

  function handleResize() {
    if (dotNetReference) {
      dotNetReference.invokeMethodAsync('SetMaxWidthBasedOnWindowSize').catch(() => {
      });
    }
  }

  return {
    /**
     * Initializes the resize manager with a .NET object reference.
     * @param {DotNetObjectReference} dotNetRef - The .NET object reference.
     */
    initialize(dotNetRef) {
      if (!dotNetRef) {
        throw new Error('dotNetRef must not be null or undefined');
      }
      if (dotNetReference) {
        this.dispose();
      }

      dotNetReference = dotNetRef;
      debouncedHandleResize = debounce(handleResize, 100);
      window.addEventListener('resize', debouncedHandleResize);

      // Trigger an initial call to SetMaxWidthBasedOnWindowSize
      handleResize();
    },

    /**
     * Disposes of the resize manager by removing event listeners.
     */
    dispose() {
      if (dotNetReference) {
        window.removeEventListener('resize', debouncedHandleResize);
        dotNetReference = null;
      }
    },
  };
})();
/**
 * DropBearThemeManager module
 * Manages theme preferences and applies color schemes.
 */
window.DropBearThemeManager = (() => {
  let dotNetReference = null;
  const STORAGE_KEY = 'dropbear-theme-preference';
  let mediaQuery = null;

  /**
   * Sets the color scheme and stores the preference.
   * @param {'auto' | 'light' | 'dark'} scheme - The color scheme.
   */
  function setColorScheme(scheme) {
    document.documentElement.dataset.colorScheme = scheme;
    localStorage.setItem(STORAGE_KEY, scheme);
  }

  /**
   * Retrieves the stored color scheme preference.
   * @returns {'auto' | 'light' | 'dark'} The stored color scheme.
   */
  function getColorScheme() {
    return localStorage.getItem(STORAGE_KEY) || 'auto';
  }

  /**
   * Applies the specified color scheme.
   * @param {'auto' | 'light' | 'dark'} scheme - The color scheme to apply.
   */
  function applyColorScheme(scheme) {
    if (!['auto', 'light', 'dark'].includes(scheme)) {
      scheme = 'auto';
    }

    let effectiveScheme = scheme;
    if (scheme === 'auto') {
      effectiveScheme = window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
    }

    document.documentElement.dataset.colorScheme = effectiveScheme;
    localStorage.setItem(STORAGE_KEY, scheme);

    if (dotNetReference) {
      dotNetReference.invokeMethodAsync('OnThemeChanged', effectiveScheme, scheme).catch(() => {
      });
    }
  }

  const debouncedApplyColorScheme = debounce(applyColorScheme, 50);

  function handleSystemThemeChange() {
    if (getColorScheme() === 'auto') {
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
        this.dispose();
      }

      dotNetReference = dotNetRef;
      const storedScheme = getColorScheme();
      setColorScheme(storedScheme);

      mediaQuery = window.matchMedia('(prefers-color-scheme: dark)');
      mediaQuery.addEventListener('change', handleSystemThemeChange);

      if (storedScheme === 'auto') {
        handleSystemThemeChange();
      }
    },

    /**
     * Toggles the theme between 'dark', 'light', and 'auto'.
     */
    toggleTheme() {
      const currentScheme = getColorScheme();
      const newScheme = currentScheme === 'dark' ? 'light' : currentScheme === 'light' ? 'auto' : 'dark';
      applyColorScheme(newScheme);
    },

    /**
     * Sets the theme to a specific scheme.
     * @param {'auto' | 'light' | 'dark'} scheme - The color scheme to set.
     */
    setTheme(scheme) {
      applyColorScheme(scheme);
    },

    /**
     * Gets the current theme preference.
     * @returns {'auto' | 'light' | 'dark'} The current theme preference.
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
      dotNetReference = null;
    },
  };
})();
/**
 * Utility function for getting the window dimensions.
 * @returns {{width: number, height: number}} An object containing the width and height of the window.
 */
window.getWindowDimensions = () => ({
  width: window.innerWidth,
  height: window.innerHeight,
});
