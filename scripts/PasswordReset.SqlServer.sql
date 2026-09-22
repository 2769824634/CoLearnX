-- Review and apply to an existing SQL Server database before starting this version.
-- Fresh databases are created by EF EnsureCreated. Existing users are marked verified
-- so this upgrade does not lock out accounts that predate email verification.
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

IF COL_LENGTH(N'dbo.Users', N'EmailVerifiedAt') IS NULL
BEGIN
    ALTER TABLE dbo.Users ADD EmailVerifiedAt datetime2 NULL;
    UPDATE dbo.Users SET EmailVerifiedAt = SYSUTCDATETIME() WHERE EmailVerifiedAt IS NULL;
END;

IF OBJECT_ID(N'dbo.EmailVerificationTokens', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.EmailVerificationTokens (
        UserId int NOT NULL,
        TokenHash nvarchar(64) NOT NULL,
        RequestedAt datetime2 NOT NULL,
        ExpiresAt datetime2 NOT NULL,
        CONSTRAINT PK_EmailVerificationTokens PRIMARY KEY (UserId),
        CONSTRAINT FK_EmailVerificationTokens_Users_UserId FOREIGN KEY (UserId)
            REFERENCES dbo.Users(Id) ON DELETE CASCADE
    );
    CREATE UNIQUE INDEX IX_EmailVerificationTokens_TokenHash ON dbo.EmailVerificationTokens(TokenHash);
END;
