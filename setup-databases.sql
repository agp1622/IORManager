-- Create IORManager_dev database (copy of existing IORManager)
CREATE DATABASE IORManager_dev;

-- Create IORManager_prod database (copy of existing IORManager)
CREATE DATABASE IORManager_prod;

-- Verify databases were created
SELECT name FROM sys.databases WHERE name LIKE 'IORManager%';
