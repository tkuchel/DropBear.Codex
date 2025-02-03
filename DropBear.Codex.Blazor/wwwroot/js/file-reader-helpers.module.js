/**
 * @fileoverview Provides helper functions for reading files in a browser context
 * @module file-reader-helpers
 */

import {DropBearUtils} from './utils.module.js';

const logger = DropBearUtils.createLogger('DropBearFileReaderHelpers');
let isInitialized = false;

/** @type {Object} Reader configuration constants */
const READER_CONFIG = {
  MAX_CHUNK_SIZE: 1024 * 1024 * 10, // 10MB max chunk size
  DEFAULT_CHUNK_SIZE: 1024 * 1024, // 1MB default chunk size
  READ_TIMEOUT: 30000 // 30 second timeout for read operations
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
  }
};

// Attach to window first
window["file-reader-helpers"] = {
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
      window["file-reader-helpers"].__initialized = true;

      logger.debug('File reader helpers module initialized');
    } catch (error) {
      logger.error('File reader helpers initialization failed:', error);
      throw error;
    }
  },

  isInitialized: () => isInitialized,

  dispose: () => {
    isInitialized = false;
    window["file-reader-helpers"].__initialized = false;
    logger.debug('File reader helpers module disposed');
  }
};

// Register with ModuleManager after window attachment
window.DropBearModuleManager.register(
  'file-reader-helpers',
  {
    initialize: () => window["file-reader-helpers"].initialize(),
    isInitialized: () => window["file-reader-helpers"].isInitialized(),
    dispose: () => window["file-reader-helpers"].dispose()
  },
  ['DropBearUtils']
);

// Export helper functions
export const {getFileInfo, readFileChunk, getDroppedFiles} = FileReaderHelpers;
