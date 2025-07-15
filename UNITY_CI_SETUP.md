# Unity CI/CD Setup Guide

This guide explains how to set up and configure the Unity CI/CD workflow for the Schema project.

## Overview

The Unity workflow (`.github/workflows/unity.yml`) provides comprehensive build verification for the Unity project, including:

- **Multi-platform builds** (Windows, macOS, Linux)
- **Unit testing** with Unity Test Framework
- **Package validation** for the custom schema package
- **Compilation verification** to catch build errors early
- **Artifact management** for build outputs and test results

## Required Secrets

To use the Unity workflow, you need to configure the following repository secrets:

### Unity License Configuration

You have two options for Unity license activation:

#### Option 1: Unity Pro/Plus License (Recommended)
1. `UNITY_LICENSE` - Your Unity license file content
2. `UNITY_EMAIL` - Email associated with your Unity account
3. `UNITY_PASSWORD` - Password for your Unity account

#### Option 2: Unity Personal License (Email/Password Only)
1. `UNITY_EMAIL` - Email associated with your Unity account
2. `UNITY_PASSWORD` - Password for your Unity account

**Note**: If you only have `UNITY_EMAIL` and `UNITY_PASSWORD`, the workflow will automatically perform license activation during the CI run. You don't need to provide `UNITY_LICENSE` for Unity Personal licenses.

### How to Set Up Unity Secrets

1. **Get your Unity license file:**
   ```bash
   # For Unity Pro/Plus users
   # Copy the contents of your Unity license file
   # Usually located at:
   # Windows: C:\ProgramData\Unity\Unity_lic.ulf
   # macOS: /Library/Application Support/Unity/Unity_lic.ulf
   # Linux: ~/.local/share/unity3d/Unity/Unity_lic.ulf
   ```

2. **Add secrets to your GitHub repository:**
   - Go to your repository on GitHub
   - Navigate to Settings → Secrets and variables → Actions
   - Add the following secrets:
     - `UNITY_EMAIL`: Your Unity account email (required)
     - `UNITY_PASSWORD`: Your Unity account password (required)
     - `UNITY_LICENSE`: Your license file content (optional, for Pro/Plus licenses only)

## Workflow Jobs

### 1. Build Unity (`build-unity`)
- **Purpose**: Builds the Unity project for multiple platforms
- **Platforms**: Windows, macOS, Linux
- **Outputs**: Build artifacts for each platform
- **Caching**: Library folder cached for faster builds

### 2. Test Unity (`test-unity`)
- **Purpose**: Runs Unity Test Framework tests
- **Test Modes**: EditMode and PlayMode tests
- **Outputs**: Test results and coverage reports
- **Integration**: Results posted as GitHub check

### 3. Validate Package (`validate-package`)
- **Purpose**: Validates the custom Unity package structure
- **Checks**:
  - Package manifest JSON validation
  - Required fields verification
  - Assembly definition validation
  - Unity version compatibility

### 4. Compilation Check (`compilation-check`)
- **Purpose**: Verifies project compiles without errors
- **Method**: Performs a compilation-only build
- **Error Detection**: Scans build logs for compilation errors

### 5. Summary (`summary`)
- **Purpose**: Provides a consolidated build status report
- **Output**: GitHub step summary with job results

## Workflow Triggers

The workflow runs on:
- **Push** to `main` branch (when Unity project files change)
- **Pull requests** to `main` branch (when Unity project files change)

### Path Filtering
The workflow only runs when changes are made to:
- `SchemaPlayground/**` (Unity project files)
- `.github/workflows/unity.yml` (workflow file itself)

## Project Structure

```
SchemaPlayground/                    # Unity project root
├── Assets/                         # Unity assets
├── Packages/                       # Package dependencies
│   ├── manifest.json              # Package manifest
│   └── dev.czarzappy.schema.unity/ # Custom schema package
│       ├── package.json           # Package definition
│       ├── Core/                  # Core functionality
│       └── Editor/                # Editor scripts
├── ProjectSettings/               # Unity project settings
└── Library/                       # Unity library (cached)
```

## Unity Version

- **Current Version**: Unity 2020.3.21f1 (LTS)
- **Compatibility**: Package requires Unity 2020.3+
- **Update Process**: To update Unity version, modify:
  1. `SchemaPlayground/ProjectSettings/ProjectVersion.txt`
  2. `SchemaPlayground/Packages/dev.czarzappy.schema.unity/package.json`
  3. `.github/workflows/unity.yml` (unityVersion fields)

## Build Artifacts

The workflow generates the following artifacts:

### Build Outputs
- **Name**: `Schema-Build-{Platform}`
- **Content**: Compiled Unity builds for each platform
- **Retention**: 7 days

### Test Results
- **Name**: `Unity-Test-Results`
- **Content**: Test reports and coverage data
- **Retention**: 7 days

## Troubleshooting

### Common Issues

1. **License Activation Failed**
   - For Unity Pro/Plus: Verify `UNITY_LICENSE`, `UNITY_EMAIL`, and `UNITY_PASSWORD` secrets
   - For Unity Personal: Only `UNITY_EMAIL` and `UNITY_PASSWORD` are required
   - Ensure license file content is complete (including headers/footers) if using Pro/Plus
   - Check Unity account credentials
   - Verify your Unity account has the appropriate license type

2. **Build Compilation Errors**
   - Review the compilation check job logs
   - Ensure all dependencies are properly configured
   - Check for Unity version compatibility issues

3. **Package Validation Failed**
   - Verify `package.json` syntax is valid JSON
   - Ensure all required fields are present
   - Check assembly definition files for syntax errors

4. **Cache Issues**
   - Clear GitHub Actions cache if builds become inconsistent
   - Library folder cache keys are based on package manifest hash

### Manual Testing

To test the Unity project locally:

```bash
# Open Unity Hub
# Add the SchemaPlayground project
# Open with Unity 2020.3.21f1
# Run tests via Window → General → Test Runner
# Build via File → Build Settings
```

## Email/Password Only Setup (Unity Personal)

If you're using Unity Personal license and don't have access to a license file:

1. **Skip the license file step** - You don't need `UNITY_LICENSE`
2. **Only add these secrets to GitHub:**
   ```
   UNITY_EMAIL=your-unity-email@example.com
   UNITY_PASSWORD=your-unity-password
   ```
3. **The workflow will automatically:**
   - Detect that `UNITY_LICENSE` is not provided
   - Activate Unity using your email and password
   - Run all builds and tests normally

This is the recommended approach for Unity Personal license users.

## Integration with .NET Workflow

The Unity workflow complements the existing `.NET Core CI` workflow:

- **.NET workflow** tests the core Schema library
- **Unity workflow** tests Unity integration and editor functionality
- Both workflows run independently and can be triggered separately

## Performance Optimization

The workflow includes several optimizations:

- **Caching**: Unity Library folder cached between runs
- **Path filtering**: Only runs when Unity files change
- **Parallel execution**: Build matrix runs platforms in parallel
- **Artifact cleanup**: 7-day retention prevents storage bloat

## Security Considerations

- Unity license and credentials are stored as encrypted secrets
- Build artifacts are temporary and automatically cleaned up
- No sensitive data is logged in build outputs
- Workflow only has access to necessary repository contents

## Future Enhancements

Potential workflow improvements:

- **Code coverage integration** with Codecov
- **Performance benchmarking** for build times
- **Automated package publishing** to Unity Package Manager
- **Integration testing** with external Unity projects
- **Documentation generation** from Unity XML comments