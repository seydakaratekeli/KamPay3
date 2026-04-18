$basePath = 'C:\Users\seyda\source\repos\seydakaratekeli\KamPay3\KamPay\Converters'
function Move-ConverterFile {
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
Move-ConverterFile 'AllTrueConverter.cs' 'Generic'
Move-ConverterFile 'BoolToColorConverter.cs' 'Generic'
Move-ConverterFile 'BoolToMultiColorConverter.cs' 'Generic'
Move-ConverterFile 'BoolToTextConverter.cs' 'Generic'
Move-ConverterFile 'DateTimeToTimeAgoConverter.cs' 'Generic'
Move-ConverterFile 'InvertedBoolConverter.cs' 'Generic'
Move-ConverterFile 'IsNotNullOrEmptyConverter.cs' 'Generic'
Move-ConverterFile 'IsNotZeroConverter.cs' 'Generic'
Move-ConverterFile 'LessThan100Converter.cs' 'Generic'
Move-ConverterFile 'MissingConverters.cs' 'Generic'
Move-ConverterFile 'ChatConverters.cs' 'Chat'
Move-ConverterFile 'IsCurrentUserConverter.cs' 'Chat'
Move-ConverterFile 'UnreadToIconConverter.cs' 'Chat'
Move-ConverterFile 'ProductConverters.cs' 'Product'
Move-ConverterFile 'ProductPriceConverters.cs' 'Product'
Move-ConverterFile 'ProductSortOptionToTextConverter.cs' 'Product'
Move-ConverterFile 'ProductTypeShowPriceConverter.cs' 'Product'
Move-ConverterFile 'ProductTypeToBadgeTextConverter.cs' 'Product'
Move-ConverterFile 'ProductTypeToEmojiConverter.cs' 'Product'
Move-ConverterFile 'CanAcceptNegotiationConverter.cs' 'Negotiation'
Move-ConverterFile 'CanApproveAfterNegotiationConverter.cs' 'Negotiation'
Move-ConverterFile 'CanNegotiateConverter.cs' 'Negotiation'
Move-ConverterFile 'IsNegotiatingConverter.cs' 'Negotiation'
Move-ConverterFile 'NegotiationStatusConverter.cs' 'Negotiation'
Move-ConverterFile 'NegotiationStatusTextConverter.cs' 'Negotiation'
Move-ConverterFile 'CanPayConverter.cs' 'Transaction'
Move-ConverterFile 'ConfirmDonationButtonVisibilityConverter.cs' 'Transaction'
Move-ConverterFile 'IsPendingConverter.cs' 'Transaction'
Move-ConverterFile 'SimulatePaymentButtonVisibilityConverter.cs' 'Transaction'
