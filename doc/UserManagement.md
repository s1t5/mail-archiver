# 👥 User Management and Mailbox Permissions

[← Back to Documentation Index](Index.md)

## 📋 Overview

This guide provides detailed instructions for creating new user accounts and assigning mailbox permissions in the Mail Archiver application.


## 🛠️ Prerequisites

- Administrative access to the Mail Archiver application
- Existing email accounts configured in the system

## 🧑‍💻 Creating a New User Account

1. Log into the Mail Archiver application with an account that has administrative privileges
2. Navigate to the "Users" section from the main menu
3. Click the "Create New User" button
4. Fill in the required user information:
   - **Username**: Enter a unique username for the new user
   - **Password**: Enter a secure password for the user
   - **Email**: Email address of the user
   - **Admin**: Check this box if the user should have administrative privileges
   - **Self Manager**: Check this box if the user should be able to manage their own mail accounts (edit existing ones and add new ones as well as delete accounts)
   - **active**: Should be checked to allow logins for the user
5. Click "Create User" to save the new user account

## 🔐 Assigning Mailbox Permissions to Users

1. After creating the user, or when editing an existing user, navigate to the "Users" section from the main menu
2. Find the user in the list where you want to assign mail accounts and click on the "Assign" button for that user
3. In the "Assign Mail Accounts" page, you will see all available mail accounts and checkboxes which indicate if they are assigned to the user
5. To assign a new email account check the corresponding box for the specific account
6. To remove an email account from the user uncheck the box
7. Click "Save Assignments" to apply the changes

## 👤 User Account Permissions

Standard Users with access to specific email accounts can:
- View archived emails from those accounts
- Search within those email accounts
- Export emails from those accounts
- Restore emails from those accounts
- Access email attachments

Users cannot access:
- Email accounts that have not been assigned to them
- Administrative settings (unless they have the Admin flag)
- Other users' account information

Users with the "Self Manager"-Role can additionally manage their own account settings, as well as add new Accounts. They can also delete accounts assigned to them.

## 🔒 Security Considerations

1. Use strong passwords for all user accounts
2. Only assign the Admin permission to users who need it
3. Only assign the Self Manager permission to users who need to manage their own accounts
4. Regularly review user permissions to ensure access is appropriate
5. Remove user access when it is no longer needed
6. Use the principle of least privilege - only assign access to the email accounts that users need
