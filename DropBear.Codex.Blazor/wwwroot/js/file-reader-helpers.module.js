/**
 * @fileoverview Provides helper functions for reading files in a browser context
 * @module file-reader-helpers
 */

import { DropBearUtils } from './utils.module.js';
import { ModuleManager } from './module-manager.module.js';

const logger = DropBearUtils.createLogger('DropBearFileReaderHelpers');
let isInitialized = false;

/**
 * Helper functions for file operations
 * @type {Object}
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
   * @throws {TypeError} If invalid parameters are provided
   */
  async readFileChunk(file, offset, count) {
    if (!(file instanceof File)) {
      logger.error('Invalid file object provided to readFileChunk');
      throw new TypeError('Input must be a File object');
    }

    if (typeof offset !== 'number' || offset < 0) {
      throw new TypeError('Offset must be a non-negative number');
    }

    if (typeof count !== 'number' || count <= 0) {
      throw new TypeError('Count must be a positive number');
    }

    try {
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
   * @throws {TypeError} If invalid DataTransfer object is provided
   */
  getDroppedFiles(dataTransfer) {
    if (!dataTransfer?.items) {
      logger.error('Invalid DataTransfer object provided');
      throw new TypeError('Invalid DataTransfer object');
    }

    try {
      const files = Array.from(dataTransfer.items)
        .filter(item => item.kind === 'file')
        .map(item => item.getAsFile())
        .filter(file => file !== null);

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
   * Read a file as text
   * @param {File} file - The file to read
   * @returns {Promise<string>} The file contents as text
   * @throws {TypeError} If invalid file is provided
   */
  async readFileAsText(file) {
    if (!(file instanceof File)) {
      logger.error('Invalid file object provided to readFileAsText');
      throw new TypeError('Input must be a File object');
    }

    try {
      const text = await new Promise((resolve, reject) => {
        const reader = new FileReader();
        reader.onload = () => resolve(reader.result);
        reader.onerror = () => reject(reader.error);
        reader.readAsText(file);
      });

      logger.debug('File read as text:', {
        fileName: file.name,
        size: text.length
      });

      return text;
    } catch (error) {
      logger.error('Error reading file as text:', error);
      throw error;
    }
  },

  /**
   * Read a file as an ArrayBuffer
   * @param {File} file - The file to read
   * @returns {Promise<ArrayBuffer>} The file contents as ArrayBuffer
   * @throws {TypeError} If invalid file is provided
   */
  async readFileAsArrayBuffer(file) {
    if (!(file instanceof File)) {
      logger.error('Invalid file object provided to readFileAsArrayBuffer');
      throw new TypeError('Input must be a File object');
    }

    try {
      const buffer = await file.arrayBuffer();

      logger.debug('File read as ArrayBuffer:', {
        fileName: file.name,
        size: buffer.byteLength
      });

      return buffer;
    } catch (error) {
      logger.error('Error reading file as ArrayBuffer:', error);
      throw error;
    }
  }
};

// Register with ModuleManager
ModuleManager.register(
  'DropBearFileReaderHelpers',
  {
    /**
     * Initialize the file reader helpers module
     * @returns {Promise<void>}
     */
    async initialize() {
      if (isInitialized) {
        return;
      }

      try {
        logger.debug('File reader helpers module initializing');

        isInitialized = true;
        window.DropBearFileReaderHelpers.__initialized = true;

        logger.debug('File reader helpers module initialized');
      } catch (error) {
        logger.error('File reader helpers initialization failed:', error);
        throw error;
      }
    },

    /**
     * Check if the module is initialized
     * @returns {boolean}
     */
    isInitialized() {
      return isInitialized;
    },

    /**
     * Get file reader helper functions
     * @returns {Object} The helper functions
     */
    getHelpers() {
      if (!isInitialized) {
        throw new Error('Module not initialized');
      }
      return FileReaderHelpers;
    },

    /**
     * Dispose the module
     */
    dispose() {
      isInitialized = false;
      window.DropBearFileReaderHelpers.__initialized = false;
      logger.debug('File reader helpers module disposed');
    }
  },
  [] // No dependencies
);

// Get module reference
const fileReaderHelpersModule = ModuleManager.get('DropBearFileReaderHelpers');

// Attach to window
window.DropBearFileReaderHelpers = {
  __initialized: false,
  initialize: () => fileReaderHelpersModule.initialize(),
  ...FileReaderHelpers,
  dispose: () => fileReaderHelpersModule.dispose()
};

// Export helper functions
export const {
  getFileInfo,
  readFileChunk,
  getDroppedFiles,
  readFileAsText,
  readFileAsArrayBuffer
} = FileReaderHelpers;
