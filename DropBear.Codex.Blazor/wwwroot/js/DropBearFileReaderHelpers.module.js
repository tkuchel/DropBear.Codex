/**
 * @fileoverview Provides helper functions for reading files in a browser context
 * @module file-reader-helpers
 */

import {DropBearUtils} from './DropBearUtils.module.js';

const logger = DropBearUtils.createLogger('DropBearFileReaderHelpers');
let isInitialized = false;
const moduleName = 'DropBearFileReaderHelpers';
const droppedFileStore = new Map();

/** @type {Object} Reader configuration constants */
const READER_CONFIG = {
  MAX_CHUNK_SIZE: 1024 * 1024 * 10, // 10MB max chunk size
  DEFAULT_CHUNK_SIZE: 1024 * 1024,    // 1MB default chunk size
  READ_TIMEOUT: 30000                 // 30 second timeout for read operations
};

/**
 * Generates a UUID string.
 * Uses crypto.randomUUID if available, otherwise falls back to a simple implementation.
 * @returns {string} A UUID string.
 */
function generateUUID() {
  if (crypto && typeof crypto.randomUUID === 'function') {
    return crypto.randomUUID();
  } else {
    // Fallback: simple pseudo-random UUID (not RFC-compliant)
    return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, c => {
      const r = Math.random() * 16 | 0;
      const v = c === 'x' ? r : r & 0x3 | 0x8;
      return v.toString(16);
    });
  }
}

/**
 * Private helper to extract File objects from a DataTransfer object.
 * @param {DataTransfer} dataTransfer - The DataTransfer object from a drop event.
 * @returns {File[]} An array of File objects.
 */
function extractFiles(dataTransfer) {
  let files = [];

  try {
    // First try to get files directly from the files property
    if (dataTransfer.files && dataTransfer.files.length > 0) {
      files = Array.from(dataTransfer.files);
      logger.debug('Extracted files from files property:', {
        count: files.length,
        files: files.map(f => ({
          name: f.name,
          size: f.size,
          type: f.type
        }))
      });
    }

    // If no files found and items are available, try items as fallback
    if (files.length === 0 && dataTransfer.items) {
      const items = Array.from(dataTransfer.items);

      for (const item of items) {
        if (item.kind === 'file') {
          try {
            const file = item.getAsFile();
            if (file) {
              files.push(file);
            }
          } catch (itemError) {
            logger.warn('Error getting file from item:', itemError);

          }
        }
      }

      if (files.length > 0) {
        logger.debug('Extracted files from items:', {
          count: files.length,
          files: files.map(f => ({
            name: f.name,
            size: f.size,
            type: f.type
          }))
        });
      }
    }

    // Validate that we actually got File objects
    files = files.filter(file => file instanceof File);

    if (files.length === 0) {
      logger.warn('No valid files found in DataTransfer object');
    }

    return files;
  } catch (error) {
    logger.error('Error extracting files:', error);
    return [];
  }
}

/**
 * Helper functions for file operations.
 */
