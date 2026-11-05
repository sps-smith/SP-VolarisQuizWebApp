-- FixExistingUsers.sql
-- Sets OrganizationID and PortfolioID to 0 for any users where the value
-- couldn't be carried over from the old GUID columns (will show as 0 already
-- if the ALTER TABLE set them to 0/NULL by default).
-- Safe to run multiple times.

UPDATE Users 
SET OrganizationID = 0 
WHERE OrganizationID IS NULL;

UPDATE Users 
SET PortfolioID = 0 
WHERE PortfolioID IS NULL;

-- Confirm all rows
SELECT UserID, Email, OrganizationID, PortfolioID FROM Users ORDER BY AccountCreatedAt;
