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

// DropBearSnackbar
window.DropBearSnackbar = (function () {
  const snackbars = new Map();

  function removeSnackbar(snackbarId) {
    const snackbar = document.getElementById(snackbarId);
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
      console.warn(`Snackbar ${snackbarId} not found for removal`);
      snackbars.delete(snackbarId);
    }
  }

  return {
    startProgress(snackbarId, duration) {
      console.log(`Starting progress for snackbar ${snackbarId} with duration ${duration}`);
      const snackbar = document.getElementById(snackbarId);
      if (!snackbar) {
        console.error(`Snackbar ${snackbarId} not found`);
        return;
      }

      const progressBar = snackbar.querySelector('.snackbar-progress');
      if (!progressBar) {
        console.error('Progress bar not found');
        return;
      }

      progressBar.style.transition = 'none';
      progressBar.style.width = '100%';
      progressBar.style.backgroundColor = getComputedStyle(progressBar).getPropertyValue('color');

      setTimeout(() => {
        progressBar.style.transition = `width ${duration}ms linear`;
        progressBar.style.width = '0%';
      }, 10);

      snackbars.set(snackbarId, setTimeout(() => this.hideSnackbar(snackbarId), duration));
    },

    hideSnackbar(snackbarId) {
      console.log(`Hiding snackbar ${snackbarId}`);
      if (snackbars.has(snackbarId)) {
        clearTimeout(snackbars.get(snackbarId));
        removeSnackbar(snackbarId);
      } else {
        console.warn(`Snackbar ${snackbarId} not found in active snackbars`);
      }
    },

    disposeSnackbar(snackbarId) {
      console.log(`Disposing snackbar ${snackbarId}`);
      this.hideSnackbar(snackbarId);
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


// Utility function for getting the window dimensions
window.getWindowDimensions = function () {
  return {
    width: window.innerWidth,
    height: window.innerHeight
  };
};

