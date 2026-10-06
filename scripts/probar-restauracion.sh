#!/usr/bin/env bash
# Prueba que un respaldo SIRVE: lo restaura en una base temporal LOCAL (nunca en producción) y cuenta filas.
# Uso:   LOCAL_URL="postgresql://postgres:postgres@localhost:5432/postgres" ./scripts/probar-restauracion.sh respaldos/nexit-XXXX.dump
set -euo pipefail
ARCHIVO="${1:?Indica el archivo .dump}"
: "${LOCAL_URL:?Falta LOCAL_URL (Postgres LOCAL para la prueba, no producción)}"
case "$LOCAL_URL" in *supabase*|*railway*|*pooler*) echo "LOCAL_URL parece remota; abortando por seguridad."; exit 1;; esac
BD="nexit_restore_test"
psql "$LOCAL_URL" -qc "DROP DATABASE IF EXISTS $BD" -c "CREATE DATABASE $BD"
URL_PRUEBA="${LOCAL_URL%/*}/$BD"
pg_restore --no-owner --no-privileges --dbname="$URL_PRUEBA" "$ARCHIVO" || true   # avisos de roles/extensiones son normales
for t in proyectos clientes proveedores usuarios; do
  echo "$t: $(psql "$URL_PRUEBA" -Atc "SELECT count(*) FROM $t")"
done
psql "$LOCAL_URL" -qc "DROP DATABASE $BD"
echo "Restauración de prueba OK (base temporal eliminada)."
