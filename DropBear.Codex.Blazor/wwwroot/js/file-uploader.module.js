/**
 * @fileoverview File uploader module for handling file uploads with chunking support
 * @module file-uploader
 */

import {CircuitBreaker, EventEmitter} from './core.module.js';
import {DropBearUtils} from './utils.module.js';
import {ModuleManager} from './module-manager.module.js';

/**
 * Create a logger for this module
 */
const logger = DropBearUtils.createLogger('DropBearFileUploader');

/**
 * Circuit breaker for handling repeated failures.
 */
const circuitBreaker = new CircuitBreaker({failureThreshold: 3, resetTimeout: 30000});

/**
 * Constants for upload configuration
 */
const CHUNK_SIZE = 1024 * 1024; // 1MB chunks
const MAX_CONCURRENT_CHUNKS = 3; // (Currently unused, but kept for extension)
const RETRY_ATTEMPTS = 3;
const RETRY_DELAY = 1000; // 1 second

/**
 * Class to manage chunked file upload operations
 */
class ChunkUploader {
  /**
   * @param {File} file - The file to upload
   * @param {number} [chunkSize=CHUNK_SIZE] - Size of each chunk in bytes
   */
  constructor(file, chunkSize = CHUNK_SIZE) {
    /** @type {File} */
    this.file = file;

    /** @type {number} */
    this.chunkSize = chunkSize;

    /** @type {number} */
    this.totalChunks = Math.ceil(file.size / chunkSize);

    /** @type {number} */
    this.currentChunk = 0;

    /** @type {boolean} */
    this.aborted = false;
  }

  /**
   * Get the next chunk of data
   * @returns {Blob|null} Next chunk or null if complete
   */
  getNextChunk() {
    if (this.currentChunk >= this.totalChunks) {
      return null;
    }

    const start = this.currentChunk * this.chunkSize;
    const end = Math.min(start + this.chunkSize, this.file.size);
    const chunk = this.file.slice(start, end);
    this.currentChunk++;

    return chunk;
  }

  /**
   * Get upload progress as a percentage
   * @returns {number} Progress percentage
   */
  getProgress() {
    return (this.currentChunk / this.totalChunks) * 100;
  }

  /**
   * Reset the uploader to its initial state
   */
  reset() {
    this.currentChunk = 0;
    this.aborted = false;
  }

  /**
   * Abort the upload
   */
  abort() {
    this.aborted = true;
  }
}

/**
 * Manager for file upload operations
 * @implements {IFileUploadManager}
 */
class FileUploadManager {
  /**
   * @param {string} id - The ID of the upload container element
   * @param {Object} dotNetRef - .NET reference for Blazor interop
   */
  constructor(id, dotNetRef) {
    DropBearUtils.validateArgs([id, dotNetRef], ['string', 'object'], 'FileUploadManager');

    /** @type {string} */
    this.id = id;

    /** @type {HTMLElement|null} */
    this.element = document.getElementById(id);

    /** @type {Object|null} */
    this.dotNetRef = dotNetRef;

    /** @type {boolean} */
    this.isDisposed = false;

    /**
     * Track active uploads by their unique ID
     * @type {Map<string, ChunkUploader>}
     */
    this.activeUploads = new Map();

    /** @type {number} */
    this.dragCounter = 0;

    if (!DropBearUtils.isElement(this.element)) {
      throw new TypeError('Invalid element provided to FileUploadManager');
    }

    this._setupEventListeners();
    EventEmitter.emit(
      this.element,
      'created',
      DropBearUtils.createEvent(this.id, 'created', null)
    );
  }

  /**
   * Set up drag and drop event listeners
   * @private
   */
  _setupEventListeners() {
    const handlers = {
      dragenter: e => this._handleDragEnter(e),
      dragover: e => this._handleDragOver(e),
      dragleave: e => this._handleDragLeave(e),
      drop: e => this._handleDrop(e),
    };

    Object.entries(handlers).forEach(([event, handler]) =>
      this.element.addEventListener(event, handler)
    );

    // Store references for cleanup
    this.handlers = handlers;
  }

  /**
   * Handle dragenter event
   * @private
   * @param {DragEvent} e - Drag event
   */
  _handleDragEnter(e) {
    e.preventDefault();
    e.stopPropagation();
    this.dragCounter++;

    if (this.dragCounter === 1) {
      this.element.classList.add('dragover');
      EventEmitter.emit(
        this.element,
        'dragenter',
        DropBearUtils.createEvent(this.id, 'dragenter', null)
      );
    }
  }

