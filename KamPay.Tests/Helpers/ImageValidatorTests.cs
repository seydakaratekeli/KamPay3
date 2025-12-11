using KamPay.Helpers;

namespace KamPay.Tests.Helpers;

/// <summary>
/// ImageValidator helper testleri
/// Dosya uzantýsý, boyut, güvenlik ve format testleri
/// </summary>
public class ImageValidatorTests
{
    #region IsAllowedExtension Tests

    [Theory]
    [InlineData("image.jpg", true)]
    [InlineData("image.jpeg", true)]
    [InlineData("image.png", true)]
    [InlineData("image.gif", true)]
    [InlineData("image.webp", true)]
    [InlineData("IMAGE.JPG", true)] // Büyük harf
    [InlineData("image.bmp", false)] // Ýzin verilmeyen format
    [InlineData("image.tiff", false)]
    [InlineData("document.pdf", false)]
    [InlineData("script.exe", false)]
    public void IsAllowedExtension_ChecksExtensionCorrectly(string filename, bool expected)
    {
        // Act
        var result = ImageValidator.IsAllowedExtension(filename);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("noextension")]
    public void IsAllowedExtension_WithInvalidInput_ReturnsFalse(string filename)
    {
        // Act
        var result = ImageValidator.IsAllowedExtension(filename);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region GetSafeFileName Tests

    [Theory]
    [InlineData("../../etc/passwd", false)]
    [InlineData("..\\windows\\system32\\", false)]
    [InlineData("normal-file.jpg", true)]
    [InlineData("my_image_2024.png", true)]
    public void GetSafeFileName_RemovesDangerousCharacters(string input, bool shouldContainOriginal)
    {
        // Act
        var result = ImageValidator.GetSafeFileName(input);

        // Assert
        result.Should().NotContain("..");
        result.Should().NotContain("/");
        result.Should().NotContain("\\");
        
        if (shouldContainOriginal)
        {
            result.Should().Contain(Path.GetFileNameWithoutExtension(input));
        }
    }

    [Fact]
    public void GetSafeFileName_WithNull_ReturnsGuid()
    {
        // Act
        var result = ImageValidator.GetSafeFileName(null);

        // Assert
        result.Should().NotBeNullOrEmpty();
        Guid.TryParse(Path.GetFileNameWithoutExtension(result), out _).Should().BeTrue();
    }

    #endregion

    #region FormatFileSize Tests

    [Theory]
    [InlineData(0, "0 B")]
    [InlineData(1023, "1023 B")]
    [InlineData(1024, "1 KB")]
    [InlineData(1048576, "1 MB")]
    [InlineData(5242880, "5 MB")]
    [InlineData(1073741824, "1 GB")]
    public void FormatFileSize_FormatsCorrectly(long bytes, string expected)
    {
        // Act
        var result = ImageValidator.FormatFileSize(bytes);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void FormatFileSize_WithNegative_Returns0B()
    {
        // Act
        var result = ImageValidator.FormatFileSize(-100);

        // Assert
        result.Should().Be("0 B");
    }

    #endregion

    #region IsAllowedMimeType Tests

    [Theory]
    [InlineData("image/jpeg", true)]
    [InlineData("image/png", true)]
    [InlineData("image/gif", true)]
    [InlineData("image/webp", true)]
    [InlineData("application/pdf", false)]
    [InlineData("text/html", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsAllowedMimeType_ChecksMimeTypeCorrectly(string mimeType, bool expected)
    {
        // Act
        var result = ImageValidator.IsAllowedMimeType(mimeType);

        // Assert
        result.Should().Be(expected);
    }

    #endregion
}
