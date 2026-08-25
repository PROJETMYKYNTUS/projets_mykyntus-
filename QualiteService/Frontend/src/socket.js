import { io } from "socket.io-client";

const API_BASE_URL = import.meta.env.VITE_API_URL || "/api/qualite";
const SOCKET_URL = API_BASE_URL.replace(/\/api\/qualite\/?$/, "") || window.location.origin;

let socket = null;

export function getSocket() {
  if (socket) return socket;

  const token = localStorage.getItem("token") || "";
  socket = io(SOCKET_URL, {
    autoConnect: true,
    transports: ["websocket", "polling"],
    path: "/socket.io",
    auth: { token },
  });

  return socket;
}

export function resetSocketAuth() {
  if (!socket) return;
  const token = localStorage.getItem("token") || "";
  socket.auth = { token };
  if (!socket.connected) socket.connect();
}
