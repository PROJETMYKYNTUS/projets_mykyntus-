import { useEffect, useRef } from "react";
import { getSocket } from "../socket.js";

// Simple realtime refresh hook for CQ/Management score lists & dashboards.
// It listens to a global `scores:changed` event and triggers the provided callback.
// Debounced to avoid refresh storms when multiple items are saved in a row.
export default function useRealtimeScores(onRefresh, { enabled = true } = {}) {
  const cbRef = useRef(onRefresh);
  cbRef.current = onRefresh;
  const timerRef = useRef(null);

  useEffect(() => {
    if (!enabled || typeof cbRef.current !== "function") return;

    const socket = getSocket();

    const handler = () => {
      // debounce 250ms
      if (timerRef.current) clearTimeout(timerRef.current);
      timerRef.current = setTimeout(() => {
        try {
          cbRef.current?.();
        } catch (_) {
          // ignore
        }
      }, 250);
    };

    socket.on("scores:changed", handler);

    return () => {
      socket.off("scores:changed", handler);
      if (timerRef.current) clearTimeout(timerRef.current);
    };
  }, [enabled]);
}
