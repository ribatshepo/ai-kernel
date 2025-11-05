-- AiKernel Database Initialization Script
-- Creates initial database structure for development

-- Create schemas
CREATE SCHEMA IF NOT EXISTS core;
CREATE SCHEMA IF NOT EXISTS secrets;
CREATE SCHEMA IF NOT EXISTS catalog;
CREATE SCHEMA IF NOT EXISTS audit;

-- Core orchestration tables
CREATE TABLE IF NOT EXISTS core.workloads (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name VARCHAR(255) NOT NULL,
    type VARCHAR(100) NOT NULL,
    status VARCHAR(50) NOT NULL DEFAULT 'pending',
    configuration JSONB NOT NULL DEFAULT '{}',
    metadata JSONB NOT NULL DEFAULT '{}',
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    created_by VARCHAR(255) NOT NULL,
    version INTEGER NOT NULL DEFAULT 1
);

CREATE TABLE IF NOT EXISTS core.resource_allocations (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    workload_id UUID NOT NULL REFERENCES core.workloads(id) ON DELETE CASCADE,
    allocation_id VARCHAR(255) NOT NULL UNIQUE,
    resource_type VARCHAR(100) NOT NULL,
    resource_id VARCHAR(255) NOT NULL,
    status VARCHAR(50) NOT NULL DEFAULT 'allocated',
    properties JSONB NOT NULL DEFAULT '{}',
    allocated_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    deallocated_at TIMESTAMP WITH TIME ZONE,
    INDEX idx_workload_allocations (workload_id),
    INDEX idx_allocation_id (allocation_id),
    INDEX idx_resource_type (resource_type)
);

CREATE TABLE IF NOT EXISTS core.policies (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    policy_id VARCHAR(255) NOT NULL UNIQUE,
    name VARCHAR(255) NOT NULL,
    description TEXT,
    content TEXT NOT NULL,
    version VARCHAR(50) NOT NULL,
    active BOOLEAN NOT NULL DEFAULT true,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    created_by VARCHAR(255) NOT NULL
);

