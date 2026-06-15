#nullable disable
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SoccerWizard.Models;

namespace SoccerWizard.Data;

/// <summary>
/// Seeder — 2026 FIFA World Cup Qualification Edition
/// Data nyata dari semua konfederasi: AFC, UEFA, CONMEBOL, CONCACAF, CAF
/// </summary>
public static class DataSeeder
{
    public static async Task SeedAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

        await context.Database.EnsureCreatedAsync();
        if (context.Leagues.Any()) return;

        // ===== ROLES =====
        if (!await roleManager.RoleExistsAsync("Admin"))
            await roleManager.CreateAsync(new IdentityRole("Admin"));
        if (!await roleManager.RoleExistsAsync("User"))
            await roleManager.CreateAsync(new IdentityRole("User"));

        // ===== USERS =====
        var adminUser = new IdentityUser { UserName = "admin@soccerwizard.com", Email = "admin@soccerwizard.com", EmailConfirmed = true };
        if (await userManager.FindByEmailAsync(adminUser.Email) == null)
        {
            await userManager.CreateAsync(adminUser, "Admin123!");
            await userManager.AddToRoleAsync(adminUser, "Admin");
            context.UserProfiles.Add(new UserProfile { UserId = adminUser.Id, FullName = "Admin SoccerWizard", FavoriteTeam = "Argentina", FavoriteLeague = "World Cup 2026 Qualifiers", Bio = "Administrator", CreatedAt = DateTime.UtcNow, LastLoginAt = DateTime.UtcNow });
        }

        var demoUser = new IdentityUser { UserName = "demo@soccerwizard.com", Email = "demo@soccerwizard.com", EmailConfirmed = true };
        if (await userManager.FindByEmailAsync(demoUser.Email) == null)
        {
            await userManager.CreateAsync(demoUser, "Demo123!");
            await userManager.AddToRoleAsync(demoUser, "User");
            context.UserProfiles.Add(new UserProfile { UserId = demoUser.Id, FullName = "Demo User", FavoriteTeam = "Japan", FavoriteLeague = "AFC Qualifiers", Bio = "Football fan", PredictionsCount = 45, CorrectPredictions = 28, CreatedAt = DateTime.UtcNow, LastLoginAt = DateTime.UtcNow });
        }

        await context.SaveChangesAsync();

        // ===== LEAGUES (Qualification Zones) =====
        var afcQualifiers = new League { Name = "AFC World Cup Qualifiers", Country = "Asia", Season = "2026", TotalTeams = 46, TotalRounds = 3, Description = "Asian Football Confederation qualification for FIFA World Cup 2026. 8 direct slots + 1 playoff." };
        var uefaQualifiers = new League { Name = "UEFA World Cup Qualifiers", Country = "Europe", Season = "2026", TotalTeams = 55, TotalRounds = 1, Description = "UEFA qualification — 12 group winners qualify directly, runners-up to playoffs. 16 total slots." };
        var conmebolQualifiers = new League { Name = "CONMEBOL World Cup Qualifiers", Country = "South America", Season = "2026", TotalTeams = 10, TotalRounds = 1, Description = "South American qualifiers — round-robin league, top 6 qualify directly, 7th to playoffs." };
        var concacafQualifiers = new League { Name = "CONCACAF World Cup Qualifiers", Country = "North America", Season = "2026", TotalTeams = 35, TotalRounds = 3, Description = "CONCACAF qualification — 3 group winners + 2 best runners-up advance. 6 total slots." };
        var cafQualifiers = new League { Name = "CAF World Cup Qualifiers", Country = "Africa", Season = "2026", TotalTeams = 54, TotalRounds = 2, Description = "African qualifiers — 9 group winners qualify directly. 9 total slots." };

        context.Leagues.AddRange(afcQualifiers, uefaQualifiers, conmebolQualifiers, concacafQualifiers, cafQualifiers);
        await context.SaveChangesAsync();

