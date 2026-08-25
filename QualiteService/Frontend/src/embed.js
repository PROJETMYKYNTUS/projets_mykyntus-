export function isCqEmbed() {
  return new URLSearchParams(window.location.search).get("embed") === "1";
}

export function applyEmbedDocumentClass() {
  document.documentElement.classList.toggle("embed", isCqEmbed());
}
