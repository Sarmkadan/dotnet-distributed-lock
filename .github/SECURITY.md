# Security Policy

## Supported Versions

The following versions of the library are currently supported with security updates:

| Version | Supported          |
| ------- | ------------------ |
| 2.0.x   | :white_check_mark: |
| 1.x     | :white_check_mark: |
| < 1.0   | :x:                |

## Reporting a Vulnerability

If you discover a security vulnerability in this project, please follow these steps:

1. **Do not** report the vulnerability through public GitHub Issues.
2. Please email the maintainers directly at **rutova2@gmail.com**.
3. We will acknowledge receipt of your vulnerability report within **48 hours**.
4. We will provide an estimated timeline for addressing the vulnerability.
5. We will notify you when the vulnerability is fixed and a patch is released.

## Security Best Practices

When using this library, please consider the following security practices:

- Always validate and sanitize input parameters before passing them to lock operations
- Use appropriate lock durations based on your security requirements
- Monitor lock acquisition patterns for potential abuse
- Keep your dependencies updated through Dependabot or similar tools
- Consider using fencing tokens for write-heavy operations to prevent zombie writes