-- Schema: Clientes, Produtos, Pedidos, ItensPedido.
-- Idempotente: pode ser executado várias vezes sem erro.
-- Fonte da verdade do schema (o EF Core apenas mapeia estas tabelas).
USE Pedidos;
SET NOCOUNT ON;
GO

/* ---------------------------------------------------------------- Clientes */
IF OBJECT_ID(N'dbo.Clientes', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Clientes
    (
        Id           INT IDENTITY(1, 1) NOT NULL,
        Nome         NVARCHAR(150)      NOT NULL,
        Email        NVARCHAR(200)      NOT NULL,
        DataCadastro DATETIME2(0)       NOT NULL CONSTRAINT DF_Clientes_DataCadastro DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_Clientes PRIMARY KEY CLUSTERED (Id)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_Clientes_Email' AND object_id = OBJECT_ID(N'dbo.Clientes'))
    CREATE UNIQUE INDEX UX_Clientes_Email ON dbo.Clientes (Email);
GO

/* ---------------------------------------------------------------- Produtos */
IF OBJECT_ID(N'dbo.Produtos', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Produtos
    (
        Id         INT IDENTITY(1, 1) NOT NULL,
        Nome       NVARCHAR(150)      NOT NULL,
        Preco      DECIMAL(18, 2)     NOT NULL,
        Estoque    INT                NOT NULL,
        CONSTRAINT PK_Produtos PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT CK_Produtos_Preco   CHECK (Preco >= 0),
        CONSTRAINT CK_Produtos_Estoque CHECK (Estoque >= 0) -- última barreira; a baixa usa UPDATE ... WHERE Estoque >= @q
    );
END
GO

/* ----------------------------------------------------------------- Pedidos */
IF OBJECT_ID(N'dbo.Pedidos', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Pedidos
    (
        Id         INT IDENTITY(1, 1) NOT NULL,
        ClienteId  INT                NOT NULL,
        DataPedido DATETIME2(3)       NOT NULL CONSTRAINT DF_Pedidos_DataPedido DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_Pedidos PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_Pedidos_Clientes FOREIGN KEY (ClienteId) REFERENCES dbo.Clientes (Id)
    );
END
GO

-- GET /pedidos?clienteId=..&dataInicio=..&dataFim=..  -> seek por cliente + range de data (e índice da FK)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Pedidos_ClienteId_DataPedido' AND object_id = OBJECT_ID(N'dbo.Pedidos'))
    CREATE INDEX IX_Pedidos_ClienteId_DataPedido ON dbo.Pedidos (ClienteId, DataPedido DESC);
GO

-- GET /pedidos só por período e relatório top clientes -> range seek em DataPedido, cobrindo ClienteId
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Pedidos_DataPedido' AND object_id = OBJECT_ID(N'dbo.Pedidos'))
    CREATE INDEX IX_Pedidos_DataPedido ON dbo.Pedidos (DataPedido DESC) INCLUDE (ClienteId);
GO

/* ------------------------------------------------------------- ItensPedido */
IF OBJECT_ID(N'dbo.ItensPedido', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ItensPedido
    (
        Id            INT IDENTITY(1, 1) NOT NULL,
        PedidoId      INT                NOT NULL,
        ProdutoId     INT                NOT NULL,
        Quantidade    INT                NOT NULL,
        PrecoUnitario DECIMAL(18, 2)     NOT NULL, -- preço congelado no momento do pedido
        CONSTRAINT PK_ItensPedido PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_ItensPedido_Pedidos  FOREIGN KEY (PedidoId)  REFERENCES dbo.Pedidos (Id) ON DELETE CASCADE,
        CONSTRAINT FK_ItensPedido_Produtos FOREIGN KEY (ProdutoId) REFERENCES dbo.Produtos (Id),
        CONSTRAINT CK_ItensPedido_Quantidade    CHECK (Quantidade > 0),
        CONSTRAINT CK_ItensPedido_PrecoUnitario CHECK (PrecoUnitario >= 0)
    );
END
GO

-- Um produto aparece uma vez por pedido (RN08) + soma dos itens por pedido sem key lookup
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_ItensPedido_PedidoId_ProdutoId' AND object_id = OBJECT_ID(N'dbo.ItensPedido'))
    CREATE UNIQUE INDEX UX_ItensPedido_PedidoId_ProdutoId
        ON dbo.ItensPedido (PedidoId, ProdutoId) INCLUDE (Quantidade, PrecoUnitario);
GO

-- Índice da FK para Produtos
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ItensPedido_ProdutoId' AND object_id = OBJECT_ID(N'dbo.ItensPedido'))
    CREATE INDEX IX_ItensPedido_ProdutoId ON dbo.ItensPedido (ProdutoId);
GO
