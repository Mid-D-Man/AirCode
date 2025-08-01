# Security Policy

## Supported Versions

| Version | Supported          |
| ------- | ------------------ |
| 1.0.x   | :white_check_mark: |
| < 1.0   | :x:                |

## Reporting a Vulnerability

### Contact Methods
- **Email**: security@[your-domain].com
- **GitHub**: Private security advisories
- **Response Time**: 48 hours acknowledgment, 7 days initial assessment

### Process
1. **DO NOT** create public issues for security vulnerabilities
2. Provide detailed reproduction steps
3. Include impact assessment
4. Suggest mitigation if known

### What to Expect
- Acknowledgment within 48 hours
- Regular updates every 72 hours
- Public disclosure coordination after fix deployment
- Recognition in security credits (if desired)

## Security Measures

### Current Protections
- Content Security Policy (CSP) implementation
- Auth0 OIDC with PKCE flow
- Role-based authorization policies
- Encrypted local storage for sensitive data

### Known Security Considerations
- Firebase API keys are public by design (client-side apps)
- Firestore security rules provide actual access control
- Local storage encryption for offline credentials

### Reporting Guidelines
**Critical**: Authentication bypass, data exposure, privilege escalation
**High**: XSS, CSRF, insecure direct object references
**Medium**: Information disclosure, weak cryptography
**Low**: UI redressing, verbose error messages
