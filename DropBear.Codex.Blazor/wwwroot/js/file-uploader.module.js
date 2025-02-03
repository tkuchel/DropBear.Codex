/**
 * @fileoverview File uploader module for handling file uploads with chunking support
 * @module file-uploader
 */

import {CircuitBreaker, DOMOperationQueue, EventEmitter} from './core.module.js';
import {DropBearUtils} from './utils.module.js';

const logger = DropBearUtils.createLogger('DropBearFileUploader');
const circuitBreaker = new CircuitBreaker({failureThreshold: 3, resetTimeout: 30000});
let isInitialized = false;

/** @type {Object} Upload configuration constants */
const UPLOAD_CONFIG = {
  CHUNK_SIZE: 1024 * 1024, // 1MB chunks
  MAX_CONCURRENT_CHUNKS: 3,
  RETRY_ATTEMPTS: 3,
  RETRY_DELAY: 1000 // 1 second
};

/**
 * Class to manage chunked file upload operations
 * @implements {IChunkUploader}
 */
class ChunkUploader {
  /**
   * @param {File} file - The file to upload
   * @param {number} [chunkSize=UPLOAD_CONFIG.CHUNK_SIZE] - Size of each chunk in bytes
   */
  constructor(file, chunkSize = UPLOAD_CONFIG.CHUNK_SIZE) {
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

    /** @type {boolean} */
    this.isDisposed = false;

    /** @type {Set<number>} */
    this.uploadedChunks = new Set();

    logger.debug('ChunkUploader created:', {
      fileName: file.name,
      fileSize: file.size,
      chunkSize: this.chunkSize,
      totalChunks: this.totalChunks
    });
  }

  /**
   * Get the next chunk of data for upload
   * @returns {Blob|null} Next chunk or null if complete
   */
  getNextChunk() {
    if (this.isDisposed || this.aborted || this.currentChunk >= this.totalChunks) {
      return null;
    }

    const start = this.currentChunk * this.chunkSize;
    const end = Math.min(start + this.chunkSize, this.file.size);
    const chunk = this.file.slice(start, end);
    this.currentChunk++;

    logger.debug('Getting next chunk:', {
      currentChunk: this.currentChunk,
      startByte: start,
      endByte: end,
      chunkSize: chunk.size
    });

    return chunk;
  }

  /**
   * Get upload progress as a percentage
   * @returns {number} Progress percentage
   */
  getProgress() {
    const progress = (this.uploadedChunks.size / this.totalChunks) * 100;
    return Math.round(progress * 100) / 100; // Round to 2 decimal places
  }

  /**
   * Mark a chunk as successfully uploaded
   * @param {number} chunkIndex - The index of the uploaded chunk
   */
  markChunkUploaded(chunkIndex) {
    this.uploadedChunks.add(chunkIndex);
    logger.debug('Chunk marked as uploaded:', {
      chunkIndex,
      progress: this.getProgress()
    });
  }

  /**
   * Reset the uploader to its initial state
   */
  reset() {
    this.currentChunk = 0;
    this.aborted = false;
    this.uploadedChunks.clear();
    logger.debug('ChunkUploader reset');
  }

  /**
   * Abort the upload
   */
  abort() {
    this.aborted = true;
    logger.debug('ChunkUploader aborted');
  }

  /**
   * Dispose of the chunk uploader
   */
  dispose() {
    if (this.isDisposed) return;

    this.isDisposed = true;
    this.aborted = true;
    this.uploadedChunks.clear();
    logger.debug('ChunkUploader disposed');
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

    /** @type {Map<string, ChunkUploader>} */
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

    logger.debug('FileUploadManager created:', {id});
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
      drop: e => this._handleDrop(e)
    };

    Object.entries(handlers).forEach(([event, handler]) => this.element.addEventListener(event, handler.bind(this)));

    // Store references for cleanup
    this.handlers = handlers;
    logger.debug('Event listeners initialized');
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
      DOMOperationQueue.add(() => this.element.classList.add('dragover'));

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
      DOMOperationQueue.add(() => this.element.classList.remove('dragover'));

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

