-- PostgreSQL initialization script for SarmKadan.DistributedLock
-- This script creates the necessary tables and indexes

-- Create locks table
CREATE TABLE IF NOT EXISTS locks (
    key VARCHAR(255) PRIMARY KEY,
    owner_id VARCHAR(255) NOT NULL,
    created_at TIMESTAMP NOT NULL DEFAULT NOW(),
    expires_at TIMESTAMP NOT NULL,
    status VARCHAR(50) NOT NULL,
    renewal_count INTEGER NOT NULL DEFAULT 0
);

-- Create indexes for performance
CREATE INDEX IF NOT EXISTS idx_locks_expires_at ON locks(expires_at);
CREATE INDEX IF NOT EXISTS idx_locks_owner_id ON locks(owner_id);

-- Create function to clean up expired locks
CREATE OR REPLACE FUNCTION cleanup_expired_locks()
RETURNS void AS $$
BEGIN
    DELETE FROM locks WHERE expires_at < NOW();
END;
$$ LANGUAGE plpgsql;

-- Create trigger to run cleanup on access
CREATE OR REPLACE FUNCTION trigger_cleanup_expired_locks()
RETURNS TRIGGER AS $$
BEGIN
    PERFORM cleanup_expired_locks();
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS cleanup_on_select ON locks;
CREATE TRIGGER cleanup_on_select
AFTER INSERT OR UPDATE ON locks
FOR EACH STATEMENT
EXECUTE FUNCTION trigger_cleanup_expired_locks();

-- Grant permissions to lockuser
GRANT CONNECT ON DATABASE distributed_locks TO lockuser;
GRANT USAGE ON SCHEMA public TO lockuser;
GRANT SELECT, INSERT, UPDATE, DELETE ON locks TO lockuser;
GRANT EXECUTE ON FUNCTION cleanup_expired_locks() TO lockuser;

-- Display initialization confirmation
\echo 'Distributed Locks tables created successfully'
