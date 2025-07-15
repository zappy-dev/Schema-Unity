#!/bin/bash

# Unity License Helper Script
# This script helps with Unity license setup for CI/CD and local development

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Function to print colored output
print_status() {
    echo -e "${BLUE}[INFO]${NC} $1"
}

print_success() {
    echo -e "${GREEN}[SUCCESS]${NC} $1"
}

print_warning() {
    echo -e "${YELLOW}[WARNING]${NC} $1"
}

print_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

# Function to check if Unity is installed
check_unity_installation() {
    print_status "Checking Unity installation..."
    
    # Check common Unity installation paths
    UNITY_PATHS=(
        "/Applications/Unity/Hub/Editor/2020.3.21f1/Unity.app/Contents/MacOS/Unity"
        "/opt/unity/Editor/Unity"
        "/c/Program Files/Unity/Hub/Editor/2020.3.21f1/Editor/Unity.exe"
        "C:\Program Files\Unity\Hub\Editor\2020.3.21f1\Editor\Unity.exe"
    )
    
    UNITY_PATH=""
    for path in "${UNITY_PATHS[@]}"; do
        if [ -f "$path" ]; then
            UNITY_PATH="$path"
            break
        fi
    done
    
    if [ -z "$UNITY_PATH" ]; then
        print_error "Unity 2020.3.21f1 not found in standard locations"
        print_status "Please install Unity 2020.3.21f1 via Unity Hub"
        return 1
    fi
    
    print_success "Unity found at: $UNITY_PATH"
    export UNITY_PATH
    return 0
}

# Function to find Unity license file
find_unity_license() {
    print_status "Looking for Unity license file..."
    
    # Check common license file locations
    LICENSE_PATHS=(
        "$HOME/.local/share/unity3d/Unity/Unity_lic.ulf"
        "/Library/Application Support/Unity/Unity_lic.ulf"
        "C:\ProgramData\Unity\Unity_lic.ulf"
        "$APPDATA/Unity/Unity_lic.ulf"
    )
    
    LICENSE_FILE=""
    for path in "${LICENSE_PATHS[@]}"; do
        if [ -f "$path" ]; then
            LICENSE_FILE="$path"
            break
        fi
    done
    
    if [ -z "$LICENSE_FILE" ]; then
        print_warning "Unity license file not found"
        print_status "This is normal for Unity Personal licenses"
        print_status "You can still use email/password authentication"
        export LICENSE_FILE=""
        return 0
    fi
    
    print_success "License file found at: $LICENSE_FILE"
    export LICENSE_FILE
    return 0
}

# Function to validate Unity license
validate_unity_license() {
    print_status "Validating Unity license..."
    
    if [ ! -f "$LICENSE_FILE" ] || [ -z "$LICENSE_FILE" ]; then
        print_warning "No license file found - using email/password authentication"
        print_status "This is normal for Unity Personal licenses"
        print_status "Unity will be activated during CI using UNITY_EMAIL and UNITY_PASSWORD"
        return 0
    fi
    
    # Check if license file contains expected content
    if ! grep -q "DeveloperData" "$LICENSE_FILE"; then
        print_error "License file appears to be invalid or corrupted"
        return 1
    fi
    
    # Check license type
    if grep -q "UnityPersonal" "$LICENSE_FILE"; then
        print_success "Unity Personal license detected"
    elif grep -q "UnityPro" "$LICENSE_FILE"; then
        print_success "Unity Pro license detected"
    else
        print_warning "Unknown license type detected"
    fi
    
    return 0
}

