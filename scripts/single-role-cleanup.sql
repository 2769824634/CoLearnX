-- Idempotent SQL Server cleanup for the known multi-role accounts (2026-11-11).
-- The User row and all owned content remain; only extra UserRoles are removed.
SET XACT_ABORT ON;
BEGIN TRANSACTION;

DECLARE @KeepRoles TABLE (Email nvarchar(256) NOT NULL, Role int NOT NULL);
INSERT INTO @KeepRoles (Email, Role) VALUES
    ('huang.yousheng@colearnx.com', 1),
    ('youshengh1@colearnx.xyz', 1),
    ('gu.yincheng@colearnx.com', 2),
    ('2769824634@qq.com', 2),
    ('18267711719@163.com', 2),
    ('qa.trainer@colearnx.test', 2),
    ('qa.trainer2@colearnx.test', 2),
    ('qa.trainer3@colearnx.test', 2),
    ('qa.trainer4@colearnx.test', 2),
    ('zou.ruiqi@colearnx.com', 3),
    ('zouruiqi5@gmail.com', 3);

DELETE roleRow
FROM UserRoles AS roleRow
INNER JOIN Users AS userRow ON userRow.Id = roleRow.UserId
INNER JOIN @KeepRoles AS keepRow ON keepRow.Email = userRow.Email
WHERE roleRow.Role <> keepRow.Role;

COMMIT TRANSACTION;
