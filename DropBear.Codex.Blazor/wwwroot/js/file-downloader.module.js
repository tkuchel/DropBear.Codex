/**
 * @fileoverview File downloader module for handling downloads from streams or byte arrays
 * @module file-downloader
 */

import { CircuitBreaker, DOMOperationQueue, EventEmitter } from './core.module.js';
import { DropBearUtils } from './utils.module.js';
import { ModuleManager } from './module-manager.module.js';

const logger = DropBearUtils.createLogger('DropBearFileDownloader');
const circuitBreaker = new CircuitBreaker({ failureThreshold: 3, resetTimeout: 30000 });
let isInitialized = false;

/**
 * Download manager for handling file downloads
 * @implements {IDownloadManager}
 */
class DownloadManager {
  constructor() {
    /** @type {Set<string>} - Track active downloads by ID */
    this.activeDownloads = new Set();

    /** @type {boolean} */
    this.isDisposed = false;

    EventEmitter.emit(
      this,
      'created',
      DropBearUtils.createEvent(crypto.randomUUID(), 'created', {
        timestamp: Date.now()
      })
    );

    logger.debug('DownloadManager instance created');
  }

  /**
   * Download a file from a stream or byte array
   * @param {string} fileName - The file name for the download
   * @param {Blob | ArrayBuffer | Uint8Array} content - The file content
   * @param {string} [contentType] - The MIME type for the file
   * @returns {Promise<void>}
   */
  async downloadFileFromStream(fileName, content, contentType) {
    if (this.isDisposed) {
      throw new Error('Cannot download from disposed manager');
    }

    const downloadId = crypto.randomUUID();

    try {
      this.activeDownloads.add(downloadId);
      logger.debug('Starting download:', { fileName, contentType, downloadId });

      const blob = await this._createBlob(content, contentType);
      await this._initiateDownload(blob, fileName);

      EventEmitter.emit(
        this,
        'download-complete',
        DropBearUtils.createEvent(downloadId, 'download-complete', {
          fileName,
          timestamp: Date.now()
        })
      );

      logger.debug('Download completed:', { fileName, downloadId });
    } catch (error) {
      logger.error('Download failed:', { fileName, error, downloadId });

      EventEmitter.emit(
        this,
        'download-failed',
        DropBearUtils.createEvent(downloadId, 'download-failed', {
          fileName,
          error: error.message,
          timestamp: Date.now()
        })
      );

      throw error;
    } finally {
      this.activeDownloads.delete(downloadId);
    }
  }

  /**
   * Create a Blob from the provided content
   * @private
   * @param {Blob | ArrayBuffer | Uint8Array} content - The file content
   * @param {string} [contentType] - MIME type
   * @returns {Promise<Blob>}
   */
  async _createBlob(content, contentType) {
    return circuitBreaker.execute(async () => {
      let blob;

      try {
        if (content instanceof Blob) {
          logger.debug('Content is a Blob');
          blob = content;
        } else if (content instanceof Uint8Array) {
          logger.debug('Content is a Uint8Array');
          blob = new Blob([content], { type: contentType });
        } else if (content instanceof ArrayBuffer) {
          logger.debug('Content is an ArrayBuffer');
          blob = new Blob([content], { type: contentType });
        } else if (typeof content.arrayBuffer === 'function') {
          // .NET StreamRef case
          logger.debug('Content is a StreamRef');
          const arrayBuffer = await content.arrayBuffer();
          blob = new Blob([arrayBuffer], { type: contentType });
        } else {
          throw new Error('Unsupported content type. Must be Blob, ArrayBuffer, Uint8Array or DotNetStreamReference.');
        }

        logger.debug('Blob created, size:', blob.size);
        return blob;
      } catch (error) {
        logger.error('Error creating blob:', error);
        throw error;
      }
    });
  }

