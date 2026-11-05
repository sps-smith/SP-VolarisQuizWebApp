-- FixColumnTypes.sql
-- Run this in Azure Portal Query Editor to fix OrganizationID and PortfolioID
-- column types from UNIQUEIDENTIFIER to INT in the Users table

-- Check current column types first
SELECT 
    COLUMN_NAME, 
    DATA_TYPE, 
    IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'Users' 
  AND COLUMN_NAME IN ('OrganizationID', 'PortfolioID');

-- Clear any existing GUID values before altering (can't cast GUID to INT)
UPDATE Users SET OrganizationID = NULL WHERE OrganizationID IS NOT NULL;
UPDATE Users SET PortfolioID    = NULL WHERE PortfolioID    IS NOT NULL;

-- Alter column types
ALTER TABLE Users ALTER COLUMN OrganizationID INT NULL;
ALTER TABLE Users ALTER COLUMN PortfolioID    INT NULL;

-- Confirm
SELECT 
    COLUMN_NAME, 
    DATA_TYPE, 
    IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'Users' 
  AND COLUMN_NAME IN ('OrganizationID', 'PortfolioID');

PRINT 'Done. OrganizationID and PortfolioID are now INT.';
