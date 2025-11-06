// AI Kernel Neo4j Graph Schema Initialization
// Creates node labels, relationship types, and constraints for catalog system

// =====================================================
// Node Label Constraints and Indexes
// =====================================================

// Resource node - represents any cataloged resource
CREATE CONSTRAINT resource_id IF NOT EXISTS
FOR (r:Resource) REQUIRE r.id IS UNIQUE;

CREATE INDEX resource_type IF NOT EXISTS
FOR (r:Resource) ON (r.resource_type);

CREATE INDEX resource_namespace IF NOT EXISTS
FOR (r:Resource) ON (r.namespace);

CREATE INDEX resource_name IF NOT EXISTS
FOR (r:Resource) ON (r.name);

CREATE FULLTEXT INDEX resource_search IF NOT EXISTS
FOR (r:Resource) ON EACH [r.name, r.description, r.tags];

// Service node - specialized resource type
CREATE CONSTRAINT service_id IF NOT EXISTS
FOR (s:Service) REQUIRE s.id IS UNIQUE;

CREATE INDEX service_version IF NOT EXISTS
FOR (s:Service) ON (s.version);

// Database node - specialized resource type
CREATE CONSTRAINT database_id IF NOT EXISTS
FOR (d:Database) REQUIRE d.id IS UNIQUE;

// Table node - database schema component
CREATE CONSTRAINT table_id IF NOT EXISTS
FOR (t:Table) REQUIRE t.id IS UNIQUE;

CREATE INDEX table_schema IF NOT EXISTS
FOR (t:Table) ON (t.schema_name);

// Model node - AI/ML models
CREATE CONSTRAINT model_id IF NOT EXISTS
FOR (m:Model) REQUIRE m.id IS UNIQUE;

CREATE INDEX model_version IF NOT EXISTS
FOR (m:Model) ON (m.version);

// Dataset node - data collections
CREATE CONSTRAINT dataset_id IF NOT EXISTS
FOR (d:Dataset) REQUIRE d.id IS UNIQUE;

// User node - system users and service accounts
CREATE CONSTRAINT user_id IF NOT EXISTS
FOR (u:User) REQUIRE u.id IS UNIQUE;

CREATE INDEX user_email IF NOT EXISTS
FOR (u:User) ON (u.email);

// =====================================================
// Relationship Type Indexes
// =====================================================

// DEPENDS_ON - resource dependencies
CREATE INDEX depends_on_created IF NOT EXISTS
FOR ()-[r:DEPENDS_ON]-() ON (r.created_at);

CREATE INDEX depends_on_type IF NOT EXISTS
FOR ()-[r:DEPENDS_ON]-() ON (r.dependency_type);

// PRODUCES - data lineage (source produces target)
CREATE INDEX produces_created IF NOT EXISTS
FOR ()-[r:PRODUCES]-() ON (r.created_at);

CREATE INDEX produces_transformation IF NOT EXISTS
FOR ()-[r:PRODUCES]-() ON (r.transformation_type);

// CONSUMES - data consumption (target consumes source)
CREATE INDEX consumes_created IF NOT EXISTS
FOR ()-[r:CONSUMES]-() ON (r.created_at);

// CONTAINS - containment relationships
CREATE INDEX contains_created IF NOT EXISTS
FOR ()-[r:CONTAINS]-() ON (r.created_at);

// TRAINED_WITH - ML model training data
CREATE INDEX trained_with_created IF NOT EXISTS
FOR ()-[r:TRAINED_WITH]-() ON (r.created_at);

CREATE INDEX trained_with_version IF NOT EXISTS
FOR ()-[r:TRAINED_WITH]-() ON (r.model_version);

// HAS_ACCESS - access control relationships
CREATE INDEX has_access_granted IF NOT EXISTS
FOR ()-[r:HAS_ACCESS]-() ON (r.granted_at);

CREATE INDEX has_access_level IF NOT EXISTS
FOR ()-[r:HAS_ACCESS]-() ON (r.access_level);

// DERIVES_FROM - derived resources
CREATE INDEX derives_from_created IF NOT EXISTS
FOR ()-[r:DERIVES_FROM]-() ON (r.created_at);

// =====================================================
// Sample Data Model Documentation
// =====================================================

