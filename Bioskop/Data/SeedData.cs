using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Bioskop.Models;

namespace Bioskop.Data;

/// <summary>
/// Seeder untuk membuat sample data awal aplikasi
/// </summary>
public static class SeedData
{
    public static async Task Initialize(IServiceProvider serviceProvider)
    {
        var context = serviceProvider.GetRequiredService<ApplicationDbContext>();
        var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();

        // Ensure database is created
        await context.Database.EnsureCreatedAsync();

        // ===== Create Roles =====
        string[] roles = { "Admin", "Operator", "User" };
        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
        }

        // ===== Create Users =====
        var adminUser = await CreateUserIfNotExists(userManager, "admin@bioskop.com", "Admin123!", "Admin Bioskop", "Admin");
        var operatorUser = await CreateUserIfNotExists(userManager, "operator@bioskop.com", "Operator123!", "Operator Bioskop", "Operator");
        var user1 = await CreateUserIfNotExists(userManager, "budi@email.com", "User1234!", "Budi Santoso", "User");
        var user2 = await CreateUserIfNotExists(userManager, "siti@email.com", "User1234!", "Siti Nurhaliza", "User");
        var user3 = await CreateUserIfNotExists(userManager, "dodi@email.com", "User1234!", "Dodi Pratama", "User");
        var user4 = await CreateUserIfNotExists(userManager, "rini@email.com", "User1234!", "Rini Wulandari", "User");

        // ===== Create Studios =====
        if (!await context.Studios.AnyAsync())
        {
            var studios = new List<Studio>
            {
                new() { Name = "Studio 1 - Regular", TotalRows = 8, TotalColumns = 12, Description = "Studio regular dengan kursi nyaman" },
                new() { Name = "Studio 2 - Regular", TotalRows = 8, TotalColumns = 12, Description = "Studio regular dengan sound system Dolby" },
                new() { Name = "Studio VIP", TotalRows = 6, TotalColumns = 8, Description = "Studio VIP dengan kursi recliner dan selimut" },
                new() { Name = "Studio IMAX", TotalRows = 10, TotalColumns = 16, Description = "Studio IMAX dengan layar besar dan suara immersive" },
                new() { Name = "Studio 4DX", TotalRows = 8, TotalColumns = 10, Description = "Studio 4DX dengan efek gerakan, angin, dan air" }
            };
            context.Studios.AddRange(studios);
            await context.SaveChangesAsync();

            // Create seats for each studio
            foreach (var studio in context.Studios.ToList())
            {
                var seats = new List<Seat>();
                for (int r = 0; r < studio.TotalRows; r++)
                {
                    char rowLabel = (char)('A' + r);
                    for (int c = 1; c <= studio.TotalColumns; c++)
                    {
                        string seatType = "Regular";
                        decimal multiplier = 1.0m;

                        // Premium seats: baris tengah adalah premium
                        if (r >= studio.TotalRows / 3 && r < studio.TotalRows * 2 / 3)
                        {
                            seatType = "Premium";
                            multiplier = 1.5m;
                        }

                        // VIP seats: baris paling belakang
                        if (studio.Name.Contains("VIP"))
                        {
                            seatType = "VIP";
                            multiplier = 2.0m;
                        }

                        seats.Add(new Seat
                        {
                            StudioId = studio.Id,
                            RowLabel = rowLabel.ToString(),
                            ColumnNumber = c,
                            SeatType = seatType,
                            PriceMultiplier = multiplier,
                            Status = "Available"
                        });
                    }
                }
                context.Seats.AddRange(seats);
            }
            await context.SaveChangesAsync();
        }

