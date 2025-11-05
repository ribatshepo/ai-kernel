# Multi-Service Docker Strategy

## Current Issue
- Root-level Dockerfiles don't scale for multi-technology architecture
- Single CI/CD workflow is monolithic
- No clear separation of concerns by technology

## Proposed Structure

### 1. .NET Services (Core, Proxy, Secrets, Catalog)
```
src/Core/Dockerfile
src/Proxy/Dockerfile  
src/Secrets/Dockerfile
src/Catalog/Dockerfile
```

### 2. Next.js Frontend (Web/Presentation)
```
src/Web/Dockerfile
src/Web/package.json
src/Web/next.config.js
```

### 3. Python AI Services 
```
src/AI/Dockerfile
src/AI/requirements.txt
src/AI/pyproject.toml
```

### 4. CI/CD Workflows by Technology
```
.github/workflows/dotnet-services.yml    # Core, Proxy, Secrets, Catalog
.github/workflows/nextjs-frontend.yml    # Web presentation layer
.github/workflows/python-ai.yml          # AI inference services
.github/workflows/integration.yml        # Cross-service integration tests
```

## Benefits
- **Technology isolation** - Each stack has its own build/deploy cycle
- **Independent scaling** - Services can be deployed independently  
- **Technology-specific optimizations** - Tailored CI/CD for each stack
- **Cleaner dependencies** - No cross-technology pollution

## Migration Strategy
1. Move Dockerfiles to service directories
2. Create technology-specific CI/CD workflows
3. Update docker-compose to reference service-specific Dockerfiles
4. Maintain integration workflow for E2E testing