    DOMOperationQueue.add(() => this.element.classList.remove('dragover'));

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
      logger.debug('Starting upload of multiple files:', {count: files.length});
      const uploadPromises = files.map(file => this.uploadFile(file));
      await Promise.all(uploadPromises);
      logger.debug('All files uploaded successfully');
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
      logger.debug('Starting single file upload:', {
        uploadId,
        fileName: file.name,
        fileSize: file.size
      });

      // Notify upload start
      await circuitBreaker.execute(() =>
        this.dotNetRef.invokeMethodAsync('OnUploadStart', {
          id: uploadId,
          fileName: file.name,
          size: file.size,
          type: file.type
        })
      );

      const uploader = new ChunkUploader(file);
      this.activeUploads.set(uploadId, uploader);

      await this._processChunks(uploadId, uploader);

      // Notify completion
      await circuitBreaker.execute(() =>
        this.dotNetRef.invokeMethodAsync('OnUploadComplete', uploadId)
      );

      EventEmitter.emit(
        this.element,
        'upload-complete',
        DropBearUtils.createEvent(this.id, 'upload-complete', {
          uploadId,
          fileName: file.name
        })
      );

      logger.debug('File upload completed:', {uploadId, fileName: file.name});
    } catch (error) {
      logger.error(`Error uploading file ${file.name}:`, error);

      await circuitBreaker.execute(() =>
        this.dotNetRef.invokeMethodAsync('OnUploadError', uploadId, error.message)
      );

      throw error;
    } finally {
      this.activeUploads.delete(uploadId);
    }
  }

  /**
   * Process file chunks with retry logic
   * @private
   * @param {string} uploadId - Unique upload identifier
   * @param {ChunkUploader} uploader - The ChunkUploader instance
   * @returns {Promise<void>}
   */
  async _processChunks(uploadId, uploader) {
    let chunk;
    let retryCount = 0;

    while ((chunk = uploader.getNextChunk()) !== null && !uploader.aborted) {
      try {
        const chunkIndex = uploader.currentChunk - 1;
        const chunkData = await this._readChunk(chunk);

        await circuitBreaker.execute(() =>
          this.dotNetRef.invokeMethodAsync('OnChunkUpload', uploadId, {
            data: chunkData,
            index: chunkIndex,
            total: uploader.totalChunks
          })
        );

        uploader.markChunkUploaded(chunkIndex);

        // Update progress
        await circuitBreaker.execute(() =>
          this.dotNetRef.invokeMethodAsync('OnUploadProgress', uploadId, uploader.getProgress())
        );

        retryCount = 0;
      } catch (error) {
        if (retryCount < UPLOAD_CONFIG.RETRY_ATTEMPTS) {
          retryCount++;
          logger.warn('Retrying chunk upload:', {
            uploadId,
            chunkIndex: uploader.currentChunk - 1,
            attempt: retryCount
          });

          // Move back one chunk to retry
          uploader.currentChunk--;
          await new Promise(resolve => setTimeout(resolve, UPLOAD_CONFIG.RETRY_DELAY));
          continue;
        }
        throw error;
      }
    }

    if (uploader.aborted) {
      throw new Error('Upload aborted');
    }
  }

  /**
   * Read chunk data as ArrayBuffer
   * @private
   * @param {Blob} chunk - The chunk to read
   * @returns {Promise<ArrayBuffer>}
   */
  async _readChunk(chunk) {
    try {
      return await chunk.arrayBuffer();
    } catch (error) {
      logger.error('Error reading chunk:', error);
      throw error;
    }
  }

  /**
   * Cancel an active upload
   * @param {string} uploadId - Unique identifier of the upload
   */
  cancelUpload(uploadId) {
    const uploader = this.activeUploads.get(uploadId);
    if (uploader) {
      uploader.abort();
      uploader.dispose();
      this.activeUploads.delete(uploadId);

      EventEmitter.emit(
        this.element,
        'upload-cancelled',
        DropBearUtils.createEvent(this.id, 'upload-cancelled', {uploadId})
      );

      logger.debug('Upload cancelled:', {uploadId});
    }
  }

  /**
   * Cancel all active uploads
   */
  cancelAllUploads() {
    Array.from(this.activeUploads.keys()).forEach(uploadId => this.cancelUpload(uploadId));
    logger.debug('All uploads cancelled');
  }

  /**
   * Get current upload status
   * @returns {{ activeUploads: number, totalProgress: number }}
   */
  getUploadStatus() {
    const activeUploads = this.activeUploads.size;
    let totalProgress = 0;

    if (activeUploads > 0) {
      const progressSum = Array.from(this.activeUploads.values())
        .reduce((sum, uploader) => sum + uploader.getProgress(), 0);
      totalProgress = progressSum / activeUploads;
    }

    return {activeUploads, totalProgress};
  }

  /**
   * Dispose of the file upload manager
   */
  dispose() {
    if (this.isDisposed) return;

    logger.debug(`Disposing file upload manager ${this.id}`);
    this.isDisposed = true;

    // Cancel any active uploads
    this.cancelAllUploads();

    // Remove event listeners
    Object.entries(this.handlers).forEach(([event, handler]) => this.element.removeEventListener(event, handler));

    this.dotNetRef = null;

    EventEmitter.emit(
      this.element,
      'disposed',
      DropBearUtils.createEvent(this.id, 'disposed', null)
    );
  }
}

