# 👥 User Management and Mailbox Permissions

[← Back to Documentation Index](index.md)

## 📋 Overview

This guide provides detailed instructions for creating new user accounts and assigning mailbox permissions in the Mail Archiver application. It also covers changing your own password and the forced password change on a freshly installed instance.


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

### Admin Users
Admin users have full access to the application and can:
- Manage all user accounts and their permissions
- Access all email accounts in the system
- View all access logs
- Perform all administrative tasks

### Self Manager Users
Self manager users can:
- Add new email accounts to their own account
- Edit existing email accounts assigned to them
- Delete email accounts assigned to them
- Access and manage emails from their assigned accounts (including deletion)
- View their own access logs

### Standard Users
Standard users have limited access and can:
- View archived emails from assigned email accounts
- Search within their assigned email accounts
- Export emails from their assigned accounts
- Restore emails from their assigned accounts
- Access email attachments from assigned accounts

## 🔑 Changing Your Own Password

Every user with a local password can change it without an administrator:

1. Click your **username** in the top-right corner to open the user menu.
2. Choose **Change Password**.
3. Enter your **current password** and the **new password** twice, then submit.

The new password must meet the requirements below, and it must be **different
from the current one**.

> ℹ️ **OIDC users cannot change their password here.** The page answers with
> *"OIDC users cannot change their password. Password management is handled by
> your OIDC provider."* Change the password at your identity provider instead —
> see [OIDC / SSO](OIDC_Implementation.md).

### Password requirements

- at least **10 characters**
- at least one **uppercase** letter
- at least one **lowercase** letter
- at least one **number**
- at least one **special character**

The form shows these as a live checklist while you type. The same rules apply when
an administrator creates a user or resets a password.

## ⚠️ Forced Password Change (Initial Setup)

A freshly installed instance starts with the administrator credentials from the
configuration (`Authentication__Username` / `Authentication__Password`). As long as
that default password is still in use **and no mail account exists yet**, the first
login forces a change:

- A warning banner explains why: *"For security reasons, you must change your
  password before continuing. This is required because you are using the default
  system credentials on a newly set up system."*
- You cannot navigate away from the page: *"You must change your password before
  you can continue using the system."*
- The new password must be different from the current one.
- After saving, the flag is cleared and you are taken to the dashboard.

Once you have changed the default password, the forced change never appears again.

## 🕒 Staying Signed In — the "Remember Me" Checkbox

The login form has a checkbox labelled **Remember Me** (*angemeldet bleiben* in
the German UI). It does exactly one thing: it decides whether the
authentication cookie is a session cookie or a persistent one.

| | Checkbox unticked | Checkbox ticked |
|---|---|---|
| Cookie lifetime | `Authentication__SessionTimeoutMinutes` (default **60 minutes**) | **7 days** |
| Cookie type | Session cookie — discarded when the browser closes | Persistent cookie — stored on disk |
| Browser restart | You must log in again | You stay signed in |
| Idle timeout | Extended on every request (`SlidingExpiration`) | Extended on every request, up to the absolute 7-day limit |
| Sign out | Ends the session immediately | Ends the session immediately |

So the checkbox turns "signed in for this browser session, at most 60 minutes
idle" into "signed in for up to 7 days, including browser and machine
restarts".

### Things the checkbox does not do

- **It does not remember your password or username.** The login form is not
  pre-filled on the next visit; only the session cookie is kept.
- **The 7 days are a hard limit and cannot be configured.** The value is fixed
  in the code (`Auth/Services/CookieAuthenticationService.cs`). Only the
  non-persistent lifetime follows `Authentication__SessionTimeoutMinutes`.
- **It is not an inactivity timer.** 7 days are counted from the login; once
  they are up, a new login is required even if you used the archive a minute
  ago.
- **It does not survive a logout.** Clicking *Logout* deletes the cookie
  regardless of the checkbox.

### Interaction with 2FA

With two-factor authentication enabled, the checkbox is remembered during the
2FA step and applied only after the second factor succeeds. The 2FA hand-off
lives in the server-side session, which has a **60-minute idle timeout** of its
own: if the code is not entered within that window, the login is aborted and
the checkbox selection is lost with it.

### Interaction with the application session

Mail Archiver keeps a second, separate server-side session for short-lived
state — the 2FA hand-off, the batch-restore selection and the last search
state. That session has a fixed **60-minute idle timeout** and is *not*
persistent. After a browser restart with a valid "Remember Me" cookie you are
signed in, but any pending operation held in that session starts over.

### Interaction with OIDC / SSO

Users who sign in through an external identity provider never get the
persistent cookie: the OIDC callback always starts the session with
`persistAuthentication: false`. For those accounts the checkbox has **no
effect** — how long a session lasts is decided by the identity provider's
session settings.

> ⚠️ **Security note.** On a shared or unattended machine, leave the checkbox
> unticked — a persistent cookie keeps the archive accessible for up to 7 days
> without a password. In environments where that is unacceptable, shorten
> `Authentication__SessionTimeoutMinutes` and require users not to use the
> checkbox, or use OIDC where the provider enforces its own session policy.

## 🔒 Security Considerations

1. Use strong passwords for all user accounts
2. Change the default administrator password immediately after installation
3. Only assign the Admin permission to users who need it
4. Only assign the Self Manager permission to users who need to manage their own accounts
5. Regularly review user permissions to ensure access is appropriate
6. Remove user access when it is no longer needed
7. Use the principle of least privilege - only assign access to the email accounts that users need