const FileReaderHelpers = {
  /**
   * Initializes a drop zone with direct file capture.
   * @param {HTMLElement} element - The drop zone element.
   */
  initializeDropZone(element) {
    if (!element) {
      logger.error('No element provided to initialize drop zone');
      return;
    }

    element.addEventListener('drop', e => {
      e.preventDefault();
      const files = Array.from(e.dataTransfer.files);
      element._actualFiles = files;

      logger.debug('Captured files directly:', files.map(f => ({
        name: f.name,
        size: f.size,
        type: f.type
      })));
    });
  },
  /**
   * Get file info from a File object.
   * @param {File} file - The file to get info from.
   * @returns {{name: string, size: number, type: string, lastModified: number}}
   * @throws {TypeError} If invalid file is provided.
   */
  getFileInfo(file) {
    if (!(file instanceof File)) {
      logger.error('Invalid file object provided to getFileInfo');
      throw new TypeError('Input must be a File object');
    }
    try {
      const info = {
        name: file.name,
        size: file.size,
        type: file.type || 'application/octet-stream',
        lastModified: file.lastModified
      };
      logger.debug('File info retrieved:', info);
      return info;
    } catch (error) {
      logger.error('Error getting file info:', error);
      throw error;
    }
  },

  /**
   * Read a portion (chunk) of a File object.
   * @param {File} file - The file to read from.
   * @param {number} offset - Starting byte index.
   * @param {number} count - Number of bytes to read.
   * @returns {Promise<Uint8Array>} The file chunk as a Uint8Array.
   */
  async readFileChunk(file, offset, count) {
    if (!(file instanceof File)) {
      logger.error('Invalid file object provided to readFileChunk');
      throw new TypeError('Input must be a File object');
    }
    try {
      if (typeof offset !== 'number' || offset < 0) {
        throw new TypeError('Offset must be a non-negative number');
      }
      if (typeof count !== 'number' || count <= 0) {
        throw new TypeError('Count must be a positive number');
      }
      if (count > READER_CONFIG.MAX_CHUNK_SIZE) {
        throw new Error(`Chunk size cannot exceed ${READER_CONFIG.MAX_CHUNK_SIZE} bytes`);
      }
      const blob = file.slice(offset, offset + count);
      const arrayBuffer = await blob.arrayBuffer();
      const chunk = new Uint8Array(arrayBuffer);
      logger.debug('File chunk read:', {
        fileName: file.name,
        offset,
        count,
        actualSize: chunk.length
      });
      return chunk;
    } catch (error) {
      logger.error('Error reading file chunk:', error);
      throw error;
    }
  },

  /**
   * Retrieve dropped files from a DataTransfer object.
   * @param {DataTransfer} dataTransfer - DataTransfer object from a drop event.
   * @returns {File[]} An array of File objects.
   */
  getDroppedFiles(dataTransfer) {
    if (!dataTransfer) {
      logger.error('Invalid DataTransfer object provided');
      throw new TypeError('Invalid DataTransfer object');
    }
    try {
      logger.debug('DataTransfer details:', {
        itemsCount: dataTransfer.items ? dataTransfer.items.length : 'N/A',
        filesCount: dataTransfer.files ? dataTransfer.files.length : 'N/A'
      });
      const files = extractFiles(dataTransfer);
      logger.debug('Dropped files retrieved:', {
        count: files.length,
        fileNames: files.map(f => f.name)
      });
      return files;
    } catch (error) {
      logger.error('Error getting dropped files:', error);
      throw error;
    }
  },

  /**
   * Retrieve dropped file keys from a Blazor DataTransfer object.
   * @param {Object} blazorDataTransfer - The DataTransfer object from Blazor
   * @returns {string[]} An array of keys referencing the dropped files.
   */
  getDroppedFileKeys(blazorTransfer) {
    if (!blazorTransfer || !blazorTransfer.fileNames || !blazorTransfer.fileTypes) {
      logger.error('Invalid transfer data provided');
      throw new TypeError('Invalid transfer data');
    }

    try {
      logger.debug('Processing Blazor transfer data:', blazorTransfer);

      const dropzone = document.querySelector('.file-upload-dropzone');
      const actualFiles = dropzone._actualFiles || [];

      logger.debug('Retrieved actual files:', actualFiles.map(f => ({
        name: f.name,
        size: f.size,
        type: f.type
      })));

      const files = blazorTransfer.fileNames.map((fileName, index) => {
        const actualFile = actualFiles.find(f => f.name === fileName);
        if (!actualFile) {
          logger.warn(`No actual file found for ${fileName}`);
          return null;
        }
        return actualFile; // Store the actual File object instead of just metadata
      }).filter(f => f !== null);

      const keys = files.map(file => {
        const key = generateUUID();
        droppedFileStore.set(key, file);
        logger.debug('Stored file with key:', {
          key,
          name: file.name,
          size: file.size,
          type: file.type,
          hasSlice: typeof file.slice === 'function'
        });
        return key;
      });

      return keys;
    } catch (error) {
      logger.error('Error processing files:', error);
      return [];
    }
  },

  /**
   * Retrieve file info for a stored file referenced by its key.
   * Returns a serializable object with properties: Name, Extension, Size, Type, LastModified.
   * @param {string} key - The key referencing the file.
   * @returns {{Name: string, Extension: string, Size: number, Type: string, LastModified: number}}
   */
  getFileInfoByKey(key) {
    const file = droppedFileStore.get(key);
    if (!file) {
      logger.error('File not found for key:', key);
      throw new Error(`File not found for key: ${key}`);
    }

    logger.debug('Retrieved file from store:', {
      key,
      fileName: file.name,
      fileSize: file.size,
      fileType: file.type
    });

    // Ensure we return an object structure that Blazor can deserialize
    return {
      Name: file.name,
      Extension: file.name ? file.name.split('.').pop() || '' : '',
      Size: file.size || 0,
      Type: file.type || 'application/octet-stream',
      LastModified: file.lastModified || Date.now()
    };
  },

  /**
   * Reads a chunk from a stored file identified by key.
   * @param {string} key - The key referencing the file.
   * @param {number} offset - The starting byte index.
   * @param {number} count - The number of bytes to read.
   * @returns {Promise<Uint8Array>} The file chunk.
   */
  async readFileChunkByKey(key, offset, count) {
    const file = droppedFileStore.get(key);
    if (!file) {
      throw new Error("File not found for key: " + key);
    }

    try {
      logger.debug('Reading chunk:', {key, offset, count});

      const blob = file.slice(offset, offset + count);
      const arrayBuffer = await blob.arrayBuffer();
      const chunk = new Uint8Array(arrayBuffer);

      logger.debug('Chunk read:', {
        size: chunk.length,
        offset,
        count
      });

      return Array.from(chunk); // Convert to regular array for serialization
    } catch (error) {
      logger.error('Error reading chunk:', error);
      throw error;
    }
  },

  /**
   * Retrieve a dropped File object by its key.
   * @param {string} key - The key referencing the file.
   * @returns {File|null} The File associated with the key, or null if not found.
   */
  getDroppedFileByKey(key) {
    return droppedFileStore.get(key) || null;
  },

  /**
   * Captures the drop data from a drag and drop event.
   * @param {DataTransfer} dataTransfer - The DataTransfer object from the drop event.
   */
  captureDropData(dataTransfer) {
    logger.debug('Capturing drop data:', dataTransfer);
    const dropzone = document.querySelector('.file-upload-dropzone');
    if (dropzone) {
      dropzone.dropData = dataTransfer;
      logger.debug('Stored drop data:', dropzone.dropData);
    } else {
      logger.error('No dropzone found');
    }
  },

  /**
   * Clear the dropped file store.
   * Call this after processing files to avoid memory buildup.
   */
  clearDroppedFileStore() {
    droppedFileStore.clear();
  },

  /**
   * Initialize global event listeners to prevent default browser behavior on dragover and drop events.
   * This prevents files from being opened by the browser when dropped anywhere.
   */
  initGlobalDropPrevention() {
    document.addEventListener('dragover', e => e.preventDefault(), false);
    document.addEventListener('drop', e => e.preventDefault(), false);
    logger.debug('Global drop prevention enabled.');
  }
};

