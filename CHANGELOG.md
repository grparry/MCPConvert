# Changelog

All notable changes to the MCPConvert project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.0] - 2025-04-23

### Added
- Initial release of MCPConvert
- Web UI for Swagger/OpenAPI to MCP conversion
- Support for file upload and URL-based conversion
- Source mapping between MCP elements and Swagger source
- Diagnostic mode for detailed conversion information
- Caching for improved performance
- Usage tracking and quota enforcement
- MCPBuckle integration for `.well-known/mcp-context` endpoint
- Comprehensive documentation
- Azure deployment configuration
- GitHub Actions workflow for CI/CD

### Security
- Input validation for all Swagger/OpenAPI inputs
- Resource limits to prevent exhaustion
- No persistent storage of user data
