/* =====================================================================================
   Medição da Parte 4: leituras lógicas, tempo e plano de execução (original x corrigida).
   Os resultados vão para tabelas temporárias para que o tempo medido seja o da consulta,
   não o de enviar milhares de linhas ao cliente.
   Rode depois de 10-carga-volume.sql.
   ===================================================================================== */
USE Pedidos;
SET NOCOUNT ON;
GO

-- Cache frio não é reproduzível em container compartilhado; aquecemos e medimos a 2ª execução.
DROP TABLE IF EXISTS #original, #corrigida;
CREATE TABLE #original  (Nome NVARCHAR(150), Total DECIMAL(38, 2));
CREATE TABLE #corrigida (Posicao BIGINT, Id INT, Nome NVARCHAR(150), QuantidadePedidos INT, Total DECIMAL(38, 2));
GO

PRINT '=================== ORIGINAL ===================';
SET STATISTICS IO, TIME ON;
INSERT INTO #original
SELECT c.Nome, SUM(i.Quantidade * i.PrecoUnitario) AS Total
FROM Clientes c
LEFT JOIN Pedidos p ON p.ClienteId = c.Id
LEFT JOIN ItensPedido i ON i.PedidoId = p.Id
WHERE YEAR(p.DataPedido) = 2026
GROUP BY c.Nome
ORDER BY Total;
SET STATISTICS IO, TIME OFF;
GO

PRINT '=================== CORRIGIDA ===================';
DECLARE @inicio DATETIME2(3) = '2026-01-01', @fim DATETIME2(3) = '2027-01-01';
SET STATISTICS IO, TIME ON;
WITH TotaisPorCliente AS (
    SELECT p.ClienteId, COUNT(DISTINCT p.Id) AS QuantidadePedidos, SUM(i.Quantidade * i.PrecoUnitario) AS Total
    FROM dbo.Pedidos p
    JOIN dbo.ItensPedido i ON i.PedidoId = p.Id
    WHERE p.DataPedido >= @inicio AND p.DataPedido < @fim
    GROUP BY p.ClienteId
)
INSERT INTO #corrigida
SELECT DENSE_RANK() OVER (ORDER BY t.Total DESC), c.Id, c.Nome, t.QuantidadePedidos, t.Total
FROM TotaisPorCliente t
JOIN dbo.Clientes c ON c.Id = t.ClienteId;
SET STATISTICS IO, TIME OFF;
GO

PRINT '=================== RESULTADO ===================';
SELECT 'original' AS Versao, COUNT(*) AS Linhas, SUM(Total) AS SomaTotal FROM #original
UNION ALL
SELECT 'corrigida', COUNT(*), SUM(Total) FROM #corrigida;

PRINT 'Top 3 da original (note: nomes agregados e ordem crescente):';
SELECT TOP (3) Nome, Total FROM #original ORDER BY Total;

PRINT 'Top 3 da corrigida:';
SELECT TOP (3) Posicao, Id, Nome, Total FROM #corrigida ORDER BY Posicao, Id;
GO

-- Plano estimado (texto) de cada versão
SET SHOWPLAN_TEXT ON;
GO
SELECT c.Nome, SUM(i.Quantidade * i.PrecoUnitario) AS Total
FROM Clientes c
LEFT JOIN Pedidos p ON p.ClienteId = c.Id
LEFT JOIN ItensPedido i ON i.PedidoId = p.Id
WHERE YEAR(p.DataPedido) = 2026
GROUP BY c.Nome
ORDER BY Total;
GO
SELECT p.ClienteId, SUM(i.Quantidade * i.PrecoUnitario) AS Total
FROM dbo.Pedidos p
JOIN dbo.ItensPedido i ON i.PedidoId = p.Id
WHERE p.DataPedido >= '2026-01-01' AND p.DataPedido < '2027-01-01'
GROUP BY p.ClienteId;
GO
SET SHOWPLAN_TEXT OFF;
GO
