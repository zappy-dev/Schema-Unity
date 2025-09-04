# Schema-Unity

Schema-Unity provides Unity integrations for the Schema content management tool, enabling robust, flexible, and maintainable management of game design data within Unity projects.

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
      dev.czarzappy.schema.unity/
        Core/
        Editor/
```

## Getting Started

### Prerequisites

- **Unity** (recommended version: 2021.3+)
- **.NET SDK** (for building and testing core libraries)

### Setup

1. **Clone the repository:**
   ```bash
   git clone https://github.com/zappy-dev/Schema-Unity.git
   cd Schema-Unity
   ```

2. **Open in Unity:**
   - Open `SchemaPlayground/` as a Unity project.

3. **Build Core Libraries:**
   - Open `Schema/Schema.sln` in Visual Studio or Rider.
   - Build the solution to generate the core DLLs.

4. **Run Tests:**
   - Run tests in `Schema.Core.Tests` using your preferred .NET test runner.

## Usage

- Use the Unity Editor tools (under `Window > Schema`) to import, edit, and validate schema-based data.
- Place your JSON/CSV data files in `SchemaPlayground/Content/`.
- Extend or customize schema definitions in `Schema/Core/Data/`.

## CI/CD & Build Verification

This project includes comprehensive CI/CD workflows to ensure code quality:

### .NET Core CI
- **File**: `.github/workflows/dotnet.yml`
- **Purpose**: Tests the core Schema library
- **Coverage**: Runs unit tests with code coverage reporting
- **Triggers**: Push/PR to main branch


### Status Badges
![.NET Build](https://github.com/zappy-dev/Schema-Unity/workflows/.NET%20Core%20CI/badge.svg)

## Contributing

Contributions are welcome! To contribute:

1. Fork the repository.
2. Create a new branch (`git checkout -b feature/your-feature`).
3. Make your changes and add tests.
4. Commit and push your branch.
5. Open a pull request describing your changes.

## License

This project is licensed under the MIT License. See the [LICENSE](LICENSE) file for details.

## Contact

For questions or support, open an issue or contact the maintainer.
