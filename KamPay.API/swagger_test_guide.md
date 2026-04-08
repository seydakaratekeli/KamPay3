# Swagger UI ile API Test Etme Rehberi 🧪

## Adım 1: "Try it out" tıklandıktan sonra → **Execute** butonuna bas

"Try it out" butonuna bastığında, Swagger seni **düzenleme moduna** alır. Asıl isteği göndermek için mavi **"Execute"** butonuna basman gerekiyor:

![Try it out sonrası — Execute butonu görünür](C:\Users\seyda\.gemini\antigravity\brain\876175bb-ee4e-4ab8-ac1b-1bc61c5f67ed\swagger_try_it_out_correct_click_1775678474208.png)

> [!IMPORTANT]
> "Try it out" sadece düzenleme modunu açar. **Execute** butonuna basmadan istek gitmez!

---

## Adım 2: Sonuçları kontrol et

Execute'a bastıktan sonra aşağıda **Server response** bölümü açılır:

![API Yanıtı — 200 OK ve ürün verileri](C:\Users\seyda\.gemini\antigravity\brain\876175bb-ee4e-4ab8-ac1b-1bc61c5f67ed\swagger_response_full_1775678496976.png)

Burada şunları görüyorsun:
- **Code: 200** → İstek başarılı ✅
- **Response body** → Firebase'den gelen ürün verileri (JSON formatında)
- **Curl** → Aynı isteği terminalden nasıl atarsın gösterir
- **Request URL** → `http://localhost:5011/api/v1/Products`

---

## Endpoint Özeti

| Endpoint | Auth Gerekli mi? | Nasıl Test Edilir? |
|---|---|---|
| `GET /api/v1/Products` | ❌ Hayır | Direkt Execute'a bas |
| `POST /api/v1/Products` | ✅ Evet (JWT) | Önce Authorize ol, sonra body doldur |
| `GET /WeatherForecast` | ❌ Hayır | Direkt Execute'a bas |

---

## POST Endpoint'ini Test Etme (JWT Gerektiren)

`POST /api/v1/Products` endpoint'i `[Authorize]` ile korunuyor. Test etmek için:

1. **Firebase'den JWT token al** (MAUI uygulamanızdan login olduğunuzda alınan token)
2. Sağ üstteki **"Authorize"** butonuna tıkla
3. Açılan pencereye token'ı yapıştır: `Bearer eyJhbGc...` (Bearer kelimesini yazma, sadece token'ı yaz)
4. **Authorize** → **Close** 
5. `POST /api/v1/Products` → **Try it out** → Body'yi doldur → **Execute**

Örnek POST body:
```json
{
  "title": "Test Ürün",
  "description": "Bu bir test ürünüdür",
  "price": 100,
  "categoryId": "1",
  "categoryName": "Elektronik",
  "condition": 0,
  "isActive": true,
  "imageUrls": []
}
```