// Example Resource Node Properties:
// (:Resource {
//   id: "uuid",
//   resource_type: "service|database|table|model|dataset",
//   name: "resource-name",
//   namespace: "namespace",
//   description: "description",
//   tags: ["tag1", "tag2"],
//   metadata: {key: "value"},
//   version: "1.0.0",
//   created_at: datetime(),
//   updated_at: datetime(),
//   created_by: "user-id",
//   is_active: true
// })

// Example Service Node Properties:
// (:Service:Resource {
//   ... Resource properties ...
//   endpoint: "https://api.example.com",
//   protocol: "https|grpc|kafka",
//   health_check_url: "/health",
//   sla_tier: "critical|high|medium|low"
// })

// Example Database Node Properties:
// (:Database:Resource {
//   ... Resource properties ...
//   connection_string: "encrypted",
//   database_type: "postgresql|mysql|mongodb",
//   encryption_enabled: true
// })

// Example Table Node Properties:
// (:Table:Resource {
//   ... Resource properties ...
//   schema_name: "schema",
//   table_name: "table",
//   row_count: 1000000,
//   data_classification: "public|internal|confidential|restricted",
//   has_pii: false
// })

// Example Model Node Properties:
// (:Model:Resource {
//   ... Resource properties ...
//   model_type: "classification|regression|clustering|llm",
//   framework: "tensorflow|pytorch|sklearn",
//   accuracy: 0.95,
//   training_date: datetime()
// })

// Example DEPENDS_ON Relationship Properties:
// (a:Resource)-[:DEPENDS_ON {
//   dependency_type: "runtime|build|data",
//   required: true,
//   version_constraint: ">=1.0.0",
//   created_at: datetime()
// }]->(b:Resource)

// Example PRODUCES Relationship Properties:
// (a:Service)-[:PRODUCES {
//   transformation_type: "etl|aggregation|ml_inference",
//   transformation_logic: "SQL query or code",
//   created_at: datetime(),
//   confidence: 1.0
// }]->(b:Dataset)

// Example TRAINED_WITH Relationship Properties:
// (m:Model)-[:TRAINED_WITH {
//   model_version: "v1.0",
//   training_date: datetime(),
//   dataset_snapshot: "snapshot-id",
//   metrics: {accuracy: 0.95, precision: 0.93}
// }]->(d:Dataset)

// Example HAS_ACCESS Relationship Properties:
// (u:User)-[:HAS_ACCESS {
//   access_level: "read|write|admin",
//   granted_at: datetime(),
//   granted_by: "admin-user-id",
//   expires_at: datetime(),
//   conditions: ["ip_whitelist", "mfa_required"]
// }]->(r:Resource)

// =====================================================
// Utility Queries for Catalog Operations
// =====================================================

// Find all resources of a specific type:
// MATCH (r:Resource {resource_type: 'service'})
// RETURN r

// Find dependencies of a resource (direct and transitive):
// MATCH path = (r:Resource {id: 'resource-id'})-[:DEPENDS_ON*1..5]->(dep)
// RETURN path

// Find data lineage upstream (sources):
// MATCH path = (source)-[:PRODUCES*1..10]->(target:Dataset {id: 'dataset-id'})
// RETURN path

// Find data lineage downstream (consumers):
// MATCH path = (source:Dataset {id: 'dataset-id'})-[:PRODUCES*1..10]->(consumer)
// RETURN path

// Find all resources with PII data:
// MATCH (t:Table {has_pii: true})
// RETURN t

// Find impact of a resource change:
// MATCH path = (r:Resource {id: 'resource-id'})<-[:DEPENDS_ON|CONSUMES*1..5]-(affected)
// RETURN DISTINCT affected

// Find ML model lineage:
// MATCH path = (m:Model {id: 'model-id'})-[:TRAINED_WITH]->(d:Dataset)
// OPTIONAL MATCH upstream = (source)-[:PRODUCES*]->(d)
// RETURN path, upstream

// =====================================================
// Performance Optimization Notes
// =====================================================

// 1. Use UNIQUE constraints for all ID fields to leverage automatic indexes
// 2. Create indexes on frequently queried properties (type, namespace, version)
// 3. Use fulltext indexes for search capabilities
// 4. Limit relationship traversal depth to prevent expensive queries
// 5. Use relationship property indexes for time-based queries
// 6. Consider graph projection for complex analytics (GDS library)
