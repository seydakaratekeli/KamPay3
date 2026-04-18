$basePath = 'C:\Users\seyda\source\repos\seydakaratekeli\KamPay3\KamPay\ViewModels'
function Move-ViewModelFile {
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
Move-ViewModelFile 'LoginViewModel.cs' 'Auth'
Move-ViewModelFile 'RegisterViewModel.cs' 'Auth'

Move-ViewModelFile 'AppShellViewModel.cs' 'Core'
Move-ViewModelFile 'MainViewModel.cs' 'Core'

Move-ViewModelFile 'ProfileViewModel.cs' 'Users'
Move-ViewModelFile 'EditProfileViewModel.cs' 'Users'

Move-ViewModelFile 'ProductListViewModel.cs' 'Products'
Move-ViewModelFile 'ProductDetailViewModel.cs' 'Products'
Move-ViewModelFile 'AddProductViewModel.cs' 'Products'
Move-ViewModelFile 'EditProductViewModel.cs' 'Products'
Move-ViewModelFile 'FavoritesViewModel.cs' 'Products'

Move-ViewModelFile 'PaymentViewModel.cs' 'Transactions'
Move-ViewModelFile 'TradeOfferViewModel.cs' 'Transactions'
Move-ViewModelFile 'OffersViewModel.cs' 'Transactions'

Move-ViewModelFile 'QRCodeViewModel.cs' 'Features'
Move-ViewModelFile 'SurpriseBoxViewModel.cs' 'Features'
Move-ViewModelFile 'GoodDeedBoardViewModel.cs' 'Social'

Move-ViewModelFile 'ServiceSharingViewModel.cs' 'ServiceSharing'
Move-ViewModelFile 'CreateCustomerRequestViewModel.cs' 'ServiceSharing'
Move-ViewModelFile 'CustomerRequestDetailsViewModel.cs' 'ServiceSharing'
Move-ViewModelFile 'CustomerRequestsListViewModel.cs' 'ServiceSharing'
Move-ViewModelFile 'ServiceRequestsViewModel.cs' 'ServiceSharing'

Move-ViewModelFile 'MessagesViewModel.cs' 'Messaging'
Move-ViewModelFile 'ChatViewModel.cs' 'Messaging'

Move-ViewModelFile 'NotificationsViewModel.cs' 'Notifications'

Move-ViewModelFile 'ImageViewerViewModel.cs' 'Shared'
