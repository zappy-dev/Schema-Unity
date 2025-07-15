# Unity CI/CD Troubleshooting Guide

This guide covers common issues and solutions for the Unity GitHub Actions workflow using `game-ci/unity-builder@v4`.

## 🔍 Common Error Types

### 1. License Activation Errors

#### Error: "License activation failed"
```
Error: License activation failed
Unity activation failed with exit code 1
```

**Solutions:**
- **Unity Personal**: Ensure only `UNITY_EMAIL` and `UNITY_PASSWORD` are set (do NOT set `UNITY_LICENSE`)
- **Unity Pro/Plus**: Verify all three secrets are correctly set
- Check that your Unity account credentials are correct
- Ensure your Unity account has the appropriate license type

#### Error: "Invalid license file"
```
Error: Invalid license file format
```

**Solutions:**
- Copy the entire license file content including headers and footers
- Ensure no extra whitespace or formatting issues
- Use the helper script to generate proper secrets: `./scripts/unity-license-helper.sh secrets`

#### Error: "Unity Hub license activation failed"
```
Error: Unity Hub license activation failed
```

**Solutions:**
- This often happens with Unity Personal licenses
- Remove the `UNITY_LICENSE` secret entirely (leave only email/password)
- The workflow will automatically handle Personal license activation

### 2. Build Configuration Errors

#### Error: "Project path not found"
```
Error: Could not find Unity project at path: SchemaPlayground
```

**Solutions:**
- Verify the `projectPath` in the workflow matches your Unity project directory
- Check that `SchemaPlayground/` exists in your repository
- Ensure `ProjectSettings/ProjectVersion.txt` exists in the project

#### Error: "Unity version mismatch"
```
Error: Unity version 2020.3.21f1 not found
```

**Solutions:**
- Update the workflow to use a different Unity version
- Or ensure your project uses Unity 2020.3.21f1
- Check `ProjectSettings/ProjectVersion.txt` for the correct version

#### Error: "Build target not supported"
```
Error: Build target StandaloneLinux64 not supported
```

**Solutions:**
- Remove unsupported build targets from the workflow matrix
- Common supported targets: `StandaloneWindows64`, `StandaloneOSX`, `StandaloneLinux64`

### 3. Package and Dependency Errors

#### Error: "Package resolution failed"
```
Error: Failed to resolve packages
```

**Solutions:**
- Check `Packages/manifest.json` for invalid package references
- Verify all git packages are accessible
- Remove any local file references that won't work in CI

#### Error: "Missing assembly references"
```
Error: Assembly 'Assembly-CSharp' will not be loaded due to errors
```

**Solutions:**
- Ensure all assembly definition files are valid JSON
- Check for missing dependencies in `.asmdef` files
- Verify package dependencies are correctly specified

### 4. Build Performance Issues

#### Error: "Build timeout"
```
Error: The job was canceled because it exceeded the maximum execution time
```

**Solutions:**
- Increase the timeout in the workflow (default is 60 minutes)
- Optimize Unity project settings for faster builds
- Use build caching more effectively
- Consider reducing the number of build targets

## 🛠️ Debugging Steps

### Step 1: Check Basic Configuration
```bash
# Verify your Unity project structure
ls -la SchemaPlayground/
ls -la SchemaPlayground/ProjectSettings/
cat SchemaPlayground/ProjectSettings/ProjectVersion.txt
```

### Step 2: Validate Package Manifest
```bash
# Check package.json syntax
jq . SchemaPlayground/Packages/manifest.json
jq . SchemaPlayground/Packages/dev.czarzappy.schema.unity/package.json
```

### Step 3: Test License Setup
```bash
# Use the helper script to check your setup
./scripts/unity-license-helper.sh check
./scripts/unity-license-helper.sh secrets
```

### Step 4: Review GitHub Secrets
- Go to GitHub repository → Settings → Secrets and variables → Actions
- Verify these secrets exist and are correctly named:
  - `UNITY_EMAIL` (required)
  - `UNITY_PASSWORD` (required)
  - `UNITY_LICENSE` (optional, only for Pro/Plus)