        // ===== TEAMS (40+ national teams with real ELO ratings) =====
        var teams = new List<Team>
        {
            // AFC — Qualified
            new Team { Name = "Japan", ShortName = "Japan", Code = "JPN", Country = "Japan", City = "Tokyo", Stadium = "Japan National Stadium", FoundedYear = 1921, LeagueId = afcQualifiers.Id, EloRating = 1830, AttackStrength = 2.3, DefenseStrength = 0.9, MidfieldStrength = 2.0, Momentum = 0.88, AvgGoalsScored = 2.6, AvgGoalsConceded = 0.5, Description = "Asian powerhouse with exceptional technical ability. Qualified as Group C winners." },
            new Team { Name = "Iran", ShortName = "Iran", Code = "IRN", Country = "Iran", City = "Tehran", Stadium = "Azadi Stadium", FoundedYear = 1920, LeagueId = afcQualifiers.Id, EloRating = 1760, AttackStrength = 2.1, DefenseStrength = 0.8, MidfieldStrength = 1.8, Momentum = 0.90, AvgGoalsScored = 2.4, AvgGoalsConceded = 0.6, Description = "Team Melli — dominant in Group A with 30 points." },
            new Team { Name = "South Korea", ShortName = "Korea Rep", Code = "KOR", Country = "South Korea", City = "Seoul", Stadium = "Seoul World Cup Stadium", FoundedYear = 1933, LeagueId = afcQualifiers.Id, EloRating = 1780, AttackStrength = 2.2, DefenseStrength = 0.85, MidfieldStrength = 2.0, Momentum = 0.82, AvgGoalsScored = 2.3, AvgGoalsConceded = 0.7, Description = "Son Heung-min leads the Taegeuk Warriors. Group B winners." },
            new Team { Name = "Australia", ShortName = "Australia", Code = "AUS", Country = "Australia", City = "Sydney", Stadium = "Stadium Australia", FoundedYear = 1961, LeagueId = afcQualifiers.Id, EloRating = 1740, AttackStrength = 2.0, DefenseStrength = 0.9, MidfieldStrength = 1.9, Momentum = 0.78, AvgGoalsScored = 2.1, AvgGoalsConceded = 0.8, Description = "Socceroos qualify as Group C runners-up behind Japan." },
            new Team { Name = "Uzbekistan", ShortName = "Uzbekistan", Code = "UZB", Country = "Uzbekistan", City = "Tashkent", Stadium = "Bunyodkor Stadium", FoundedYear = 1946, LeagueId = afcQualifiers.Id, EloRating = 1680, AttackStrength = 1.8, DefenseStrength = 0.9, MidfieldStrength = 1.7, Momentum = 0.80, AvgGoalsScored = 1.8, AvgGoalsConceded = 0.8, Description = "White Wolves — surprising Group A runners-up, qualified directly." },
            new Team { Name = "Saudi Arabia", ShortName = "Saudi Arabia", Code = "KSA", Country = "Saudi Arabia", City = "Riyadh", Stadium = "King Saud University Stadium", FoundedYear = 1956, LeagueId = afcQualifiers.Id, EloRating = 1650, AttackStrength = 1.6, DefenseStrength = 1.1, MidfieldStrength = 1.5, Momentum = 0.60, AvgGoalsScored = 1.4, AvgGoalsConceded = 1.1, Description = "Green Falcons — finished 3rd in Group C, to playoffs." },
            new Team { Name = "Jordan", ShortName = "Jordan", Code = "JOR", Country = "Jordan", City = "Amman", Stadium = "Amman International Stadium", FoundedYear = 1949, LeagueId = afcQualifiers.Id, EloRating = 1600, AttackStrength = 1.5, DefenseStrength = 1.0, MidfieldStrength = 1.4, Momentum = 0.75, AvgGoalsScored = 1.5, AvgGoalsConceded = 1.0, Description = "Al-Nashama — qualified as Group B runners-up." },
            new Team { Name = "Qatar", ShortName = "Qatar", Code = "QAT", Country = "Qatar", City = "Doha", Stadium = "Khalifa International Stadium", FoundedYear = 1960, LeagueId = afcQualifiers.Id, EloRating = 1620, AttackStrength = 1.6, DefenseStrength = 1.0, MidfieldStrength = 1.5, Momentum = 0.55, AvgGoalsScored = 1.5, AvgGoalsConceded = 1.2, Description = "2022 World Cup hosts — 4th in Group A." },
            // AFC — Notable non-qualified
            new Team { Name = "Indonesia", ShortName = "Indonesia", Code = "IDN", Country = "Indonesia", City = "Jakarta", Stadium = "Gelora Bung Karno", FoundedYear = 1930, LeagueId = afcQualifiers.Id, EloRating = 1520, AttackStrength = 1.2, DefenseStrength = 1.4, MidfieldStrength = 1.2, Momentum = 0.50, AvgGoalsScored = 1.1, AvgGoalsConceded = 1.8, Description = "Garuda — 4th in Group C with 12 points, historic campaign." },
            new Team { Name = "China PR", ShortName = "China", Code = "CHN", Country = "China", City = "Beijing", Stadium = "Workers' Stadium", FoundedYear = 1924, LeagueId = afcQualifiers.Id, EloRating = 1540, AttackStrength = 1.3, DefenseStrength = 1.3, MidfieldStrength = 1.2, Momentum = 0.30, AvgGoalsScored = 0.9, AvgGoalsConceded = 1.9, Description = "Dragon Team — bottom of Group C." },

            // CONMEBOL
            new Team { Name = "Argentina", ShortName = "Argentina", Code = "ARG", Country = "Argentina", City = "Buenos Aires", Stadium = "Estadio Monumental", FoundedYear = 1893, LeagueId = conmebolQualifiers.Id, EloRating = 1950, AttackStrength = 2.6, DefenseStrength = 0.7, MidfieldStrength = 2.4, Momentum = 0.92, AvgGoalsScored = 2.2, AvgGoalsConceded = 0.6, Description = "World Champions — topped CONMEBOL with 38 points. Messi's legacy continues." },
            new Team { Name = "Brazil", ShortName = "Brazil", Code = "BRA", Country = "Brazil", City = "Rio de Janeiro", Stadium = "Maracanã", FoundedYear = 1914, LeagueId = conmebolQualifiers.Id, EloRating = 1900, AttackStrength = 2.5, DefenseStrength = 0.8, MidfieldStrength = 2.3, Momentum = 0.75, AvgGoalsScored = 2.1, AvgGoalsConceded = 0.8, Description = "Seleção — 5th in CONMEBOL, 8W 4D 6L. Rebuilding phase." },
            new Team { Name = "Colombia", ShortName = "Colombia", Code = "COL", Country = "Colombia", City = "Bogotá", Stadium = "Estadio Metropolitano", FoundedYear = 1924, LeagueId = conmebolQualifiers.Id, EloRating = 1780, AttackStrength = 2.0, DefenseStrength = 0.9, MidfieldStrength = 1.9, Momentum = 0.80, AvgGoalsScored = 1.8, AvgGoalsConceded = 0.9, Description = "Los Cafeteros — 3rd place with 28 pts. Luis Díaz stars." },
            new Team { Name = "Uruguay", ShortName = "Uruguay", Code = "URU", Country = "Uruguay", City = "Montevideo", Stadium = "Estadio Centenario", FoundedYear = 1900, LeagueId = conmebolQualifiers.Id, EloRating = 1840, AttackStrength = 2.2, DefenseStrength = 0.8, MidfieldStrength = 1.9, Momentum = 0.78, AvgGoalsScored = 1.9, AvgGoalsConceded = 0.7, Description = "La Celeste — 4th, Bielsa's intense style." },
            new Team { Name = "Ecuador", ShortName = "Ecuador", Code = "ECU", Country = "Ecuador", City = "Quito", Stadium = "Estadio Rodrigo Paz Delgado", FoundedYear = 1925, LeagueId = conmebolQualifiers.Id, EloRating = 1740, AttackStrength = 1.8, DefenseStrength = 0.9, MidfieldStrength = 1.7, Momentum = 0.82, AvgGoalsScored = 1.5, AvgGoalsConceded = 0.6, Description = "La Tri — 2nd place, best defense (only 5 conceded)." },
            new Team { Name = "Paraguay", ShortName = "Paraguay", Code = "PAR", Country = "Paraguay", City = "Asunción", Stadium = "Estadio Defensores del Chaco", FoundedYear = 1906, LeagueId = conmebolQualifiers.Id, EloRating = 1660, AttackStrength = 1.4, DefenseStrength = 0.9, MidfieldStrength = 1.4, Momentum = 0.74, AvgGoalsScored = 1.2, AvgGoalsConceded = 0.8, Description = "La Albirroja — 6th, direct qualification." },

            // UEFA — Qualified & Notable
            new Team { Name = "France", ShortName = "France", Code = "FRA", Country = "France", City = "Paris", Stadium = "Stade de France", FoundedYear = 1904, LeagueId = uefaQualifiers.Id, EloRating = 1920, AttackStrength = 2.8, DefenseStrength = 0.7, MidfieldStrength = 2.5, Momentum = 0.86, AvgGoalsScored = 2.8, AvgGoalsConceded = 0.5, Description = "Les Bleus — Group D winners with 16 pts. Mbappé leads." },
            new Team { Name = "Spain", ShortName = "Spain", Code = "ESP", Country = "Spain", City = "Madrid", Stadium = "Estadio Santiago Bernabéu", FoundedYear = 1913, LeagueId = uefaQualifiers.Id, EloRating = 1900, AttackStrength = 2.6, DefenseStrength = 0.7, MidfieldStrength = 2.6, Momentum = 0.90, AvgGoalsScored = 2.5, AvgGoalsConceded = 0.4, Description = "La Roja — Group E winners, Euro 2024 champions." },
            new Team { Name = "Germany", ShortName = "Germany", Code = "GER", Country = "Germany", City = "Berlin", Stadium = "Olympiastadion", FoundedYear = 1900, LeagueId = uefaQualifiers.Id, EloRating = 1880, AttackStrength = 2.5, DefenseStrength = 0.8, MidfieldStrength = 2.4, Momentum = 0.88, AvgGoalsScored = 2.4, AvgGoalsConceded = 0.5, Description = "Die Mannschaft — Group A winners with 15 pts." },
            new Team { Name = "England", ShortName = "England", Code = "ENG", Country = "England", City = "London", Stadium = "Wembley Stadium", FoundedYear = 1863, LeagueId = uefaQualifiers.Id, EloRating = 1870, AttackStrength = 2.4, DefenseStrength = 0.8, MidfieldStrength = 2.3, Momentum = 0.84, AvgGoalsScored = 2.3, AvgGoalsConceded = 0.6, Description = "Three Lions — Group K winners." },
            new Team { Name = "Portugal", ShortName = "Portugal", Code = "POR", Country = "Portugal", City = "Lisbon", Stadium = "Estádio José Alvalade", FoundedYear = 1914, LeagueId = uefaQualifiers.Id, EloRating = 1850, AttackStrength = 2.4, DefenseStrength = 0.8, MidfieldStrength = 2.2, Momentum = 0.82, AvgGoalsScored = 2.2, AvgGoalsConceded = 0.6, Description = "Seleção — Group F winners. Ronaldo still going strong." },
            new Team { Name = "Netherlands", ShortName = "Netherlands", Code = "NED", Country = "Netherlands", City = "Amsterdam", Stadium = "Johan Cruijff ArenA", FoundedYear = 1889, LeagueId = uefaQualifiers.Id, EloRating = 1830, AttackStrength = 2.2, DefenseStrength = 0.85, MidfieldStrength = 2.1, Momentum = 0.80, AvgGoalsScored = 2.1, AvgGoalsConceded = 0.7, Description = "Oranje — Group G winners." },
            new Team { Name = "Belgium", ShortName = "Belgium", Code = "BEL", Country = "Belgium", City = "Brussels", Stadium = "King Baudouin Stadium", FoundedYear = 1895, LeagueId = uefaQualifiers.Id, EloRating = 1800, AttackStrength = 2.0, DefenseStrength = 0.9, MidfieldStrength = 2.0, Momentum = 0.72, AvgGoalsScored = 1.9, AvgGoalsConceded = 0.8, Description = "Red Devils — Group J winners." },
            new Team { Name = "Croatia", ShortName = "Croatia", Code = "CRO", Country = "Croatia", City = "Zagreb", Stadium = "Stadion Maksimir", FoundedYear = 1912, LeagueId = uefaQualifiers.Id, EloRating = 1810, AttackStrength = 1.9, DefenseStrength = 0.85, MidfieldStrength = 2.2, Momentum = 0.78, AvgGoalsScored = 1.8, AvgGoalsConceded = 0.7, Description = "Vatreni — Group L winners. Modrić's last dance." },
            new Team { Name = "Norway", ShortName = "Norway", Code = "NOR", Country = "Norway", City = "Oslo", Stadium = "Ullevaal Stadion", FoundedYear = 1902, LeagueId = uefaQualifiers.Id, EloRating = 1750, AttackStrength = 2.0, DefenseStrength = 0.9, MidfieldStrength = 1.8, Momentum = 0.85, AvgGoalsScored = 2.0, AvgGoalsConceded = 0.8, Description = "Løvene — Group I winners. Haaland unstoppable." },
            new Team { Name = "Austria", ShortName = "Austria", Code = "AUT", Country = "Austria", City = "Vienna", Stadium = "Ernst Happel Stadion", FoundedYear = 1904, LeagueId = uefaQualifiers.Id, EloRating = 1730, AttackStrength = 1.8, DefenseStrength = 0.9, MidfieldStrength = 1.8, Momentum = 0.82, AvgGoalsScored = 1.8, AvgGoalsConceded = 0.8, Description = "Das Team — Group H winners under Rangnick." },
            // UEFA Playoff teams
            new Team { Name = "Italy", ShortName = "Italy", Code = "ITA", Country = "Italy", City = "Rome", Stadium = "Stadio Olimpico", FoundedYear = 1898, LeagueId = uefaQualifiers.Id, EloRating = 1860, AttackStrength = 2.0, DefenseStrength = 0.7, MidfieldStrength = 2.1, Momentum = 0.76, AvgGoalsScored = 1.9, AvgGoalsConceded = 0.6, Description = "Azzurri — Group I runners-up." },
            new Team { Name = "Switzerland", ShortName = "Switzerland", Code = "SUI", Country = "Switzerland", City = "Bern", Stadium = "Stade de Suisse", FoundedYear = 1895, LeagueId = uefaQualifiers.Id, EloRating = 1740, AttackStrength = 1.7, DefenseStrength = 0.85, MidfieldStrength = 1.7, Momentum = 0.74, AvgGoalsScored = 1.7, AvgGoalsConceded = 0.8, Description = "Nati — Group B winners." },
            new Team { Name = "Scotland", ShortName = "Scotland", Code = "SCO", Country = "Scotland", City = "Glasgow", Stadium = "Hampden Park", FoundedYear = 1873, LeagueId = uefaQualifiers.Id, EloRating = 1700, AttackStrength = 1.6, DefenseStrength = 0.95, MidfieldStrength = 1.6, Momentum = 0.72, AvgGoalsScored = 1.5, AvgGoalsConceded = 0.9, Description = "Tartan Army — Group C winners." },
            new Team { Name = "Sweden", ShortName = "Sweden", Code = "SWE", Country = "Sweden", City = "Stockholm", Stadium = "Friends Arena", FoundedYear = 1904, LeagueId = uefaQualifiers.Id, EloRating = 1720, AttackStrength = 1.7, DefenseStrength = 0.9, MidfieldStrength = 1.7, Momentum = 0.70, AvgGoalsScored = 1.7, AvgGoalsConceded = 0.9, Description = "Blågult — Group B runners-up, playoff bound." },

            // CONCACAF
            new Team { Name = "Panama", ShortName = "Panama", Code = "PAN", Country = "Panama", City = "Panama City", Stadium = "Estadio Rommel Fernández", FoundedYear = 1937, LeagueId = concacafQualifiers.Id, EloRating = 1650, AttackStrength = 1.5, DefenseStrength = 1.0, MidfieldStrength = 1.4, Momentum = 0.82, AvgGoalsScored = 1.6, AvgGoalsConceded = 0.7, Description = "Los Canaleros — Group A winners, 12 pts." },
            new Team { Name = "Jamaica", ShortName = "Jamaica", Code = "JAM", Country = "Jamaica", City = "Kingston", Stadium = "Independence Park", FoundedYear = 1910, LeagueId = concacafQualifiers.Id, EloRating = 1630, AttackStrength = 1.5, DefenseStrength = 1.0, MidfieldStrength = 1.4, Momentum = 0.78, AvgGoalsScored = 1.5, AvgGoalsConceded = 0.8, Description = "Reggae Boyz — Group B runners-up, advanced to playoffs." },
            new Team { Name = "Mexico", ShortName = "Mexico", Code = "MEX", Country = "Mexico", City = "Mexico City", Stadium = "Estadio Azteca", FoundedYear = 1927, LeagueId = concacafQualifiers.Id, EloRating = 1780, AttackStrength = 1.9, DefenseStrength = 0.9, MidfieldStrength = 1.8, Momentum = 0.65, AvgGoalsScored = 2.0, AvgGoalsConceded = 0.8, Description = "El Tri — co-hosts, auto-qualified." },

            // CAF
            new Team { Name = "Morocco", ShortName = "Morocco", Code = "MAR", Country = "Morocco", City = "Rabat", Stadium = "Stade Mohammed V", FoundedYear = 1955, LeagueId = cafQualifiers.Id, EloRating = 1800, AttackStrength = 2.1, DefenseStrength = 0.8, MidfieldStrength = 2.0, Momentum = 0.88, AvgGoalsScored = 2.2, AvgGoalsConceded = 0.5, Description = "Atlas Lions — 2022 semi-finalists, dominant in CAF." },
            new Team { Name = "Egypt", ShortName = "Egypt", Code = "EGY", Country = "Egypt", City = "Cairo", Stadium = "Cairo International Stadium", FoundedYear = 1921, LeagueId = cafQualifiers.Id, EloRating = 1750, AttackStrength = 1.9, DefenseStrength = 0.85, MidfieldStrength = 1.8, Momentum = 0.80, AvgGoalsScored = 1.9, AvgGoalsConceded = 0.6, Description = "Pharaohs — Salah leads, Group A dominant." },
            new Team { Name = "Senegal", ShortName = "Senegal", Code = "SEN", Country = "Senegal", City = "Dakar", Stadium = "Stade Abdoulaye Wade", FoundedYear = 1960, LeagueId = cafQualifiers.Id, EloRating = 1780, AttackStrength = 2.0, DefenseStrength = 0.85, MidfieldStrength = 1.9, Momentum = 0.82, AvgGoalsScored = 2.0, AvgGoalsConceded = 0.6, Description = "Lions of Teranga — AFCON champions, strong qualifiers." },
            new Team { Name = "Algeria", ShortName = "Algeria", Code = "ALG", Country = "Algeria", City = "Algiers", Stadium = "Stade Nelson Mandela", FoundedYear = 1962, LeagueId = cafQualifiers.Id, EloRating = 1730, AttackStrength = 1.9, DefenseStrength = 0.9, MidfieldStrength = 1.8, Momentum = 0.78, AvgGoalsScored = 1.9, AvgGoalsConceded = 0.7, Description = "Les Fennecs — strong CAF qualifying campaign." },
            new Team { Name = "Nigeria", ShortName = "Nigeria", Code = "NGA", Country = "Nigeria", City = "Abuja", Stadium = "Moshood Abiola Stadium", FoundedYear = 1945, LeagueId = cafQualifiers.Id, EloRating = 1700, AttackStrength = 1.8, DefenseStrength = 1.0, MidfieldStrength = 1.7, Momentum = 0.72, AvgGoalsScored = 1.7, AvgGoalsConceded = 0.8, Description = "Super Eagles — Osimhen leads the attack." },

            // OFC
            new Team { Name = "New Zealand", ShortName = "New Zealand", Code = "NZL", Country = "New Zealand", City = "Wellington", Stadium = "Sky Stadium", FoundedYear = 1891, LeagueId = null, EloRating = 1620, AttackStrength = 1.5, DefenseStrength = 1.0, MidfieldStrength = 1.4, Momentum = 0.85, AvgGoalsScored = 2.0, AvgGoalsConceded = 0.5, Description = "All Whites — OFC qualifiers winner. Chris Wood leads." },
        };
        context.Teams.AddRange(teams);
        await context.SaveChangesAsync();

