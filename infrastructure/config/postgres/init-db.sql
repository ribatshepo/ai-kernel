-- AI Kernel PostgreSQL Initialization Script
-- Creates necessary databases, schemas, and extensions

-- Create databases if they don't exist
SELECT 'CREATE DATABASE aikernel_catalog'
WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'aikernel_catalog')\gexec

SELECT 'CREATE DATABASE aikernel_audit'
WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'aikernel_audit')\gexec

-- Connect to catalog database
\c aikernel_catalog;

-- Create extensions
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
CREATE EXTENSION IF NOT EXISTS "pg_stat_statements";
CREATE EXTENSION IF NOT EXISTS "pgcrypto";

-- Create schemas
CREATE SCHEMA IF NOT EXISTS catalog;
CREATE SCHEMA IF NOT EXISTS metadata;
CREATE SCHEMA IF NOT EXISTS lineage;

-- Create catalog tables
CREATE TABLE IF NOT EXISTS catalog.resources (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    resource_type VARCHAR(100) NOT NULL,
    resource_name VARCHAR(255) NOT NULL,
    resource_namespace VARCHAR(255),
    metadata JSONB NOT NULL DEFAULT '{}',
    tags JSONB DEFAULT '[]',
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    created_by VARCHAR(255),
    version INTEGER DEFAULT 1,
    is_active BOOLEAN DEFAULT TRUE,
    UNIQUE(resource_type, resource_name, resource_namespace)
);

CREATE INDEX idx_resources_type ON catalog.resources(resource_type);
CREATE INDEX idx_resources_namespace ON catalog.resources(resource_namespace);
CREATE INDEX idx_resources_tags ON catalog.resources USING GIN(tags);
CREATE INDEX idx_resources_metadata ON catalog.resources USING GIN(metadata);

-- Create metadata tracking table
CREATE TABLE IF NOT EXISTS metadata.properties (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    resource_id UUID REFERENCES catalog.resources(id) ON DELETE CASCADE,
    property_key VARCHAR(255) NOT NULL,
    property_value TEXT,
    property_type VARCHAR(50),
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    UNIQUE(resource_id, property_key)
);

CREATE INDEX idx_properties_resource ON metadata.properties(resource_id);
CREATE INDEX idx_properties_key ON metadata.properties(property_key);

-- Create lineage tracking table
CREATE TABLE IF NOT EXISTS lineage.data_lineage (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    source_resource_id UUID REFERENCES catalog.resources(id) ON DELETE CASCADE,
    target_resource_id UUID REFERENCES catalog.resources(id) ON DELETE CASCADE,
    lineage_type VARCHAR(50) NOT NULL,
    transformation_logic TEXT,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    UNIQUE(source_resource_id, target_resource_id, lineage_type)
);

CREATE INDEX idx_lineage_source ON lineage.data_lineage(source_resource_id);
CREATE INDEX idx_lineage_target ON lineage.data_lineage(target_resource_id);

-- Create version history table
CREATE TABLE IF NOT EXISTS catalog.resource_versions (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    resource_id UUID REFERENCES catalog.resources(id) ON DELETE CASCADE,
    version INTEGER NOT NULL,
    metadata JSONB NOT NULL,
    changed_by VARCHAR(255),
    changed_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    change_reason TEXT
);

CREATE INDEX idx_versions_resource ON catalog.resource_versions(resource_id);
CREATE INDEX idx_versions_version ON catalog.resource_versions(version);

-- Connect to audit database
\c aikernel_audit;

-- Create extensions
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
CREATE EXTENSION IF NOT EXISTS "pg_stat_statements";
CREATE EXTENSION IF NOT EXISTS "pgcrypto";

-- Create audit schema
CREATE SCHEMA IF NOT EXISTS audit;

-- Create audit log table
CREATE TABLE IF NOT EXISTS audit.event_log (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    event_type VARCHAR(100) NOT NULL,
    event_timestamp TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    user_identity VARCHAR(255),
    resource_type VARCHAR(100),
    resource_id VARCHAR(255),
    action VARCHAR(50) NOT NULL,
    result VARCHAR(20) NOT NULL,
    details JSONB DEFAULT '{}',
    ip_address INET,
    user_agent TEXT
);

CREATE INDEX idx_audit_timestamp ON audit.event_log(event_timestamp DESC);
CREATE INDEX idx_audit_user ON audit.event_log(user_identity);
CREATE INDEX idx_audit_resource ON audit.event_log(resource_type, resource_id);
CREATE INDEX idx_audit_action ON audit.event_log(action);

-- Create function to update updated_at timestamp
\c aikernel_catalog;

CREATE OR REPLACE FUNCTION update_updated_at_column()
RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at = CURRENT_TIMESTAMP;
    RETURN NEW;
END;
$$ language 'plpgsql';

-- Create trigger for resources table
CREATE TRIGGER update_resources_updated_at BEFORE UPDATE ON catalog.resources
FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();

-- Grant permissions
GRANT ALL PRIVILEGES ON DATABASE aikernel_catalog TO aikernel;
GRANT ALL PRIVILEGES ON DATABASE aikernel_audit TO aikernel;
GRANT ALL PRIVILEGES ON ALL TABLES IN SCHEMA catalog TO aikernel;
GRANT ALL PRIVILEGES ON ALL TABLES IN SCHEMA metadata TO aikernel;
GRANT ALL PRIVILEGES ON ALL TABLES IN SCHEMA lineage TO aikernel;
GRANT ALL PRIVILEGES ON ALL SEQUENCES IN SCHEMA catalog TO aikernel;
GRANT ALL PRIVILEGES ON ALL SEQUENCES IN SCHEMA metadata TO aikernel;
GRANT ALL PRIVILEGES ON ALL SEQUENCES IN SCHEMA lineage TO aikernel;

\c aikernel_audit;
GRANT ALL PRIVILEGES ON ALL TABLES IN SCHEMA audit TO aikernel;
GRANT ALL PRIVILEGES ON ALL SEQUENCES IN SCHEMA audit TO aikernel;
