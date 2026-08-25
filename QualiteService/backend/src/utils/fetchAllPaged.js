import api from "../api";

/**
 * Fetch all items from a paginated endpoint returning { items, total, page, limit }.
 * It will iterate pages until all items are retrieved or maxPages reached.
 */
export async function fetchAllPaged(endpoint, params = {}, { limit = 5000, maxPages = 100 } = {}) {
  const out = [];
  let page = 1;
  for (let i = 0; i < maxPages; i += 1) {
    const res = await api.get(endpoint, { params: { ...params, page, limit } });
    const data = res?.data || {};
    const items = Array.isArray(data.items) ? data.items : Array.isArray(data) ? data : [];
    out.push(...items);
    const total = Number(data.total);
    if (!Number.isFinite(total)) {
      if (items.length < limit) break;
    } else if (out.length >= total) {
      break;
    }
    page += 1;
  }
  return out;
}