        // ===== PLAYERS (Key stars) =====
        var players = new List<Player>
        {
            // AFC
            new Player { Name = "Kaoru Mitoma", Position = "FWD", ShirtNumber = "7", Nationality = "Japan", TeamId = teams[0].Id, Rating = 8.5, Goals = 5, Assists = 6, Appearances = 8 },
            new Player { Name = "Mehdi Taremi", Position = "FWD", ShirtNumber = "9", Nationality = "Iran", TeamId = teams[1].Id, Rating = 8.4, Goals = 8, Assists = 3, Appearances = 10 },
            new Player { Name = "Son Heung-min", Position = "FWD", ShirtNumber = "7", Nationality = "South Korea", TeamId = teams[2].Id, Rating = 9.0, Goals = 10, Assists = 5, Appearances = 10 },
            new Player { Name = "Harry Souttar", Position = "DEF", ShirtNumber = "19", Nationality = "Australia", TeamId = teams[3].Id, Rating = 7.8, Goals = 2, Assists = 0, Appearances = 9 },
            new Player { Name = "Eldor Shomurodov", Position = "FWD", ShirtNumber = "14", Nationality = "Uzbekistan", TeamId = teams[4].Id, Rating = 7.9, Goals = 6, Assists = 2, Appearances = 10 },
            new Player { Name = "Salem Al-Dawsari", Position = "FWD", ShirtNumber = "10", Nationality = "Saudi Arabia", TeamId = teams[5].Id, Rating = 8.0, Goals = 4, Assists = 3, Appearances = 8 },
            new Player { Name = "Musa Al-Taamari", Position = "FWD", ShirtNumber = "13", Nationality = "Jordan", TeamId = teams[6].Id, Rating = 7.8, Goals = 4, Assists = 4, Appearances = 10 },
            new Player { Name = "Rafael Struick", Position = "FWD", ShirtNumber = "9", Nationality = "Indonesia", TeamId = teams[8].Id, Rating = 7.0, Goals = 2, Assists = 1, Appearances = 8 },
            // CONMEBOL
            new Player { Name = "Lionel Messi", Position = "FWD", ShirtNumber = "10", Nationality = "Argentina", TeamId = teams[10].Id, Rating = 9.5, Goals = 9, Assists = 4, Appearances = 12 },
            new Player { Name = "Vinícius Jr.", Position = "FWD", ShirtNumber = "7", Nationality = "Brazil", TeamId = teams[11].Id, Rating = 8.9, Goals = 5, Assists = 4, Appearances = 10 },
            new Player { Name = "Luis Díaz", Position = "FWD", ShirtNumber = "7", Nationality = "Colombia", TeamId = teams[12].Id, Rating = 8.7, Goals = 6, Assists = 3, Appearances = 12 },
            new Player { Name = "Federico Valverde", Position = "MID", ShirtNumber = "15", Nationality = "Uruguay", TeamId = teams[13].Id, Rating = 8.8, Goals = 4, Assists = 3, Appearances = 12 },
            new Player { Name = "Moisés Caicedo", Position = "MID", ShirtNumber = "23", Nationality = "Ecuador", TeamId = teams[14].Id, Rating = 8.3, Goals = 1, Assists = 2, Appearances = 12 },
            // UEFA
            new Player { Name = "Kylian Mbappé", Position = "FWD", ShirtNumber = "10", Nationality = "France", TeamId = teams[16].Id, Rating = 9.3, Goals = 8, Assists = 4, Appearances = 6 },
            new Player { Name = "Lamine Yamal", Position = "FWD", ShirtNumber = "19", Nationality = "Spain", TeamId = teams[17].Id, Rating = 8.8, Goals = 3, Assists = 5, Appearances = 6 },
            new Player { Name = "Jamal Musiala", Position = "MID", ShirtNumber = "10", Nationality = "Germany", TeamId = teams[18].Id, Rating = 8.7, Goals = 4, Assists = 4, Appearances = 6 },
            new Player { Name = "Harry Kane", Position = "FWD", ShirtNumber = "9", Nationality = "England", TeamId = teams[19].Id, Rating = 9.1, Goals = 7, Assists = 2, Appearances = 6 },
            new Player { Name = "Cristiano Ronaldo", Position = "FWD", ShirtNumber = "7", Nationality = "Portugal", TeamId = teams[20].Id, Rating = 8.5, Goals = 6, Assists = 1, Appearances = 6 },
            new Player { Name = "Erling Haaland", Position = "FWD", ShirtNumber = "9", Nationality = "Norway", TeamId = teams[25].Id, Rating = 9.2, Goals = 9, Assists = 2, Appearances = 6 },
            new Player { Name = "Mohamed Salah", Position = "FWD", ShirtNumber = "10", Nationality = "Egypt", TeamId = teams[35].Id, Rating = 9.0, Goals = 7, Assists = 4, Appearances = 6 },
        };
        context.Players.AddRange(players);
        await context.SaveChangesAsync();

