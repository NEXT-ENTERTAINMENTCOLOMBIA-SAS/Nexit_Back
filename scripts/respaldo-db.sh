#!/usr/bin/env bash
# Respaldo de la base de datos de Nexit (solo lectura sobre la base: pg_dump no modifica nada).
# Uso:   DATABASE_URL="postgresql://usuario:clave@host:5432/postgres" ./scripts/respaldo-db.sh [carpeta]
# Genera:  <carpeta>/nexit-YYYYmmdd-HHMMSS.dump  (formato custom, comprimido, restaurable con pg_restore)
# La clave NUNCA se escribe en el repositorio: se pasa por variable de entorno.
set -euo pipefail
: "${DATABASE_URL:?Falta DATABASE_URL (cadena de conexión de Postgres/Supabase)}"
DESTINO="${1:-./respaldos}"
mkdir -p "$DESTINO"
ARCHIVO="$DESTINO/nexit-$(date +%Y%m%d-%H%M%S).dump"
pg_dump "$DATABASE_URL" --format=custom --no-owner --no-privileges --schema=public --file="$ARCHIVO"
echo "Respaldo creado: $ARCHIVO ($(du -h "$ARCHIVO" | cut -f1))"
pg_restore --list "$ARCHIVO" > /dev/null && echo "Verificación: el archivo se puede leer."
