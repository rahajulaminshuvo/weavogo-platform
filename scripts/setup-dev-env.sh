#!/bin/bash

# Setup WeavoGo Platform Development Environment

set -e

echo "🚀 WeavoGo Platform - Development Environment Setup"
echo "====================================================="

# Check prerequisites
echo "📋 Checking prerequisites..."

commands=("docker" "docker-compose" "dotnet" "git")
for cmd in "${commands[@]}"; do
    if command -v $cmd &> /dev/null; then
        echo "✅ $cmd is installed"
    else
        echo "❌ $cmd is not installed. Please install it."
        exit 1
    fi
done

# Setup environment
echo ""
echo "📝 Setting up environment..."

if [ ! -f .env ]; then
    echo "Creating .env file from .env.example..."
    cp .env.example .env
    echo "✅ Created .env file"
else
    echo "✅ .env file already exists"
fi

# Initialize directories
echo ""
echo "📁 Creating necessary directories..."

mkdir -p services/{api-gateway,auth-security,master-data,hcm,plm,erp,mes}
mkdir -p shared/{libs,models}
mkdir -p infrastructure/{docker,kubernetes,terraform,helm,scripts,monitoring}
mkdir -p docs/api-specs
mkdir -p tests/{integration,e2e,load}
mkdir -p tools/{postman,scripts}

echo "✅ Directories created"

# Start Docker services
echo ""
echo "🐳 Starting Docker services..."
echo "   Command: docker-compose -f docker-compose.monorepo.yml up -d"

read -p "Do you want to start Docker services now? (y/n) " -n 1 -r
echo
if [[ $REPLY =~ ^[Yy]$ ]]; then
    docker-compose -f docker-compose.monorepo.yml up -d
    echo "✅ Docker services started"
    echo ""
    echo "🔍 Service URLs:"
    echo "   API Gateway: http://localhost:5000"
    echo "   Auth Service: http://localhost:5001"
    echo "   Master Data: http://localhost:5002"
    echo "   HCM Service: http://localhost:5003"
    echo "   PLM Service: http://localhost:5004"
    echo "   ERP Service: http://localhost:5005"
    echo "   MES Service: http://localhost:5006"
    echo "   Consul UI: http://localhost:8500"
    echo "   RabbitMQ UI: http://localhost:15672 (guest/guest)"
    echo "   PostgreSQL: localhost:5432"
    echo "   Redis: localhost:6379"
else
    echo "⏭️  Skipped Docker startup"
fi

echo ""
echo "====================================================="
echo "✅ Setup complete!"
echo ""
echo "📚 Next steps:"
echo "1. Review MONOREPO_STRUCTURE.md for project layout"
echo "2. Check docs/ for development guides"
echo "3. See CONTRIBUTING.md for development workflow"
echo "4. Run 'docker-compose -f docker-compose.monorepo.yml logs' to view logs"
echo ""