        // ===== Create Movies =====
        if (!await context.Movies.AnyAsync())
        {
            var movies = new List<Movie>
            {
                new()
                {
                    Title = "Petualangan Nusantara",
                    Description = "Seorang pemuda dari desa terpencil memulai petualangan epik melintasi Indonesia untuk menemukan harta karun legendaris. Diperankan oleh aktor ternama dengan sinematografi yang memukau.",
                    Genre = "Adventure, Action",
                    DurationMinutes = 145,
                    PosterUrl = "/images/movies/petualangan-nusantara.jpg",
                    TrailerUrl = "https://www.youtube.com/watch?v=example1",
                    AgeRating = "SU",
                    ReleaseDate = DateTime.UtcNow.AddDays(-10),
                    EndDate = DateTime.UtcNow.AddDays(45),
                    IsNowPlaying = true,
                    BasePrice = 50000
                },
                new()
                {
                    Title = "Misteri Kota Tua",
                    Description = "Seorang detektif swasta mengungkap misteri pembunuhan berantai di kawasan kota tua Jakarta. Thriller psikologis yang akan membuatmu terus bertanya-tanya sampai akhir.",
                    Genre = "Thriller, Mystery",
                    DurationMinutes = 130,
                    PosterUrl = "/images/movies/misteri-kota-tua.jpg",
                    AgeRating = "R13",
                    ReleaseDate = DateTime.UtcNow.AddDays(-5),
                    EndDate = DateTime.UtcNow.AddDays(40),
                    IsNowPlaying = true,
                    BasePrice = 50000
                },
                new()
                {
                    Title = "Komedi Siang Malam",
                    Description = "Dua sahabat yang bekerja shift malam di sebuah minimarket mengalami kejadian-kejadian kocak setiap malam. Komedi segar yang akan membuatmu tertawa lepas!",
                    Genre = "Comedy",
                    DurationMinutes = 110,
                    PosterUrl = "/images/movies/komedi-siang-malam.jpg",
                    AgeRating = "R13",
                    ReleaseDate = DateTime.UtcNow.AddDays(-3),
                    EndDate = DateTime.UtcNow.AddDays(35),
                    IsNowPlaying = true,
                    BasePrice = 45000
                },
                new()
                {
                    Title = "Horor Rumah Kosong",
                    Description = "Sekelompok mahasiswa yang menginap di rumah kosong untuk tugas kuliah harus bertahan dari teror mengerikan. Film horor terseram tahun ini!",
                    Genre = "Horror",
                    DurationMinutes = 105,
                    PosterUrl = "/images/movies/horor-rumah-kosong.jpg",
                    AgeRating = "D17",
                    ReleaseDate = DateTime.UtcNow.AddDays(-7),
                    EndDate = DateTime.UtcNow.AddDays(50),
                    IsNowPlaying = true,
                    BasePrice = 50000
                },
                new()
                {
                    Title = "Romansa Bintang Jatuh",
                    Description = "Kisah cinta antara seorang astronom dan penulis yang bertemu saat hujan meteor. Drama romantis yang menyentuh hati dengan akting memukau.",
                    Genre = "Romance, Drama",
                    DurationMinutes = 125,
                    PosterUrl = "/images/movies/romansa-bintang.jpg",
                    AgeRating = "R13",
                    ReleaseDate = DateTime.UtcNow.AddDays(-2),
                    EndDate = DateTime.UtcNow.AddDays(45),
                    IsNowPlaying = true,
                    BasePrice = 50000
                },
                new()
                {
                    Title = "Pahlawan Langit",
                    Description = "Kisah inspiratif pilot wanita pertama Indonesia yang berjuang melawan segala rintangan. Drama biografi yang penuh semangat dan kebanggaan.",
                    Genre = "Biography, Drama",
                    DurationMinutes = 140,
                    PosterUrl = "/images/movies/pahlawan-langit.jpg",
                    AgeRating = "SU",
                    ReleaseDate = DateTime.UtcNow.AddDays(7),
                    EndDate = DateTime.UtcNow.AddDays(50),
                    IsNowPlaying = true,
                    BasePrice = 55000
                },
                new()
                {
                    Title = "Animasi Si Kancil",
                    Description = "Petualangan si kancil cerdik dalam animasi 3D terbaru! Cerita rakyat Indonesia yang dikemas modern untuk seluruh keluarga.",
                    Genre = "Animation, Family",
                    DurationMinutes = 95,
                    PosterUrl = "/images/movies/si-kancil.jpg",
                    AgeRating = "SU",
                    ReleaseDate = DateTime.UtcNow.AddDays(-14),
                    EndDate = DateTime.UtcNow.AddDays(60),
                    IsNowPlaying = true,
                    BasePrice = 40000
                },
                new()
                {
                    Title = "Sci-Fi 2099",
                    Description = "Di tahun 2099, seorang hacker harus menyelamatkan umat manusia dari AI yang menguasai dunia. Visual efek memukau dengan cerita yang mendalam.",
                    Genre = "Sci-Fi, Action",
                    DurationMinutes = 155,
                    PosterUrl = "/images/movies/scifi-2099.jpg",
                    AgeRating = "R13",
                    ReleaseDate = DateTime.UtcNow.AddDays(14),
                    EndDate = DateTime.UtcNow.AddDays(60),
                    IsNowPlaying = true,
                    BasePrice = 60000
                }
            };
            context.Movies.AddRange(movies);
            await context.SaveChangesAsync();
        }

