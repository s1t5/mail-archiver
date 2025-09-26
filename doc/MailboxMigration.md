# Mailbox Migration Guide

This guide explains how to migrate a mailbox from one email provider to another using the Mail Archiver application.

## Overview

The Mail Archiver application provides functionality to migrate emails from a source mailbox to a target mailbox. This is particularly useful when changing email providers while preserving your email history.

## Migration Process

Follow these steps to migrate your mailbox:

1. **Add the source account**
   - Navigate to the "Email Accounts" section
   - Click "Add Account"
   - Enter your source account details and credentials
   - Save the account

2. **Synchronize the source account**
   - Allow the application to synchronize all emails from the source account
   - Wait for the synchronization process to complete

3. **Add the target account**
   - In the "Email Accounts" section, click "Add Account"
   - Enter your target account details and credentials
   - Save the account

4. **Synchronize the target account**
   - If the target account is new, synchronize it to ensure it's properly set up
   - Wait for the synchronization process to complete

5. **Initiate the migration**
   - In the "Email Accounts" section, identify the source account
   - Go to the account details
   - Select "Copy All Emails to Another Mailbox" option
   - Choose the target account and the target folder in this account
   - Start the migration process

6. **Monitor the migration**
   - If there is a large number of emails, the migration will be carried out as a background task
   - The status and progress of the migration can be viewed in the "Jobs" tab
   - Wait for the migration to complete successfully

7. **Disable the source account**
   - After the successful transfer, go back to the source account
   - Set the source account to "Disabled" in the account settings
   - This ensures that it will no longer be archived in the future

## Important Notes

- Ensure that the source account has sufficient permissions to read all emails
- Make sure that the target account has sufficient storage space for the migrated emails
- The migration process may take some time depending on the number of emails and the connection speed
- It is recommended to perform the migration during off-peak hours to minimize the impact on the email service
- Regularly check the migration status in the "Jobs" tab to ensure the process is progressing
- After migration, verify that all emails have been successfully transferred to the target account

## Best Practices

- Always test the migration process with a small subset of emails before migrating the entire mailbox
- Keep a backup of important emails before starting the migration
- Schedule the migration during a time when email usage is minimal
- Monitor the migration progress closely and be prepared to address any issues that may arise
- After the migration is complete, verify that all emails are correctly transferred and accessible