        // ===== MATCHES (Real matchups from qualifiers) =====
        var matches = new List<Match>();
        var baseDate = new DateTime(2025, 3, 20);

        // AddMatch helper
        void AddMatch(Team home, Team away, int daysOffset, int? hg, int? ag, string status, string round, int h2hH = 0, int h2hA = 0, int h2hD = 0)
        {
            var d = baseDate.AddDays(daysOffset);
            matches.Add(new Match
            {
                HomeTeamId = home.Id, AwayTeamId = away.Id, LeagueId = home.LeagueId,
                MatchDate = d, Status = status, Venue = home.Stadium,
                HomeScore = hg, AwayScore = ag,
                HomePossession = hg.HasValue ? 45 + new Random(d.Day).Next(0, 20) : null,
                AwayPossession = hg.HasValue ? 100 - (45 + new Random(d.Day).Next(0, 20)) : null,
                HomeShots = hg.HasValue ? 5 + new Random(d.Day).Next(0, 15) : null,
                AwayShots = hg.HasValue ? 3 + new Random(d.Day + 1).Next(0, 12) : null,
                HomeXG = hg.HasValue ? Math.Round(1.0 + new Random(d.Day).NextDouble() * 2.0, 2) : null,
                AwayXG = hg.HasValue ? Math.Round(0.5 + new Random(d.Day + 1).NextDouble() * 1.5, 2) : null,
                Weather = "Clear", Temperature = 20, Humidity = 55,
                HomeFormation = "4-3-3", AwayFormation = "4-2-3-1",
                HomeWinsH2H = h2hH, AwayWinsH2H = h2hA, DrawsH2H = h2hD,
                Round = round, CreatedAt = d, UpdatedAt = d
            });
        }

