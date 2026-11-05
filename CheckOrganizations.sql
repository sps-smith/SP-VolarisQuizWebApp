-- CheckOrganizations.sql
-- See what's in the Organizations table
SELECT * FROM Organizations ORDER BY OrganizationID;

-- See the FK constraint definition
SELECT 
    fk.name AS FK_Name,
    fk.is_disabled,
    COL_NAME(fkc.parent_object_id, fkc.parent_column_id) AS FK_Column,
    OBJECT_NAME(fkc.referenced_object_id) AS Referenced_Table,
    COL_NAME(fkc.referenced_object_id, fkc.referenced_column_id) AS Referenced_Column
FROM sys.foreign_keys fk
JOIN sys.foreign_key_columns fkc ON fk.object_id = fkc.constraint_object_id
WHERE OBJECT_NAME(fk.parent_object_id) = 'Users';
