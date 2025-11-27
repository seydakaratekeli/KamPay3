using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;

namespace KamPay.Models
{
    public class Category
    {
        public string CategoryId { get; set; }
        public string Name { get; set; }
        public string IconName { get; set; } // Sizin modelinizdeki ismi kullanıyoruz
        public string Description { get; set; }

        // Constructor'ı boşaltın veya tamamen silin.
        public Category() { }

        // Varsayılan kategoriler
        public static List<Category> GetDefaultCategories()
        {
            return new List<Category>
            {
                new Category { Name = "Elektronik", IconName = "laptop.png", Description = "Bilgisayar, telefon, tablet vb." },
                new Category { Name = "Kitap ve Kırtasiye", IconName = "book.png", Description = "Ders kitapları, romanlar, defterler" },
                new Category { Name = "Giyim", IconName = "tshirt.png", Description = "Kıyafet, ayakkabı, aksesuar" },
                new Category { Name = "Ev Eşyası", IconName = "white_goods.png", Description = "Mobilya, dekorasyon, mutfak" },
                new Category { Name = "Spor Malzemeleri", IconName = "dumbbell.png", Description = "Spor ekipmanları, kamp malzemeleri" },
                new Category { Name = "Müzik Aletleri", IconName = "guitar.png", Description = "Enstrümanlar ve aksesuarları" },
                new Category { Name = "Oyun ve Hobi", IconName = "gamepad.png", Description = "Oyun konsolu, board game, puzzle" },
                new Category { Name = "Bebek Ürünleri", IconName = "baby.png", Description = "Bebek kıyafeti, oyuncak, ekipman" }, 
                new Category { Name = "Diğer", IconName = "grid.png", Description = "Diğer kategorilere uymayan ürünler" }
            };
        }
    }
}