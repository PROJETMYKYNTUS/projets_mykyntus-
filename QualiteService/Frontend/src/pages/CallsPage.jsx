import React, { useEffect, useMemo, useRef, useState } from "react";
import api from "../api.js";

function formatSeconds(seconds) {
  if (!Number.isFinite(seconds)) return "00:00";
  const mins = Math.floor(seconds / 60);
  const secs = Math.floor(seconds % 60);
  return `${String(mins).padStart(2, "0")}:${String(secs).padStart(2, "0")}`;
}

function formatDateTimeRaw(raw) {
  const match = /^(\d{4})-(\d{2})-(\d{2})-(\d{2})-(\d{2})-(\d{2})$/.exec(raw || "");
  if (!match) return raw || "";
  const [, y, m, d, hh, mm, ss] = match;
  return `${d}/${m}/${y} ${hh}:${mm}:${ss}`;
}

export default function CallsPage() {
  const [dates, setDates] = useState([]);
  const [selectedDate, setSelectedDate] = useState("");
  const [search, setSearch] = useState("");
  const [calls, setCalls] = useState([]);
  const [loadingDates, setLoadingDates] = useState(false);
  const [loadingCalls, setLoadingCalls] = useState(false);
  const [error, setError] = useState("");

  const [currentCall, setCurrentCall] = useState(null);
  const [isPlaying, setIsPlaying] = useState(false);
  const [currentTime, setCurrentTime] = useState(0);
  const [duration, setDuration] = useState(0);

  const audioRef = useRef(null);

  async function loadDates() {
    try {
      setLoadingDates(true);
      setError("");
      const res = await api.get("/calls/dates", { toast: false });
      const data = res.data;
      const safeDates = Array.isArray(data) ? data : [];
      setDates(safeDates);
      if (safeDates.length > 0 && !selectedDate) {
        setSelectedDate(safeDates[0]);
      }
    } catch (e) {
      setError(e.message || "Erreur chargement des dates.");
    } finally {
      setLoadingDates(false);
    }
  }

  async function loadCalls(date, searchValue = "") {
    if (!date) return;
    try {
      setLoadingCalls(true);
      setError("");

      const res = await api.get("/calls", {
        params: { date, search: searchValue || "" },
        toast: false,
      });

      const data = res.data;
      setCalls(Array.isArray(data) ? data : []);
    } catch (e) {
      setError(e.message || "Erreur chargement des appels.");
      setCalls([]);
    } finally {
      setLoadingCalls(false);
    }
  }

  useEffect(() => {
    loadDates();
  }, []);

  useEffect(() => {
    if (selectedDate) {
      loadCalls(selectedDate, search);
    }
  }, [selectedDate]);

  useEffect(() => {
    const audio = audioRef.current;
    if (!audio) return;

    const onLoadedMetadata = () => setDuration(audio.duration || 0);
    const onTimeUpdate = () => setCurrentTime(audio.currentTime || 0);
    const onPlay = () => setIsPlaying(true);
    const onPause = () => setIsPlaying(false);
    const onEnded = () => setIsPlaying(false);

    audio.addEventListener("loadedmetadata", onLoadedMetadata);
    audio.addEventListener("timeupdate", onTimeUpdate);
    audio.addEventListener("play", onPlay);
    audio.addEventListener("pause", onPause);
    audio.addEventListener("ended", onEnded);

    return () => {
      audio.removeEventListener("loadedmetadata", onLoadedMetadata);
      audio.removeEventListener("timeupdate", onTimeUpdate);
      audio.removeEventListener("play", onPlay);
      audio.removeEventListener("pause", onPause);
      audio.removeEventListener("ended", onEnded);
    };
  }, [currentCall]);

  const currentAudioUrl = useMemo(() => {
    if (!currentCall?.streamUrl) return "";
    const token = localStorage.getItem("token") || "";
    const path = currentCall.streamUrl.startsWith("http")
      ? currentCall.streamUrl
      : `/api/qualite${currentCall.streamUrl.startsWith("/") ? "" : "/"}${currentCall.streamUrl}`;
    return token ? `${path}${path.includes("?") ? "&" : "?"}access_token=${encodeURIComponent(token)}` : path;
  }, [currentCall]);

  function handleSelectCall(call) {
    setCurrentCall(call);
    setCurrentTime(0);
    setDuration(0);
    setIsPlaying(false);

    setTimeout(() => {
      if (audioRef.current) {
        audioRef.current.load();
      }
    }, 0);
  }

  function togglePlayPause() {
    const audio = audioRef.current;
    if (!audio || !currentCall) return;

    if (audio.paused) {
      audio.play().catch(() => {});
    } else {
      audio.pause();
    }
  }

  function skip(seconds) {
    const audio = audioRef.current;
    if (!audio) return;

    const next = Math.max(0, Math.min((audio.currentTime || 0) + seconds, duration || 0));
    audio.currentTime = next;
    setCurrentTime(next);
  }

  function handleSeek(e) {
    const audio = audioRef.current;
    if (!audio) return;

    const next = Number(e.target.value || 0);
    audio.currentTime = next;
    setCurrentTime(next);
  }

  async function handleSearchSubmit(e) {
    e.preventDefault();
    await loadCalls(selectedDate, search);
  }

  return (
    <div style={styles.page}>
      <div style={styles.header}>
        <div>
          <h1 style={styles.title}>Appels enregistrés</h1>
          <p style={styles.subtitle}>
            Consultation et écoute des enregistrements stockés sur le NAS.
          </p>
        </div>
      </div>

      <div style={styles.grid}>
        <aside style={styles.sidebar}>
          <div style={styles.card}>
            <div style={styles.cardTitle}>Filtres</div>

            <label style={styles.label}>Date</label>
            <select
              style={styles.select}
              value={selectedDate}
              onChange={(e) => setSelectedDate(e.target.value)}
              disabled={loadingDates}
            >
              <option value="">-- choisir une date --</option>
              {dates.map((date) => (
                <option key={date} value={date}>
                  {date}
                </option>
              ))}
            </select>

            <form onSubmit={handleSearchSubmit} style={{ marginTop: 16 }}>
              <label style={styles.label}>Recherche numéro</label>
              <div style={styles.searchRow}>
                <input
                  style={styles.input}
                  type="text"
                  placeholder="Ex: 0180432030"
                  value={search}
                  onChange={(e) => setSearch(e.target.value)}
                />
                <button type="submit" style={styles.searchBtn}>
                  Rechercher
                </button>
              </div>
            </form>

            <button
              type="button"
              style={styles.refreshBtn}
              onClick={() => {
                loadDates();
                if (selectedDate) loadCalls(selectedDate, search);
              }}
            >
              Actualiser
            </button>

            <div style={styles.metaBox}>
              <div><strong>Dates :</strong> {loadingDates ? "chargement..." : dates.length}</div>
              <div><strong>Appels :</strong> {loadingCalls ? "chargement..." : calls.length}</div>
            </div>
          </div>

          <div style={styles.card}>
            <div style={styles.cardTitle}>Liste des appels</div>

            {loadingCalls ? (
              <div style={styles.emptyState}>Chargement des appels...</div>
            ) : calls.length === 0 ? (
              <div style={styles.emptyState}>Aucun appel trouvé pour ce filtre.</div>
            ) : (
              <div style={styles.list}>
                {calls.map((call) => {
                  const active =
                    currentCall?.filename === call.filename &&
                    currentCall?.date === call.date;

                  return (
                    <button
                      key={`${call.date}-${call.filename}`}
                      type="button"
                      onClick={() => handleSelectCall(call)}
                      style={{
                        ...styles.callItem,
                        ...(active ? styles.callItemActive : {}),
                      }}
                    >
                      <div style={styles.callLineTop}>
                        <span style={styles.callStrong}>{call.caller || "-"}</span>
                        <span style={styles.arrow}>→</span>
                        <span style={styles.callStrong}>{call.callee || "-"}</span>
                      </div>

                      <div style={styles.callLineBottom}>
                        <span>{formatDateTimeRaw(call.datetimeRaw)}</span>
                      </div>

                      {call.source ? (
                        <div style={styles.callSource}>{call.source}</div>
                      ) : null}
                    </button>
                  );
                })}
              </div>
            )}
          </div>
        </aside>

        <main style={styles.main}>
          <div style={styles.card}>
            <div style={styles.cardTitle}>Lecteur audio</div>

            {!currentCall ? (
              <div style={styles.playerEmpty}>
                Sélectionne un appel dans la liste pour lancer l’écoute.
              </div>
            ) : (
              <>
                <div style={styles.playerInfo}>
                  <div style={styles.playerInfoRow}>
                    <span style={styles.infoLabel}>Appel :</span>
                    <span>
                      <strong>{currentCall.caller || "-"}</strong> →{" "}
                      <strong>{currentCall.callee || "-"}</strong>
                    </span>
                  </div>
                  <div style={styles.playerInfoRow}>
                    <span style={styles.infoLabel}>Date/heure :</span>
                    <span>{formatDateTimeRaw(currentCall.datetimeRaw)}</span>
                  </div>
                  <div style={styles.playerInfoRow}>
                    <span style={styles.infoLabel}>Fichier :</span>
                    <span style={styles.filename}>{currentCall.filename}</span>
                  </div>
                </div>

                <audio ref={audioRef} preload="metadata">
                  <source src={currentAudioUrl} />
                </audio>

                <div style={styles.controls}>
                  <button style={styles.controlBtn} onClick={() => skip(-10)} type="button">
                    ⏪ -10s
                  </button>

                  <button style={styles.playBtn} onClick={togglePlayPause} type="button">
                    {isPlaying ? "Pause" : "Play"}
                  </button>

                  <button style={styles.controlBtn} onClick={() => skip(10)} type="button">
                    +10s ⏩
                  </button>
                </div>

                <div style={styles.seekBox}>
                  <span style={styles.timeText}>{formatSeconds(currentTime)}</span>

                  <input
                    type="range"
                    min="0"
                    max={Math.max(duration, 0)}
                    step="0.1"
                    value={Math.min(currentTime, duration || 0)}
                    onChange={handleSeek}
                    style={styles.range}
                  />

                  <span style={styles.timeText}>{formatSeconds(duration)}</span>
                </div>

                <div style={styles.nativePlayerBox}>
                  <audio controls style={styles.nativePlayer}>
                    <source src={currentAudioUrl} />
                  </audio>
                </div>
              </>
            )}
          </div>

          {error ? <div style={styles.errorBox}>{error}</div> : null}
        </main>
      </div>
    </div>
  );
}

