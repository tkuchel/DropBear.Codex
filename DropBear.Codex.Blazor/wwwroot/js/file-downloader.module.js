/**
 * @fileoverview File downloader module for handling downloads from streams or byte arrays
 * @module file-downloader
 */


import {DropBearUtils} from './utils.module.js';
import {CircuitBreaker, DOMOperationQueue} from './core.module.js';
import {ModuleManager} from './module-manager.module.js';

/**
 * Create a logger for this module
 */
const logger = DropBearUtils.createLogger('DropBearFileDownloader');

/**
 * Circuit breaker to handle repeated failures
 */
const circuitBreaker = new CircuitBreaker({failureThreshold: 3, resetTimeout: 30000});

/**
 * DownloadManager: Provides a method to download files from various content types.
 * This class aligns with an IDownloadManager interface in TypeScript.
 */
class DownloadManager {
  constructor() {
    /**
     * Track active downloads by a unique ID.
     * @type {Set<string>}
     */
    this.activeDownloads = new Set();
  }

  /**
   * Download a file from a stream or byte array.
   *
   * @param {string} fileName - The file name for the download
   * @param {Blob | ArrayBuffer | Uint8Array} content - The file content
   * @param {string} [contentType] - The MIME type for the file (optional)
   * @returns {Promise<void>}
   */
  async downloadFileFromStream(fileName, content, contentType) {
    const downloadId = crypto.randomUUID();

    try {
      this.activeDownloads.add(downloadId);
      logger.debug('Starting download:', {fileName, contentType, downloadId});

      const blob = await this._createBlob(content, contentType);
      await this._initiateDownload(blob, fileName);

      logger.debug('Download completed:', {fileName, downloadId});
    } catch (error) {
      logger.error('Download failed:', {fileName, error, downloadId});
      throw error;
    } finally {
      this.activeDownloads.delete(downloadId);
    }
  }

  /**
   * Create a Blob from the provided content. This only handles Blob, ArrayBuffer,
   * and Uint8Array to match the IDownloadManager interface exactly.
   *
   * @private
   * @param {DownloadContent} content - The file content
   * @param {string} [contentType] - MIME type
   * @returns {Promise<Blob>}
   */
  async _createBlob(content, contentType) {
    return circuitBreaker.execute(async () => {
      let blob;

      if (content instanceof Blob) {
        logger.debug('Content is a Blob');
        blob = content;
      } else if (content instanceof Uint8Array) {
        logger.debug('Content is a Uint8Array');
        blob = new Blob([content], {type: contentType});
      } else if (content instanceof ArrayBuffer) {
        logger.debug('Content is an ArrayBuffer');
        blob = new Blob([content], {type: contentType});
      } else if (typeof content.arrayBuffer === 'function') {
        // .NET StreamRef case
        const arrayBuffer = await content.arrayBuffer();
        blob = new Blob([arrayBuffer], {type: contentType});
      } else {
        // Because we've typed the content strictly, TypeScript won't let us get here.
        // But if we do at runtime, we throw an error.
        throw new Error('Unsupported content type. Must be Blob, ArrayBuffer, or Uint8Array or DotNetStreamReference.');
      }

      logger.debug('Blob created, size:', blob.size);
      return blob;
    });
  }

  /**
   * Initiate the file download by creating a temporary link in the DOM and clicking it.
   * @private
   * @param {Blob} blob - The file content as a Blob
   * @param {string} [fileName='download'] - Desired filename for the downloaded file
   */
  async _initiateDownload(blob, fileName = 'download') {
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = fileName;

    // Use DOMOperationQueue to schedule DOM work
    DOMOperationQueue.add(() => {
      document.body.appendChild(link);
      link.click();
      document.body.removeChild(link);
    });

    // Clean up the object URL after a short delay
    setTimeout(() => URL.revokeObjectURL(url), 100);
  }

  /**
   * Dispose any internal references. Clears active downloads, if needed.
   */
  dispose() {
    this.activeDownloads.clear();
  }
}

/**
 * Register the module with the DropBear ModuleManager.
 * The object we pass here can have additional methods (initialize, dispose)
 * beyond the IDownloadManager interface, so it does not conflict with strict type checking
 * as long as your TypeScript definitions treat it as a custom module interface.
 */
ModuleManager.register(
  'DropBearFileDownloader',
  {
    /**
     * A single download manager instance
     * @type {DownloadManager}
     */
    downloadManager: new DownloadManager(),

    /**
     * Global initialization method (no-arg).
     * This is optional if we want to match a typical "IModule" pattern that
     * the ModuleManager might expect (some modules have an initialize method).
     */
    async initialize() {
      logger.debug('DropBearFileDownloader module init done (no-arg).');
    },

    /**
     * Download a file from a stream or byte array.
     *
     * @param {string} fileName
     * @param {Blob | ArrayBuffer | Uint8Array} content
     * @param {string} [contentType]
     * @returns {Promise<void>}
     */
    downloadFileFromStream: async (fileName, content, contentType) => {
      const moduleRef = ModuleManager.get('DropBearFileDownloader');
      return moduleRef.downloadManager.downloadFileFromStream(fileName, content, contentType);
    },

    /**
     * Dispose the module, clearing any active downloads.
     */
    dispose() {
      this.downloadManager.dispose();
      this.downloadManager = null;
    },
  },
  // Dependencies:
  ['DropBearCore']
);

/**
 * Grab the registered downloader module so we can attach it to window or use directly.
 */
const dropBearFileDownloaderModule = ModuleManager.get('DropBearFileDownloader');

/**
 * Attach to the global window, if desired, for easy consumption
 * without imports in your HTML.
 */
window.DropBearFileDownloader = {
  initialize: () => dropBearFileDownloaderModule.initialize(),
  downloadFileFromStream: (fileName, content, contentType) =>
    dropBearFileDownloaderModule.downloadFileFromStream(fileName, content, contentType),
  dispose: () => dropBearFileDownloaderModule.dispose(),
};

/**
 * Export the DownloadManager class so it can be imported directly if needed.
 */
export {DownloadManager};
