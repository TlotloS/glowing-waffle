/*
    OT Assessment database

    Builds OT_Assessment_DB from scratch.
    Warning: if the database already exists, this script drops it along with all of its data.
*/

USE master;
GO

IF DB_ID(N'OT_Assessment_DB') IS NOT NULL
BEGIN
    ALTER DATABASE OT_Assessment_DB SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE OT_Assessment_DB;
END
GO

CREATE DATABASE OT_Assessment_DB;
GO

USE OT_Assessment_DB;
GO


-- ============================================================
-- Tables
-- ============================================================

CREATE TABLE dbo.Provider
(
    ProviderId  INT IDENTITY(1, 1) NOT NULL,
    Name        NVARCHAR(100)      NOT NULL,

    CONSTRAINT PK_Provider PRIMARY KEY (ProviderId),
    CONSTRAINT UQ_Provider_Name UNIQUE (Name)
);
GO

-- The same game name can exist under different providers.
CREATE TABLE dbo.Game
(
    GameId      INT IDENTITY(1, 1) NOT NULL,
    ProviderId  INT                NOT NULL,
    Name        NVARCHAR(100)      NOT NULL,

    CONSTRAINT PK_Game PRIMARY KEY (GameId),
    CONSTRAINT FK_Game_Provider FOREIGN KEY (ProviderId) REFERENCES dbo.Provider (ProviderId),
    CONSTRAINT UQ_Game_ProviderId_Name UNIQUE (ProviderId, Name)
);
GO

-- TotalAmountSpend is updated as wagers come in, so the top spenders query doesn't have to sum every wager.
CREATE TABLE dbo.Player
(
    AccountId         UNIQUEIDENTIFIER NOT NULL,
    Username          NVARCHAR(100)    NOT NULL,
    TotalAmountSpend  DECIMAL(19, 4)   NOT NULL CONSTRAINT DF_Player_TotalAmountSpend DEFAULT (0),

    CONSTRAINT PK_Player PRIMARY KEY (AccountId)
);
GO

CREATE INDEX IX_Player_TotalAmountSpend ON dbo.Player (TotalAmountSpend DESC) INCLUDE (Username);
GO

-- Only the fields the API returns are stored. InsertedDateTimeUtc is used to measure how long the load test took to store everything.
CREATE TABLE dbo.CasinoWager
(
    WagerId              UNIQUEIDENTIFIER NOT NULL,
    AccountId            UNIQUEIDENTIFIER NOT NULL,
    GameId               INT              NOT NULL,
    Amount               DECIMAL(19, 4)   NOT NULL,
    CreatedDateTimeUtc   DATETIME2(7)     NOT NULL,
    InsertedDateTimeUtc  DATETIME2(7)     NOT NULL CONSTRAINT DF_CasinoWager_InsertedDateTimeUtc DEFAULT (SYSUTCDATETIME()),

    -- WagerId is random, so clustering on it would scatter inserts all over the table.
    CONSTRAINT PK_CasinoWager PRIMARY KEY NONCLUSTERED (WagerId),
    CONSTRAINT FK_CasinoWager_Player FOREIGN KEY (AccountId) REFERENCES dbo.Player (AccountId),
    CONSTRAINT FK_CasinoWager_Game FOREIGN KEY (GameId) REFERENCES dbo.Game (GameId)
);
GO

-- Rows are stored by player, newest wager first, so fetching a player's latest wagers is one index seek.
CREATE UNIQUE CLUSTERED INDEX CIX_CasinoWager_AccountId_CreatedDateTimeUtc
    ON dbo.CasinoWager (AccountId, CreatedDateTimeUtc DESC, WagerId);
GO


-- ============================================================
-- Table type for sending a batch of wagers in one call
-- ============================================================

CREATE TYPE dbo.CasinoWagerTableType AS TABLE
(
    WagerId             UNIQUEIDENTIFIER NOT NULL,
    AccountId           UNIQUEIDENTIFIER NOT NULL,
    Username            NVARCHAR(100)    NOT NULL,
    ProviderName        NVARCHAR(100)    NOT NULL,
    GameName            NVARCHAR(100)    NOT NULL,
    Amount              DECIMAL(19, 4)   NOT NULL,
    CreatedDateTimeUtc  DATETIME2(7)     NOT NULL
);
GO