// Attach to window first
window["file-uploader"] = {
  __initialized: false,
  uploaders: new Map(),

  initialize: async () => {
    if (isInitialized) {
      return;
    }

    try {
      logger.debug('File uploader module initializing');

      // Initialize dependencies first
      await window.DropBearUtils.initialize();
      await window.DropBearCore.initialize();

      isInitialized = true;
      window["file-uploader"].__initialized = true;

      logger.debug('File uploader module initialized');
    } catch (error) {
      logger.error('File uploader initialization failed:', error);
      throw error;
    }
  },

  createUploader: (elementId, dotNetRef) => {
    try {
      if (!isInitialized) {
        throw new Error('Module not initialized');
      }

      if (window["file-uploader"].uploaders.has(elementId)) {
        logger.warn(`Uploader already exists for ${elementId}, disposing old instance`);
        window["file-uploader"].dispose(elementId);
      }

      const manager = new FileUploadManager(elementId, dotNetRef);
      window["file-uploader"].uploaders.set(elementId, manager);
      logger.debug(`File uploader created for ID: ${elementId}`);
    } catch (error) {
      logger.error('File uploader creation error:', error);
      throw error;
    }
  },

  uploadFiles: async (elementId, files) => {
    const manager = window["file-uploader"].uploaders.get(elementId);
    if (!manager) {
      const error = new Error(`Uploader not found for ID: ${elementId}`);
      logger.error('Upload error:', error);
      throw error;
    }
    return manager.uploadFiles(files);
  },

  cancelUpload: (elementId, uploadId) => {
    const manager = window["file-uploader"].uploaders.get(elementId);
    if (manager) {
      manager.cancelUpload(uploadId);
      logger.debug(`Upload cancelled: ${uploadId}`);
    }
  },

  getUploadStatus: elementId => {
    const manager = window["file-uploader"].uploaders.get(elementId);
    return manager ? manager.getUploadStatus() : null;
  },

  isInitialized: () => isInitialized,

  dispose: elementId => {
    const manager = window["file-uploader"].uploaders.get(elementId);
    if (manager) {
      manager.dispose();
      window["file-uploader"].uploaders.delete(elementId);
      logger.debug(`Uploader disposed for ID: ${elementId}`);
    }
  },

  disposeAll: () => {
    Array.from(window["file-uploader"].uploaders.keys()).forEach(id =>
      window["file-uploader"].dispose(id)
    );
    window["file-uploader"].uploaders.clear();
    isInitialized = false;
    window["file-uploader"].__initialized = false;
    logger.debug('All uploaders disposed');
  }
};

// Register with ModuleManager after window attachment
window.DropBearModuleManager.register(
  'file-uploader',
  {
    initialize: () => window["file-uploader"].initialize(),
    isInitialized: () => window["file-uploader"].isInitialized(),
    dispose: () => window["file-uploader"].disposeAll()
  },
  ['DropBearUtils', 'DropBearCore']
);

// Export FileUploadManager and ChunkUploader classes
export {FileUploadManager, ChunkUploader};
