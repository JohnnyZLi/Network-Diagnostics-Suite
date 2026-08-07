export type BridgeEventName =
  | 'diagnostic.progress'
  | 'diagnostic.completed'
  | 'diagnostic.cancelled'
  | 'diagnostic.failed';

type BridgeResponse<T> = {
  type: 'response';
  id?: string | null;
  ok: boolean;
  payload?: T;
  error?: string | null;
};

type BridgeEvent<T> = {
  type: 'event';
  event: BridgeEventName;
  payload: T;
};

type BridgeMessage<T = unknown> = BridgeResponse<T> | BridgeEvent<T>;

type PendingRequest = {
  resolve: (value: unknown) => void;
  reject: (error: Error) => void;
  timeout: number;
};

type PhotinoExternal = {
  sendMessage?: (message: string) => void;
  receiveMessage?: (callback: (message: string) => void) => void;
};

type EventListener = (payload: unknown) => void;

class DesktopBridgeClient {
  private readonly pending = new Map<string, PendingRequest>();
  private readonly listeners = new Map<BridgeEventName, Set<EventListener>>();
  private sequence = 0;
  readonly available: boolean;

  constructor() {
    const external = window.external as unknown as PhotinoExternal;
    this.available = typeof external?.sendMessage === 'function'
      && typeof external?.receiveMessage === 'function';

    if (this.available) {
      external.receiveMessage?.((message) => this.receive(message));
    }
  }

  request<T>(method: string, payload: Record<string, unknown> = {}): Promise<T> {
    if (!this.available) {
      return Promise.reject(new Error('Photino host bridge is not available.'));
    }

    const id = `ui-${Date.now()}-${++this.sequence}`;
    const external = window.external as unknown as PhotinoExternal;

    return new Promise<T>((resolve, reject) => {
      const timeout = window.setTimeout(() => {
        this.pending.delete(id);
        reject(new Error(`Desktop bridge request timed out: ${method}`));
      }, 15_000);

      this.pending.set(id, {
        resolve: (value) => resolve(value as T),
        reject,
        timeout,
      });

      external.sendMessage?.(JSON.stringify({ id, method, payload }));
    });
  }

  on<T>(eventName: BridgeEventName, listener: (payload: T) => void): () => void {
    const bucket = this.listeners.get(eventName) ?? new Set<EventListener>();
    const wrapped: EventListener = (payload) => listener(payload as T);
    bucket.add(wrapped);
    this.listeners.set(eventName, bucket);

    return () => {
      bucket.delete(wrapped);
      if (bucket.size === 0) this.listeners.delete(eventName);
    };
  }

  private receive(rawMessage: string): void {
    let message: BridgeMessage;
    try {
      message = JSON.parse(rawMessage) as BridgeMessage;
    } catch {
      return;
    }

    if (message.type === 'response') {
      if (!message.id) return;
      const pending = this.pending.get(message.id);
      if (!pending) return;

      window.clearTimeout(pending.timeout);
      this.pending.delete(message.id);
      if (message.ok) pending.resolve(message.payload);
      else pending.reject(new Error(message.error || 'Desktop bridge request failed.'));
      return;
    }

    const bucket = this.listeners.get(message.event);
    if (!bucket) return;
    for (const listener of bucket) listener(message.payload);
  }
}

export const desktopBridge = new DesktopBridgeClient();
