# Schema-Unity

[![openupm](https://img.shields.io/npm/v/com.devzappy.schema.unity?label=openupm&registry_uri=https://package.openupm.com)](https://openupm.com/packages/com.devzappy.schema.unity/)
[![openupm](https://img.shields.io/badge/dynamic/json?color=brightgreen&label=downloads&query=%24.downloads&suffix=%2Fmonth&url=https%3A%2F%2Fpackage.openupm.com%2Fdownloads%2Fpoint%2Flast-month%2Fcom.devzappy.schema.unity)](https://openupm.com/packages/com.devzappy.schema.unity/)
[![Unity CI](https://github.com/zappy-dev/Schema-Unity/actions/workflows/dotnet.yml/badge.svg?branch=main)](https://github.com/zappy-dev/Schema-Unity/actions/workflows/dotnet.yml)
[![codecov](https://codecov.io/gh/zappy-dev/Schema-Unity/branch/main/graph/badge.svg)](https://codecov.io)
[![GitHub Sponsors](https://img.shields.io/github/sponsors/zappy-dev?style=social)](https://github.com/sponsors/zappy-dev)
[![Ko‑fi](https://img.shields.io/badge/Ko--fi-support-%23FF5E5B?logo=kofi&logoColor=white)](https://ko-fi.com/devzappy)

![Try Schema](docs/schema_brand_600x300.png)

Schema-Unity provides Unity integrations for the Schema content management tool, enabling robust, flexible, and maintainable management of game design data within Unity projects.

### Highlights
- Table editor with fast, virtualized grid and publishing
  
  ![Schema Table View](docs/images/schema_window_table_view.png)

- Rich attribute type system
  
  ![Scheme Type Selection](docs/images/scheme_type_selection.png)

- Code Generation for Runtime usage

```csharp
public partial class ProfilesEntry : EntryWrapper
{
        /// <summary>
        /// Represents a single entry (row) in the 'Profiles' data scheme.
        /// </summary>
        public ProfilesEntry(DataScheme dataScheme, DataEntry entry) : base(dataScheme, entry) {}

     
        /// <summary>
        /// Gets the value of 'ID'.
        /// </summary>
     
        public string ID => DataEntry.GetDataAsString("ID");
     
        /// <summary>
        /// Gets the value of 'Personality Notes'.
        /// </summary>
     
        public string PersonalityNotes => DataEntry.GetDataAsString("Personality Notes");
     
        /// <summary>
        /// Gets the value of 'InitialPlayerConnection'.
        /// </summary>
     
        public bool InitialPlayerConnection => DataEntry.GetDataAsBool("InitialPlayerConnection");
     
        /// <summary>
        /// Gets the value of 'Profile Picture'.
        /// </summary>
     
        public string ProfilePicture => DataEntry.GetDataAsString("Profile Picture");
 
    }
```

## Documentation

- **Start here:** [docs/Overview](docs/Overview.md)
- **Quickstart:** [docs/Quickstart](docs/Quickstart.md)
- **Key Concepts:** [docs/KeyConcepts](docs/KeyConcepts.md)
- **Unity Editor Guide:** [docs/EditorGuide](docs/EditorGuide.md)
- **Runtime Integration:** [docs/RuntimeGuide](docs/RuntimeGuide.md)
- **FAQ & Troubleshooting:** [docs/FAQ](docs/FAQ.md)

## Features

- **Schema Core**: C# library for defining, loading, and validating data schemas.
- **Unity Integration**: Custom Unity editor tools for managing schema-based data.
- **Sample Content**: Example JSON and CSV files for rapid prototyping and testing.
- **Extensible**: Easily add new data types, serialization formats, and editor extensions.

## Getting Started

See the full [Quickstart](docs/Quickstart.md).

### 1) Quick Start: Integrate into an existing project (Unity Package Manager)

#### Install via OpenUPM
1. Open Edit/Project Settings/Package Manager
2. Add a new Scoped Registry (or edit the existing OpenUPM entry)
   - Name: `package.openupm.com`
   - URL: `https://package.openupm.com`
   - Scope(s): `com.devzappy.schema.unity`
3. Click **Save** or **Apply**
4. Open Window/Package Manager
   - Click `+`
   - Select `Add package by name...` or `Add package from git URL...`
   - Paste `com.devzappy.schema.unity` into name
   - Paste `0.2.0` into version
   - Click **Add**
   
#### Alternatively, merge the snippet to Packages/manifest.json 
```json
{
    "scopedRegistries": [
        {
            "name": "package.openupm.com",
            "url": "https://package.openupm.com",
            "scopes": [
                "com.devzappy.schema.unity"
            ]
        }
    ],
    "dependencies": {
        "com.devzappy.schema.unity": "0.2.0"
    }
}
```

#### Install via Git URL (no download required):
1. Open your Unity project
2. Open `Window > Package Manager`
3. Click `+` and select `Add package from git URL...`
4. Paste: `https://github.com/zappy-dev/Schema-Unity.git?path=/SchemaPlayground/Packages/com.devzappy.schema.unity#main`
5. After install, open `Tools > Schema Editor`
   
   ![Open Schema Editor](docs/images/unity_open_schema_editor.png)

#### Try it quickly (optional):
- Create a simple scheme and entries, then publish and generate C#.
- Initialize at runtime with `Schema.Runtime.SchemaRuntime.Initialize()`.

### 2) Local Setup (clone the repo)

Prerequisites:
- **Unity** (2021.3+ recommended)
- **.NET SDK 8** (for building and testing core libraries)

Steps:
1. Clone:
   ```bash
   git clone https://github.com/zappy-dev/Schema-Unity.git
   cd Schema-Unity
   ```
2. Open `SchemaPlayground/` in Unity
3. Try it now:
   - Load `Assets/Scenes/SampleScene.unity`
   - Open `Tools > Schema Editor` and explore `Manifest`, `Entities`, `Quests`
   - Press Play to see the `Player2DController` sample
4. Build core libraries (optional):
   - Open `Schema/Schema.sln` in Visual Studio or Rider and build
5. Run tests (optional):
   - `dotnet test Schema/Schema.Core.Tests/Schema.Core.Tests.csproj`

Add as local package from disk (optional):
1. `Window > Package Manager`
2. Click `+` > `Add package from disk...`
3. Select `SchemaPlayground/Packages/com.devzappy.schema.unity/package.json`

## Usage

- Use the Unity Editor tools (under `Tools > Schema Editor`) to import, edit, and validate schema-based data.
- Place your JSON/CSV data files in `SchemaPlayground/Content/`.
- Extend or customize schema definitions in `SchemaPlayground/Packages/com.devzappy.schema.unity/Core/Data/`. 

## Project Structure

```
Schema-Unity/
  Schema/                  # Core C# library and tests
    Schema.Core/
    Schema.Core.Tests/
  SchemaPlayground/        # Unity project with sample content and editor extensions
    Content/
    Packages/
      com.devzappy.schema.unity/
        Core/
        Editor/
  docs/                    # Guides and reference documentation
```

## Contributing

Contributions are welcome! See [docs/Contributing](docs/Contributing.md).

## Support

For help, email support@devzappy.com.

If you find Schema-Unity helpful and want to support ongoing development:
- GitHub Sponsors: [https://github.com/sponsors/zappy-dev](https://github.com/sponsors/zappy-dev)
- Ko‑fi: [https://ko-fi.com/devzappy](https://ko-fi.com/devzappy)

## License

This project is licensed under the MIT License. See the [LICENSE](LICENSE) file for details.
