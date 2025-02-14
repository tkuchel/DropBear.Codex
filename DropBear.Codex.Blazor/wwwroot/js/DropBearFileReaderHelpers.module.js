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
  if (dataTransfer.items && dataTransfer.items.length > 0) {
    const items = Array.from(dataTransfer.items);
    const fileItems = items.filter(
      item => item.kind === 'file' && typeof item.getAsFile === 'function'
    );
    if (fileItems.length > 0) {
      files = fileItems
        .map(item => item.getAsFile())
        .filter(file => file !== null);
    }
  }
  if (files.length === 0 && dataTransfer.files && dataTransfer.files.length > 0) {
    files = Array.from(dataTransfer.files);
  }
  return files;
}

/**
 * Helper functions for file operations.
 */
const FileReaderHelpers = {
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
   * Retrieve dropped file keys from a DataTransfer object.
   * The actual File objects are stored in a global dictionary,
   * and an array of keys is returned so they can be retrieved later.
   * @param {DataTransfer} dataTransfer - DataTransfer object from a drop event.
   * @returns {string[]} An array of keys referencing the dropped files.
   */
  getDroppedFileKeys(dataTransfer) {
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
      const keys = files.map(file => {
        const key = generateUUID();
        droppedFileStore.set(key, file);
        return key;
      });
      logger.debug('Dropped file keys retrieved:', {
        count: keys.length,
        keys
      });
      return keys;
    } catch (error) {
      logger.error('Error getting dropped file keys:', error);
      throw error;
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
      throw new Error("File not found for key: " + key);
    }

    // Fall back to the key if the file name is missing.
    const name = file.name || key;

    // Extract the extension from the file name (everything after the last dot).
    let extension = '';
    const dotIndex = name.lastIndexOf('.');
    if (dotIndex > -1 && dotIndex < name.length - 1) {
      extension = name.substring(dotIndex + 1).toLowerCase();
    }

    return {
      Name: name,
      Extension: extension,
      Size: file.size,
      Type: file.type || 'application/octet-stream',
      LastModified: file.lastModified
    };
  },

  /**
   * Reads a chunk from a stored file identified by key.
   * @param {string} key - The key referencing the file.
   * @param {number} offset - The starting byte index.
   * @param {number} count - The number of bytes to read.
   * @returns {Promise<ArrayBuffer>} The file chunk.
   */
  async readFileChunkByKey(key, offset, count) {
    const file = droppedFileStore.get(key);
    if (!file) {
      throw new Error("File not found for key: " + key);
    }
    const blob = file.slice(offset, offset + count);
    return await blob.arrayBuffer();
  },


  /**
   * Retrieve a dropped File object by its key.
   * @param {string} key - The key referencing the file.
   * @returns {File|null} The File associated with the key, or null if not found.
   */
  getDroppedFileByKey(key) {
    return droppedFileStore.get(key) || null;
  }
  ,

  /**
   * (Optional) Clear the dropped file store.
   * Call this after processing files to avoid memory buildup.
   */
  clearDroppedFileStore() {
    droppedFileStore.clear();
  }
  ,

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

// Attach to window first
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
    // Optionally, remove the module from window if needed:
    // delete window[moduleName];
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

// Export the helper functions under a unique namespace for dynamic import.
export const DropBearFileReaderHelpersAPI = {
  initialize: async () => window[moduleName].initialize(),
  getFileInfo: (...args) => window[moduleName].getFileInfo(...args),
  readFileChunk: async (...args) => window[moduleName].readFileChunk(...args),
  getDroppedFiles: (...args) => window[moduleName].getDroppedFiles(...args),
  getDroppedFileKeys: (...args) => window[moduleName].getDroppedFileKeys(...args),
  getDroppedFileByKey: (...args) => window[moduleName].getDroppedFileByKey(...args),
  getFileInfoByKey: (...args) => window[moduleName].getFileInfoByKey(...args),
  readFileChunkByKey: (...args) => window[moduleName].readFileChunkByKey(...args),
  initGlobalDropPrevention: () => window[moduleName].initGlobalDropPrevention(),
  isInitialized: () => window[moduleName].isInitialized(),
  dispose: async () => window[moduleName].dispose()
};

export const {
  getFileInfo,
  readFileChunk,
  getDroppedFiles,
  getDroppedFileKeys,
  getDroppedFileByKey,
  getFileInfoByKey,
  initGlobalDropPrevention,
  clearDroppedFileStore
} = FileReaderHelpers;

