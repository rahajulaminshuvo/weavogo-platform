#!/bin/bash

# WeavoGo Platform - GitHub CLI Setup Script
# Creates all repositories under a monorepo structure

set -e

echo "🚀 WeavoGo Platform - Repository Setup"
echo "========================================"

# Check if GitHub CLI is installed
if ! command -v gh &> /dev/null; then
    echo "❌ GitHub CLI not found. Please install from https://cli.github.com/"
    exit 1
fi

# Check if authenticated
if ! gh auth status &> /dev/null; then
    echo "❌ Not authenticated with GitHub. Run 'gh auth login'"
    exit 1
fi

OWNER="rahajulaminshuvo"
echo "📝 Creating repositories under $OWNER..."

# Define repositories
declare -a REPOS=(
    "weavogo-api-gateway:YARP-based API Gateway for microservices routing and authentication"
    "weavogo-auth-security:Authentication & security services platform"
    "weavogo-master-data:Master data platform for reference data management"
    "weavogo-hcm:Human Capital Management service"
    "weavogo-plm:Product Lifecycle Management service"
    "weavogo-erp:Enterprise Resource Planning service"
    "weavogo-mes:Manufacturing Execution System service"
    "weavogo-shared-libs:Shared libraries and utilities"
    "weavogo-infrastructure:DevOps and infrastructure configurations"
    "weavogo-documentation:API specs and architecture documentation"
    "weavogo-postman:Postman API collections and tests"
)

# Create each repository
for repo_info in "${REPOS[@]}"; do
    IFS=':' read -r repo_name repo_desc <<< "$repo_info"
    
    echo ""
    echo "📦 Creating repository: $repo_name"
    
    # Check if repo already exists
    if gh repo view "$OWNER/$repo_name" &> /dev/null; then
        echo "⏭️  Repository already exists: $repo_name"
    else
        # Create repository
        gh repo create "$OWNER/$repo_name" \
            --public \
            --description "$repo_desc" \
            --disable-wiki \
            --disable-projects
        
        echo "✅ Created: $repo_name"
    fi
done

echo ""
echo "========================================"
echo "✅ Repository setup complete!"
echo ""
echo "Next steps:"
echo "1. Update weavogo-platform as main monorepo"
echo "2. Add all services as git submodules or organize under services/"
echo "3. Run: git clone https://github.com/$OWNER/weavogo-platform.git"
echo "4. Populate services with code templates"
echo ""