        // ===== Create Showtimes =====
        if (!await context.Showtimes.AnyAsync())
        {
            var movies = await context.Movies.ToListAsync();
            var studios = await context.Studios.ToListAsync();
            var rng = new Random();
            var showtimes = new List<Showtime>();

            foreach (var studio in studios)
            {
                // Each studio shows 3-4 different movies per day
                var studioMovies = movies.OrderBy(_ => rng.Next()).Take(Math.Min(4, movies.Count)).ToList();

                foreach (var movie in studioMovies)
                {
                    for (int day = 0; day < 7; day++)
                    {
                        var times = new[] { 10, 13, 16, 19, 21 }; // Jam tayang
                        foreach (var hour in times)
                        {
                            var startTime = DateTime.UtcNow.Date.AddDays(day).AddHours(hour).AddMinutes(rng.Next(0, 4) * 15);
                            var endTime = startTime.AddMinutes(movie.DurationMinutes + 30); // 30 menit jeda

                            showtimes.Add(new Showtime
                            {
                                MovieId = movie.Id,
                                StudioId = studio.Id,
                                StartTime = startTime,
                                EndTime = endTime,
                                Price = movie.BasePrice,
                                ShowType = studio.Name.Contains("IMAX") ? "IMAX" :
                                           studio.Name.Contains("4DX") ? "4DX" :
                                           studio.Name.Contains("VIP") ? "VIP" : "Regular"
                            });
                        }
                    }
                }
            }
            context.Showtimes.AddRange(showtimes);
            await context.SaveChangesAsync();
        }

        // ===== Create Snacks =====
        if (!await context.Snacks.AnyAsync())
        {
            var snacks = new List<Snack>
            {
                new() { Name = "Popcorn Regular", Description = "Popcorn gurih ukuran regular", Price = 25000, Category = "Popcorn", ImageUrl = "/images/snacks/popcorn-reg.jpg", Stock = 500 },
                new() { Name = "Popcorn Large", Description = "Popcorn gurih ukuran jumbo", Price = 35000, Category = "Popcorn", ImageUrl = "/images/snacks/popcorn-large.jpg", Stock = 400 },
                new() { Name = "Popcorn Caramel", Description = "Popcorn manis karamel", Price = 30000, Category = "Popcorn", ImageUrl = "/images/snacks/popcorn-caramel.jpg", Stock = 300 },
                new() { Name = "Coca Cola", Description = "Minuman soda segar 500ml", Price = 15000, Category = "Minuman", ImageUrl = "/images/snacks/coca-cola.jpg", Stock = 600 },
                new() { Name = "Sprite", Description = "Minuman lemon 500ml", Price = 15000, Category = "Minuman", ImageUrl = "/images/snacks/sprite.jpg", Stock = 500 },
                new() { Name = "Air Mineral", Description = "Air mineral 600ml", Price = 8000, Category = "Minuman", ImageUrl = "/images/snacks/air-mineral.jpg", Stock = 800 },
                new() { Name = "Nachos Cheese", Description = "Nachos dengan saus keju", Price = 30000, Category = "Snack", ImageUrl = "/images/snacks/nachos.jpg", Stock = 200 },
                new() { Name = "Hot Dog", Description = "Hot dog sosis dengan saus spesial", Price = 28000, Category = "Snack", ImageUrl = "/images/snacks/hotdog.jpg", Stock = 150 },
                new() { Name = "Combo Hemat 1", Description = "Popcorn Regular + Coca Cola", Price = 35000, Category = "Combo", ImageUrl = "/images/snacks/combo1.jpg", Stock = 999 },
                new() { Name = "Combo Hemat 2", Description = "Popcorn Large + 2 Coca Cola + Nachos", Price = 70000, Category = "Combo", ImageUrl = "/images/snacks/combo2.jpg", Stock = 999 },
                new() { Name = "Combo Keluarga", Description = "2 Popcorn Large + 4 Minuman + 2 Nachos", Price = 120000, Category = "Combo", ImageUrl = "/images/snacks/combo-family.jpg", Stock = 999 },
                new() { Name = "Es Krim Coklat", Description = "Es krim coklat premium", Price = 18000, Category = "Snack", ImageUrl = "/images/snacks/eskrim.jpg", Stock = 100 }
            };
            context.Snacks.AddRange(snacks);
            await context.SaveChangesAsync();
        }

