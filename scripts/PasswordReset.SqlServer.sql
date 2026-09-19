-- Review and apply to an existing SQL Server database before starting this version.
-- Fresh databases are created by EF EnsureCreated. This script does not alter users.
IF OBJECT_ID(N'dbo.PasswordResetTokens', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.PasswordResetTokens (
        UserId int NOT NULL,
        TokenHash nvarchar(64) NOT NULL,
        RequestedAt datetime2 NOT NULL,
        ExpiresAt datetime2 NOT NULL,
        UsedAt datetime2 NULL,
        CONSTRAINT PK_PasswordResetTokens PRIMARY KEY (UserId),
        CONSTRAINT FK_PasswordResetTokens_Users_UserId FOREIGN KEY (UserId)
            REFERENCES dbo.Users(Id) ON DELETE CASCADE
    );
    CREATE UNIQUE INDEX IX_PasswordResetTokens_TokenHash ON dbo.PasswordResetTokens(TokenHash);
END;
