// base.d.ts
export {};  // Makes this file a module

// Common Types
export type EventCallback = (data: any) => void;
export type EventUnsubscribe = () => void;

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

export interface IResourcePool {
  create(type: string, factory: () => any, initialSize?: number): void;

  acquire<T>(type: string): T | null;

  release<T>(type: string, resource: T): void;
}

export interface IModuleManager {
  register<T>(name: string, module: T, dependencies?: string[]): void;

  initialize(moduleName: string): Promise<void>;

  get<T>(moduleName: string): T;
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

export interface ISnackbarManager extends IDisposable {
  show(): Promise<boolean>;

  startProgress(duration: number): void;

  hide(): Promise<boolean>;
}

export interface IResizeManager extends IDisposable {
  SetMaxWidthBasedOnWindowSize(): Promise<void>;
}

export interface INavigationManager extends IDisposable {
  scrollToTop(): void;

  goBack(): void;

  UpdateVisibility(isVisible: boolean): Promise<void>;
}

export interface IContextMenuManager extends IDisposable {
  show(x: number, y: number): Promise<void>;

  hide(): Promise<void>;
}

export interface IValidationErrorsManager extends IDisposable {
  updateAriaAttributes(isCollapsed: boolean): Promise<void>;
}

export interface IProgressBarManager extends IDisposable {
  updateProgress(taskProgress: number, overallProgress: number): boolean;

  updateStepDisplay(currentIndex: number, totalSteps: number): boolean;

  smoothProgressUpdate(targetProgress: number): Promise<void>;
}

export interface IDownloadManager extends IDisposable {
  downloadFileFromStream(fileName: string, content: Blob | ArrayBuffer | Uint8Array, contentType: string): Promise<void>;
}

export interface IPageAlertManager extends IDisposable {
  show(): Promise<boolean>;

  hide(): Promise<boolean>;
}

export interface IDropBearUtils {
  createLogger(namespace: string): ILogger;

  debounce<T extends Function>(func: T, wait: number): T;

  throttle<T extends Function>(func: T, limit: number): T;

  validateArgs(args: any[], types: string[], functionName: string): void;

  isElement(element: any): element is Element | HTMLDocument;
}

// Global Window Interface
declare global {
  export interface Window {
    DropBearUtils: IDropBearUtils;

    DropBearSnackbar: {
      initialize(snackbarId: string): Promise<void>;
      show(snackbarId: string): Promise<boolean>;
      startProgress(snackbarId: string, duration: number): boolean;
      dispose(snackbarId: string): void;
    };

    DropBearResizeManager: {
      initialize(dotNetRef: IDotNetReference): void;
      dispose(): void;
    };

    DropBearNavigationButtons: {
      initialize(dotNetRef: IDotNetReference): void;
      scrollToTop(): void;
      goBack(): void;
      dispose(): void;
    };

    DropBearContextMenu: {
      initialize(menuId: string, dotNetRef: IDotNetReference): void;
      show(menuId: string, x: number, y: number): Promise<void>;
      dispose(menuId: string): void;
      disposeAll(): void;
    };

    DropBearValidationErrors: {
      initialize(containerId: string): void;
      updateAriaAttributes(containerId: string, isCollapsed: boolean): Promise<void>;
      dispose(containerId: string): void;
      disposeAll(): void;
    };

    DropBearFileDownloader: {
      downloadFileFromStream(fileName: string, content: Blob | ArrayBuffer | Uint8Array, contentType: string): Promise<void>;
    };

    DropBearPageAlert: {
      create(id: string, duration?: number, isPermanent?: boolean): boolean;
      hide(id: string): Promise<boolean>;
      hideAll(): Promise<boolean[]>;
    };

    DropBearProgressBar: {
      initialize(progressId: string, dotNetRef: IDotNetReference): boolean;
      updateProgress(progressId: string, taskProgress: number, overallProgress: number): boolean;
      updateStepDisplay(progressId: string, currentIndex: number, totalSteps: number): boolean;
      dispose(progressId: string): void;
      disposeAll(): void;
    };

    Blazor: any;
  }
}
