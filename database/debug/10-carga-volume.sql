/* =====================================================================================
   Carga de VOLUME para medir a query da Parte 4 (não é executada pelo docker compose).
   Gera ~20 mil clientes (com muitos homônimos), ~500 mil pedidos entre 2020 e 2026
   e ~1,25 milhão de itens. Leva poucos segundos.

   Para voltar ao estado original: docker compose down -v && docker compose up
   ===================================================================================== */
USE Pedidos;
SET NOCOUNT ON;
GO

IF (SELECT COUNT(*) FROM dbo.Pedidos) > 100000
    PRINT 'Carga de volume já aplicada.';
ELSE
BEGIN

    -- Tabela de números 1..1.000.000 (sem depender de objetos auxiliares)
    DROP TABLE IF EXISTS #n;
    WITH d AS (SELECT n FROM (VALUES (0),(1),(2),(3),(4),(5),(6),(7),(8),(9)) v(n))
    SELECT n = 1 + a.n + 10*b.n + 100*c.n + 1000*e.n + 10000*f.n + 100000*g.n
    INTO #n
    FROM d a CROSS JOIN d b CROSS JOIN d c CROSS JOIN d e CROSS JOIN d f CROSS JOIN d g;
    CREATE UNIQUE CLUSTERED INDEX ix ON #n (n);

    -- 20.000 clientes: 200 nomes distintos -> cada nome se repete ~100 vezes (homônimos)
    INSERT INTO dbo.Clientes (Nome, Email)
    SELECT CONCAT(N'Cliente ', n % 200), CONCAT(N'volume', n, N'@exemplo.com')
    FROM #n WHERE n <= 20000;

    DECLARE @minCliente INT = (SELECT MIN(Id) FROM dbo.Clientes WHERE Email LIKE N'volume%');

    -- 500.000 pedidos espalhados entre 2020-01-01 e 2026-12-31 (~1/7 em 2026)
    INSERT INTO dbo.Pedidos (ClienteId, DataPedido)
    SELECT @minCliente + (ABS(CHECKSUM(NEWID())) % 20000),
           DATEADD(MINUTE, ABS(CHECKSUM(NEWID())) % (7 * 525600), CAST('2020-01-01' AS DATETIME2(3)))
    FROM #n WHERE n <= 500000;

    -- 1 a 4 itens por pedido, produtos distintos dentro do pedido ((Id + 7k) % 12 não repete para k = 1..4)
    INSERT INTO dbo.ItensPedido (PedidoId, ProdutoId, Quantidade, PrecoUnitario)
    SELECT p.Id, pr.Id, 1 + (p.Id * k.n) % 5, pr.Preco
    FROM dbo.Pedidos p
    JOIN #n k ON k.n <= 1 + p.Id % 4
    JOIN dbo.Produtos pr ON pr.Id = 1 + (p.Id + 7 * k.n) % 12
    WHERE p.ClienteId >= @minCliente;

    UPDATE STATISTICS dbo.Pedidos WITH FULLSCAN;
    UPDATE STATISTICS dbo.ItensPedido WITH FULLSCAN;
    UPDATE STATISTICS dbo.Clientes WITH FULLSCAN;

END;

SELECT (SELECT COUNT(*) FROM dbo.Clientes) AS Clientes,
       (SELECT COUNT(*) FROM dbo.Pedidos) AS Pedidos,
       (SELECT COUNT(*) FROM dbo.ItensPedido) AS ItensPedido;
GO
