import axios from "axios";
import { toast } from "./toast/toastBus";

const API_BASE_URL = import.meta.env.VITE_API_URL || "/api/qualite";

const api = axios.create({
  baseURL: API_BASE_URL,
});

api.interceptors.request.use((config) => {
  const token = localStorage.getItem("token");
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
});

api.interceptors.response.use(
  (res) => {
    const method = (res.config?.method || "get").toLowerCase();
    const wantsToast = res.config?.toast !== false;
    if (wantsToast && ["post", "put", "patch", "delete"].includes(method)) {
      const msg = res.config?.toastSuccessMessage;
      toast.success(msg || "Action effectuée avec succès");
    }
    return res;
  },
  (err) => {
    const status = err.response?.status;
    const method = (err.config?.method || "get").toLowerCase();
    const url = (err.config?.url || "").toString();
    const embed = new URLSearchParams(window.location.search).get("embed") === "1";

    const wantsToast = err.config?.toast !== false;
    if (wantsToast) {
      const serverMsg = err.response?.data?.message;
      const msg = err.config?.toastErrorMessage || serverMsg || "Une erreur est survenue";
      const shouldToast = ["post", "put", "patch", "delete"].includes(method) || err.config?.toastOnGet === true;
      if (shouldToast) toast.error(msg);
    }

    if (status === 401) {
      localStorage.removeItem("token");
      localStorage.removeItem("user");
      const isAuthRoute = url.includes("/auth/login") || url.includes("/auth/ping");
      if (!isAuthRoute && method !== "get") {
        toast.error("Session expirée. Merci de vous reconnecter.", { durationMs: 4200 });
        if (embed) {
          window.parent?.postMessage({ type: "KYNTUS_CQ_SESSION_EXPIRED" }, "*");
        } else {
          window.location.href = "/";
        }
      }
    }

    return Promise.reject(err);
  }
);

export default api;
