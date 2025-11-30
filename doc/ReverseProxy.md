# 🌐 Reverse Proxy Configuration Guide

[← Back to Documentation Index](Index.md)

## 📋 Overview

This guide provides comprehensive instructions for configuring reverse proxy servers to work properly with the Mail Archiver application, particularly for OIDC authentication scenarios where HTTPS protocol handling is critical.

## 📚 Table of Contents

1. [Overview](#overview)
3. [Required HTTP Headers](#required-http-headers)

## 🌐 Overview

When deploying Mail Archiver behind a reverse proxy, proper configuration of HTTP headers is essential for:
- Correct HTTPS protocol detection
- Proper OIDC redirect URL generation
- Accurate client IP address logging
- Security header management

## 📡 Required HTTP Headers

Mail Archiver requires the following headers to be set by your reverse proxy:

| Header | Value | Purpose |
|--------|-------|---------|
| `X-Forwarded-Proto` | `https` | Indicates the original protocol |
| `X-Forwarded-Host` | `mailarchiver.domain.com` | Original host header |
| `X-Forwarded-For` | Client IP address | Original client IP |

---

**Note**: This guide is current as of 2025. Reverse proxy software regularly updates their configurations and features, so some settings may differ. Always refer to the latest documentation from your reverse proxy provider for the most up-to-date information.
