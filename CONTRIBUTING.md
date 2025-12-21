# 🤝 Contributing to Mail Archiver

Thank you for your interest in contributing to Mail Archiver! This document provides guidelines for contributing to the project.

## 📬 How to Contribute

### Code Changes by Third Parties

Code changes by third parties are welcome and can be made on a small scale after **prior coordination via email**. 

**Please contact us before making any code changes:**
📧 **Email:** mail@s1t5.dev

This coordination helps ensure that:
- Your proposed changes align with the project's goals
- There's no duplicate work being done
- The changes can be properly integrated into the codebase

### Types of Contributions Welcome

We welcome various types of contributions:
- Bug fixes
- Small feature enhancements
- Documentation improvements
- Translation updates
- UI/UX improvements
- Performance optimizations

## 🛠️ Development Process

### Getting Started

1. **Fork the repository** on GitHub
2. **Clone your fork** to your local development environment
3. **Create a new branch** for your changes:
   ```bash
   git checkout -b feature/your-feature-name
   ```
4. **Make your changes** following the project's coding standards
5. **Test your changes** thoroughly
6. **Commit your changes** with clear, descriptive commit messages
7. **Push to your fork** on GitHub
8. **Create a Pull Request** to the main repository

### Coding Standards

- Follow the existing code style and conventions
- Write clear, self-documenting code
- Include appropriate comments for complex logic
- Update documentation as needed

### Testing

- Test your changes thoroughly before submitting
- Ensure existing functionality is not broken
- Consider edge cases and error conditions

## 🗄️ Database Migrations

When contributing changes that require database schema modifications:

- **Follow the existing naming pattern**: Migrations should follow the format `YYYYMMDDHHMMSS_MigrateVYYMM_Z.cs` where:
  - `YYYYMMDDHHMMSS` is the timestamp in UTC format
  - `VYYMM` represents the version
  - `Z` is a sequential number for the version

- **Structure and conventions**:
  - Use the same SQL pattern checking for column/index existence before adding/removing
  - Include appropriate comments for database columns using `COMMENT ON COLUMN`
  - Make migrations reversible by implementing both `Up()` and `Down()` methods
  - Use schema-qualified table names (`mail_archiver."TableName"`)
  - Follow the existing pattern for nullable/required column changes

- **Migration safety**:
  - Ensure migrations are backward compatible when possible
  - Test migrations in a development environment first
  - Consider the impact of long-running migrations on production systems

## 📝 Documentation

When making changes that affect functionality:
- Update relevant documentation files in the `doc/` directory
- Add new documentation if introducing new features
- Ensure all documentation is clear and accurate

## 🌍 Localization

If you're adding new user-facing text or messages:
- Add the text to the shared resource files
- Follow the existing localization pattern
- Ensure text is added to all relevant language resource files

## 🐛 Reporting Issues

If you find a bug or have a feature request:
1. Check if there's already an open issue
2. If not, create a new issue using the appropriate template
3. Provide detailed information about the problem or request
4. Include steps to reproduce for bug reports

## 📄 License

By contributing to Mail Archiver, you agree that your contributions will be licensed under the GNU General Public License Version 3. See the [LICENSE](LICENSE) file for details.

## 🤝 Community Guidelines

- Be respectful and constructive in all interactions
- Provide helpful feedback on pull requests
- Follow the project's code of conduct
- Help others who are new to the project

## 📞 Contact

For questions about contributing or to coordinate code changes:
📧 **Email:** mail@s1t5.dev

Thank you for helping make Mail Archiver better!
