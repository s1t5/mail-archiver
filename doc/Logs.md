# 📋 Access Log

[← Back to Documentation Index](Index.md)

## 📋 Overview

This guide explains the access log functionality in the Mail Archiver application, which enables logging and viewing of user activities within the system. This functionality is crucial for monitoring, security, and debugging purposes.

## 📊 What is Logged

The application logs various types of user activities, including:

1. **Login and Logout** - Logging of login and logout operations
2. **Search Operations** - Logging of email search requests within the system
3. **Email Opening** - Logging when users open email content
4. **Email Downloading** - Logging when users download emails
5. **Email Restoration** - Logging when users restore archived emails
6. **Account Management** - Logging of account-related actions
7. **Deletion Operations** - Logging of deletion actions

## 📝 Action Types and Descriptions

The logged actions are categorized into the following types:

### Login
- **Description**: Logs when a user logs into the system
- **Details**: Username, timestamp

### Logout
- **Description**: Logs when a user logs out of the system
- **Details**: Username, timestamp

### Search
- **Description**: Logs search requests within the email system
- **Details**: Username, timestamp, search parameters

### Open
- **Description**: Logs when a user opens an email
- **Details**: Username, timestamp, email ID, sender, subject

### Download
- **Description**: Logs when users download emails
- **Details**: Username, timestamp, email ID, sender, subject

### Restore
- **Description**: Logs when users restore archived emails
- **Details**: Username, timestamp, email ID, sender, subject

### Account
- **Description**: Logs account-related actions (creation, modification, etc.)
- **Details**: Username, timestamp, account ID

### Deletion
- **Description**: Logs deletion operations on emails or other resources
- **Details**: Username, timestamp, affected resource
- **Compliance Note**: Deletion operations are logged for compliance auditing purposes

## 🔍 Filter Options

The log page offers the following filter options:

1. **Date** - Filter by date range (From-To)
2. **Username** - Filter by specific user (Admin only)
3. **Action Type** - Filter by specific action type

## 🖥️ Log Display

Logs are displayed in a table format with the following columns:

- **Timestamp**: Date and time of the action
- **Username**: Name of the involved user
- **Type**: Type of action with color coding
- **Information**: Additional details about the action

For mobile devices, logs are displayed in card format.

## 🔐 Access Permissions

- **Non-admin users**: Can only view their own logs
- **Admin users**: Can view logs for all users and filter by user