// Attach to window first with complete method exposure
window[moduleName] = {
  __initialized: false,
  ...FileReaderHelpers,
  initialize: async () => {
    if (isInitialized) {
      return;
    }
    try {
      logger.debug('File reader helpers module initializing');
      await window.DropBearUtils.initialize();
      isInitialized = true;
      window[moduleName].__initialized = true;
      logger.debug('File reader helpers module initialized');
    } catch (error) {
      logger.error('File reader helpers initialization failed:', error);
      throw error;
    }
  },
  isInitialized: () => isInitialized,
  dispose: () => {
    isInitialized = false;
    window[moduleName].__initialized = false;
    logger.debug('File reader helpers module disposed');
  }
};

// Register with ModuleManager after window attachment
window.DropBearModuleManager.register(
  moduleName,
  {
    initialize: () => window[moduleName].initialize(),
    isInitialized: () => window[moduleName].isInitialized(),
    dispose: () => window[moduleName].dispose()
  },
  ['DropBearUtils']
);

// Export the helper functions under a unique namespace with all methods properly exposed
export const DropBearFileReaderHelpersAPI = {
  initialize: async () => window[moduleName].initialize(),
  getFileInfo: (...args) => window[moduleName].getFileInfo(...args),
  readFileChunk: async (...args) => window[moduleName].readFileChunk(...args),
  getDroppedFiles: (...args) => window[moduleName].getDroppedFiles(...args),
  getDroppedFileKeys: (...args) => window[moduleName].getDroppedFileKeys(...args),
  getDroppedFileByKey: (...args) => window[moduleName].getDroppedFileByKey(...args),
  getFileInfoByKey: (...args) => window[moduleName].getFileInfoByKey(...args),
  readFileChunkByKey: async (...args) => window[moduleName].readFileChunkByKey(...args),
  clearDroppedFileStore: () => window[moduleName].clearDroppedFileStore(),
  initGlobalDropPrevention: () => window[moduleName].initGlobalDropPrevention(),
  initializeDropZone: element => window[moduleName].initializeDropZone(element),
  captureDropData: dataTransfer => window[moduleName].captureDropData(dataTransfer),
  isInitialized: () => window[moduleName].isInitialized(),
  dispose: async () => window[moduleName].dispose()
};

// Individual exports for direct imports if needed
export const {
  getFileInfo,
  readFileChunk,
  getDroppedFiles,
  getDroppedFileKeys,
  getDroppedFileByKey,
  getFileInfoByKey,
  readFileChunkByKey,
  clearDroppedFileStore,
  initGlobalDropPrevention,
  captureDropData,
  initializeDropZone
} = FileReaderHelpers;