        // === AFC QUALIFIERS — March 2025 ===
        AddMatch(teams[2], teams[6], 0, 1, 1, "FINISHED", "AFC Round 3 — Group B", 7, 2, 4);   // South Korea 1-1 Jordan
        AddMatch(teams[0], teams[5], 0, 0, 0, "FINISHED", "AFC Round 3 — Group C", 9, 5, 3);      // Japan 0-0 Saudi Arabia
        AddMatch(teams[8], teams[3], 0, 0, 0, "FINISHED", "AFC Round 3 — Group C", 0, 3, 2);      // Indonesia 0-0 Australia
        AddMatch(teams[1], teams[4], 0, 2, 2, "FINISHED", "AFC Round 3 — Group A", 8, 2, 5);      // Iran 2-2 Uzbekistan
        AddMatch(teams[9], teams[3], -3, 0, 2, "FINISHED", "AFC Round 3 — Group C", 5, 8, 4);     // China 0-2 Australia
        AddMatch(teams[8], teams[1], -5, 0, 2, "FINISHED", "AFC Round 3 — Group A", 0, 4, 0);     // Indonesia 0-2 Iran (hypothetical)

        // === CONMEBOL QUALIFIERS ===
        AddMatch(teams[10], teams[11], -5, 1, 0, "FINISHED", "CONMEBOL Matchday 18", 40, 42, 26);   // Argentina 1-0 Brazil
        AddMatch(teams[12], teams[14], -5, 2, 1, "FINISHED", "CONMEBOL Matchday 18", 10, 12, 8);    // Colombia 2-1 Ecuador
        AddMatch(teams[13], teams[15], -5, 0, 0, "FINISHED", "CONMEBOL Matchday 18", 18, 15, 10);   // Uruguay 0-0 Paraguay
        AddMatch(teams[11], teams[10], -10, 0, 1, "FINISHED", "CONMEBOL Matchday 17", 42, 40, 26);  // Brazil 0-1 Argentina
        AddMatch(teams[10], teams[13], -15, 3, 0, "FINISHED", "CONMEBOL Matchday 16", 55, 40, 35);  // Argentina 3-0 Uruguay
        AddMatch(teams[11], teams[14], -15, 1, 0, "FINISHED", "CONMEBOL Matchday 16", 28, 8, 10);   // Brazil 1-0 Ecuador

