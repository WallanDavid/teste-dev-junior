#!/bin/bash
# Executado pelo serviço `db-init` do docker-compose depois que o SQL Server está saudável.
# Roda todos os scripts numerados de /scripts em ordem. Os scripts são idempotentes.
set -euo pipefail

SQLCMD=/opt/mssql-tools18/bin/sqlcmd
HOST="${DB_HOST:-sqlserver}"

for f in /scripts/[0-9][0-9]-*.sql; do
  echo ">> Executando $(basename "$f")"
  "$SQLCMD" -S "$HOST" -U sa -P "$MSSQL_SA_PASSWORD" -C -b -I -i "$f"
done

echo ">> Banco inicializado."
