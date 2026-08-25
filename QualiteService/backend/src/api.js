// src/api.js
import axios from "axios";

// BACKEND URL (with /api)
const API_BASE_URL = import.meta.env.VITE_API_URL || "http://localhost:5000/api";

const api = axios.create({
  baseURL: API_BASE_URL,
});

// 🔒 JWT token injection
api.interceptors.request.use((config) => {
  const token = localStorage.getItem("token");
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
});

// 🔁 Auto logout if token invalid
// Patch: avoid hard redirect on background GET calls (can happen while user is filling a form)
// - Keep existing behavior for non-GET (save actions), so security/UX remains consistent.
// - Ignore /auth/login and /auth/ping to prevent redirect loops.
api.interceptors.response.use(
  (res) => res,
  (err) => {
    const status = err.response?.status;
    const method = (err.config?.method || "get").toLowerCase();
    const url = (err.config?.url || "").toString();

    if (status === 401) {
      // always clear stored auth (existing behavior)
      localStorage.removeItem("token");
      localStorage.removeItem("user");

      const isAuthRoute = url.includes("/auth/login") || url.includes("/auth/ping");

      // Only redirect on non-GET requests (user-triggered actions),
      // and avoid redirect loops on auth endpoints.
      if (!isAuthRoute && method !== "get") {
        window.location.href = "/";
      }
    }

    return Promise.reject(err);
  }
);

export default api;