### Step 5: Check Workflow Syntax
```bash
# Validate YAML syntax (if you have yamllint)
yamllint .github/workflows/unity.yml
```

## 🔧 Quick Fixes

### Fix 1: Reset License Configuration
```yaml
# In your workflow, ensure environment variables are set correctly
env:
  UNITY_LICENSE: ${{ secrets.UNITY_LICENSE && secrets.UNITY_LICENSE || '' }}
  UNITY_EMAIL: ${{ secrets.UNITY_EMAIL }}
  UNITY_PASSWORD: ${{ secrets.UNITY_PASSWORD }}
```

### Fix 2: Minimal Working Configuration
```yaml
# Simplest working configuration for Unity Personal
- name: Build Unity Project
  uses: game-ci/unity-builder@v4
  env:
    UNITY_EMAIL: ${{ secrets.UNITY_EMAIL }}
    UNITY_PASSWORD: ${{ secrets.UNITY_PASSWORD }}
  with:
    projectPath: SchemaPlayground
    targetPlatform: StandaloneLinux64
    unityVersion: 2020.3.21f1
```

### Fix 3: Enable Debug Logging
```yaml
# Add verbose logging to troubleshoot issues
- name: Build Unity Project
  uses: game-ci/unity-builder@v4
  env:
    UNITY_EMAIL: ${{ secrets.UNITY_EMAIL }}
    UNITY_PASSWORD: ${{ secrets.UNITY_PASSWORD }}
  with:
    projectPath: SchemaPlayground
    targetPlatform: StandaloneLinux64
    unityVersion: 2020.3.21f1
    customParameters: -logFile /dev/stdout
```

## 🚨 Error Message Analysis

### Common Error Patterns

1. **"exit code 1"** - Usually license activation issues
2. **"exit code 2"** - Build compilation errors
3. **"exit code 125"** - Unity crash or critical error
4. **"timeout"** - Build taking too long or hanging

### Log Analysis Tips

Look for these key indicators in the logs:
- `LICENSE SYSTEM [date time] Posting...` - License activation
- `Compilation failed` - Build errors
- `UnityException` - Unity-specific errors
- `System.Exception` - General system errors

## 🎯 Workflow Optimization

### Performance Improvements
```yaml
# Add caching for faster builds
- uses: actions/cache@v4
  with:
    path: SchemaPlayground/Library
    key: Library-${{ matrix.targetPlatform }}-${{ hashFiles('SchemaPlayground/Packages/manifest.json') }}
    restore-keys: |
      Library-${{ matrix.targetPlatform }}-
      Library-
```

### Parallel Build Strategy
```yaml
# Use matrix strategy for multiple platforms
strategy:
  fail-fast: false
  matrix:
    targetPlatform:
      - StandaloneLinux64
      - StandaloneWindows64
      - StandaloneOSX
```

## 📞 Getting Help

If you're still experiencing issues:

1. **Check the full error log** in GitHub Actions
2. **Search game-ci documentation**: https://game.ci/docs
3. **Review Unity version compatibility**: https://unity.com/releases
4. **Check GitHub Issues**: https://github.com/game-ci/unity-builder/issues

### When Reporting Issues

Include these details:
- Complete error message from GitHub Actions log
- Unity version used
- License type (Personal/Pro/Plus)
- Target platform
- Repository structure (especially Unity project path)

## 🔄 Alternative Approaches

### Fallback: Manual Unity Installation
```yaml
# If game-ci actions fail, try manual Unity installation
- name: Install Unity
  run: |
    wget -O UnitySetup.AppImage https://download.unity3d.com/download_unity/...
    chmod +x UnitySetup.AppImage
    ./UnitySetup.AppImage --help
```

### Simplified Build (Development Testing)
```yaml
# Minimal build for testing
- name: Simple Unity Build
  uses: game-ci/unity-builder@v4
  with:
    projectPath: SchemaPlayground
    targetPlatform: StandaloneLinux64
    unityVersion: 2020.3.21f1
```

---

**💡 Pro Tip**: Start with the minimal configuration and gradually add features to identify exactly where the issue occurs!