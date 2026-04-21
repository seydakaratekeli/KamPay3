# Implementation Plan: Centralizing Colors and Securing Keys

This plan outlines the steps to refactor the KamPay application for better maintainability and security by centralizing color codes and managing sensitive keys properly.

## 1. Centralizing UI Colors and Styles

### 1.1. Consolidate Color Definitions
Currently, colors are defined in both `App.xaml` and `Resources/Styles/Colors.xaml` with conflicting values. We will consolidate them into `Resources/Styles/Colors.xaml`.

**Actions:**
- Move the "Modern Blue Theme" colors from `App.xaml` to `Resources/Styles/Colors.xaml`.
- Remove hardcoded color definitions from `App.xaml`.
- Audit `App.xaml` and ensure it only contains global styles and converters.

### 1.2. Refactor XAML Pages
Many pages (e.g., `ProfilePage.xaml`, `EditProfilePage.xaml`, `TradeOfferView.xaml`) use hardcoded hex codes like `#1E88E5`.

**Actions:**
- Replace hardcoded hex codes with `{StaticResource Primary}`, `{StaticResource Secondary}`, etc.
- Create reusable `Style` definitions for recurring patterns (e.g., `InputFrameStyle`, `PrimaryButtonStyle`).
- Use `DynamicResource` where theme switching might be needed in the future.

## 2. Securing Sensitive Keys and Configuration

### 2.1. Centralized Configuration Service
Currently, keys are scattered in `MauiProgram.cs` and `appsettings.json`.

**Actions:**
- Create a `ConfigService` to handle all application settings.
- Use the `IOptions` pattern to inject settings into services.
- Remove hardcoded default keys from `MauiProgram.cs`.

### 2.2. Secure Local Development
Avoid committing sensitive keys to Git.

**Actions:**
- Use `dotnet user-secrets` for local development.
- Ensure `appsettings.json` only contains non-sensitive or placeholder values.
- Create an `appsettings.Development.json` (excluded from Git) for local keys.

### 2.3. Constants Management
For non-sensitive keys (like internal IDs or configuration names), use a centralized `Constants.cs` class.

## 3. Implementation Workflow

1.  **Phase 1: Color Consolidation**
    - Update `Colors.xaml` with the preferred theme.
    - Clean up `App.xaml`.
2.  **Phase 2: UI Refactoring**
    - Search and replace hex codes in all `.xaml` files.
    - Apply shared styles.
3.  **Phase 4: Security Refactoring**
    - Implement `IConfigurationService`.
    - Migrate Firebase and Email settings to the new pattern.
    - Set up User Secrets for the development environment.

## 4. Expected Benefits
- **Theming**: Easily change the app's look by modifying one file.
- **Security**: Reduced risk of leaking API keys and SMTP credentials.
- **Clean Code**: More readable and professional codebase.
- **Maintainability**: Changing a key or a color no longer requires a "Find and Replace All" operation across dozens of files.
