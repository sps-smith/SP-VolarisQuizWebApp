-- FixOrgFK.sql
-- Option 1: Disable the FK constraint so OrganizationID can be any int value
ALTER TABLE Users NOCHECK CONSTRAINT FK_Users_Org;

-- Option 2 (alternative): Drop it entirely if it's not needed
-- ALTER TABLE Users DROP CONSTRAINT FK_Users_Org;

-- Confirm
SELECT fk.name, fk.is_disabled 
FROM sys.foreign_keys fk
WHERE OBJECT_NAME(fk.parent_object_id) = 'Users';
