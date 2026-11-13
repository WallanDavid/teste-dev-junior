-- Carga de dados de exemplo (idempotente: só insere se as tabelas estiverem vazias).
-- Datas fixas para que o relatório e a query da Parte 4 tenham resultado previsível.
USE Pedidos;
SET NOCOUNT ON;
GO

IF NOT EXISTS (SELECT 1 FROM dbo.Clientes)
BEGIN
    INSERT INTO dbo.Clientes (Nome, Email) VALUES
        (N'Ana Souza',        N'ana.souza@exemplo.com'),
        (N'Bruno Lima',       N'bruno.lima@exemplo.com'),
        (N'Carla Mendes',     N'carla.mendes@exemplo.com'),
        (N'Diego Ferreira',   N'diego.ferreira@exemplo.com'),
        (N'Elisa Rocha',      N'elisa.rocha@exemplo.com'),
        (N'Felipe Andrade',   N'felipe.andrade@exemplo.com'),
        (N'Gabriela Nunes',   N'gabriela.nunes@exemplo.com'),
        (N'Henrique Castro',  N'henrique.castro@exemplo.com'),
        -- Homônimo proposital: mostra o bug do GROUP BY c.Nome da Parte 4
        (N'Ana Souza',        N'ana.souza2@exemplo.com'),
        -- Cliente sem pedidos
        (N'Igor Barbosa',     N'igor.barbosa@exemplo.com');
END
GO

IF NOT EXISTS (SELECT 1 FROM dbo.Produtos)
BEGIN
    INSERT INTO dbo.Produtos (Nome, Preco, Estoque) VALUES
        (N'Notebook 14"',          4299.90,  25),
        (N'Monitor 27" 4K',        2199.00,  40),
        (N'Teclado mecânico',       459.90, 120),
        (N'Mouse sem fio',          129.90, 200),
        (N'Headset USB',            349.00,  80),
        (N'Webcam Full HD',         289.90,  60),
        (N'Cadeira ergonômica',    1899.00,  15),
        (N'Hub USB-C 7 em 1',       239.00, 150),
        (N'SSD NVMe 1TB',           549.90,  70),
        (N'Suporte para notebook',   99.90, 300),
        (N'Cabo HDMI 2m',            39.90, 500),
        (N'Mesa digitalizadora',    699.00,   3);
END
GO

IF NOT EXISTS (SELECT 1 FROM dbo.Pedidos)
BEGIN
    DECLARE @p TABLE (Ordem INT PRIMARY KEY, ClienteId INT, DataPedido DATETIME2(3));
    INSERT INTO @p (Ordem, ClienteId, DataPedido) VALUES
        ( 1, 1, '2025-11-20T14:10:00'), ( 2, 2, '2025-12-05T09:30:00'), ( 3, 3, '2025-12-23T18:45:00'),
        ( 4, 1, '2026-01-08T10:00:00'), ( 5, 4, '2026-01-15T16:20:00'), ( 6, 5, '2026-02-02T11:05:00'),
        ( 7, 2, '2026-02-14T19:40:00'), ( 8, 6, '2026-03-01T08:15:00'), ( 9, 1, '2026-03-18T13:30:00'),
        (10, 7, '2026-04-04T17:00:00'), (11, 9, '2026-04-22T10:45:00'), (12, 3, '2026-05-09T15:10:00'),
        (13, 8, '2026-05-27T09:00:00'), (14, 2, '2026-06-12T20:25:00'), (15, 9, '2026-07-01T12:00:00'),
        (16, 5, '2026-07-19T14:35:00'), (17, 4, '2026-08-06T11:50:00'), (18, 6, '2026-08-25T16:05:00'),
        (19, 1, '2026-09-10T10:20:00'), (20, 7, '2026-09-28T18:10:00'), (21, 3, '2026-10-15T09:40:00'),
        (22, 8, '2026-10-31T23:59:00'), (23, 2, '2026-11-05T13:15:00'), (24, 5, '2026-11-12T15:55:00');

    DECLARE @map TABLE (Ordem INT PRIMARY KEY, PedidoId INT);

    -- MERGE permite capturar Ordem -> Id gerado (INSERT ... OUTPUT não expõe colunas da origem)
    MERGE dbo.Pedidos AS t
    USING @p AS s ON 1 = 0
    WHEN NOT MATCHED THEN INSERT (ClienteId, DataPedido) VALUES (s.ClienteId, s.DataPedido)
    OUTPUT s.Ordem, inserted.Id INTO @map (Ordem, PedidoId);

    -- (Ordem do pedido, ProdutoId, Quantidade). Preço unitário = preço atual do produto.
    DECLARE @i TABLE (Ordem INT, ProdutoId INT, Quantidade INT);
    INSERT INTO @i (Ordem, ProdutoId, Quantidade) VALUES
        ( 1, 1, 1), ( 1, 4, 1),
        ( 2, 3, 2), ( 2, 11, 3),
        ( 3, 7, 1),
        ( 4, 2, 2), ( 4, 8, 1),
        ( 5, 9, 2),
        ( 6, 5, 1), ( 6, 6, 1),
        ( 7, 1, 1),
        ( 8, 10, 4), ( 8, 4, 2),
        ( 9, 7, 1), ( 9, 3, 1),
        (10, 2, 1),
        (11, 1, 1), (11, 2, 1),
        (12, 12, 1),
        (13, 9, 1), (13, 11, 5),
        (14, 5, 2),
        (15, 3, 1),
        (16, 1, 1), (16, 8, 2),
        (17, 6, 2),
        (18, 4, 3), (18, 10, 1),
        (19, 2, 1), (19, 5, 1),
        (20, 7, 1),
        (21, 9, 3),
        (22, 3, 2), (22, 4, 2),
        (23, 1, 1),
        (24, 11, 10);

    INSERT INTO dbo.ItensPedido (PedidoId, ProdutoId, Quantidade, PrecoUnitario)
    SELECT m.PedidoId, i.ProdutoId, i.Quantidade, pr.Preco
    FROM @i i
    JOIN @map m ON m.Ordem = i.Ordem
    JOIN dbo.Produtos pr ON pr.Id = i.ProdutoId;
END
GO

SELECT
    (SELECT COUNT(*) FROM dbo.Clientes)    AS Clientes,
    (SELECT COUNT(*) FROM dbo.Produtos)    AS Produtos,
    (SELECT COUNT(*) FROM dbo.Pedidos)     AS Pedidos,
    (SELECT COUNT(*) FROM dbo.ItensPedido) AS ItensPedido;
GO
