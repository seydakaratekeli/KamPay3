{
  "rules": {
    "users": {
      ".read": true,
      ".write": true,
      ".indexOn": ["Email"]
    },
    "user_profiles": {
      ".read": true,
      ".write": true
    },
    "user_stats": {
      ".read": true,
      ".write": true
    },
    "pending_verifications": {
      ".read": true,
      ".write": true
    },
    "products": {
      ".read": true,
      ".write": true,
      ".indexOn": ["IsActive", "CategoryId", "UserId", "CreatedAt", "IsForSurpriseBox"]
    },
    "notifications": {
      ".read": true,
      ".write": true,
      ".indexOn": ["UserId", "CreatedAt"]
    },
    "favorites": {
      ".read": true,
      ".write": true,
      ".indexOn": ["UserId", "ProductId"]
    },
    "conversations": {
      ".read": true,
      ".write": true,
      ".indexOn": ["User1Id", "User2Id", "LastMessageTime", "ConversationType", "UpdatedAt"]
    },
    "messages": {
      ".read": true,
      ".write": true,
      ".indexOn": ["SentAt"]
    },
    "message_images": {
      ".read": true,
      ".write": true
    },
    "typing": {
      ".read": true,
      ".write": true
    },
    "transactions": {
      ".read": true,
      ".write": true,
      ".indexOn": ["SellerId", "BuyerId", "ProductId", "ConversationId", "Status"]
    },
    "negotiation_offers": {
      ".read": true,
      ".write": true,
      "$transactionId": {
        ".indexOn": ["Status", "CreatedAt", "ProposerId", "TransactionId"]
      }
    },
    "good_deed_posts": {
      ".read": true,
      ".write": true,
      ".indexOn": ["UserId", "CreatedAt"]
    },
    "service_offers": {
      ".read": true,
      ".write": true,
      ".indexOn": ["ProviderId", "Category", "CreatedAt"]
    },
    "service_requests": {
      ".read": true,
      ".write": true,
      ".indexOn": ["ProviderId", "RequesterId"]
    },
    "customer_service_requests": {
      ".read": true,
      ".write": true,
      ".indexOn": ["CustomerId", "Category"]
    },
    "provider_proposals": {
      ".read": true,
      ".write": true,
      ".indexOn": ["CustomerRequestId", "ProviderId"]
    },
    "service_reviews": {
      ".read": true,
      ".write": true,
      ".indexOn": ["ProviderId"]
    },
    "surprise_boxes": {
      ".read": true,
      ".write": true,
      ".indexOn": ["UserId", "CreatedAt"]
    },
    "surprise_box_transactions": {
      ".read": true,
      ".write": true,
      ".indexOn": ["RecipientId", "DonorId", "CreatedAt"]
    },
    "badges": {
      ".read": true,
      ".write": true
    },
    "user_badges": {
      ".read": true,
      ".write": true,
      ".indexOn": ["UserId", "EarnedAt"]
    },
    "delivery_qrcodes": {
      ".read": true,
      ".write": true,
      ".indexOn": ["TransactionId"]
    },
    "categories": {
      ".read": true,
      ".write": true,
      ".indexOn": ["Name"]
    },
    "temp_otps": {
      ".read": true,
      ".write": true,
      ".indexOn": ["ExpiresAt"]
    },
    "user_conversations": {
      ".read": true,
      ".write": true,
      "$uid": {
        ".indexOn": ["LastMessageTime"]
      }
    },
    "micro_businesses": {
      ".read": true,
      ".write": true,
      ".indexOn": ["IsVerified", "IsActive", "Category", "DisplayOrder", "VerificationStatus", "OwnerUserId"]
    },
    "campaigns": {
      ".read": true,
      ".write": true,
      ".indexOn": ["BusinessId", "CreatedByUserId", "IsActive", "Status", "StartsAt", "EndsAt", "DisplayOrder"]
    }
  }
}
