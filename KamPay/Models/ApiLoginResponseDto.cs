using System;
using System.Text.Json.Serialization;

namespace KamPay.Models
{
    public class ApiLoginResponseDto
    {
        [JsonPropertyName("token")]
        public string Token { get; set; } = string.Empty;

        [JsonPropertyName("uid")]
        public string Uid { get; set; } = string.Empty;

        [JsonPropertyName("expiration")]
        public DateTime Expiration { get; set; }
    }
}