-- ============================================================
-- Stored procedures
-- ============================================================

/*
    Stores a batch of wagers in one transaction.
    Wagers that are already stored get skipped, so a redelivered message is never saved twice.
    The consumer sends one batch at a time, so two calls never race to insert the same rows.
*/
CREATE PROCEDURE dbo.usp_CasinoWager_InsertBatch
    @Wagers dbo.CasinoWagerTableType READONLY
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRANSACTION;

    -- 1. New providers
    INSERT INTO dbo.Provider (Name)
    SELECT DISTINCT ProviderName
    FROM @Wagers
    WHERE ProviderName NOT IN (SELECT Name FROM dbo.Provider);

    -- 2. New games
    INSERT INTO dbo.Game (ProviderId, Name)
    SELECT DISTINCT provider.ProviderId, wager.GameName
    FROM @Wagers AS wager
    INNER JOIN dbo.Provider AS provider ON provider.Name = wager.ProviderName
    WHERE NOT EXISTS
    (
        SELECT 1
        FROM dbo.Game AS game
        WHERE game.ProviderId = provider.ProviderId
          AND game.Name = wager.GameName
    );

    -- 3. New players
    INSERT INTO dbo.Player (AccountId, Username)
    SELECT DISTINCT AccountId, Username
    FROM @Wagers
    WHERE AccountId NOT IN (SELECT AccountId FROM dbo.Player);

    -- 4. New wagers. DISTINCT drops a wager that appears twice in the same batch.
    INSERT INTO dbo.CasinoWager (WagerId, AccountId, GameId, Amount, CreatedDateTimeUtc)
    SELECT DISTINCT wager.WagerId, wager.AccountId, game.GameId, wager.Amount, wager.CreatedDateTimeUtc
    FROM @Wagers AS wager
    INNER JOIN dbo.Provider AS provider ON provider.Name = wager.ProviderName
    INNER JOIN dbo.Game AS game ON game.ProviderId = provider.ProviderId AND game.Name = wager.GameName
    WHERE wager.WagerId NOT IN (SELECT WagerId FROM dbo.CasinoWager);

    -- 5. Recalculate the total for every player in this batch from their stored wagers.
    UPDATE dbo.Player
    SET TotalAmountSpend =
    (
        SELECT SUM(Amount)
        FROM dbo.CasinoWager
        WHERE CasinoWager.AccountId = Player.AccountId
    )
    WHERE AccountId IN (SELECT AccountId FROM @Wagers);

    COMMIT TRANSACTION;
END
GO

/*
    Returns one page of a player's wagers, newest first.
    First result: the player's total number of wagers. Second result: the wagers on the requested page.
*/
CREATE PROCEDURE dbo.usp_CasinoWager_GetByPlayer
    @AccountId  UNIQUEIDENTIFIER,
    @Page       INT,
    @PageSize   INT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @RowsToSkip INT = (@Page - 1) * @PageSize;

    SELECT COUNT(*) AS TotalCount
    FROM dbo.CasinoWager
    WHERE AccountId = @AccountId;

    SELECT
        wager.WagerId,
        game.Name                 AS Game,
        provider.Name             AS Provider,
        wager.Amount,
        wager.CreatedDateTimeUtc  AS CreatedDate
    FROM dbo.CasinoWager AS wager
    INNER JOIN dbo.Game AS game ON game.GameId = wager.GameId
    INNER JOIN dbo.Provider AS provider ON provider.ProviderId = game.ProviderId
    WHERE wager.AccountId = @AccountId
    ORDER BY wager.CreatedDateTimeUtc DESC, wager.WagerId
    OFFSET @RowsToSkip ROWS
    FETCH NEXT @PageSize ROWS ONLY;
END
GO

-- Returns the players with the highest total spend, highest first.
CREATE PROCEDURE dbo.usp_Player_GetTopSpenders
    @Count INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT TOP (@Count) AccountId, Username, TotalAmountSpend
    FROM dbo.Player
    ORDER BY TotalAmountSpend DESC, AccountId;
END
GO
