# 🔍 Mail Archiver Search Guide

[← Back to Documentation Index](Index.md)

## 📋 Overview

This guide provides detailed instructions for using the search functionality in the Mail Archiver application.


## 🚀 Accessing the Search Function

1. After logging in, navigate to the "Archive" section from the main menu at the top
2. The search interface will be displayed at the top of the email list

## 🎯 Search Interface

The search interface includes several fields that allow you to filter your emails:

- **Search Term**: Enter keywords to search for
- **From Date**: Filter emails received after a specific date
- **To Date**: Filter emails received before a specific date
- **Account**: Filter emails by specific email accounts
- **Direction**: Filter emails by direction (Incoming, Outgoing, or All)

Note: The search term is automatically applied to the email content, subject, body as well as sender and recipient fields (From, To, Cc, and Bcc).

## 💡 Search Tips

### 📝 Basic Search
- Enter keywords in the search term field to search for emails containing those keywords
- The search is case-insensitive
- Keywords are matched in the email content, subject, and body, as well as in the sender and recipient fields (From, To, Cc, and Bcc)

### 🔍 Advanced Search Features
- **Exact phrases**: Use quotes to search for exact phrases, e.g., "important meeting"
- **Field-specific search**: Search within specific fields using field:term format
  - subject:invoice - Search for emails with "invoice" in the subject
  - from:john - Search for emails from "john" in the sender field
  - to:domain - Search for emails to a specific domain in the recipient field
  - body:"important" - Search for emails with "important" in the body
- **Combining features**: You can combine both exact phrases and field-specific search
  - "car insurance" subject:invoice from:insurance

## 👁️ Previewing Emails

When you click on "Details" at the right side of every search result:

- The email content will be displayed in the email viewer
- Attachments will be listed in the attachment section
- You can download attachments directly
- You can download the mail as EML
- You can use the restore functionality to restore the mail to the mail server

## 🚀 Performance Tips

- For better search performance, use specific date ranges when possible
- Use multiple filter criteria to reduce the number of results
- If you have a large email archive, consider using specific keywords rather than general terms
