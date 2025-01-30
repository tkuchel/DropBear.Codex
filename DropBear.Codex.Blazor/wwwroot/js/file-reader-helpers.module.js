// FileReaderHelpers.module.js
export function getFileInfo() {
  return {
    name: this.name,
    size: this.size,
    type: this.type,
    lastModified: this.lastModified
  };
}

export async function readFileChunk(offset, count) {
  const blob = this.slice(offset, offset + count);
  const arrayBuffer = await blob.arrayBuffer();
  return new Uint8Array(arrayBuffer);
}

export function getDroppedFiles(dataTransfer) {
  if (!dataTransfer?.items) {
    return [];
  }

  return Array.from(dataTransfer.items)
    .filter(item => item.kind === 'file')
    .map(item => item.getAsFile());
}
