// Utility function for debouncing events
function debounce(func, wait) {
  let timeout;
  return function (...args) {
    clearTimeout(timeout);
    timeout = setTimeout(() => func.apply(this, args), wait);
  };
}

// Utility function for throttling events
function throttle(func, limit) {
  let inThrottle;
  return function (...args) {
    if (!inThrottle) {
      func.apply(this, args);
      inThrottle = true;
      setTimeout(() => inThrottle = false, limit);
    }
  };
}

// DropBearSnackbar (v1.0.0)
window.DropBearSnackbar = (function () {
  const snackbars = new Map();

  function log(message, type = 'log') {
    console[type](`[DropBearSnackbar] ${message}`);
  }

  function getSnackbarElement(snackbarId) {
    const snackbar = document.getElementById(snackbarId);
    if (!snackbar) {
      log(`Snackbar ${snackbarId} not found in DOM`, 'warn');
    }
    return snackbar;
  }

  function getProgressBarElement(snackbar) {
    const progressBar = snackbar.querySelector('.snackbar-progress');
    if (!progressBar) {
      log('Progress bar not found', 'error');
    }
    return progressBar;
  }

  function animateProgressBar(progressBar, duration) {
    progressBar.style.transition = 'none';
    progressBar.style.width = '100%';
    progressBar.style.backgroundColor = getComputedStyle(progressBar).getPropertyValue('color');

    setTimeout(() => {
      progressBar.style.transition = `width ${duration}ms linear`;
      progressBar.style.width = '0%';
    }, 10);
  }

  function removeSnackbar(snackbarId) {
    log(`Attempting to remove snackbar ${snackbarId}`);
    const snackbar = getSnackbarElement(snackbarId);
    if (snackbar) {
      snackbar.addEventListener('animationend', () => {
        snackbar.remove();
        snackbars.delete(snackbarId);
        log(`Snackbar ${snackbarId} removed from DOM and active snackbars`);
      }, {once: true});
      snackbar.style.animation = 'slideOutDown 0.3s ease-out forwards';
    } else {
      snackbars.delete(snackbarId);
    }
  }

  return {
    startProgress(snackbarId, duration) {
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

      snackbars.set(snackbarId, setTimeout(() => this.hideSnackbar(snackbarId), duration));
      log(`Snackbar ${snackbarId} added to active snackbars`);
    },

    hideSnackbar(snackbarId) {
      log(`Attempting to hide snackbar ${snackbarId}`);
      if (snackbars.has(snackbarId)) {
        clearTimeout(snackbars.get(snackbarId));
        removeSnackbar(snackbarId);
      } else {
        log(`Snackbar ${snackbarId} not found in active snackbars`, 'warn');
      }
    },

    disposeSnackbar(snackbarId) {
      log(`Disposing snackbar ${snackbarId}`);
      this.hideSnackbar(snackbarId);
    },

    isSnackbarActive(snackbarId) {
      const isActive = snackbars.has(snackbarId);
      log(`Checking if snackbar ${snackbarId} is active: ${isActive}`);
      return isActive;
    }
  };
})();

// DropBearFileUploader
window.DropBearFileUploader = (function () {
  let droppedFiles = [];

  function handleDrop(e) {
    e.preventDefault();
    e.stopPropagation();

    droppedFiles = [];

    try {
      if (e.dataTransfer.items) {
        for (let i = 0; i < e.dataTransfer.items.length; i++) {
          if (e.dataTransfer.items[i].kind === 'file') {
            const file = e.dataTransfer.items[i].getAsFile();
            droppedFiles.push({
              name: file.name,
              size: file.size,
              type: file.type
            });
          }
        }
      } else {
        for (let i = 0; i < e.dataTransfer.files.length; i++) {
          const file = e.dataTransfer.files[i];
          droppedFiles.push({
            name: file.name,
            size: file.size,
            type: file.type
          });
        }
      }
    } catch (error) {
      console.error('Error handling dropped files:', error);
    }
  }

  function init() {
    document.addEventListener(
      'drop',
      function (e) {
        if (e.target.closest('.file-upload-dropzone')) {
          handleDrop(e);
        }
      }
    );

    document.addEventListener(
      'dragover',
      function (e) {
        if (e.target.closest('.file-upload-dropzone')) {
          e.preventDefault();
          e.stopPropagation();
        }
      }
    );
  }

  // Initialize when the DOM is ready
  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', init);
  } else {
    init();
  }

  return {
    getDroppedFiles() {
      const files = droppedFiles;
      droppedFiles = [];
      return files;
    },

    clearDroppedFiles() {
      droppedFiles = [];
    }
  };
})();