        // === UEFA QUALIFIERS ===
        AddMatch(teams[16], teams[18], -10, 2, 1, "FINISHED", "UEFA Group Stage", 15, 10, 8);       // France 2-1 Germany
        AddMatch(teams[17], teams[19], -10, 1, 0, "FINISHED", "UEFA Group Stage", 13, 13, 5);       // Spain 1-0 England
        AddMatch(teams[20], teams[21], -10, 2, 0, "FINISHED", "UEFA Group Stage", 8, 6, 8);         // Portugal 2-0 Netherlands
        AddMatch(teams[22], teams[16], -10, 1, 1, "FINISHED", "UEFA Group Stage", 14, 16, 10);      // Belgium 1-1 France
        AddMatch(teams[23], teams[24], -10, 2, 0, "FINISHED", "UEFA Group Stage", 8, 5, 6);         // Croatia 2-0 Italy
        AddMatch(teams[25], teams[17], -10, 1, 2, "FINISHED", "UEFA Group Stage", 3, 7, 4);         // Norway 1-2 Spain
        AddMatch(teams[18], teams[26], -8, 3, 1, "FINISHED", "UEFA Group Stage", 8, 5, 5);          // Germany 3-1 Austria
        AddMatch(teams[19], teams[27], -8, 2, 0, "FINISHED", "UEFA Group Stage", 12, 5, 6);         // England 2-0 Switzerland
        AddMatch(teams[24], teams[23], -8, 1, 1, "FINISHED", "UEFA Group Stage", 6, 8, 5);          // Italy 1-1 Croatia
        AddMatch(teams[21], teams[22], -8, 3, 2, "FINISHED", "UEFA Group Stage", 28, 24, 20);       // Netherlands 3-2 Belgium
        AddMatch(teams[28], teams[29], -8, 1, 0, "FINISHED", "UEFA Group Stage", 30, 28, 18);       // Scotland 1-0 Sweden

        // === CONCACAF QUALIFIERS ===
        AddMatch(teams[30], teams[31], -15, 2, 1, "FINISHED", "CONCACAF Round 3 — Group A", 4, 3, 5);    // Panama 2-1 Jamaica
        AddMatch(teams[31], teams[32], -12, 0, 2, "FINISHED", "CONCACAF Round 3", 5, 18, 8);             // Jamaica 0-2 Mexico

        // === CAF QUALIFIERS ===
        AddMatch(teams[34], teams[35], -15, 1, 0, "FINISHED", "CAF Group Stage", 5, 4, 8);               // Morocco 1-0 Egypt
        AddMatch(teams[36], teams[37], -15, 2, 1, "FINISHED", "CAF Group Stage", 6, 6, 10);              // Senegal 2-1 Algeria
        AddMatch(teams[35], teams[38], -10, 2, 0, "FINISHED", "CAF Group Stage", 8, 5, 6);               // Egypt 2-0 Nigeria
        AddMatch(teams[34], teams[36], -10, 0, 0, "FINISHED", "CAF Group Stage", 6, 5, 9);               // Morocco 0-0 Senegal

        // === UPCOMING WORLD CUP MATCHES (SCHEDULED) ===
        AddMatch(teams[10], teams[17], 90, null, null, "SCHEDULED", "World Cup 2026 — Group Stage", 0, 0, 0);      // Argentina vs Spain
        AddMatch(teams[16], teams[11], 91, null, null, "SCHEDULED", "World Cup 2026 — Group Stage", 0, 0, 0);      // France vs Brazil
        AddMatch(teams[18], teams[19], 92, null, null, "SCHEDULED", "World Cup 2026 — Group Stage", 0, 0, 0);      // Germany vs England
        AddMatch(teams[0], teams[20], 93, null, null, "SCHEDULED", "World Cup 2026 — Group Stage", 0, 0, 0);       // Japan vs Portugal
        AddMatch(teams[2], teams[21], 94, null, null, "SCHEDULED", "World Cup 2026 — Group Stage", 0, 0, 0);       // South Korea vs Netherlands
        AddMatch(teams[34], teams[17], 95, null, null, "SCHEDULED", "World Cup 2026 — Group Stage", 0, 0, 0);      // Morocco vs Spain (rematch!)
        AddMatch(teams[10], teams[25], 96, null, null, "SCHEDULED", "World Cup 2026 — Round of 16", 0, 0, 0);       // Argentina vs Norway
        AddMatch(teams[16], teams[1], 97, null, null, "SCHEDULED", "World Cup 2026 — Round of 16", 0, 0, 0);       // France vs Iran

