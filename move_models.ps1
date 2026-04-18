$basePath = 'C:\Users\seyda\source\repos\seydakaratekeli\KamPay3\KamPay\Models'
function Move-ModelFile {
    param([string]$File, [string]$DestFolder)
    $fullPath = Join-Path $basePath $File
    $destPath = Join-Path $basePath $DestFolder
    if (Test-Path $fullPath) {
        if (!(Test-Path $destPath)) { New-Item -ItemType Directory -Force -Path $destPath | Out-Null }
        Move-Item -Path $fullPath -Destination $destPath -Force
        Write-Host "Moved $File to $DestFolder"
    } else {
        Write-Host "Warning: $File not found"
    }
}
Move-ModelFile 'Category.cs' 'Core'
Move-ModelFile 'User.cs' 'Users'
Move-ModelFile 'UserProfile.cs' 'Users'
Move-ModelFile 'UserStats.cs' 'Users'
Move-ModelFile 'Badge.cs' 'Users'
Move-ModelFile 'ApiLoginResponseDto.cs' 'Auth'
Move-ModelFile 'Product.cs' 'Products'
Move-ModelFile 'ProductPagedResponse.cs' 'Products'
Move-ModelFile 'Favorite.cs' 'Products'
Move-ModelFile 'Transaction.cs' 'Transactions'
Move-ModelFile 'TransactionHistory.cs' 'Transactions'
Move-ModelFile 'PaymentModels.cs' 'Transactions'
Move-ModelFile 'DeliveryQRCode.cs' 'Transactions'
Move-ModelFile 'DeliveryPhotoUploadResult.cs' 'Transactions'
Move-ModelFile 'ServiceOffer.cs' 'ServiceSharing'
Move-ModelFile 'CustomerServiceRequest.cs' 'ServiceSharing'
Move-ModelFile 'ProviderProposal.cs' 'ServiceSharing'
Move-ModelFile 'Message.cs' 'Messaging'
Move-ModelFile 'Conversation.cs' 'Messaging'
Move-ModelFile 'ScrollToChatMessage.cs' 'Messaging'
Move-ModelFile 'Notification.cs' 'Notifications'
Move-ModelFile 'ValidationResult.cs' 'Shared'
Move-ModelFile 'Comment.cs' 'Social'
Move-ModelFile 'GoodDeedPost.cs' 'Social'
Move-ModelFile 'SurpriseBox.cs' 'Features'
Move-ModelFile 'SupportTicket.cs' 'Support'
