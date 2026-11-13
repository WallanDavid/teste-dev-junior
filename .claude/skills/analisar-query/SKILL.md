---
name: analisar-query
description: Investiga uma query SQL lenta ou com resultado errado no SQL Server do projeto usando o MCP `sqlserver` — confere schema e índices, compara resultados, mede leituras (STATISTICS IO/TIME) e lê o plano de execução. Use quando o usuário colar uma query e disser que está lenta, errada, ou pedir para otimizar.
---

# Analisar query lenta/incorreta

Pré-requisito: o SQL Server do compose rodando (`docker compose up -d sqlserver db-init`) e o MCP `sqlserver` conectado (`/mcp` deve listá-lo). Ferramentas do MCP (DBHub): `search_objects` (descobrir tabelas, colunas e índices) e `execute_sql` (rodar consultas).

`SET SHOWPLAN_*`/`SET STATISTICS *` precisam de lotes separados e mensagens do servidor, que o `execute_sql` não devolve. Para essas medições use o sqlcmd do compose:

```bash
MSYS_NO_PATHCONV=1 docker compose run --rm -T --entrypoint /opt/mssql-tools18/bin/sqlcmd db-init \
  -S sqlserver -U sa -P Pedidos_Dev2026 -C -d Pedidos -W -i /scripts/debug/<arquivo>.sql
```

## Passo 1 — Corretude antes de performance

1. Leia a query e liste suspeitas lógicas. Checklist:
   - `LEFT JOIN` anulado por filtro na tabela da direita no `WHERE` (vira INNER JOIN implícito).
   - `GROUP BY` por coluna não única (ex.: nome) somando entidades diferentes.
   - `ORDER BY` na direção errada / sem desempate determinístico.
   - Agregação sobre JOIN 1:N que multiplica linhas (fan-out).
   - `NULL` em `SUM`/`COUNT` e divisões.
2. Prove cada suspeita com uma **query de verificação** via MCP (ex.: `SELECT Nome, COUNT(*) FROM Clientes GROUP BY Nome HAVING COUNT(*) > 1`).
3. Escreva a versão corrigida e compare o resultado com a original (`EXCEPT` nos dois sentidos).

## Passo 2 — Performance

1. Confira os índices existentes:
   ```sql
   SELECT t.name AS tabela, i.name AS indice, i.type_desc,
          STRING_AGG(CASE WHEN ic.is_included_column = 0 THEN c.name END, ', ') AS chave,
          STRING_AGG(CASE WHEN ic.is_included_column = 1 THEN c.name END, ', ') AS include
   FROM sys.indexes i
   JOIN sys.tables t ON t.object_id = i.object_id
   JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
   JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
   GROUP BY t.name, i.name, i.type_desc ORDER BY t.name, i.name;
   ```
2. Se houver pouco volume, rode `database/debug/10-carga-volume.sql` (gera ~500 mil pedidos e ~1,25 milhão de itens) para que a diferença apareça. Use `database/debug/05-medicao.sql` como modelo de medição.
3. Meça original e corrigida no mesmo lote:
   ```sql
   SET STATISTICS IO, TIME ON;
   -- query
   SET STATISTICS IO, TIME OFF;
   ```
   Registre *logical reads* por tabela e *CPU/elapsed time*.
4. Plano estimado: `SET SHOWPLAN_XML ON;` + query + `SET SHOWPLAN_XML OFF;`. Procure: `Index Scan`/`Clustered Index Scan` onde deveria haver `Seek`, predicado com função na coluna (`YEAR(...)`, `CONVERT(...)`) → não sargável, `Key Lookup`, `Sort` caro, avisos de conversão implícita, *missing index*.
5. Consulte também `sys.dm_db_missing_index_details` — mas só aceite a sugestão se fizer sentido para as consultas reais do sistema.

## Passo 3 — Entrega

- Atualize/crie o script em `database/debug/` com: query original, query corrigida e índices.
- Se criar índice, coloque-o em `database/02-create-tables.sql` (idempotente) e na `*Configuration.cs` do EF.
- Resuma em uma tabela: problema → evidência (número de leituras / operador do plano) → correção.