        // === LIVE MATCHES ===
        AddMatch(teams[10], teams[14], 0, 1, 0, "LIVE", "World Cup 2026 — Group H", 5, 2, 4);          // Argentina 1-0 Ecuador
        AddMatch(teams[18], teams[0], 0, 2, 1, "LIVE", "World Cup 2026 — Group E", 3, 2, 3);           // Germany 2-1 Japan
        AddMatch(teams[16], teams[36], 0, 0, 0, "LIVE", "World Cup 2026 — Group A", 2, 1, 1);          // France 0-0 Senegal

        context.Matches.AddRange(matches);
        await context.SaveChangesAsync();

        // ===== HEAD TO HEAD =====
        var h2hList = new List<HeadToHead>
        {
            new HeadToHead { Team1Id = teams[10].Id, Team2Id = teams[11].Id, TotalMatches = 110, Team1Wins = 40, Team2Wins = 42, Draws = 28, Team1Goals = 165, Team2Goals = 170, LastMatchDate = baseDate.AddDays(-5), UpdatedAt = DateTime.UtcNow },
            new HeadToHead { Team1Id = teams[16].Id, Team2Id = teams[18].Id, TotalMatches = 34, Team1Wins = 15, Team2Wins = 10, Draws = 9, Team1Goals = 48, Team2Goals = 45, LastMatchDate = baseDate.AddDays(-10), UpdatedAt = DateTime.UtcNow },
            new HeadToHead { Team1Id = teams[17].Id, Team2Id = teams[20].Id, TotalMatches = 40, Team1Wins = 17, Team2Wins = 7, Draws = 16, Team1Goals = 52, Team2Goals = 38, LastMatchDate = baseDate.AddDays(-8), UpdatedAt = DateTime.UtcNow },
            new HeadToHead { Team1Id = teams[19].Id, Team2Id = teams[18].Id, TotalMatches = 37, Team1Wins = 14, Team2Wins = 14, Draws = 9, Team1Goals = 55, Team2Goals = 48, LastMatchDate = baseDate.AddDays(-12), UpdatedAt = DateTime.UtcNow },
            new HeadToHead { Team1Id = teams[0].Id, Team2Id = teams[2].Id, TotalMatches = 82, Team1Wins = 16, Team2Wins = 42, Draws = 24, Team1Goals = 72, Team2Goals = 125, LastMatchDate = baseDate.AddDays(-90), UpdatedAt = DateTime.UtcNow },
            new HeadToHead { Team1Id = teams[10].Id, Team2Id = teams[17].Id, TotalMatches = 15, Team1Wins = 7, Team2Wins = 6, Draws = 2, Team1Goals = 20, Team2Goals = 22, LastMatchDate = baseDate.AddDays(-200), UpdatedAt = DateTime.UtcNow },
        };
        context.HeadToHeads.AddRange(h2hList);

