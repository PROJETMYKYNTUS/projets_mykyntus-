// src/socket.js
import { io } from "socket.io-client";

// BACKEND base without /api for socket.io default path (/socket.io)
const API_BASE_URL = import.meta.env.VITE_API_URL || "http://localhost:5000/api";
const SOCKET_URL = API_BASE_URL.replace(/\/api\/?$/, "");

let socket = null;

export function getSocket() {
  if (socket) return socket;

  const token = localStorage.getItem("token") || "";
  socket = io(SOCKET_URL, {
    autoConnect: true,
    transports: ["websocket", "polling"],
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
