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
- **Triggers**: Push to `main` or `develop`, Pull Requests to `main` or `develop`
- **Jobs**: Project validation, code quality, testing, security scanning
- **Purpose**: Ensure code quality before merging

### Branch Protection Rules to Configure in GitHub

1. **Main Branch Protection**:
   ```
   - Require pull request reviews before merging
   - Require status checks to pass before merging
   - Required status checks: project-validation, code-quality, test, security
   - Require branches to be up to date before merging
   - Include administrators
   - Allow only develop branch to merge to main
   ```

2. **Develop Branch Protection**:
   ```
   - Require pull request reviews before merging  
   - Require status checks to pass before merging
   - Required status checks: project-validation, code-quality, test, security
   - Require branches to be up to date before merging
   - Allow feat/* and fix/* branches to merge to develop
   ```

## Workflow

1. Create feature branch from develop: `git checkout -b feat/new-feature develop`
2. Develop and commit changes on feature branch
3. Create Pull Request from `feat/new-feature` to `develop`
4. CI/CD runs validation, code quality, tests, and security scans
5. Code review and approval required
6. Merge to develop after approvals and CI success
7. For releases: Create Pull Request from `develop` to `main`
8. Production deployment triggered from main branch

## Current Branch: feat/setup-dev-environment

This current branch should follow the Git Flow by:
- Merging to `develop` when development environment setup is complete
- Not merging directly to `main`