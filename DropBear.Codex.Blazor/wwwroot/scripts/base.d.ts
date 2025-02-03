// base.d.ts
export {};  // Makes this file a module

// Common Types
export type EventCallback = (data: any) => void;
export type EventUnsubscribe = () => void;
export type DotNetStreamReference = {
  arrayBuffer: () => Promise<ArrayBuffer>;
};

export type DownloadContent = Blob | ArrayBuffer | Uint8Array | DotNetStreamReference;

// Common Interfaces
export interface IInitializable {
  __initialized: boolean;
  initialize(): Promise<void>;
  isInitialized(): boolean;
}

export interface IDotNetReference {
  invokeMethodAsync<T>(methodName: string, ...args: any[]): Promise<T>;
}

export interface IManagerState {
  isDisposed: boolean;
  id: string;
}

export interface IProgressState extends IManagerState {
  progress: number;
  duration: number;
}

export interface IDropBearError extends Error {
  code: string;
  component?: string;
  details?: any;
}

export interface IDropBearEvent {
  id: string;
  type: string;
  data?: any;
}

export type DropBearEventHandler = (event: IDropBearEvent) => void;

// Core Interfaces
export interface ILogger {
  debug(message: string, ...args: any[]): void;
  info(message: string, ...args: any[]): void;
  warn(message: string, ...args: any[]): void;
  error(message: string, ...args: any[]): void;
}

export interface IDisposable {
  dispose(): void;
}

export interface IEventEmitter {
  on(target: object, event: string, callback: EventCallback): EventUnsubscribe;
  off(target: object, event: string, callback: EventCallback): void;
  emit(target: object, event: string, data?: any): void;
}

export interface IDOMOperationQueue {
  add(operation: () => void): void;
  flush(): void;
}

export interface IResourcePool extends IInitializable {
  instance: any;
  createPool(type: string, factory: () => any, options?: any): Promise<void>;
  acquire<T>(type: string): Promise<T | null>;
  release<T>(type: string, resource: T): Promise<boolean>;
  getSize(type: string): number;
  hasPool(type: string): boolean;
  getPoolConfig(type: string): any;
  clear(type: string): Promise<void>;
  clearAll(): Promise<void>;
  deletePool(type: string): Promise<boolean>;
  dispose(): void;
}

export interface IModuleManager {
  register<T>(name: string, module: T, dependencies?: string[]): void;
  initialize(moduleName: string): Promise<void>;
  waitForDependencies(dependencies: string[]): Promise<void>;
  get<T>(moduleName: string): T;
  isInitialized(moduleName: string): boolean;
  isInitializing(moduleName: string): boolean;
  dispose(moduleName: string): boolean;
  clear(): void;
}

export interface ICircuitBreakerOptions {
  failureThreshold?: number;
  resetTimeout?: number;
}

export interface ICircuitBreaker {
  readonly state: 'open' | 'closed' | 'half-open';
  execute<T>(operation: () => Promise<T>): Promise<T>;
  reset(): void;
}

// Manager Interfaces
export interface IManagerConstructor<T> {
  new(id: string, ...args: any[]): T;
}

export interface ISnackbarManager extends IInitializable {
  snackbars: Map<string, any>;
  createSnackbar(snackbarId: string, options?: any): Promise<void>;
  setDotNetReference(snackbarId: string, dotNetRef: IDotNetReference): Promise<boolean>;
  show(snackbarId: string, duration?: number): Promise<boolean>;
  updateContent(snackbarId: string, content: string): Promise<boolean>;
  startProgress(snackbarId: string, duration: number): boolean;
  hide(snackbarId: string): Promise<boolean>;
  dispose(snackbarId: string): void;
  disposeAll(): void;
}

export interface IResizeManager extends IInitializable {
  instance: any;
  createResizeManager(dotNetRef: IDotNetReference, options?: any): void;
  forceResize(): Promise<void>;
  getDimensions(): { width: number, height: number, scale: number };
  dispose(): void;
}

export interface INavigationManager extends IInitializable {
  instance: any;
  createNavigationManager(dotNetRef: IDotNetReference): void;
  scrollToTop(): void;
  goBack(): void;
  forceVisibilityUpdate(isVisible: boolean): Promise<void>;
  dispose(): void;
}

export interface IContextMenuManager extends IInitializable {
  menuInstances: Map<string, any>;
  createContextMenu(menuId: string, dotNetRef: IDotNetReference): void;
  show(menuId: string, x: number, y: number): Promise<void>;
  hide(menuId: string): Promise<void>;
  updateItems(menuId: string, items: any[]): Promise<void>;
  getState(menuId: string): any;
  dispose(menuId: string): void;
  disposeAll(): void;
}

export interface IValidationErrorsManager extends IInitializable {
  validationContainers: Map<string, any>;
  createValidationContainer(containerId: string, options?: any): void;
  updateErrors(containerId: string, errors: string[]): Promise<void>;
  updateAriaAttributes(containerId: string, isCollapsed: boolean): Promise<void>;
  show(containerId: string): Promise<void>;
  hide(containerId: string): Promise<void>;
  clearErrors(containerId: string): Promise<void>;
  getErrorCount(containerId: string): number;
  dispose(containerId: string): void;
  disposeAll(): void;
}

export interface IDownloadManager extends IInitializable {
  downloadManager: any;
  downloadFileFromStream(
    fileName: string,
    content: DownloadContent,
    contentType: string
  ): Promise<void>;
  getActiveDownloadCount(): number;
  dispose(): void;
}

export interface IPageAlertManager extends IInitializable {
  alerts: Map<string, any>;
  create(id: string, duration?: number, isPermanent?: boolean, options?: any): boolean;
  updateContent(id: string, content: string): Promise<boolean>;
  show(id: string): Promise<boolean>;
  hide(id: string): Promise<boolean>;
  hideAll(): Promise<boolean[]>;
  dispose(id: string): void;
  disposeAll(): void;
}

export interface IDropBearUtils extends IInitializable {
  createLogger(namespace: string): ILogger;
  debounce<T extends Function>(func: T, wait: number): T;
  throttle<T extends Function>(func: T, limit: number): T;
  validateArgs(args: any[], types: string[], functionName: string): void;
  isElement(element: any): element is Element | HTMLDocument;
}

// Global Window Interface
declare global {
  export interface Window {
    DropBearModuleManager: IModuleManager & IInitializable;
    DropBearUtils: IDropBearUtils;
    DropBearCore: IInitializable & {
      DOMOperationQueue: IDOMOperationQueue;
      EventEmitter: IEventEmitter;
      CircuitBreaker: new (options?: ICircuitBreakerOptions) => ICircuitBreaker;
    };
    DropBearSnackbar: ISnackbarManager;
    DropBearResizeManager: IResizeManager;
    DropBearNavigationButtons: INavigationManager;
    DropBearContextMenu: IContextMenuManager;
    DropBearValidationErrors: IValidationErrorsManager;
    DropBearFileDownloader: IDownloadManager;
    DropBearPageAlert: IPageAlertManager;
    DropBearFileReaderHelpers: IInitializable & {
      getFileInfo(file: File): { name: string; size: number; type: string; lastModified: number };
      readFileChunk(file: File, offset: number, count: number): Promise<Uint8Array>;
      getDroppedFiles(dataTransfer: DataTransfer): File[];
    };
    DropBearResourcePool: IResourcePool;
    Blazor: any;
  }
}
