#!/bin/bash

# Pico.DI release script
# Build, test, pack and publish Pico.DI NuGet packages.

set -e

# Configuration
CONFIGURATION="${CONFIGURATION:-Release}"
OUTPUT_DIR="${OUTPUT_DIR:-./nupkg}"
PUBLISH="${PUBLISH:-false}"
API_KEY="${API_KEY:-$NUGET_API_KEY}"
VERSION="${VERSION:-0.0.3-dev}"

if [[ "$OUTPUT_DIR" != /* ]]; then
    OUTPUT_DIR="$(pwd)/${OUTPUT_DIR#./}"
fi

# Packable projects
PACKABLE_PROJECTS=(
    "src/Pico.DI.Abs/Pico.DI.Abs.csproj"
    "src/Pico.DI.Gen/Pico.DI.Gen.csproj"
    "src/Pico.DI/Pico.DI.csproj"
)

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
NC='\033[0m' # No Color

# Logging functions
log_info() {
    echo -e "${CYAN}[INFO]${NC} $1"
}

log_success() {
    echo -e "${GREEN}[SUCCESS]${NC} $1"
}

log_warning() {
    echo -e "${YELLOW}[WARNING]${NC} $1"
}

log_error() {
    echo -e "${RED}[ERROR]${NC} $1"
    exit 1
}

# Validate .NET SDK
check_dotnet() {
    if ! command -v dotnet &> /dev/null; then
        log_error "dotnet command not found. Please install .NET SDK."
    fi
    DOTNET_VERSION=$(dotnet --version)
    log_info "Using .NET SDK $DOTNET_VERSION"
}

# Clean previous build outputs
clean() {
    log_info "Cleaning build outputs..."
    dotnet clean --configuration "$CONFIGURATION"
    if [ -d "$OUTPUT_DIR" ]; then
        rm -rf "$OUTPUT_DIR"
    fi
}

# Restore dependencies
restore() {
    log_info "Restoring dependencies..."
    dotnet restore -p:UseProjectReferences=true
}

# Build solution
build() {
    log_info "Building with configuration: $CONFIGURATION..."
    dotnet build --configuration "$CONFIGURATION" --no-restore -p:UseProjectReferences=true
    if [ $? -ne 0 ]; then
        log_error "Build failed"
    fi
}

# Run tests
test() {
    log_info "Running tests..."
    dotnet test --configuration "$CONFIGURATION" --no-build --verbosity normal -p:UseProjectReferences=true
    if [ $? -ne 0 ]; then
        log_error "Tests failed"
    fi
}

# Create NuGet packages
pack() {
    log_info "Creating NuGet packages..."
    rm -rf "$OUTPUT_DIR"
    mkdir -p "$OUTPUT_DIR"
    
    for project in "${PACKABLE_PROJECTS[@]}"; do
        log_info "Packing $project..."
        RESTORE_SOURCES="https://api.nuget.org/v3/index.json"
        RESTORE_ADDITIONAL=""
        if [ "$project" != "src/Pico.DI.Abs/Pico.DI.Abs.csproj" ]; then
            if [ "$project" = "src/Pico.DI.Gen/Pico.DI.Gen.csproj" ]; then
                RESTORE_ADDITIONAL="$OUTPUT_DIR"
            else
                RESTORE_SOURCES="$OUTPUT_DIR"
            fi
        fi

        dotnet pack "$project" \
            --configuration "$CONFIGURATION" \
            --output "$OUTPUT_DIR" \
            --include-symbols \
            --no-cache \
            "-p:RestoreSources=$RESTORE_SOURCES" \
            ${RESTORE_ADDITIONAL:+"-p:RestoreAdditionalProjectSources=$RESTORE_ADDITIONAL"} \
            -p:UseProjectReferences=false \
            -p:PicoDiPackageVersion=$VERSION \
            -p:Version=$VERSION
        if [ $? -ne 0 ]; then
            log_error "Pack failed for $project"
        fi
    done
    
    # Count packages
    NUPKG_COUNT=$(find "$OUTPUT_DIR" -name "*.nupkg" ! -name "*.symbols.nupkg" ! -name "*.snupkg" | wc -l)
    SNUPKG_COUNT=$(find "$OUTPUT_DIR" \( -name "*.snupkg" -o -name "*.symbols.nupkg" \) | wc -l)
    
    log_success "Created $NUPKG_COUNT package(s) and $SNUPKG_COUNT symbol package(s)"
    find "$OUTPUT_DIR" -name "*.nupkg" ! -name "*.symbols.*" ! -name "*.snupkg" -exec sh -c 'echo "  - $(basename {}) ($(($(stat -f%z {} 2>/dev/null || stat -c%s {}) / 1024)) KB)"' \;
}

# Publish to NuGet.org
publish() {
    log_info "Publishing to NuGet.org..."
    
    if [ -z "$API_KEY" ]; then
        log_error "NuGet API key not found. Set NUGET_API_KEY environment variable or API_KEY variable."
    fi
    
    PUBLISH_ORDER=(
        "Pico.DI.Abs.$VERSION.nupkg"
        "Pico.DI.Gen.$VERSION.nupkg"
        "Pico.DI.$VERSION.nupkg"
    )

    for package_name in "${PUBLISH_ORDER[@]}"; do
        file="$OUTPUT_DIR/$package_name"
        if [ ! -f "$file" ]; then
            log_error "Package not found: $package_name"
        fi

        log_info "Publishing $package_name..."
        dotnet nuget push "$file" \
            --api-key "$API_KEY" \
            --source https://api.nuget.org/v3/index.json \
            --skip-duplicate
        
        if [ $? -ne 0 ]; then
            log_error "Failed to publish $package_name"
        fi
        
        log_success "Published $package_name"
    done
}

# Main execution
main() {
    log_info "Starting Pico.DI release process..."
    log_info "Configuration: $CONFIGURATION"
    log_info "Output directory: $OUTPUT_DIR"
    log_info "Publish to NuGet.org: $PUBLISH"
    
    check_dotnet
    clean
    restore
    build
    test
    pack
    
    if [ "$PUBLISH" = "true" ]; then
        publish
        log_success "Release completed successfully!"
    else
        log_success "Build, test and pack completed successfully!"
        log_warning "Packages not published to NuGet.org. Set PUBLISH=true to publish."
    fi
}

# Parse arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        -p|--publish)
            PUBLISH="true"
            shift
            ;;
        --api-key)
            API_KEY="$2"
            shift 2
            ;;
        -c|--configuration)
            CONFIGURATION="$2"
            shift 2
            ;;
        -o|--output)
            OUTPUT_DIR="$2"
            shift 2
            ;;
        -h|--help)
            echo "Usage: $0 [OPTIONS]"
            echo ""
            echo "Options:"
            echo "  -p, --publish           Publish to NuGet.org"
            echo "  --api-key KEY           NuGet API key"
            echo "  -c, --configuration     Build configuration (default: Release)"
            echo "  -o, --output            Output directory (default: ./nupkg)"
            echo "  -h, --help              Show this help message"
            echo ""
            echo "Environment variables:"
            echo "  NUGET_API_KEY           NuGet API key for publishing"
            echo "  CONFIGURATION           Build configuration"
            echo "  OUTPUT_DIR              Output directory"
            echo "  PUBLISH                 Set to 'true' to publish"
            exit 0
            ;;
        *)
            log_error "Unknown option: $1"
            ;;
    esac
done

main "$@"
