# Gmail Best Practices

When using Gmail accounts with the Mail Archiver application, there are several specific configurations and best practices that should be followed to ensure optimal performance and avoid potential issues.

## IMAP Configuration with App Passwords

Gmail accounts can be integrated with the Mail Archiver application using IMAP with App Passwords:

1. Enable 2-Factor Authentication on your Gmail account
2. Generate an App Password specifically for the Mail Archiver application
3. Use the App Password instead of your regular Gmail password when configuring the account in Mail Archiver

## Gmail Label Settings

It is strongly recommended to adjust your Gmail label settings to prevent the "All Mail" label from being exposed via IMAP:

1. Go to your Gmail webinterface and open the settings (little gear icon at the top) → See all settings → Labels tab
2. Find the "All Mail" label in the list
3. Uncheck the "Show in IMAP" option for "All Mail"

### Background on Gmail Label Implementation

Gmail's label system works differently from traditional email providers. In Gmail:
- All emails exist in a single "All Mail" repository
- Labels act as tags rather than folders
- The "All Mail" label contains every email in your account
- When an email is labeled, it appears in both the original folder and the labeled section. So all Mails in your inbox appear in the all Mail label too.

### Issues with IMAP Retrieval

When "All Mail" is exposed via IMAP, it can cause several problems:

1. **Duplicate Processing**: The archiver may process the same email multiple times since it appears in multiple folders/labels
2. **Performance Impact**: Retrieving "All Mail" can be extremely slow for large accounts with thousands of emails
4. **Synchronization Issues**: Conflicts can arise when trying to determine which folder an email actually belongs to

By disabling IMAP access to "All Mail", you ensure that emails are only retrieved once from their primary location, improving both performance and accuracy.

## Retention Policy and Expunge Settings

As mentioned in the main README, Gmail accounts require special attention regarding retention policies:

- Gmail's Auto-Expunge feature must be disabled in Gmail settings under the "Forwarding and POP/IMAP" tab
- This setting is crucial for retention policies to work correctly with Gmail accounts

To configure this setting:
1. Go to Gmail Settings → See all settings → Forwarding and POP/IMAP tab
2. In the "IMAP Access" section, select "Auto-Expunge off - Wait for the client to update the server." and "Move the message to the Bin"

Following these best practices will help ensure smooth operation of the Mail Archiver with your Gmail accounts while maintaining data integrity and optimal performance.
