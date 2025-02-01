/**
 * @fileoverview Provides helper functions for reading files in a browser context
 * @module FileReaderHelpers
 */

/**
 * Get file info from a File object
 * @this {File}
 * @returns {{name: string, size: number, type: string, lastModified: number}}
 */
export function getFileInfo() {
  return {
    name: this.name,
    size: this.size,
    type: this.type,
    lastModified: this.lastModified,
  };
}

/**
 * Read a portion (chunk) of a File object
 * @this {File}
 * @param {number} offset - Starting byte index
 * @param {number} count - Number of bytes to read
 * @returns {Promise<Uint8Array>} The file chunk as a Uint8Array
 */
export async function readFileChunk(offset, count) {
  const blob = this.slice(offset, offset + count);
  const arrayBuffer = await blob.arrayBuffer();
  return new Uint8Array(arrayBuffer);
}

/**
 * Retrieve dropped files from a DataTransfer object (e.g., drag-and-drop)
 * @param {DataTransfer} dataTransfer - DataTransfer object from a drop event
 * @returns {File[]} An array of File objects
 */
export function getDroppedFiles(dataTransfer) {
  if (!dataTransfer?.items) {
    return [];
  }

  return Array.from(dataTransfer.items)
    .filter((item) => item.kind === 'file')
    .map((item) => item.getAsFile());
}