# Function to generate GitHub secrets
generate_github_secrets() {
    print_status "Generating GitHub secrets configuration..."
    
    # Create temporary file with secrets
    SECRETS_FILE="unity-github-secrets.txt"
    
    echo "# Unity GitHub Secrets Configuration" > "$SECRETS_FILE"
    echo "# Add these secrets to your GitHub repository:" >> "$SECRETS_FILE"
    echo "# Go to: Settings → Secrets and variables → Actions" >> "$SECRETS_FILE"
    echo "" >> "$SECRETS_FILE"
    
    echo "# Required for all Unity licenses:" >> "$SECRETS_FILE"
    echo "UNITY_EMAIL=your-unity-email@example.com" >> "$SECRETS_FILE"
    echo "UNITY_PASSWORD=your-unity-password" >> "$SECRETS_FILE"
    echo "" >> "$SECRETS_FILE"
    
    if [ -f "$LICENSE_FILE" ] && [ -n "$LICENSE_FILE" ]; then
        echo "# UNITY_LICENSE content (for Pro/Plus licenses only):" >> "$SECRETS_FILE"
        echo "# =========================================" >> "$SECRETS_FILE"
        cat "$LICENSE_FILE" >> "$SECRETS_FILE"
        echo "" >> "$SECRETS_FILE"
        echo "# =========================================" >> "$SECRETS_FILE"
        echo "" >> "$SECRETS_FILE"
        print_success "GitHub secrets configuration saved to: $SECRETS_FILE"
        print_warning "Remember to:"
        print_warning "1. Replace 'your-unity-email@example.com' with your actual Unity email"
        print_warning "2. Replace 'your-unity-password' with your actual Unity password"
        print_warning "3. Add the UNITY_LICENSE content to your GitHub repository settings"
    else
        echo "# UNITY_LICENSE is not required for Unity Personal licenses" >> "$SECRETS_FILE"
        echo "# The workflow will automatically activate Unity using email/password" >> "$SECRETS_FILE"
        echo "" >> "$SECRETS_FILE"
        print_success "GitHub secrets configuration saved to: $SECRETS_FILE"
        print_warning "Remember to:"
        print_warning "1. Replace 'your-unity-email@example.com' with your actual Unity email"
        print_warning "2. Replace 'your-unity-password' with your actual Unity password"
        print_warning "3. You don't need to add UNITY_LICENSE (Unity Personal license will be activated automatically)"
    fi
    
    print_warning "4. Add these secrets to your GitHub repository settings"
    
    return 0
}

# Function to test Unity activation
test_unity_activation() {
    print_status "Testing Unity activation..."
    
    if [ -z "$UNITY_PATH" ]; then
        print_error "Unity path not set"
        return 1
    fi
    
    # Test Unity activation with batch mode
    if "$UNITY_PATH" -batchmode -quit -logFile - 2>&1 | grep -q "LICENSE SYSTEM"; then
        print_success "Unity activation test passed"
        return 0
    else
        print_error "Unity activation test failed"
        return 1
    fi
}

# Function to show help
show_help() {
    echo "Unity License Helper Script"
    echo ""
    echo "Usage: $0 [OPTION]"
    echo ""
    echo "Options:"
    echo "  check       Check Unity installation and license"
    echo "  secrets     Generate GitHub secrets configuration"
    echo "  test        Test Unity activation"
    echo "  all         Run all checks and generate secrets"
    echo "  help        Show this help message"
    echo ""
    echo "Note: This script supports both Unity Pro/Plus licenses (with license file)"
    echo "      and Unity Personal licenses (email/password only)."
    echo ""
    echo "Examples:"
    echo "  $0 check      # Check Unity and license status"
    echo "  $0 secrets    # Generate GitHub secrets file"
    echo "  $0 all        # Run complete setup"
}

# Main function
main() {
    case "${1:-all}" in
        "check")
            check_unity_installation
            find_unity_license
            validate_unity_license
            ;;
        "secrets")
            find_unity_license
            validate_unity_license
            generate_github_secrets
            ;;
        "test")
            check_unity_installation
            find_unity_license
            test_unity_activation
            ;;
        "all")
            print_status "Starting Unity license setup..."
            echo ""
            
            if check_unity_installation && find_unity_license && validate_unity_license; then
                echo ""
                generate_github_secrets
                echo ""
                print_success "Unity license setup completed!"
                print_status "Next steps:"
                print_status "1. Review the generated secrets file"
                print_status "2. Add secrets to your GitHub repository"
                print_status "3. Test the Unity workflow"
            else
                print_error "Unity license setup failed"
                exit 1
            fi
            ;;
        "help"|"-h"|"--help")
            show_help
            ;;
        *)
            print_error "Unknown option: $1"
            show_help
            exit 1
            ;;
    esac
}

# Run main function with all arguments
main "$@"