# MCPConvert TODO List

This document tracks pending tasks, known issues, and future enhancements for the MCPConvert project.

## High Priority

### OpenAPI Parser Enhancements

- [ ] **Add OpenAPI 3.1.0 Support**: The current implementation only supports OpenAPI 2.0 and 3.0 specifications. Several APIs in the APIs.guru directory use 3.1.0, which causes parsing failures. This would require updating the underlying OpenAPI parser library.
- [ ] **Address Nullability Warnings**: Fix nullability warnings in `OpenApiToMcpConverter` for better type safety and to reduce potential runtime exceptions.

### Error Handling Improvements

- [ ] **Content Type Checking**: Add content-type checking before attempting to parse responses as JSON to avoid trying to parse HTML or other non-JSON content.
- [ ] **Robust Error Messages**: Improve error messages when encountering unsupported specifications or invalid content to provide more helpful diagnostics.
- [ ] **Null Reference Handling**: Add comprehensive null checks in error paths to prevent null reference exceptions.

## Medium Priority

### URL Detection Enhancements

- [ ] **Support More Swagger UI Variants**: Enhance detection logic to handle more non-standard Swagger UI implementations and patterns.
- [ ] **Improve HTML/JS Extraction**: Enhance extraction logic for complex JavaScript patterns in Swagger UI pages.
- [ ] **Handle Redirects**: Improve handling of HTTP redirects when fetching Swagger/OpenAPI URLs.

### Testing Improvements

- [ ] **Expand Test Coverage**: Add more real-world endpoints to integration tests, especially for non-standard Swagger UIs.
- [ ] **Add Unit Tests for Edge Cases**: Create specific unit tests for edge cases and error scenarios.
- [ ] **Performance Testing**: Add tests to measure and optimize performance for large Swagger/OpenAPI documents.

## Low Priority

### Documentation and Usability

- [ ] **Document Architecture**: Provide clear documentation for adding new detectors and updating patterns.
- [ ] **API Documentation**: Improve API documentation with examples and usage scenarios.
- [ ] **UI Improvements**: Enhance the web UI for better user experience.

### Code Quality

- [ ] **Code Cleanup**: Remove commented-out code and unused methods.
- [ ] **Refactoring**: Further modularize the codebase for better maintainability.
- [ ] **Performance Optimization**: Optimize regex patterns and HTTP requests for faster processing.

## Completed Tasks

- [x] Refactor and modularize Swagger/OpenAPI URL detection logic
- [x] Implement centralized pattern management in `SwaggerUrlPatterns`
- [x] Fix regex pattern issues in `SwaggerUiUrlDetector`
- [x] Create integration tests for real-world endpoints
- [x] Add APIs.guru test suite for comprehensive validation

## Known Issues

- OpenAPI 3.1.0 specifications are not supported and will fail to parse
- Some complex Swagger UI implementations may not be detected correctly
- HTML parsing can be fragile for non-standard Swagger UI pages