  /**
   * Handle dragover event
   * @private
   * @param {DragEvent} e - Drag event
   */
  _handleDragOver(e) {
    e.preventDefault();
    e.stopPropagation();
  }

  /**
   * Handle dragleave event
   * @private
   * @param {DragEvent} e - Drag event
   */
  _handleDragLeave(e) {
    e.preventDefault();
    e.stopPropagation();
    this.dragCounter--;

    if (this.dragCounter === 0) {
      this.element.classList.remove('dragover');
      EventEmitter.emit(
        this.element,
        'dragleave',
        DropBearUtils.createEvent(this.id, 'dragleave', null)
      );
    }
  }

  /**
   * Handle drop event
   * @private
   * @param {DragEvent} e - Drop event
   */
  async _handleDrop(e) {
    e.preventDefault();
    e.stopPropagation();
    this.dragCounter = 0;
    this.element.classList.remove('dragover');

    const files = Array.from(e.dataTransfer.files);
    EventEmitter.emit(
      this.element,
      'drop',
      DropBearUtils.createEvent(this.id, 'drop', {fileCount: files.length})
    );

    await this.uploadFiles(files);
  }

  /**
   * Upload multiple files
   * @param {File[]} files - An array of File objects to upload
   * @returns {Promise<void>}
   */
  async uploadFiles(files) {
    if (this.isDisposed) return;

    try {
      const uploadPromises = files.map(file => this.uploadFile(file));
      await Promise.all(uploadPromises);
    } catch (error) {
      logger.error('Error uploading files:', error);
      throw error;
    }
  }

  /**
   * Upload a single file
   * @param {File} file - The file to upload
   * @returns {Promise<void>}
   */
  async uploadFile(file) {
    if (this.isDisposed) return;

    const uploadId = crypto.randomUUID();
    try {
      // Notify upload start to Blazor
      await this.dotNetRef.invokeMethodAsync('OnUploadStart', {
        id: uploadId,
        fileName: file.name,
        size: file.size,
        type: file.type,
      });

      const uploader = new ChunkUploader(file);
      this.activeUploads.set(uploadId, uploader);

      // Process file chunks
      await this._processChunks(uploadId, uploader);

      // Notify complete
      await this.dotNetRef.invokeMethodAsync('OnUploadComplete', uploadId);
      EventEmitter.emit(
        this.element,
        'upload-complete',
        DropBearUtils.createEvent(this.id, 'upload-complete', {
          uploadId,
          fileName: file.name,
        })
      );
    } catch (error) {
      logger.error(`Error uploading file ${file.name}:`, error);
      await this.dotNetRef.invokeMethodAsync('OnUploadError', uploadId, error.message);
      throw error;
    } finally {
      this.activeUploads.delete(uploadId);
    }
  }

  /**
   * Process file chunks using a circuit breaker
   * @private
   * @param {string} uploadId - Unique upload identifier
   * @param {ChunkUploader} uploader - The ChunkUploader instance
   * @returns {Promise<void>}
   */
  async _processChunks(uploadId, uploader) {
    let chunk;
    let retryCount = 0;

    // Loop while there are chunks to process and upload not aborted
    // eslint-disable-next-line no-cond-assign
    while ((chunk = uploader.getNextChunk()) !== null && !uploader.aborted) {
      try {
        await circuitBreaker.execute(async () => {
          const chunkData = await this._readChunk(chunk);
          await this.dotNetRef.invokeMethodAsync('OnChunkUpload', uploadId, {
            data: chunkData,
            index: uploader.currentChunk - 1,
            total: uploader.totalChunks,
          });
        });

        // Update progress
        await this.dotNetRef.invokeMethodAsync('OnUploadProgress', uploadId, uploader.getProgress());
        retryCount = 0; // reset retry count after successful upload
      } catch (error) {
        if (retryCount < RETRY_ATTEMPTS) {
          retryCount++;
          // Move back one chunk index to retry
          uploader.currentChunk--;
          await new Promise(resolve => setTimeout(resolve, RETRY_DELAY));
          continue;
        }
        throw error; // exceed retries
      }
    }

    if (uploader.aborted) {
      throw new Error('Upload aborted');
    }
  }

  /**
   * Read the chunk data via FileReader
   * @private
   * @param {Blob} chunk - The chunk to read
   * @returns {Promise<ArrayBuffer>}
   */
  async _readChunk(chunk) {
    return new Promise((resolve, reject) => {
      const reader = new FileReader();
      reader.onload = () => resolve(reader.result);
      reader.onerror = () => reject(reader.error);
      reader.readAsArrayBuffer(chunk);
    });
  }

