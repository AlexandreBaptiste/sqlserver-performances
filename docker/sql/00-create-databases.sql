-- =============================================================================
-- Script: 00-create-databases.sql
-- Purpose: Create both benchmark databases if they do not already exist.
--          DbNaive  → minimal indexes (PKs only), shows anti-patterns
--          DbOptimized → full optimized indexes, shows best practices
-- =============================================================================

IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = N'DbNaive')
BEGIN
    CREATE DATABASE DbNaive;
    PRINT 'DbNaive created.';
END
ELSE
    PRINT 'DbNaive already exists.';
GO

IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = N'DbOptimized')
BEGIN
    CREATE DATABASE DbOptimized;
    PRINT 'DbOptimized created.';
END
ELSE
    PRINT 'DbOptimized already exists.';
GO