        // ===== NEWS ARTICLES (Real quali stories) =====
        var newsArticles = new List<NewsArticle>
        {
            new NewsArticle { Title = "Argentina Complete CONMEBOL Qualifiers on Top with 38 Points", Content = "World champions Argentina have finished the South American qualifiers in first place with 38 points from 18 matches. Lionel Messi scored 9 goals during the campaign...", Source = "ESPN", Category = "Qualification", TeamId = teams[10].Id, SentimentScore = 0.9, SentimentLabel = "POSITIVE", PublishedAt = DateTime.UtcNow.AddHours(-12) },
            new NewsArticle { Title = "Japan and Australia Seal AFC Automatic Qualification Spots", Content = "Japan topped Group C with 23 points while Australia secured the second automatic spot with 19 points. Saudi Arabia will head to the playoffs...", Source = "AFC Official", Category = "Qualification", TeamId = teams[0].Id, SentimentScore = 0.85, SentimentLabel = "POSITIVE", PublishedAt = DateTime.UtcNow.AddHours(-18) },
            new NewsArticle { Title = "Iran Dominate AFC Group A — Uzbekistan Surprise Package", Content = "Team Melli collected 30 points from a possible 30 in early matches. Uzbekistan's qualification marks their first-ever World Cup appearance...", Source = "The Guardian", Category = "Qualification", TeamId = teams[1].Id, SentimentScore = 0.88, SentimentLabel = "POSITIVE", PublishedAt = DateTime.UtcNow.AddHours(-24) },
            new NewsArticle { Title = "Brazil Scrape Through — Worst CONMEBOL Campaign in Decades?", Content = "Brazil finished 5th with 28 points in the South American qualifiers, their worst performance in recent memory. The Seleção are in transition...", Source = "Globo Esporte", Category = "Analysis", TeamId = teams[11].Id, SentimentScore = -0.5, SentimentLabel = "NEGATIVE", PublishedAt = DateTime.UtcNow.AddHours(-20) },
            new NewsArticle { Title = "Mbappé Fires France to Perfect Qualifying Campaign", Content = "Kylian Mbappé scored 8 goals as France won Group D with 16 points, conceding just twice in 6 matches. Les Bleus are among the World Cup favorites...", Source = "L'Équipe", Category = "Qualification", TeamId = teams[16].Id, SentimentScore = 0.95, SentimentLabel = "POSITIVE", PublishedAt = DateTime.UtcNow.AddHours(-30) },
            new NewsArticle { Title = "Haaland's Norway Win Group I — First World Cup Since 1998", Content = "Erling Haaland scored 9 goals as Norway topped Group I ahead of Italy. This marks Norway's return to the World Cup after 28 years...", Source = "BBC Sport", Category = "Qualification", TeamId = teams[25].Id, SentimentScore = 0.92, SentimentLabel = "POSITIVE", PublishedAt = DateTime.UtcNow.AddHours(-36) },
            new NewsArticle { Title = "Spain's New Generation Dominates — Yamal Shines in Qualifiers", Content = "Euro 2024 champions Spain continued their dominance, winning Group E with 16 points. 17-year-old Lamine Yamal contributed 3 goals and 5 assists...", Source = "Marca", Category = "Qualification", TeamId = teams[17].Id, SentimentScore = 0.9, SentimentLabel = "POSITIVE", PublishedAt = DateTime.UtcNow.AddHours(-40) },
            new NewsArticle { Title = "Morocco Qualify as CAF's Top Seed — Eyes on Another Deep Run", Content = "After their historic 2022 semi-final run, the Atlas Lions qualified comfortably. Morocco are seeded and expected to challenge again...", Source = "Al Jazeera", Category = "Qualification", TeamId = teams[34].Id, SentimentScore = 0.87, SentimentLabel = "POSITIVE", PublishedAt = DateTime.UtcNow.AddHours(-48) },
            new NewsArticle { Title = "Son Heung-min Leads Korea to 11th Consecutive World Cup", Content = "South Korea secured their 11th straight World Cup appearance. Captain Son Heung-min scored 10 goals in the qualifying campaign...", Source = "Yonhap News", Category = "Qualification", TeamId = teams[2].Id, SentimentScore = 0.9, SentimentLabel = "POSITIVE", PublishedAt = DateTime.UtcNow.AddHours(-52) },
            new NewsArticle { Title = "Italy Face Potential World Cup Disaster — Playoff Pressure", Content = "Italy finished second in Group I behind Norway and must navigate the UEFA playoffs. Memories of missing 2018 and 2022 haunt the Azzurri...", Source = "Gazzetta dello Sport", Category = "Analysis", TeamId = teams[24].Id, SentimentScore = -0.6, SentimentLabel = "NEGATIVE", PublishedAt = DateTime.UtcNow.AddHours(-56) },
            new NewsArticle { Title = "Panama, Curaçao, Haiti Win CONCACAF Groups — Jamaica Through as Best Runner-Up", Content = "CONCACAF final round concluded with Panama, Curaçao, and Haiti topping their groups. Jamaica qualified for the inter-confederation playoffs...", Source = "CONCACAF Official", Category = "Qualification", TeamId = teams[30].Id, SentimentScore = 0.8, SentimentLabel = "POSITIVE", PublishedAt = DateTime.UtcNow.AddHours(-60) },
            new NewsArticle { Title = "WORLD CUP DRAW: Argentina vs Spain Headlines Group Stage", Content = "FIFA has drawn the groups for World Cup 2026. Blockbuster clashes include Argentina vs Spain, France vs Brazil, and Germany vs England in the group stage...", Source = "FIFA.com", Category = "Preview", SentimentScore = 0.7, SentimentLabel = "NEUTRAL", PublishedAt = DateTime.UtcNow.AddHours(-4) },
        };
        await context.NewsArticles.AddRangeAsync(newsArticles);

        // ===== PREDICTIONS (for finished quali matches) =====
        var finishedMatches = matches.Where(m => m.Status == "FINISHED").ToList();
        var random = new Random(42);
        foreach (var m in finishedMatches.Take(20))
        {
            bool correct = random.NextDouble() > 0.3;
            double hp = 0.25 + random.NextDouble() * 0.5;
            double ap = 0.1 + random.NextDouble() * 0.35;
            double dp = Math.Max(0, 1.0 - hp - ap);
            double total = hp + dp + ap;
            hp = Math.Round(hp / total, 2);
            dp = Math.Round(dp / total, 2);
            ap = Math.Round(ap / total, 2);

            context.Predictions.Add(new Prediction
            {
                MatchId = m.Id, PredictionType = "HYBRID",
                PredictedOutcome = (m.HomeScore > m.AwayScore) ? "HOME_WIN" : (m.HomeScore == m.AwayScore) ? "DRAW" : "AWAY_WIN",
                HomeWinProbability = hp, DrawProbability = dp, AwayWinProbability = ap,
                PredictedHomeScore = Math.Round((m.HomeScore ?? 1) + random.NextDouble() * 0.5 - 0.25, 1),
                PredictedAwayScore = Math.Round((m.AwayScore ?? 1) + random.NextDouble() * 0.5 - 0.25, 1),
                Confidence = Math.Round(0.55 + random.NextDouble() * 0.4, 2),
                KeyFactors = "Home advantage, ELO rating differential, H2H history",
                LLMExplanation = "Based on performance metrics, ELO ratings, and historical data from the 2026 World Cup qualification campaign.",
                IsCorrect = correct, CreatedAt = m.MatchDate.AddDays(-1)
            });
        }

        // Prediction untuk upcoming World Cup matches
        var upcomingMatches = matches.Where(m => m.Status == "SCHEDULED").ToList();
        foreach (var m in upcomingMatches)
        {
            double hp = 0.25 + random.NextDouble() * 0.5;
            double ap = 0.1 + random.NextDouble() * 0.35;
            double dp = Math.Max(0, 1.0 - hp - ap);
            double total = hp + dp + ap;
            hp = Math.Round(hp / total, 2);
            dp = Math.Round(dp / total, 2);
            ap = Math.Round(ap / total, 2);

            context.Predictions.Add(new Prediction
            {
                MatchId = m.Id, PredictionType = "HYBRID",
                PredictedOutcome = hp > ap ? "HOME_WIN" : "AWAY_WIN",
                HomeWinProbability = hp, DrawProbability = dp, AwayWinProbability = ap,
                PredictedHomeScore = Math.Round(1.0 + random.NextDouble() * 2.0, 1),
                PredictedAwayScore = Math.Round(0.5 + random.NextDouble() * 1.5, 1),
                Confidence = Math.Round(0.5 + random.NextDouble() * 0.3, 2),
                KeyFactors = "World Cup stage pressure, squad depth, tournament experience",
                LLMExplanation = "World Cup 2026 prediction based on qualification performance, ELO ratings, and squad analysis.",
                IsCorrect = null, CreatedAt = DateTime.UtcNow
            });
        }

        await context.SaveChangesAsync();
    }
}
