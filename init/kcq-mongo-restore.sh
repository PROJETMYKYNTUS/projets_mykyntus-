#!/usr/bin/env bash
# One-shot : restaure backup_kcq_db (mongodump KyntusCQ) vers kcq-mongo / kcq.
# Idempotent : skip si kcq.scores n'est pas vide. Skip si dump absent.
# Compatible mongo:4.4 (cli `mongo`) et mongo:6+ (`mongosh`).
set -euo pipefail

MONGO_URI="${MONGO_URI:-mongodb://kcq-mongo:27017}"
TARGET_DB="${TARGET_DB:-kcq}"
SOURCE_DB="${SOURCE_DB:-KyntusCQ}"

mongo_eval() {
  local uri="$1"
  local js="$2"
  if command -v mongosh >/dev/null 2>&1; then
    mongosh --quiet "${uri}" --eval "${js}"
  else
    mongo --quiet "${uri}" --eval "${js}"
  fi
}

find_dump_dir() {
  local d
  for d in /backup/backup_kcq_db /backup; do
    if [ -d "${d}/${SOURCE_DB}" ]; then
      printf '%s' "$d"
      return 0
    fi
  done
  return 1
}

DUMP_DIR=""
if ! DUMP_DIR="$(find_dump_dir)"; then
  echo "kcq-mongo-restore: no ${SOURCE_DB} dump under /backup, skip"
  exit 0
fi

echo "kcq-mongo-restore: dump at ${DUMP_DIR}/${SOURCE_DB}"

until mongo_eval "${MONGO_URI}/admin" 'quit(db.runCommand({ ping: 1 }).ok == 1 ? 0 : 1)' >/dev/null 2>&1; do
  echo "kcq-mongo-restore: waiting for mongo..."
  sleep 2
done

SCORE_COUNT="$(mongo_eval "${MONGO_URI}/${TARGET_DB}" 'print(db.scores.countDocuments ? db.scores.countDocuments({}) : db.scores.count())')"
SCORE_COUNT="${SCORE_COUNT//$'\r'/}"
SCORE_COUNT="$(echo "${SCORE_COUNT}" | tr -d '[:space:]')"

if [ "${SCORE_COUNT:-0}" != "0" ]; then
  echo "kcq-mongo-restore: ${TARGET_DB}.scores already has ${SCORE_COUNT} docs, skip"
  exit 0
fi

echo "kcq-mongo-restore: restoring ${SOURCE_DB}.* -> ${TARGET_DB}.* (--drop, scores empty)"
mongorestore \
  --uri="${MONGO_URI}" \
  --drop \
  --nsInclude="${SOURCE_DB}.*" \
  --nsFrom="${SOURCE_DB}.*" \
  --nsTo="${TARGET_DB}.*" \
  --dir="${DUMP_DIR}"

echo "kcq-mongo-restore: done"
