/**
 * @fileoverview Provides helper functions for reading files in a browser context
 * @module file-reader-helpers
 */

import { DropBearUtils } from './DropBearUtils.module.js';

const logger = DropBearUtils.createLogger('DropBearFileReaderHelpers');
let isInitialized = false;
const moduleName = 'DropBearFileReaderHelpers';

/** @type {Object} Reader configuration constants */
const READER_CONFIG = {
  MAX_CHUNK_SIZE: 1024 * 1024 * 10, // 10MB max chunk size
  DEFAULT_CHUNK_SIZE: 1024 * 1024,    // 1MB default chunk size
  READ_TIMEOUT: 30000                 // 30 second timeout for read operations
};

/**
 * Helper functions for file operations
 */
const FileReaderHelpers = {
  /**
   * Get file info from a File object
   * @param {File} file - The file to get info from
   * @returns {{name: string, size: number, type: string, lastModified: number}}
   * @throws {TypeError} If invalid file is provided
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
   * Read a portion (chunk) of a File object
   * @param {File} file - The file to read from
   * @param {number} offset - Starting byte index
   * @param {number} count - Number of bytes to read
   * @returns {Promise<Uint8Array>} The file chunk as a Uint8Array
   */
  async readFileChunk(file, offset, count) {
    if (!(file instanceof File)) {
      logger.error('Invalid file object provided to readFileChunk');
      throw new TypeError('Input must be a File object');
    }

    try {
      // Validate chunk parameters
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
   * Retrieve dropped files from a DataTransfer object
   * @param {DataTransfer} dataTransfer - DataTransfer object from a drop event
   * @returns {File[]} An array of File objects
   */
  getDroppedFiles(dataTransfer) {
    if (!dataTransfer) {
      logger.error('Invalid DataTransfer object provided');
      throw new TypeError('Invalid DataTransfer object');
    }

    try {
      // Log some debug info for troubleshooting.
      logger.debug('DataTransfer details:', {
        itemsCount: dataTransfer.items ? dataTransfer.items.length : 'N/A',
        filesCount: dataTransfer.files ? dataTransfer.files.length : 'N/A'
      });

      let files = [];

      if (dataTransfer.items && dataTransfer.items.length > 0) {
        // Filter items that are files and support getAsFile.
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

      // If no files were found using items, fall back to dataTransfer.files.
      if (files.length === 0 && dataTransfer.files && dataTransfer.files.length > 0) {
        files = Array.from(dataTransfer.files);
      }

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
   * Initialize global event listeners to prevent default browser behavior on dragover and drop events.
   * This prevents files from being opened by the browser when dropped anywhere.
   */
  initGlobalDropPrevention() {
    document.addEventListener('dragover', (e) => {
      e.preventDefault();
    }, false);

    document.addEventListener('drop', (e) => {
      e.preventDefault();
    }, false);

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

      // Initialize dependencies
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
    // Optionally, to free memory, you could remove the module from window:
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
// This prevents conflicts with similar function names from other modules.
export const DropBearFileReaderHelpersAPI = {
  /**
   * Initializes the File Reader Helpers module.
   * @returns {Promise<void>}
   */
  initialize: async () => window[moduleName].initialize(),

  /**
   * Retrieves file information from a File object.
   * @param {File} file - The file to inspect.
   * @returns {{name: string, size: number, type: string, lastModified: number}}
   */
  getFileInfo: (...args) => window[moduleName].getFileInfo(...args),

  /**
   * Reads a chunk from a File object.
   * @param {File} file - The file to read from.
   * @param {number} offset - The starting byte index.
   * @param {number} count - The number of bytes to read.
   * @returns {Promise<Uint8Array>}
   */
  readFileChunk: async (...args) => window[moduleName].readFileChunk(...args),

  /**
   * Retrieves files from a DataTransfer object.
   * @param {DataTransfer} dataTransfer - The drop event's DataTransfer object.
   * @returns {File[]}
   */
  getDroppedFiles: (...args) => window[moduleName].getDroppedFiles(...args),

  /**
   * Enables global drop prevention to disable default file opening.
   */
  initGlobalDropPrevention: () => window[moduleName].initGlobalDropPrevention(),

  /**
   * Checks whether the module is initialized.
   * @returns {boolean}
   */
  isInitialized: () => window[moduleName].isInitialized(),

  /**
   * Disposes the File Reader Helpers module.
   * @returns {Promise<void>}
   */
  dispose: async () => window[moduleName].dispose()
};

// Also export helper functions
export const { getFileInfo, readFileChunk, getDroppedFiles, initGlobalDropPrevention } = FileReaderHelpers;
