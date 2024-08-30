# DropBear Codex

**DropBear Codex** is a collection of modular C# libraries built with .NET 8, designed to provide robust, reusable components for a wide range of software development needs. The libraries are organized into several projects, each addressing specific areas such as Blazor components, encoding, file management, hashing, operations management, serialization, state management, utilities, and validation. All libraries are published as NuGet packages for easy integration.

## Table of Contents
- [Projects](#projects)
- [Getting Started](#getting-started)
- [Installation](#installation)
- [Usage](#usage)
- [Contributing](#contributing)
- [License](#license)

## Projects
The solution includes the following projects:

- **DropBear.Blazor**: Custom Blazor component library.
- **DropBear.Codex.Core**: Core return type classes used throughout the other projects.
- **DropBear.Codex.Encoding**: Basic rANS encoding algorithm implementation.
- **DropBear.Codex.Files**: Custom file format with classes to read, write, and manage this custom file type, including serialization and verification hashes.
- **DropBear.Codex.Hashing**: Various hashing implementations.
- **DropBear.Codex.Operation**: Basic and advanced operation managers for queuing up operations and executing them with recovery and fallback support.
- **DropBear.Codex.Serialization**: Custom serialization wrappers around JSON, MessagePack, and more.
- **DropBear.Codex.StateManagement**: State machine implementation for managing application states.
- **DropBear.Codex.Utilities**: Extension methods and helper/utility classes.
- **DropBear.Codex.Validation**: Validation strategy implementations.

## Getting Started
To get started with the **DropBear Codex** solution:

1. **Clone the repository**:
```bash
git clone https://github.com/tkuchel/dropbear-codex.git
```
2. **Open the solution** in your preferred .NET development environment (e.g., JetBrains Rider or Visual Studio).

3. **Build the solution** to ensure all projects compile correctly.

## Installation
The libraries are available as NuGet packages. To install a package, use the following command:

```bash
dotnet add package DropBear.Codex.<LibraryName>
```

Replace `<LibraryName>` with the specific library you wish to install.

## Usage
Each library is designed to be modular and can be used independently. Detailed usage instructions can be found in the individual library’s README files (coming soon).

Here’s a basic example of how to use the `DropBear.Codex.Core` library:

```csharp
using DropBear.Codex.Core;

var result = Result.Success("Operation completed successfully.");
if (result.IsSuccess)
{
    Console.WriteLine(result.Message);
}
```

## Contributing
Contributions are welcome! If you’d like to contribute, please follow these steps:

1. Fork the repository.
2. Create a new branch (`git checkout -b feature/your-feature`).
3. Make your changes and commit them (`git commit -m 'Add your feature'`).
4. Push to the branch (`git push origin feature/your-feature`).
5. Open a Pull Request.

Please make sure your code adheres to the project's coding standards and includes relevant tests.

## License
This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.
