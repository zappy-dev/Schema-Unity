# Unity CI/CD Quick Start

This is a quick reference for setting up Unity CI/CD with different license types.

## 🚀 Quick Setup Options

### Option 1: Unity Personal License (Recommended for most users)

**GitHub Secrets Required:**
- `UNITY_EMAIL` - Your Unity account email
- `UNITY_PASSWORD` - Your Unity account password

**Steps:**
1. Go to your GitHub repository
2. Navigate to Settings → Secrets and variables → Actions
3. Add only these two secrets:
   ```
   UNITY_EMAIL: your-unity-email@example.com
   UNITY_PASSWORD: your-unity-password
   ```
4. Push to main branch - the workflow will automatically activate Unity

**✅ Pros:**
- Simple setup (only 2 secrets)
- No license file needed
- Works with Unity Personal accounts
- Automatic license activation

### Option 2: Unity Pro/Plus License (For enterprise users)

**GitHub Secrets Required:**
- `UNITY_EMAIL` - Your Unity account email
- `UNITY_PASSWORD` - Your Unity account password  
- `UNITY_LICENSE` - Your Unity license file content

**Steps:**
1. Find your Unity license file:
   - **Windows**: `C:\ProgramData\Unity\Unity_lic.ulf`
   - **macOS**: `/Library/Application Support/Unity/Unity_lic.ulf`
   - **Linux**: `~/.local/share/unity3d/Unity/Unity_lic.ulf`

2. Add all three secrets to GitHub:
   ```
   UNITY_EMAIL: your-unity-email@example.com
   UNITY_PASSWORD: your-unity-password
   UNITY_LICENSE: [paste entire license file content]
   ```

**✅ Pros:**
- Faster builds (no license activation step)
- Works with Unity Pro/Plus features
- More reliable for enterprise environments

## 🔧 Helper Script

Use the included helper script to automate setup:

```bash
# Check your Unity installation and license
./scripts/unity-license-helper.sh check

# Generate GitHub secrets configuration
./scripts/unity-license-helper.sh secrets

# Run complete setup
./scripts/unity-license-helper.sh all
```

## 📝 What the Workflow Does

The Unity workflow will:

1. **Build** your Unity project for Windows, macOS, and Linux
2. **Test** your project using Unity Test Framework
3. **Validate** your Unity package structure
4. **Check** for compilation errors
5. **Generate** build artifacts and test reports

## 🔍 How It Works

### With License File (Pro/Plus)
```yaml
env:
  UNITY_LICENSE: ${{ secrets.UNITY_LICENSE }}
  UNITY_EMAIL: ${{ secrets.UNITY_EMAIL }}
  UNITY_PASSWORD: ${{ secrets.UNITY_PASSWORD }}
```

### Without License File (Personal)
```yaml
env:
  UNITY_LICENSE: '' # Empty, triggers automatic activation
  UNITY_EMAIL: ${{ secrets.UNITY_EMAIL }}
  UNITY_PASSWORD: ${{ secrets.UNITY_PASSWORD }}
```

## 🚨 Common Issues & Solutions

### "License activation failed"
- **Unity Personal**: Make sure `UNITY_EMAIL` and `UNITY_PASSWORD` are correct
- **Unity Pro/Plus**: Verify all three secrets are set correctly
- Check that your Unity account has the appropriate license

### "Build compilation errors"
- Review the compilation check job logs
- Ensure your Unity project builds locally first
- Check Unity version compatibility (workflow uses 2020.3.21f1)

### "Package validation failed"
- Verify `package.json` syntax is valid
- Ensure all required package fields are present
- Check assembly definition files for errors

## 📊 Workflow Triggers

The workflow runs automatically when:
- You push changes to `main` branch
- You create a pull request to `main` branch
- Changes are made to `SchemaPlayground/` directory
- Changes are made to the Unity workflow file

## 🎯 Next Steps

1. **Choose your setup option** (Personal or Pro/Plus)
2. **Add the required secrets** to your GitHub repository
3. **Push a change** to trigger the workflow
4. **Monitor the workflow** in the Actions tab
5. **Review build artifacts** and test results

## 📚 Additional Resources

- [Complete Setup Guide](UNITY_CI_SETUP.md) - Detailed configuration instructions
- [Unity CI/CD Documentation](https://game.ci/docs/github/getting-started) - Official game-ci documentation
- [Unity Test Framework](https://docs.unity3d.com/Packages/com.unity.test-framework@latest) - Unity testing documentation

---

**💡 Tip**: Start with Unity Personal license setup (Option 1) - it's simpler and works for most use cases!