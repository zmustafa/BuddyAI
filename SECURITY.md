# 🔒 Security Policy – BuddyAI Desktop

BuddyAI Desktop is designed as a **desktop-native AI workspace** with support for multiple providers (OpenAI, Claude, Grok, local LLMs, etc.).

Security is a top priority, especially given:
- API key usage
- OAuth integrations
- Local and remote model execution
- Clipboard and screen capture features

---

## 📌 Supported Versions

We actively support the latest release.

| Version | Supported |
|--------|----------|
| Latest | ✅ Yes   |
| Older  | ❌ No    |

Always upgrade to the latest version to receive security fixes.

---

## 🚨 Reporting a Vulnerability

If you discover a security vulnerability:

- ❌ Do NOT open a public GitHub issue  
- ✅ Report privately via:
  - GitHub Security Advisory (preferred)
  
---

### 📋 Include in Your Report

Provide as much detail as possible:

- Description of the issue  
- Steps to reproduce  
- Impact assessment  
- Screenshots or logs (if applicable)  
- Suggested mitigation (optional)

---

### ⏱️ Response Timeline

- Initial response: **within 48 hours**
- Triage & validation: **1–5 days**
- Fix & release: depends on severity

---

## 🔐 Security Model

BuddyAI follows a **local-first, user-controlled security model**.

### Key Principles:

- No forced cloud dependency  
- No hidden data exfiltration  
- User controls all provider connections  
- Secrets remain on the user machine  

---

## 🔑 Secrets & API Keys

BuddyAI may store:

- API keys (OpenAI, Anthropic, etc.)
- Provider configurations
- OAuth tokens

### Protections:

- Stored locally (user profile / app data)
- Never transmitted except to configured provider endpoints
- No telemetry collection of secrets

---

### ⚠️ User Responsibility

- Do not share config files publicly  
- Do not commit API keys to GitHub  
- Rotate keys regularly  

---

## 🔐 OAuth Integrations

BuddyAI supports OAuth-based providers.

### Security Notes:

- Tokens are stored locally
- Standard OAuth flows are used
- No credential interception

---

## 🖥️ Local LLM Integrations (Ollama / LM Studio)

BuddyAI can connect to local endpoints:

- `http://localhost:11434` (Ollama)
- `http://127.0.0.1:1234` (LM Studio)

### Considerations:

- Ensure endpoints are not exposed publicly
- Use firewall rules where applicable
- Avoid binding to `0.0.0.0` unless secured

---

## 🧠 Prompt & Data Handling

BuddyAI processes:

- User input text
- Screenshots (optional)
- Clipboard data (Lens feature)

### Behavior:

- Data is sent only to the selected provider
- No background transmission
- No logging of sensitive content unless user-enabled

---

## 🖼️ Screenshot / Snip Feature

BuddyAI includes a **screen capture + AI analysis feature**.

### Risks:

- Sensitive data capture (tokens, passwords, PII)

### Mitigation:

- User-initiated only
- No automatic capture
- Clear UI before submission

---

## 🌐 Network Communication

BuddyAI communicates only with:

- Configured AI providers
- User-defined endpoints

### No:

- Hidden telemetry
- Background analytics
- Third-party tracking

---

## 📦 Dependency Security

We recommend:

- Keeping dependencies updated
- Monitoring known vulnerabilities
- Avoiding untrusted NuGet packages

---

## 🧪 Secure Development Practices

Contributors must:

- Never commit secrets
- Avoid hardcoded credentials
- Validate all external inputs
- Handle API errors safely
- Follow least privilege principles

---

## 🚫 Known Limitations

- No secure enclave for key storage (relies on OS/user security)
- Local configs may be readable if system is compromised
- External providers control their own security posture

---

## 🛡️ Recommended Hardening (Enterprise Use)

For enterprise environments:

- Use endpoint protection (EDR)
- Restrict outbound traffic to approved endpoints
- Enforce disk encryption (BitLocker)
- Use managed identities where possible (future support)
- Monitor usage of AI providers

---

## 🔄 Security Updates

Security fixes are released as part of normal updates.

Users should:

- Always run the latest version
- Monitor GitHub releases

---

## 🙌 Acknowledgements

We appreciate responsible disclosure and collaboration to improve security.

---

## ⚡ Summary

BuddyAI is built with:

- **User-controlled data flow**
- **Local-first architecture**
- **Transparent provider communication**

> You control your data. Always.

---
