# Contributing to CachedQueries

First off, thank you for considering contributing to CachedQueries! It's people like you that make this project great.

## Code of Conduct

This project and everyone participating in it is governed by our commitment to a respectful and inclusive community. By participating, you are expected to uphold this standard.

## How Can I Contribute?

### Reporting Bugs

Before creating bug reports, please check the existing issues to avoid duplicates. When you create a bug report, include as many details as possible:

- **Use a clear and descriptive title**
- **Describe the exact steps to reproduce the problem**
- **Provide specific examples** (code snippets, configuration)
- **Describe the behavior you observed and what you expected**
- **Include your environment details** (.NET version, EF Core version, cache provider)

### Suggesting Enhancements

Enhancement suggestions are tracked as GitHub issues. When creating an enhancement suggestion:

- **Use a clear and descriptive title**
- **Provide a detailed description of the proposed functionality**
- **Explain why this enhancement would be useful**
- **List any alternatives you've considered**

### Pull Requests

1. Fork the repo and create your branch from `main`
2. If you've added code that should be tested, add tests
3. Ensure the test suite passes (`dotnet test`)
4. Make sure your code follows the existing code style
5. Write a good commit message

## Development Setup

```bash
# Clone your fork
git clone https://github.com/YOUR_USERNAME/CachedQueries.git
cd CachedQueries

# Restore dependencies
dotnet restore

# Build the solution
dotnet build

# Run tests
dotnet test
```

## Project Structure

```
src/
├── CachedQueries/           # Main library
│   ├── Abstractions/        # Interfaces
│   ├── Extensions/          # Extension methods
│   ├── Interceptors/        # EF Core interceptors
│   ├── Internal/            # Internal implementations
│   └── Providers/           # Cache provider implementations
├── CachedQueries.Redis/     # Redis provider package
└── CachedQueries.Tests/     # Unit tests
```

## Coding Guidelines

- Follow C# naming conventions
- Use `async/await` for asynchronous code
- Add XML documentation comments for public APIs
- Keep methods focused and small
- Write unit tests for new functionality

## Commit Messages

- Use the present tense ("Add feature" not "Added feature")
- Use the imperative mood ("Move cursor to..." not "Moves cursor to...")
- Limit the first line to 72 characters or less
- Reference issues and pull requests after the first line

## Release Process

Releases are automated via GitHub Actions:

1. Update version in `Directory.Build.props` if needed
2. Update `CHANGELOG.md`
3. Create a git tag: `git tag v3.x.x`
4. Push the tag: `git push origin v3.x.x`
5. GitHub Actions will build, test, and publish to NuGet

## Questions?

Feel free to open an issue with your question or reach out to the maintainers.

Thank you for contributing! 🎉