-- Secrets management tables
CREATE TABLE IF NOT EXISTS secrets.secret_stores (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name VARCHAR(255) NOT NULL UNIQUE,
    type VARCHAR(100) NOT NULL, -- vault, k8s, azure, aws, etc.
    endpoint VARCHAR(500) NOT NULL,
    configuration JSONB NOT NULL DEFAULT '{}',
    active BOOLEAN NOT NULL DEFAULT true,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE IF NOT EXISTS secrets.secret_metadata (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    secret_id VARCHAR(255) NOT NULL,
    store_id UUID NOT NULL REFERENCES secrets.secret_stores(id) ON DELETE CASCADE,
    path VARCHAR(500) NOT NULL,
    type VARCHAR(100) NOT NULL,
    tags JSONB NOT NULL DEFAULT '[]',
    metadata JSONB NOT NULL DEFAULT '{}',
    version VARCHAR(50),
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    expires_at TIMESTAMP WITH TIME ZONE,
    UNIQUE(secret_id, store_id),
    INDEX idx_secret_store (store_id),
    INDEX idx_secret_path (path),
    INDEX idx_secret_type (type)
);

-- Catalog tables
CREATE TABLE IF NOT EXISTS catalog.resources (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    resource_id VARCHAR(255) NOT NULL UNIQUE,
    name VARCHAR(255) NOT NULL,
    type VARCHAR(100) NOT NULL,
    category VARCHAR(100) NOT NULL,
    description TEXT,
    metadata JSONB NOT NULL DEFAULT '{}',
    tags JSONB NOT NULL DEFAULT '[]',
    location VARCHAR(255),
    provider VARCHAR(100),
    status VARCHAR(50) NOT NULL DEFAULT 'available',
    capabilities JSONB NOT NULL DEFAULT '{}',
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    INDEX idx_resource_type (type),
    INDEX idx_resource_category (category),
    INDEX idx_resource_status (status),
    INDEX idx_resource_provider (provider)
);

CREATE TABLE IF NOT EXISTS catalog.resource_relationships (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    parent_id UUID NOT NULL REFERENCES catalog.resources(id) ON DELETE CASCADE,
    child_id UUID NOT NULL REFERENCES catalog.resources(id) ON DELETE CASCADE,
    relationship_type VARCHAR(100) NOT NULL,
    metadata JSONB NOT NULL DEFAULT '{}',
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    UNIQUE(parent_id, child_id, relationship_type),
    INDEX idx_parent_relationships (parent_id),
    INDEX idx_child_relationships (child_id)
);

-- Audit tables
CREATE TABLE IF NOT EXISTS audit.events (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    event_id VARCHAR(255) NOT NULL UNIQUE,
    event_type VARCHAR(100) NOT NULL,
    source VARCHAR(255) NOT NULL,
    subject VARCHAR(255),
    action VARCHAR(255) NOT NULL,
    resource VARCHAR(500),
    outcome VARCHAR(50) NOT NULL, -- success, failure, error
    timestamp TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    data JSONB NOT NULL DEFAULT '{}',
    correlation_id VARCHAR(255),
    session_id VARCHAR(255),
    ip_address INET,
    user_agent TEXT,
    INDEX idx_event_type (event_type),
    INDEX idx_event_source (source),
    INDEX idx_event_timestamp (timestamp),
    INDEX idx_event_correlation (correlation_id),
    INDEX idx_event_subject (subject)
);

CREATE TABLE IF NOT EXISTS audit.metrics (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    metric_name VARCHAR(255) NOT NULL,
    metric_type VARCHAR(50) NOT NULL, -- counter, gauge, histogram
    value DOUBLE PRECISION NOT NULL,
    tags JSONB NOT NULL DEFAULT '{}',
    timestamp TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    INDEX idx_metric_name (metric_name),
    INDEX idx_metric_timestamp (timestamp)
);

-- Create indexes for better performance
CREATE INDEX CONCURRENTLY IF NOT EXISTS idx_workloads_status ON core.workloads(status);
CREATE INDEX CONCURRENTLY IF NOT EXISTS idx_workloads_created_at ON core.workloads(created_at);
CREATE INDEX CONCURRENTLY IF NOT EXISTS idx_policies_active ON core.policies(active);
CREATE INDEX CONCURRENTLY IF NOT EXISTS idx_secret_metadata_expires ON secrets.secret_metadata(expires_at);
CREATE INDEX CONCURRENTLY IF NOT EXISTS idx_audit_events_outcome ON audit.events(outcome);

-- Insert sample data for development
INSERT INTO secrets.secret_stores (name, type, endpoint, configuration) 
VALUES 
    ('dev-vault', 'hashicorp_vault', 'http://vault:8200', '{"token": "dev_token_123"}'),
    ('k8s-secrets', 'kubernetes', 'https://kubernetes.default.svc', '{"namespace": "aikernel"}')
ON CONFLICT (name) DO NOTHING;

INSERT INTO core.policies (policy_id, name, description, content, version, created_by)
VALUES 
    ('default-access', 'Default Access Policy', 'Default policy for basic access control', 
     'package aikernel.authz\n\ndefault allow = false\n\nallow {\n    input.method == "GET"\n    input.path[0] == "health"\n}', 
     '1.0.0', 'system')
ON CONFLICT (policy_id) DO NOTHING;

-- Create extension for better UUID generation if not exists
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

-- Update timestamp function
CREATE OR REPLACE FUNCTION update_updated_at_column()
RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at = CURRENT_TIMESTAMP;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

-- Create triggers for updated_at columns
CREATE TRIGGER update_workloads_updated_at BEFORE UPDATE ON core.workloads FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();
CREATE TRIGGER update_policies_updated_at BEFORE UPDATE ON core.policies FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();
CREATE TRIGGER update_secret_stores_updated_at BEFORE UPDATE ON secrets.secret_stores FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();
CREATE TRIGGER update_secret_metadata_updated_at BEFORE UPDATE ON secrets.secret_metadata FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();
CREATE TRIGGER update_resources_updated_at BEFORE UPDATE ON catalog.resources FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();