const styles = {
  page: {
    padding: 24,
    background: "#f6f8fb",
    minHeight: "100vh",
    color: "#1f2937",
  },
  header: {
    marginBottom: 20,
  },
  title: {
    margin: 0,
    fontSize: 28,
    fontWeight: 700,
  },
  subtitle: {
    margin: "6px 0 0",
    color: "#6b7280",
    fontSize: 14,
  },
  grid: {
    display: "grid",
    gridTemplateColumns: "380px 1fr",
    gap: 20,
  },
  sidebar: {
    display: "flex",
    flexDirection: "column",
    gap: 20,
  },
  main: {
    display: "flex",
    flexDirection: "column",
    gap: 20,
  },
  card: {
    background: "#fff",
    borderRadius: 14,
    padding: 18,
    boxShadow: "0 8px 24px rgba(15, 23, 42, 0.06)",
    border: "1px solid #e5e7eb",
  },
  cardTitle: {
    fontSize: 16,
    fontWeight: 700,
    marginBottom: 14,
  },
  label: {
    display: "block",
    fontSize: 13,
    marginBottom: 6,
    color: "#374151",
    fontWeight: 600,
  },
  select: {
    width: "100%",
    height: 42,
    borderRadius: 10,
    border: "1px solid #d1d5db",
    padding: "0 12px",
    fontSize: 14,
    background: "#fff",
  },
  input: {
    flex: 1,
    height: 42,
    borderRadius: 10,
    border: "1px solid #d1d5db",
    padding: "0 12px",
    fontSize: 14,
    background: "#fff",
  },
  searchRow: {
    display: "flex",
    gap: 8,
  },
  searchBtn: {
    height: 42,
    border: "none",
    borderRadius: 10,
    padding: "0 14px",
    background: "#2563eb",
    color: "#fff",
    cursor: "pointer",
    fontWeight: 600,
  },
  refreshBtn: {
    marginTop: 12,
    width: "100%",
    height: 42,
    border: "1px solid #d1d5db",
    borderRadius: 10,
    background: "#f9fafb",
    cursor: "pointer",
    fontWeight: 600,
  },
  metaBox: {
    marginTop: 14,
    padding: 12,
    background: "#f9fafb",
    borderRadius: 10,
    fontSize: 13,
    color: "#4b5563",
    display: "flex",
    flexDirection: "column",
    gap: 6,
  },
  list: {
    display: "flex",
    flexDirection: "column",
    gap: 10,
    maxHeight: "70vh",
    overflowY: "auto",
    paddingRight: 4,
  },
  callItem: {
    border: "1px solid #e5e7eb",
    background: "#fff",
    borderRadius: 12,
    padding: 12,
    textAlign: "left",
    cursor: "pointer",
  },
  callItemActive: {
    border: "1px solid #2563eb",
    background: "#eff6ff",
  },
  callLineTop: {
    display: "flex",
    alignItems: "center",
    gap: 8,
    fontSize: 14,
  },
  callStrong: {
    fontWeight: 700,
  },
  arrow: {
    color: "#6b7280",
  },
  callLineBottom: {
    marginTop: 6,
    fontSize: 12,
    color: "#6b7280",
  },
  callSource: {
    marginTop: 6,
    fontSize: 12,
    color: "#2563eb",
    fontWeight: 600,
  },
  emptyState: {
    padding: 18,
    borderRadius: 10,
    background: "#f9fafb",
    color: "#6b7280",
    fontSize: 14,
    textAlign: "center",
  },
  playerEmpty: {
    minHeight: 220,
    border: "2px dashed #dbeafe",
    borderRadius: 12,
    display: "flex",
    alignItems: "center",
    justifyContent: "center",
    color: "#6b7280",
    background: "#f9fbff",
    textAlign: "center",
    padding: 20,
  },
  playerInfo: {
    display: "flex",
    flexDirection: "column",
    gap: 10,
    marginBottom: 18,
    padding: 14,
    borderRadius: 12,
    background: "#f9fafb",
  },
  playerInfoRow: {
    display: "flex",
    gap: 8,
    alignItems: "center",
    flexWrap: "wrap",
    fontSize: 14,
  },
  infoLabel: {
    minWidth: 80,
    color: "#6b7280",
    fontWeight: 600,
  },
  filename: {
    wordBreak: "break-all",
  },
  controls: {
    display: "flex",
    justifyContent: "center",
    gap: 12,
    marginBottom: 16,
  },
  controlBtn: {
    height: 42,
    minWidth: 100,
    border: "1px solid #d1d5db",
    borderRadius: 999,
    background: "#fff",
    cursor: "pointer",
    fontWeight: 600,
  },
  playBtn: {
    height: 46,
    minWidth: 110,
    border: "none",
    borderRadius: 999,
    background: "#2563eb",
    color: "#fff",
    cursor: "pointer",
    fontWeight: 700,
    fontSize: 15,
  },
  seekBox: {
    display: "grid",
    gridTemplateColumns: "70px 1fr 70px",
    gap: 10,
    alignItems: "center",
    marginBottom: 18,
  },
  range: {
    width: "100%",
  },
  timeText: {
    fontSize: 13,
    color: "#4b5563",
    fontWeight: 600,
    textAlign: "center",
  },
  nativePlayerBox: {
    marginTop: 10,
  },
  nativePlayer: {
    width: "100%",
  },
  errorBox: {
    background: "#fef2f2",
    color: "#991b1b",
    border: "1px solid #fecaca",
    borderRadius: 12,
    padding: 14,
  },
};
