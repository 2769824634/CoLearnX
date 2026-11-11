/*
  One-time production cleanup for accidental online test courses.
  Review the preview result before running the transaction in production.
  The script archives instead of deleting so enrolment, billing and audit history stay intact.
*/

SELECT Id, Code, Title, Status, Description
FROM dbo.Courses
WHERE Code LIKE 'DEMO-%'
   OR Code LIKE 'TEST-%'
   OR Code LIKE '%[_]ForSignUp(Testing)%'
   OR Title LIKE 'Demo:%'
   OR Title LIKE '%live test%'
   OR Title LIKE '%test course%';

BEGIN TRANSACTION;

UPDATE dbo.Courses
SET Status = 3 -- Archived
WHERE Status <> 3
  AND (
       Code LIKE 'DEMO-%'
    OR Code LIKE 'TEST-%'
    OR Code LIKE '%[_]ForSignUp(Testing)%'
    OR Title LIKE 'Demo:%'
    OR Title LIKE '%live test%'
    OR Title LIKE '%test course%'
  );

-- Published courses must not claim that they are still waiting for approval.
UPDATE dbo.Courses
SET Description = LTRIM(RTRIM(REPLACE(Description, 'Waiting for admin approval.', '')))
WHERE Status = 2
  AND Description LIKE '%Waiting for admin approval.%';

COMMIT TRANSACTION;