        // ===== Create Sample Curhat Film Posts =====
        if (!await context.Posts.AnyAsync())
        {
            var sampleUsers = new[] { user1, user2, user3, user4, adminUser };
            var posts = new List<Post>
            {
                new() { UserId = user1.Id, Content = "Baru aja nonton Petualangan Nusantara! GILA BAGUS BANGET! Sinematografinya juara! 🌟🌟🌟🌟🌟", CreatedAt = DateTime.UtcNow.AddHours(-5) },
                new() { UserId = user2.Id, Content = "Menurut kalian Misteri Kota Tua worth it gak? Ada yang udah nonton? 👀", CreatedAt = DateTime.UtcNow.AddHours(-4) },
                new() { UserId = user3.Id, Content = "Horor Rumah Kosong bikin gak bisa tidur semaleman... Seriusan serem parah! 😱", CreatedAt = DateTime.UtcNow.AddHours(-3) },
                new() { UserId = user4.Id, Content = "Rekomendasi film buat date night dong! Yang romantis tapi gak terlalu cengeng...", CreatedAt = DateTime.UtcNow.AddHours(-2) },
                new() { UserId = user1.Id, Content = "Studio VIP tuh enak banget! Kursi recliner + selimut, berasa nonton di rumah sendiri! 😍", CreatedAt = DateTime.UtcNow.AddHours(-1) },
                new() { UserId = user3.Id, Content = "Siapa yang mau nobar bareng? Planning mau bikin movie night nih! 🍿🎬", EventTitle = "Movie Night: Sci-Fi 2099", EventDate = DateTime.UtcNow.AddDays(7), EventLocation = "Bioskop Grand Indonesia", CreatedAt = DateTime.UtcNow.AddMinutes(-30) },
                new() { UserId = user2.Id, Content = "Animasinya Si Kancil keren banget! Adikku sampe ketawa-ketawa terus. Recommended buat keluarga! 👍", CreatedAt = DateTime.UtcNow.AddMinutes(-15) }
            };
            context.Posts.AddRange(posts);
            await context.SaveChangesAsync();
        }

        // ===== Create Sample Comments =====
        if (!await context.Comments.AnyAsync())
        {
            var firstPost = await context.Posts.FirstAsync();
            var comments = new List<Comment>
            {
                new() { PostId = firstPost.Id, UserId = user3.Id, Content = "Setuju banget! Gw juga kemarin nonton, gak nyesel!" },
                new() { PostId = firstPost.Id, UserId = user4.Id, Content = "Jadi makin penasaran 😍" },
                new() { PostId = firstPost.Id, UserId = user2.Id, Content = "Soundtracknya juga keren loh!" }
            };
            context.Comments.AddRange(comments);
            await context.SaveChangesAsync();
        }

        // ===== Create Sample Ratings =====
        if (!await context.MovieRatings.AnyAsync())
        {
            var firstMovie = await context.Movies.FirstAsync(m => m.Title == "Petualangan Nusantara");
            var ratings = new List<MovieRating>
            {
                new() { MovieId = firstMovie.Id, UserId = user1.Id, Rating = 5, Comment = "Film TERBAIK tahun ini! Sinematografi, akting, cerita - semuanya sempurna!" },
                new() { MovieId = firstMovie.Id, UserId = user2.Id, Rating = 4, Comment = "Bagus sih, cuma sedikit panjang. Tapi worth it!" },
                new() { MovieId = firstMovie.Id, UserId = user3.Id, Rating = 5, Comment = "Nangis di akhir film 😭❤️" }
            };
            context.MovieRatings.AddRange(ratings);
            await context.SaveChangesAsync();
        }
    }

    private static async Task<ApplicationUser> CreateUserIfNotExists(
        UserManager<ApplicationUser> userManager,
        string email, string password, string fullName, string role)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user == null)
        {
            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                FullName = fullName,
                EmailConfirmed = true,
                CreatedAt = DateTime.UtcNow
            };
            var result = await userManager.CreateAsync(user, password);
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(user, role);
            }
        }
        return user;
    }
}
