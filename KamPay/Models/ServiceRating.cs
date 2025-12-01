namespace KamPay.Models
{
    /// <summary>
    /// Service rating model for user feedback on completed services
    /// </summary>
    public class ServiceRating
    {
        public string RatingId { get; set; } = Guid.NewGuid().ToString();
        
        // Related service request
        public string ServiceRequestId { get; set; } = string.Empty;
        public string ServiceId { get; set; } = string.Empty;
        public string ServiceTitle { get; set; } = string.Empty;
        
        // Rating details
        public string ReviewerId { get; set; } = string.Empty;
        public string ReviewerName { get; set; } = string.Empty;
        public string RatedUserId { get; set; } = string.Empty;
        public string RatedUserName { get; set; } = string.Empty;
        
        public int Stars { get; set; } // 1-5
        public string? Comment { get; set; }
        
        // Detailed ratings (optional)
        public int? CommunicationRating { get; set; } // 1-5
        public int? PunctualityRating { get; set; } // 1-5
        public int? QualityRating { get; set; } // 1-5
        
        // Flags
        public bool IsReported { get; set; }
        public string? ReportReason { get; set; }
        public bool IsHidden { get; set; } // Hidden by moderator
        
        // Timestamps
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ModeratedAt { get; set; }
        
        // Helper properties
        public bool IsPositive => Stars >= 4;
        public bool IsNegative => Stars <= 2;
    }

    /// <summary>
    /// Aggregated rating statistics for a user
    /// </summary>
    public class UserRatingStats
    {
        public string UserId { get; set; } = string.Empty;
        public int TotalRatings { get; set; }
        public double AverageStars { get; set; }
        public int FiveStars { get; set; }
        public int FourStars { get; set; }
        public int ThreeStars { get; set; }
        public int TwoStars { get; set; }
        public int OneStar { get; set; }
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    }
}
