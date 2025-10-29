# Contributing to DropBear.Codex

Thank you for considering contributing to DropBear.Codex! This document provides guidelines and requirements for contributing to the project.

## Prerequisites

- **.NET 9 SDK** or later
- A .NET IDE (JetBrains Rider, Visual Studio 2022, or VS Code with C# extension)
- Git
- Familiarity with Railway-Oriented Programming and the Result pattern

## Development Setup

1. **Fork and clone** the repository:
```bash
git clone https://github.com/YOUR_USERNAME/DropBear.Codex.git
cd DropBear.Codex
```

2. **Restore packages**:
```bash
dotnet restore
```

3. **Build the solution**:
```bash
dotnet build
```

4. **Run tests**:
```bash
dotnet test
```

## Branching Strategy

- **`master`** (or `main`): Protected production branch. All changes must go through Pull Requests.
- **`develop`**: Integration branch for ongoing development.
- **Feature branches**: `feat/<short-descriptive-name>` (e.g., `feat/add-retry-policy`)
- **Bug fixes**: `fix/<short-descriptive-name>` (e.g., `fix/null-reference-in-hasher`)
- **Documentation**: `docs/<short-descriptive-name>` (e.g., `docs/update-workflow-readme`)
- **Chores**: `chore/<short-descriptive-name>` (e.g., `chore/update-dependencies`)

## Commit Guidelines

We follow **Conventional Commits** for clear and consistent commit messages:

```
<type>(<scope>): <subject>

<body>

<footer>
```

### Types:
- `feat`: New feature
- `fix`: Bug fix
- `docs`: Documentation changes
- `refactor`: Code refactoring (no functional changes)
- `test`: Adding or updating tests
- `chore`: Maintenance tasks, dependency updates
- `perf`: Performance improvements
- `ci`: CI/CD pipeline changes

### Examples:
```bash
feat(workflow): add parallel node execution support
fix(serialization): resolve null reference in MessagePack deserializer
docs(readme): update installation instructions for .NET 9
test(core): add comprehensive Result pattern tests
```

## Code Style and Standards

### General Principles

1. **Railway-Oriented Programming**: All operations that can fail MUST return `Result<T, TError>` or `Result<TError>`. Never throw exceptions for expected errors.

2. **Async/Await**: Use `async`/`await` with `ValueTask` for all async operations:
```csharp
public async ValueTask<Result<User, UserError>> GetUserAsync(int id)
{
    var user = await _repository.FindAsync(id).ConfigureAwait(false);
    return user is not null
        ? Result<User, UserError>.Success(user)
        : Result<User, UserError>.Failure(UserError.NotFound(id));
}
```

3. **ConfigureAwait**: Always use `.ConfigureAwait(false)` in library code.

4. **Nullable Reference Types**: Enabled project-wide. Handle nullability explicitly.

5. **Immutability**: Prefer `record` types and `init` properties over mutable classes.

6. **Code Analyzers**: The project uses Meziantou.Analyzer and Roslynator. Fix all warnings in Release builds.

### Result Pattern Requirements

When contributing code, follow these Result pattern guidelines:

**DO:**
- Use `Result<T, TError>` for operations that can fail
- Use factory methods for creating errors
- Preserve exception context in errors
- Add metadata to errors for debugging
- Chain operations with `Map`/`Bind` for functional pipelines

**DON'T:**
- Throw exceptions for expected errors
- Ignore `IsSuccess` checks
- Create Results with null values
- Use `ValueOrThrow()` unless absolutely necessary

### EditorConfig

Follow the `.editorconfig` settings:
- Indentation: 4 spaces
- Line endings: CRLF (Windows) or LF (Unix)
- Charset: UTF-8
- Trim trailing whitespace
- Insert final newline

## Testing Requirements

### Unit Tests

1. **Coverage**: All new public APIs must have unit tests.
2. **Framework**: Use xUnit, FluentAssertions for assertions.
3. **Naming**: Use descriptive test names:
```csharp
[Fact]
public void GetUser_WhenUserNotFound_ReturnsFailureResult()
{
    // Arrange
    var repository = new MockUserRepository();

    // Act
    var result = repository.GetUser(999);

    // Assert
    result.IsSuccess.Should().BeFalse();
    result.Error.Should().BeOfType<UserError>();
}
```

4. **Test Organization**:
   - One test class per production class
   - Group related tests with nested classes
   - Use `[Theory]` for parameterized tests

### Running Tests

```bash
# Run all tests
dotnet test

# Run tests with coverage
dotnet test --collect:"XPlat Code Coverage"

# Run specific project tests
dotnet test DropBear.Codex.Core.Tests
```

## Pull Request Process

1. **Create a feature branch** from `develop`:
```bash
git checkout develop
git pull origin develop
git checkout -b feat/your-feature-name
```

2. **Make your changes** following the code style guidelines.

3. **Write tests** for all new functionality.

4. **Update documentation**:
   - Update README.md if adding new features
   - Update CHANGELOG.md under `[Unreleased]`
   - Add XML documentation comments to public APIs

5. **Commit your changes** using Conventional Commits.

6. **Push to your fork**:
```bash
git push origin feat/your-feature-name
```

7. **Open a Pull Request** with:
   - Clear title following Conventional Commits
   - Description of what changed and why
   - Link to any related issues
   - Screenshots/examples if applicable

### PR Requirements

- [ ] All tests pass
- [ ] Code builds without warnings in Release mode
- [ ] New code has unit tests
- [ ] Public APIs have XML documentation
- [ ] CHANGELOG.md updated
- [ ] Follows Result pattern guidelines
- [ ] No introduction of exceptions for expected errors

## Documentation

### XML Documentation

All public APIs require XML documentation:

```csharp
/// <summary>
/// Retrieves a user by their unique identifier.
/// </summary>
/// <param name="userId">The unique identifier of the user.</param>
/// <returns>
/// A <see cref="Result{T, TError}"/> containing the user if found,
/// or a <see cref="UserError"/> if not found.
/// </returns>
public Result<User, UserError> GetUser(int userId)
{
    // Implementation
}
```

### README Files

- Each project should have a README.md in its directory
- Include usage examples
- Document any configuration options
- Explain error types and handling

## Release Process

1. **Version Bump**: Update version in `Directory.Build.props` or individual `.csproj` files.
2. **CHANGELOG**: Move items from `[Unreleased]` to new version section.
3. **Tag Release**: Create a tag `v<version>` (e.g., `v2025.11.0`).
4. **NuGet**: Packages are automatically published via GitHub Actions.

## Architecture Guidelines

### Dependency Rules

- **Core**: Foundation layer. No dependencies on other DropBear.Codex projects.
- **Other projects**: May depend on Core and other projects, but **no circular dependencies**.
- Use dependency injection for services.

### Result Pattern

All error-prone operations return `Result<T, TError>`:

```csharp
// Good
public Result<User, UserError> GetUser(int id)

// Bad
public User GetUser(int id) // Could throw exception
```

### Async Patterns

Use `ValueTask` for better performance:

```csharp
// Good
public async ValueTask<Result<User, UserError>> GetUserAsync(int id)

// Avoid
public async Task<Result<User, UserError>> GetUserAsync(int id)
```

## Questions or Issues?

- Open an issue for bugs or feature requests
- Start a discussion for questions or ideas
- Check existing issues before creating new ones

## Code of Conduct

- Be respectful and professional
- Provide constructive feedback
- Help others learn and grow
- Follow the project's technical standards

## License

By contributing, you agree that your contributions will be licensed under the MIT License.