  /**
   * Initiate the file download
   * @private
   * @param {Blob} blob - The file content as a Blob
   * @param {string} [fileName='download'] - Desired filename
   */
  async _initiateDownload(blob, fileName = 'download') {
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = fileName;

    try {
      // Use DOMOperationQueue to schedule DOM work
      await new Promise(resolve => {
        DOMOperationQueue.add(() => {
          document.body.appendChild(link);
          link.click();
          document.body.removeChild(link);
          resolve();
        });
      });

      logger.debug('Download link clicked:', fileName);
    } catch (error) {
      logger.error('Error initiating download:', error);
      throw error;
    } finally {
      // Clean up the object URL after a short delay
      setTimeout(() => {
        URL.revokeObjectURL(url);
        logger.debug('Object URL revoked:', fileName);
      }, 100);
    }
  }

  /**
   * Check if there are any active downloads
   * @returns {boolean}
   */
  hasActiveDownloads() {
    return this.activeDownloads.size > 0;
  }

  /**
   * Get the count of active downloads
   * @returns {number}
   */
  getActiveDownloadCount() {
    return this.activeDownloads.size;
  }

  /**
   * Dispose the download manager
   */
  dispose() {
    if (this.isDisposed) return;

    logger.debug('Disposing DownloadManager');
    this.isDisposed = true;
    this.activeDownloads.clear();

    EventEmitter.emit(
      this,
      'disposed',
      DropBearUtils.createEvent(crypto.randomUUID(), 'disposed', {
        timestamp: Date.now()
      })
    );
  }
}

// Register with ModuleManager
ModuleManager.register(
  'DropBearFileDownloader',
  {
    /** @type {DownloadManager|null} */
    downloadManager: null,

    /**
     * Initialize the file downloader module
     * @returns {Promise<void>}
     */
    async initialize() {
      if (isInitialized) {
        return;
      }

      try {
        logger.debug('File downloader module initializing');

        // Initialize dependencies
        await ModuleManager.waitForDependencies(['DropBearCore']);

        this.downloadManager = new DownloadManager();

        isInitialized = true;
        window.DropBearFileDownloader.__initialized = true;

        logger.debug('File downloader module initialized');
      } catch (error) {
        logger.error('File downloader initialization failed:', error);
        throw error;
      }
    },

    /**
     * Download a file
     * @param {string} fileName - File name
     * @param {Blob | ArrayBuffer | Uint8Array} content - File content
     * @param {string} [contentType] - Content type
     * @returns {Promise<void>}
     */
    async downloadFileFromStream(fileName, content, contentType) {
      if (!isInitialized) {
        throw new Error('Module not initialized');
      }

      if (!this.downloadManager) {
        throw new Error('DownloadManager not created');
      }

      return this.downloadManager.downloadFileFromStream(fileName, content, contentType);
    },

    /**
     * Check if the module is initialized
     * @returns {boolean}
     */
    isInitialized() {
      return isInitialized;
    },

    /**
     * Get active download count
     * @returns {number}
     */
    getActiveDownloadCount() {
      return this.downloadManager?.getActiveDownloadCount() ?? 0;
    },

    /**
     * Dispose the module
     */
    dispose() {
      if (this.downloadManager) {
        this.downloadManager.dispose();
        this.downloadManager = null;
      }
      isInitialized = false;
      window.DropBearFileDownloader.__initialized = false;
      logger.debug('File downloader module disposed');
    }
  },
  ['DropBearCore']
);

// Get module reference
const fileDownloaderModule = ModuleManager.get('DropBearFileDownloader');

// Attach to window
window.DropBearFileDownloader = {
  __initialized: false,
  initialize: () => fileDownloaderModule.initialize(),
  downloadFileFromStream: (fileName, content, contentType) =>
    fileDownloaderModule.downloadFileFromStream(fileName, content, contentType),
  getActiveDownloadCount: () => fileDownloaderModule.getActiveDownloadCount(),
  dispose: () => fileDownloaderModule.dispose()
};

// Export DownloadManager class
export { DownloadManager };
