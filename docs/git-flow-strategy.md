# Git Flow Branch Protection Strategy

This document outlines the branch protection rules and Git Flow strategy for the AI Kernel project.

## Branch Strategy

### Main Branch (`main`)
- **Purpose**: Production-ready code only
- **Source**: Only accepts merges from `develop` branch
- **Protection**: 
  - Require pull request reviews
  - Require status checks to pass
  - Require branches to be up to date before merging
  - Include administrators in restrictions

### Develop Branch (`develop`) 
- **Purpose**: Integration branch for feature development
- **Source**: Accepts merges from `feat/*` and `fix/*` branches
- **Target**: Merges to `main` for releases
- **Protection**:
  - Require pull request reviews
  - Require status checks to pass
  - Require branches to be up to date before merging

### Feature Branches (`feat/*`)
- **Purpose**: New feature development
- **Source**: Created from `develop` branch
- **Target**: Merge only to `develop` branch
- **Naming**: `feat/feature-description`

### Fix Branches (`fix/*`)
- **Purpose**: Bug fixes and hotfixes
- **Source**: Created from `develop` branch  
- **Target**: Merge only to `develop` branch
- **Naming**: `fix/bug-description`

## CI/CD Behavior

### Continuous Integration
- **Triggers**: 
  - Push to `main` or `develop` branches (quality gates)
  - Pull Requests to `main` or `develop` branches (validation before merge)
- **Jobs Execution Order**:
  1. **branch-validation** - Enforces Git Flow merge rules (runs first)
  2. **project-validation** - Validates project structure (depends on branch validation)
  3. **code-quality** - Code quality and Docker validation (parallel with test/security)
  4. **test** - Unit testing with PostgreSQL/Redis services (parallel with code-quality/security)
  5. **security** - Security scanning and CodeQL analysis (parallel with code-quality/test)
- **Purpose**: Ensure code quality and enforce Git Flow rules at critical integration points

### Git Flow Validation Rules
The CI/CD pipeline automatically enforces these merge rules:

**For Pull Requests to `main`**:
- ✅ **Allowed**: `develop` → `main` 
- ❌ **Blocked**: Any other branch → `main` (feat/*, fix/*, hotfix/*, etc.)

**For Pull Requests to `develop`**:
- ✅ **Allowed**: `feat/*` → `develop` (feature branches)
- ✅ **Allowed**: `fix/*` → `develop` (bugfix branches)  
- ❌ **Blocked**: Any other branch → `develop` (main, hotfix/*, etc.)

**Validation Logic**:
```bash
# Rule 1: Only develop can merge to main
if TARGET_BRANCH == "main" && SOURCE_BRANCH != "develop":
    FAIL: "Only 'develop' branch can merge to 'main'"

# Rule 2: Only feat/* and fix/* can merge to develop  
if TARGET_BRANCH == "develop" && SOURCE_BRANCH !~ "^(feat|fix)/":
    FAIL: "Only 'feat/*' and 'fix/*' branches can merge to 'develop'"
```

### Branch Protection Rules to Configure in GitHub

1. **Main Branch Protection**:
   ```
   - Require pull request reviews before merging
   - Require status checks to pass before merging
   - Required status checks: branch-validation, project-validation, code-quality, test, security
   - Require branches to be up to date before merging
   - Include administrators
   - Restrict pushes that create files (only allow develop branch merges)
   ```

2. **Develop Branch Protection**:
   ```
   - Require pull request reviews before merging  
   - Require status checks to pass before merging
   - Required status checks: branch-validation, project-validation, code-quality, test, security
   - Require branches to be up to date before merging
   - Restrict pushes that create files (only allow feat/* and fix/* merges)
   ```

## Workflow

1. **Create feature branch** from develop: `git checkout -b feat/new-feature develop`
2. **Develop and commit** changes on feature branch (no CI overhead during development)
3. **Create Pull Request** from `feat/new-feature` to `develop`
4. **Automated CI/CD validation**:
   - Branch validation: Ensures feat/* → develop is allowed
   - Project validation: Checks project structure and required files
   - Code quality: Builds solution and validates Docker configurations
   - Testing: Runs unit tests with PostgreSQL/Redis test services
   - Security: Performs vulnerability scanning and CodeQL analysis
5. **Code review and approval** required from repository maintainers
6. **Merge to develop** after all approvals and CI success
7. **For releases**: Create Pull Request from `develop` to `main`
8. **Production deployment** triggered automatically from main branch pushes

## Status Check Requirements

All Pull Requests must pass these required status checks:

| Status Check | Purpose | Failure Impact |
|-------------|---------|----------------|
| `branch-validation` | Enforces Git Flow merge rules | Blocks PR if wrong source→target |
| `project-validation` | Validates project structure | Blocks PR if missing required files |
| `code-quality` | Code quality and Docker validation | Blocks PR if build/quality issues |
| `test` | Unit testing with services | Blocks PR if tests fail |
| `security` | Security scanning and CodeQL | Blocks PR if vulnerabilities found |

**All 5 checks must pass** before any Pull Request can be merged.

## Current Implementation Status

### Development Environment Setup Branch
**Branch**: `feat/setup-dev-environment`
**Status**: Ready for merge to `develop`
**Completed**:
- ✅ Complete .NET 8+ development environment with Docker configurations
- ✅ CI/CD pipeline with Git Flow validation and industry-standard practices
- ✅ Project structure validation and security scanning
- ✅ Private file protection (.gitignore configured properly)
- ✅ Observability stack (PostgreSQL, Redis, Prometheus, Grafana, Jaeger, Vault, NATS)

**Next Steps**:
1. Create Pull Request from `feat/setup-dev-environment` to `develop`
2. Validate all CI/CD checks pass (branch-validation will approve feat/* → develop)
3. Merge to develop branch following Git Flow strategy
4. Continue with remaining .NET projects and health checks implementation

This branch demonstrates the Git Flow strategy by:
- ✅ Being created from develop branch concept
- ✅ Following feat/* naming convention  
- ✅ Ready to merge only to develop (not main)
- ✅ Will trigger all required status checks when PR is created