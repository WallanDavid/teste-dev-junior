/* =====================================================================================
   Parte 4 — Debug da query "ranking dos clientes que mais compraram em 2026"
   Explicação completa no README (seção "Parte 4").

   Como reproduzir (com o compose no ar):
     docker compose run --rm --entrypoint /opt/mssql-tools18/bin/sqlcmd db-init \
       -S sqlserver -U sa -P Pedidos_Dev2026 -C -d Pedidos -i /scripts/debug/04-query-top-clientes.sql
   Para medir com volume, rode antes /scripts/debug/10-carga-volume.sql.
   ===================================================================================== */
SET NOCOUNT ON;
GO

/* -------------------------------------------------------------------------------------
   ORIGINAL (lenta e incorreta)
   ------------------------------------------------------------------------------------- */
SELECT c.Nome, SUM(i.Quantidade * i.PrecoUnitario) AS Total
FROM Clientes c
LEFT JOIN Pedidos p ON p.ClienteId = c.Id
LEFT JOIN ItensPedido i ON i.PedidoId = p.Id
WHERE YEAR(p.DataPedido) = 2026        -- (1) função na coluna: não sargável -> scan
                                       -- (2) filtro na tabela da direita anula o LEFT JOIN
GROUP BY c.Nome                        -- (3) homônimos são somados como se fossem um cliente
ORDER BY Total;                        -- (4) crescente: quem mais comprou aparece por último
GO

/* -------------------------------------------------------------------------------------
   CORRIGIDA
   - intervalo semiaberto na coluna "nua" -> Index Seek em IX_Pedidos_DataPedido
   - INNER JOIN explícito (é o que a original fazia de fato, mas sem ambiguidade)
   - agrega por ClienteId antes de buscar o nome (agrupa por chave, não por texto)
   - ORDER BY decrescente com desempate determinístico + posição no ranking
   ------------------------------------------------------------------------------------- */
DECLARE @inicio DATETIME2(3) = '2026-01-01',
        @fim    DATETIME2(3) = '2027-01-01'; -- exclusivo

WITH TotaisPorCliente AS (
    SELECT p.ClienteId,
           COUNT(DISTINCT p.Id)                AS QuantidadePedidos,
           SUM(i.Quantidade * i.PrecoUnitario) AS Total
    FROM dbo.Pedidos p
    JOIN dbo.ItensPedido i ON i.PedidoId = p.Id
    WHERE p.DataPedido >= @inicio
      AND p.DataPedido <  @fim
    GROUP BY p.ClienteId
)
SELECT DENSE_RANK() OVER (ORDER BY t.Total DESC) AS Posicao,
       c.Id,
       c.Nome,
       t.QuantidadePedidos,
       t.Total
FROM TotaisPorCliente t
JOIN dbo.Clientes c ON c.Id = t.ClienteId
ORDER BY t.Total DESC, c.Id;
GO

/* Índices que a versão corrigida usa (já criados em database/02-create-tables.sql):

   CREATE INDEX IX_Pedidos_DataPedido
       ON dbo.Pedidos (DataPedido DESC) INCLUDE (ClienteId);          -- range seek + cobre ClienteId

   CREATE UNIQUE INDEX UX_ItensPedido_PedidoId_ProdutoId
       ON dbo.ItensPedido (PedidoId, ProdutoId) INCLUDE (Quantidade, PrecoUnitario); -- sem key lookup
*/
