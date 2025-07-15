# Unity Personal License Setup Guide

## The Issue

The error "Missing Unity License File and no Serial was found" occurs because Unity Personal licenses require a **manual activation step** to generate a license file that needs to be added as a GitHub secret.

## Step-by-Step Solution

### Step 1: Request Unity License File (One-time setup)

1. **Go to the GitHub Actions page** for your repository
2. **Navigate to Actions** → **All workflows** → **Unity Build**
3. **Click "Run workflow"** manually (if the workflow doesn't trigger automatically)
4. **Wait for the workflow to fail** with the license error
5. **In the workflow logs**, look for a message like:
   ```
   Please visit https://github.com/YOUR-USERNAME/YOUR-REPO/actions/runs/XXXXX 
   to obtain your license file
   ```

### Alternative Method: Use GameCI Activation Steps

If the above doesn't work, you can temporarily add activation steps to your workflow:

1. **Create a temporary workflow** file `.github/workflows/get-license.yml`:

```yaml
name: Get Unity License

on:
  workflow_dispatch:

jobs:
  get-license:
    name: Get Unity License File
    runs-on: ubuntu-latest
    
    steps:
      - name: Checkout repository
        uses: actions/checkout@v4
        
      - name: Request Unity License
        id: getManualLicenseFile
        uses: game-ci/unity-request-activation-file@v2
        with:
          unityVersion: 2020.3.21f1
          
      - name: Upload license request
        uses: actions/upload-artifact@v4
        with:
          name: Unity-License-Request
          path: ${{ steps.getManualLicenseFile.outputs.filePath }}
```

2. **Run this workflow manually** from the Actions tab
3. **Download the license request file** from the artifacts
4. **Go to Unity License Portal**: https://license.unity3d.com/manual
5. **Upload the license request file** and download the generated license file (.ulf)
6. **Delete the temporary workflow file**

### Step 2: Add GitHub Secrets

Go to your repository **Settings** → **Secrets and variables** → **Actions** and add:

1. **`UNITY_EMAIL`**: Your Unity account email
2. **`UNITY_PASSWORD`**: Your Unity account password  
3. **`UNITY_LICENSE`**: The content of the `.ulf` license file you downloaded

**Important**: For `UNITY_LICENSE`, copy the **entire content** of the `.ulf` file, including the XML tags.

### Step 3: Verify Your Workflow

Your workflow should use the environment variables like this:

```yaml
- uses: game-ci/unity-test-runner@v4
  env:
    UNITY_LICENSE: ${{ secrets.UNITY_LICENSE }}
    UNITY_EMAIL: ${{ secrets.UNITY_EMAIL }}
    UNITY_PASSWORD: ${{ secrets.UNITY_PASSWORD }}
  with:
    projectPath: SchemaPlayground
    unityVersion: 2020.3.21f1
```

## Important Notes

### For Unity Personal License:
- ✅ **UNITY_LICENSE**: Required (license file content)
- ✅ **UNITY_EMAIL**: Required (your Unity email)
- ✅ **UNITY_PASSWORD**: Required (your Unity password)
- ❌ **UNITY_SERIAL**: Not needed for Personal

### For Unity Pro/Plus License:
- ❌ **UNITY_LICENSE**: Not needed for Pro/Plus
- ✅ **UNITY_EMAIL**: Required (your Unity email)
- ✅ **UNITY_PASSWORD**: Required (your Unity password)
- ✅ **UNITY_SERIAL**: Required (your Unity serial number)

## Troubleshooting

### Common Issues:

1. **"Invalid license file"**: Make sure you copied the entire `.ulf` file content
2. **"Special characters in password"**: Use a password with only alphanumeric characters
3. **"License activation failed"**: Try regenerating the license file
4. **"Workflow still fails"**: Check that all three secrets are properly set

### Password Requirements:
- Avoid special characters in your Unity password
- Use mixed-case alphanumeric characters only
- If needed, change your Unity password to meet these requirements

## Testing

After setting up the secrets:

1. **Push a change** to trigger the workflow
2. **Check the workflow logs** to confirm license activation succeeds
3. **Look for**: `Successfully activated Unity license`

## Next Steps

Once the license is properly activated:
- Your Unity builds will work automatically
- The license will be reactivated for each build
- No manual intervention needed for future builds

## Support

If you continue to have issues:
- Check the [GameCI Documentation](https://game.ci/docs/github/activation)
- Join the [GameCI Discord](https://discord.gg/gameCI)
- Review the [Unity License Portal](https://license.unity3d.com/manual)