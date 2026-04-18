$basePath = 'C:\Users\seyda\source\repos\seydakaratekeli\KamPay3\KamPay\Views'
function Move-ViewFiles {
    param([string]$FilePrefix, [string]$DestFolder)
    $destPath = Join-Path $basePath $DestFolder
    if (!(Test-Path $destPath)) { New-Item -ItemType Directory -Force -Path $destPath | Out-Null }
    
    $xamlFile = Join-Path $basePath "$FilePrefix.xaml"
    $csFile = Join-Path $basePath "$FilePrefix.xaml.cs"
    
    if (Test-Path $xamlFile) {
        Move-Item -Path $xamlFile -Destination $destPath -Force
        Write-Host "Moved $FilePrefix.xaml to $DestFolder"
    }
    if (Test-Path $csFile) {
        Move-Item -Path $csFile -Destination $destPath -Force
        Write-Host "Moved $FilePrefix.xaml.cs to $DestFolder"
    }
}
Move-ViewFiles 'LoginPage' 'Auth'
Move-ViewFiles 'RegisterPage' 'Auth'

Move-ViewFiles 'MainPage' 'Core'

Move-ViewFiles 'ProfilePage' 'Users'
Move-ViewFiles 'EditProfilePage' 'Users'

Move-ViewFiles 'ProductListPage' 'Products'
Move-ViewFiles 'ProductDetailPage' 'Products'
Move-ViewFiles 'AddProductPage' 'Products'
Move-ViewFiles 'EditProductPage' 'Products'
Move-ViewFiles 'FavoritesPage' 'Products'

Move-ViewFiles 'PaymentPage' 'Transactions'
Move-ViewFiles 'TradeOfferView' 'Transactions'
Move-ViewFiles 'OffersPage' 'Transactions'

Move-ViewFiles 'QRCodeDisplayPage' 'Features'
Move-ViewFiles 'QRScannerPage' 'Features'
Move-ViewFiles 'SurpriseBoxPage' 'Features'

Move-ViewFiles 'GoodDeedBoardPage' 'Social'

Move-ViewFiles 'ServiceSharingPage' 'ServiceSharing'
Move-ViewFiles 'CreateCustomerRequestPage' 'ServiceSharing'
Move-ViewFiles 'CustomerRequestDetailsPage' 'ServiceSharing'
Move-ViewFiles 'CustomerRequestsListPage' 'ServiceSharing'
Move-ViewFiles 'ServiceRequestsPage' 'ServiceSharing'

Move-ViewFiles 'MessagesPage' 'Messaging'
Move-ViewFiles 'ChatPage' 'Messaging'

Move-ViewFiles 'NotificationsPage' 'Notifications'

Move-ViewFiles 'ImageViewerPage' 'Shared'
