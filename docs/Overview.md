## Overview

Schema-Unity integrates the Schema content system into Unity. It lets designers and engineers define data schemes (tables), edit entries in a purpose-built editor, validate data types, and publish both runtime-ready data and strongly-typed C# accessors.

### What problems it solves
- Centralizes game design data (e.g., Entities, Quests) in editable schemes.
- Validates data with typed attributes (Text, Number, Boolean, FilePath, etc.).
- Publishes to Unity Resources for runtime loading and generates C# wrappers for type-safe access.

### Core components
- Schema Core (C#): Data model, types, serialization, storage abstraction.
- Unity Editor tools: `Tools > Schema Editor` and `Tools > Schema Debugger` for editing and diagnostics.
- Runtime integration: `Schema.Runtime.SchemaRuntime.Initialize()` loads published data from `Resources/`.

### Typical workflow
1. Open the Unity project in `SchemaPlayground/`.
2. Open `Tools > Schema Editor` to view and edit schemes.
3. Modify entries (rows) and attributes (columns).
4. Publish data to Resources and generate C# code.
5. Access data at runtime using the generated wrappers.

### Example concepts
- Manifest: The main registry listing other schemes and codegen settings.
- Scheme: A table definition with attributes and entries.
- Attribute: A typed column (e.g., `TextDataType`, `BooleanDataType`).
- Entry: A row in a scheme, optionally with an identifier attribute.


