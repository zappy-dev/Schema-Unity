# Schema-Unity

Schema-Unity provides Unity integrations for the Schema content management tool, enabling robust, flexible, and maintainable management of game design data within Unity projects.

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

## Getting Started

See the full [Quickstart](docs/Quickstart.md). Summary:

### Prerequisites

- **Unity** (recommended version: 2021.3+)
- **.NET SDK 8** (for building and testing core libraries)

### Setup

1. **Clone the repository:**
   ```bash
   git clone https://github.com/zappy-dev/Schema-Unity.git
   cd Schema-Unity
   ```

2. **Open in Unity:**
   - Open `SchemaPlayground/` as a Unity project.

3. **Build Core Libraries (optional for Unity-only workflows):**
   - Open `Schema/Schema.sln` in Visual Studio or Rider.
   - Build the solution to generate the core DLLs.

4. **Run Tests (optional):**
   - Run tests in `Schema.Core.Tests` using your preferred .NET test runner.

## Usage

- Use the Unity Editor tools (under `Tools > Scheme Editor`) to import, edit, and validate schema-based data.
- Place your JSON/CSV data files in `SchemaPlayground/Content/`.
- Extend or customize schema definitions in `SchemaPlayground/Packages/com.devzappy.schema.unity/Core/Data/`.

## Contributing

Contributions are welcome! See [docs/Contributing](docs/Contributing.md).

## Support

For help, email support@devzappy.com.

## License

This project is licensed under the MIT License. See the [LICENSE](LICENSE) file for details.
