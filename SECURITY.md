# Security Policy

## Supported Versions

Currently, we provide security updates for the following versions:

| Version | Supported          |
| ------- | ------------------ |
| 1.0.x   | :white_check_mark: |

## Reporting a Vulnerability

We take the security of MCPConvert seriously. If you believe you've found a security vulnerability, please follow these steps:

1. **Do not disclose the vulnerability publicly**
2. **Email the project maintainers** with details about the vulnerability
3. You should receive a response within 48 hours
4. We will work with you to understand and address the issue
5. Once the vulnerability is fixed, we will coordinate the disclosure

## Security Considerations

MCPConvert is designed with the following security considerations:

1. **Input Validation**: All Swagger/OpenAPI inputs are validated before processing
2. **Resource Limits**: Quotas and rate limiting are implemented to prevent resource exhaustion
3. **No Persistent Storage**: The application does not store user data beyond the current session
4. **HTTPS**: All communications should be secured with HTTPS

## Best Practices for Deployment

When deploying MCPConvert, we recommend:

1. Using HTTPS for all communications
2. Implementing appropriate rate limiting at the infrastructure level
3. Regularly updating dependencies to address security vulnerabilities
4. Monitoring application logs for unusual activity

## Third-Party Dependencies

MCPConvert uses several third-party dependencies. We regularly monitor these for security vulnerabilities and update as necessary.
