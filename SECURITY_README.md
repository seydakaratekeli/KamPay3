# KamPay3 - Security and Configuration Guide

## Configuration Setup

### SMTP Email Configuration

The application uses `appsettings.json` for configuration management. Sensitive data like SMTP passwords should **never** be committed to version control.

#### Development Setup

1. Copy `appsettings.json` to `appsettings.Development.json`:
   ```bash
   cp KamPay/appsettings.json KamPay/appsettings.Development.json
   ```

2. Edit `appsettings.Development.json` and add your actual SMTP password:
   ```json
   {
     "EmailSettings": {
       "Password": "your-actual-smtp-password-here"
     }
   }
   ```

3. The `.gitignore` file is configured to exclude `appsettings.Development.json` from version control.

#### Production Setup

For production environments, use one of the following secure methods:

**Option 1: Environment Variables (Recommended for Azure/Cloud)**
```bash
export KAMPAY_EmailSettings__Password="your-production-password"
```

**Option 2: Azure Key Vault (Best for Azure deployments)**
- Store secrets in Azure Key Vault
- Configure app to read from Key Vault at runtime
- Use Managed Identity for authentication

**Option 3: Secure Secret Management**
- Use your platform's secret management service
- Never commit production secrets to git
- Rotate passwords regularly

## Security Features Implemented

### 1. SMTP Password Security
- Configuration-based password management
- Support for environment variables
- Placeholder detection to prevent accidental use of default values

### 2. QR Code Security
- **Location Validation**: Mandatory GPS location verification
- **Accuracy Check**: Minimum 100m accuracy requirement
- **Expiration**: Time-limited QR codes with extension capability
- **PIN Verification**: 6-digit secure PIN codes
- **Photo Requirements**: Mandatory photo upload for high-value deliveries

### 3. Payment Security
- Simulated payment system (PayTr integration ready)
- OTP verification with secure random generation
- Automatic refund processing on cancellation
- Transaction audit trail

### 4. Admin Controls
- Role-based access control (User, Moderator, Admin)
- User ban/unban functionality
- Action logging and audit trail
- Platform statistics and monitoring

### 5. Dispute Resolution
- User complaint system
- Evidence attachment support
- Admin resolution workflow
- Automatic notifications

### 6. Service Ratings
- 5-star rating system
- Review moderation capability
- Rating statistics aggregation
- Report inappropriate reviews

## API Services

All backend services are implemented and registered in the DI container:

- `IPaymentService` - Payment processing and simulation
- `IDisputeService` - Dispute management
- `IRatingService` - Rating and review system
- `IAdminService` - Administrative operations
- `IQRCodeService` - QR code generation and validation (with security features)
- `IServiceSharingService` - Service cancellation with refunds

## Development Notes

### Building the Project

This is a .NET MAUI application targeting multiple platforms:
- Android
- iOS
- macOS Catalyst
- Windows

Required workloads:
```bash
dotnet workload restore
```

### Firebase Collections

New collections added:
- `disputes` - Dispute resolution records
- `service_ratings` - Service ratings and reviews
- `admin_actions` - Admin action audit log
- `payment_transactions` - Payment history
- `user_roles` - User role assignments
- `user_rating_stats` - Cached rating statistics

### Code Quality

Run CodeQL security scanning:
```bash
# CodeQL will be run automatically in CI/CD
```

## Future Enhancements

### Phase 2 - UI Components
- Dispute management UI (DisputePage)
- Rating submission UI (RatingPage)
- Admin dashboard (AdminDashboardPage)
- Payment flow integration

### Phase 3 - Real Integrations
- PayTr payment gateway integration
- SMS OTP delivery
- Push notifications
- Real-time chat
- ML-based fraud detection

## Security Best Practices

1. **Never commit secrets** - Use environment variables or secure vaults
2. **Rotate passwords regularly** - Change SMTP and API keys periodically
3. **Monitor admin actions** - Review audit logs for suspicious activity
4. **Validate user input** - All user input is validated server-side
5. **Use HTTPS** - All API calls use encrypted connections
6. **Rate limiting** - Implement rate limiting for payment attempts
7. **Log security events** - Critical operations are logged for audit

## Support

For security issues, please contact the development team directly rather than opening a public issue.
