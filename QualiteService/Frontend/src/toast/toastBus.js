// src/toast/toastBus.js
// A tiny global bridge so non-React modules (axios interceptors, utils) can trigger toasts.

let toastApi = null;

export function setToastApi(api) {
  toastApi = api;
}

export const toast = {
  success: (message, opts) => toastApi?.success?.(message, opts),
  error: (message, opts) => toastApi?.error?.(message, opts),
  info: (message, opts) => toastApi?.info?.(message, opts),
  warning: (message, opts) => toastApi?.warning?.(message, opts),
  push: (t) => toastApi?.push?.(t),
};
