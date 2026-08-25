import api from "../api";

/**
 * Fetch all items from a paginated source.
 *
 * Supported signatures:
 *  - fetchAllPaged("/endpoint", params, { limit, maxPages })
 *  - fetchAllPaged(async ({page,limit}) => ({ items, total }), { pageSize })   // legacy usage
 */
export async function fetchAllPaged(source, params = {}, opts = {}) {
  const out = [];

  // Backward compatibility: some screens pass (fn, {pageSize})
  const isFn = typeof source === "function";
  const options = isFn ? (params || {}) : (opts || {});
  const pageLimit = Number(options.pageSize ?? options.limit ?? 5000);
  const maxPages = Number(options.maxPages ?? 100);

  const fetchPage = isFn
    ? source
    : async ({ page, limit }) => {
        const res = await api.get(String(source), { params: { ...(params || {}), page, limit } });
        return res?.data;
      };

  let page = 1;
  for (let i = 0; i < maxPages; i += 1) {
    const payload = await fetchPage({ page, limit: pageLimit });

    const data = payload || {};
    const items = Array.isArray(data.items)
      ? data.items
      : Array.isArray(data)
        ? data
        : Array.isArray(data?.data)
          ? data.data
          : [];

    out.push(...items);

    const total = Number(data.total);
    if (!Number.isFinite(total)) {
      if (items.length < pageLimit) break;
    } else if (out.length >= total) {
      break;
    }

    page += 1;
  }

  return out;
}