  /**
   * Cancel (abort) an active upload
   * @param {string} uploadId - Unique identifier of the upload
   */
  cancelUpload(uploadId) {
    const uploader = this.activeUploads.get(uploadId);
    if (uploader) {
      uploader.abort();
      this.activeUploads.delete(uploadId);

      EventEmitter.emit(
        this.element,
        'upload-cancelled',
        DropBearUtils.createEvent(this.id, 'upload-cancelled', {uploadId})
      );
    }
  }

  /**
   * Cancel all active uploads
   */
  cancelAllUploads() {
    Array.from(this.activeUploads.keys()).forEach(uploadId =>
      this.cancelUpload(uploadId)
    );
  }

  /**
   * Dispose of the file upload manager and clean up resources
   */
  dispose() {
    if (this.isDisposed) return;

    logger.debug(`Disposing file upload manager ${this.id}`);
    this.isDisposed = true;

    // Cancel any active uploads
    this.cancelAllUploads();

    // Remove event listeners
    Object.entries(this.handlers).forEach(([event, handler]) =>
      this.element.removeEventListener(event, handler)
    );

    this.dotNetRef = null;
    EventEmitter.emit(this.element, 'disposed', DropBearUtils.createEvent(this.id, 'disposed', null));
  }
}

// Register with ModuleManager
ModuleManager.register(
  'DropBearFileUploader',
  {
    /** @type {Map<string, FileUploadManager>} */
    uploaders: new Map(),

    /**
     * Initialize the file uploader module
     * @returns {Promise<void>}
     */
    async initialize() {
      logger.debug('DropBearFileUploader module initialized');
    },

    /**
     * Create a new file upload manager
     * @param {string} elementId - Upload container element ID
     * @param {Object} dotNetRef - .NET reference
     */
    createUploader(elementId, dotNetRef) {
      try {
        if (this.uploaders.has(elementId)) {
          logger.warn(`Uploader already exists for ${elementId}, disposing old instance`);
          this.dispose(elementId);
        }

        const manager = new FileUploadManager(elementId, dotNetRef);
        this.uploaders.set(elementId, manager);
        logger.debug(`File uploader created for ID: ${elementId}`);
      } catch (error) {
        logger.error('File uploader creation error:', error);
        throw error;
      }
    },

    /**
     * Upload files to a specific uploader
     * @param {string} elementId - Uploader ID
     * @param {File[]} files - Files to upload
     * @returns {Promise<void>}
     */
    uploadFiles(elementId, files) {
      const manager = this.uploaders.get(elementId);
      return manager
        ? manager.uploadFiles(files)
        : Promise.reject(new Error(`Uploader not found for ID: ${elementId}`));
    },

    /**
     * Cancel an upload
     * @param {string} elementId - Uploader ID
     * @param {string} uploadId - Upload identifier
     */
    cancelUpload(elementId, uploadId) {
      const manager = this.uploaders.get(elementId);
      if (manager) {
        manager.cancelUpload(uploadId);
      }
    },

    /**
     * Dispose of an uploader
     * @param {string} elementId - Uploader ID
     */
    dispose(elementId) {
      const manager = this.uploaders.get(elementId);
      if (manager) {
        manager.dispose();
        this.uploaders.delete(elementId);
      }
    },

    /**
     * Dispose of all uploaders
     */
    disposeAll() {
      Array.from(this.uploaders.keys()).forEach(id => this.dispose(id));
      this.uploaders.clear();
    },
  },
  ['DropBearCore']
);

// Retrieve the registered uploader module
const dropBearFileUploaderModule = ModuleManager.get('DropBearFileUploader');

/**
 * Attach the file uploader methods to the window global,
 * making it easy to call from anywhere in your application.
 */
window.DropBearFileUploader = {
  initialize: () => dropBearFileUploaderModule.initialize(),
  createUploader: (elementId, dotNetRef) => dropBearFileUploaderModule.createUploader(elementId, dotNetRef),
  uploadFiles: (elementId, files) => dropBearFileUploaderModule.uploadFiles(elementId, files),
  cancelUpload: (elementId, uploadId) => dropBearFileUploaderModule.cancelUpload(elementId, uploadId),
  dispose: elementId => dropBearFileUploaderModule.dispose(elementId),
  disposeAll: () => dropBearFileUploaderModule.disposeAll(),
};

/**
 * Export these classes so they can be imported in other modules if desired
 */
export {FileUploadManager, ChunkUploader};