// Utility function for file download
window.downloadFileFromStream = (fileName, byteArray, contentType) => {
  const blob = new Blob([byteArray], {type: contentType});
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.style.display = 'none';
  a.href = url;
  a.download = fileName;
  document.body.appendChild(a);
  a.click();
  window.URL.revokeObjectURL(url);
  document.body.removeChild(a);
};

// DropBearContextMenu
window.DropBearContextMenu = (function () {
  class ContextMenu {
    constructor(element, dotNetReference) {
      this.element = element;
      this.dotNetReference = dotNetReference;
      this.isDisposed = false;
      this.initialize();
      console.log(`ContextMenu initialized for element: ${element.id}`);
    }

    initialize() {
      this.element.addEventListener('contextmenu', this.handleContextMenu.bind(this));
      document.addEventListener('click', this.handleDocumentClick.bind(this));
      console.log('Event listeners added');
    }

    handleContextMenu(e) {
      e.preventDefault();
      const x = e.pageX;
      const y = e.pageY;
      console.log(`Context menu triggered at X: ${x}, Y: ${y} (absolute to document)`);
      this.show(x, y);
    }

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
            console.warn('DotNetObjectReference was disposed before hide could be invoked, ignoring error.');
          } else {
            console.error('Error invoking Hide method:', error);
          }
        });
    }

    show(x, y) {
      console.log(`Showing context menu at X: ${x}, Y: ${y}`);
      this.dotNetReference
        .invokeMethodAsync('Show', x, y)
        .catch(error => console.error('Error invoking Show method:', error));
    }

    dispose() {
      this.element.removeEventListener('contextmenu', this.handleContextMenu);
      document.removeEventListener('click', this.handleDocumentClick);
      this.isDisposed = true;
      console.log(`ContextMenu disposed for element: ${this.element.id}`);
    }
  }

  const menuInstances = new Map();

  return {
    initialize(elementId, dotNetReference) {
      console.log(`Initializing ContextMenu for element: ${elementId}`);
      const element = document.getElementById(elementId);
      if (!element) {
        console.error(`Element with id '${elementId}' not found.`);
        return;
      }

      if (menuInstances.has(elementId)) {
        console.warn(`Context menu for element '${elementId}' already initialized. Disposing old instance.`);
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

    show(elementId, x, y) {
      console.log(`Attempting to show context menu for element: ${elementId}`);
      const menuInstance = menuInstances.get(elementId);
      if (menuInstance) {
        menuInstance.show(x, y);
      } else {
        console.error(`No context menu instance found for element '${elementId}'.`);
      }
    },

    dispose(elementId) {
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

    disposeAll() {
      console.log('Disposing all ContextMenu instances');
      menuInstances.forEach((instance, elementId) => this.dispose(elementId));
      menuInstances.clear();
      console.log('All ContextMenu instances disposed');
    }
  };
})();

// DropBearNavigationButtons
window.DropBearNavigationButtons = (function () {
  let dotNetReference = null;

  function handleScroll() {
    const isVisible = window.scrollY > 300;
    dotNetReference.invokeMethodAsync('UpdateVisibility', isVisible);
  }

  return {
    initialize(dotNetRef) {
      if (dotNetReference) {
        console.warn('DropBearNavigationButtons already initialized. Disposing previous instance.');
        this.dispose();
      }

      dotNetReference = dotNetRef;
      // Use throttle to limit the frequency of scroll event handling
      window.addEventListener('scroll', throttle(handleScroll, 100));
      console.log('DropBearNavigationButtons initialized');

      // Trigger initial check
      handleScroll();
    },

    scrollToTop() {
      window.scrollTo({
        top: 0,
        behavior: 'smooth'
      });
    },

    goBack() {
      window.history.back();
    },

    dispose() {
      if (dotNetReference) {
        window.removeEventListener('scroll', handleScroll);
        dotNetReference = null;
        console.log('DropBearNavigationButtons disposed');
      }
    }
  };
})();

// DropBearResizeManager (v1.0.0)
window.DropBearResizeManager = (function () {
  let dotNetReference = null;

  function handleResize() {
    if (dotNetReference) {
      dotNetReference.invokeMethodAsync('SetMaxWidthBasedOnWindowSize')
        .catch(error => console.error('Error invoking SetMaxWidthBasedOnWindowSize method:', error));
    }
  }

  return {
    // Initialize the resize event listener and associate it with the DotNetObjectReference
    initialize(dotNetRef) {
      if (dotNetReference) {
        console.warn('DropBearResizeManager already initialized. Disposing previous instance.');
        this.dispose();
      }

      dotNetReference = dotNetRef;
      // Use debounce to limit the frequency of resize event handling
      window.addEventListener('resize', debounce(handleResize, 100));
      console.log('DropBearResizeManager initialized');

      // Trigger an initial call to SetMaxWidthBasedOnWindowSize to apply the size on load
      handleResize();
    },

    // Dispose of the event listener when the component is destroyed
    dispose() {
      if (dotNetReference) {
        window.removeEventListener('resize', handleResize);
        dotNetReference = null;
        console.log('DropBearResizeManager disposed');
      }
    }
  };
})();

// DropBearThemeManager (v1.0.1)
window.DropBearThemeManager = (function () {
  let dotNetReference = null;
  const STORAGE_KEY = 'dropbear-theme-preference';
  let mediaQuery = null;

  function log(message, type = 'log') {
    console[type](`[DropBearThemeManager] ${message}`);
  }

  function setColorScheme(scheme) {
    try {
      document.documentElement.style.setProperty('--color-scheme', scheme);
      localStorage.setItem(STORAGE_KEY, scheme);
      log(`Color scheme set to: ${scheme}`);
    } catch (error) {
      log(`Error setting color scheme: ${error.message}`, 'error');
    }
  }

  function getColorScheme() {
    return localStorage.getItem(STORAGE_KEY) || 'auto';
  }

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
      window.dispatchEvent(new CustomEvent('themeChanged', { detail: { scheme: effectiveScheme, preference: scheme } }));

      if (dotNetReference) {
        dotNetReference.invokeMethodAsync('OnThemeChanged', effectiveScheme, scheme)
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

  function handleSystemThemeChange(event) {
    if (getColorScheme() === 'auto') {
      log('System theme change detected');
      debouncedApplyColorScheme('auto');
    }
  }

  return {
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
        handleSystemThemeChange({ matches: mediaQuery.matches });
      }

      log('DropBearThemeManager initialized');
    },

    toggleTheme() {
      const currentScheme = getColorScheme();
      const newScheme = currentScheme === 'dark' ? 'light' :
        currentScheme === 'light' ? 'auto' : 'dark';
      applyColorScheme(newScheme);
    },

    setTheme(scheme) {
      if (['auto', 'light', 'dark'].includes(scheme)) {
        applyColorScheme(scheme);
      } else {
        log(`Invalid color scheme: ${scheme}`, 'error');
      }
    },

    getCurrentTheme() {
      return getColorScheme();
    },

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

// Utility function for getting the window dimensions
window.getWindowDimensions = function () {
  return {
    width: window.innerWidth,
    height: window.innerHeight
  };
};

