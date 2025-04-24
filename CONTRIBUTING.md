# Contributing to MCPConvert

Thank you for your interest in contributing to MCPConvert! This document provides guidelines and instructions for contributing to this project.

## Code of Conduct

By participating in this project, you agree to abide by the [Code of Conduct](CODE_OF_CONDUCT.md).

## How to Contribute

### Reporting Bugs

If you find a bug, please create an issue with the following information:
- A clear, descriptive title
- Steps to reproduce the issue
- Expected behavior
- Actual behavior
- Any relevant logs or screenshots
- Environment details (OS, browser, etc.)

### Suggesting Enhancements

Enhancement suggestions are welcome! Please provide:
- A clear description of the enhancement
- The motivation for the enhancement
- Any potential implementation details
- If applicable, examples of how the enhancement would work

### Pull Requests

1. Fork the repository
2. Create a new branch for your feature or bugfix
3. Make your changes
4. Add or update tests as necessary
5. Ensure all tests pass
6. Submit a pull request

#### Pull Request Guidelines

- Follow the coding style of the project
- Include tests for new features or bug fixes
- Update documentation as needed
- Keep pull requests focused on a single change
- Link to any relevant issues

## Development Setup

### Prerequisites

- .NET 9.0 SDK
- Visual Studio 2022 or Visual Studio Code

### Getting Started

1. Clone the repository
   ```
   git clone https://github.com/grparry/MCPConvert.git
   cd MCPConvert
   ```

2. Restore dependencies
   ```
   dotnet restore
   ```

3. Build the project
   ```
   dotnet build
   ```

4. Run the application
   ```
   dotnet run --project MCPConvert
   ```

## Testing

Run tests with:
```
dotnet test
```

## Documentation

Please update documentation when making changes:
- Update XML comments for public APIs
- Update the README.md if necessary
- Add or update examples if relevant

## Release Process

The project maintainers will handle releases. The process generally includes:
1. Updating version numbers
2. Creating release notes
3. Publishing to Azure
4. Creating a GitHub release

## Questions?

If you have questions about contributing, please open an issue with the "question" label.
