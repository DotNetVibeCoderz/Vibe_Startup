using System.Globalization;
using System.Text;
using BlazorViz.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BlazorViz.Data;

public static class SeedData
{
    public const string RoleAdmin = "Admin";
    public const string RoleAnalyst = "Analyst";
    public const string RoleViewer = "Viewer";

    public static async Task RunAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.MigrateAsync();

        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var env = scope.ServiceProvider.GetRequiredService<IWebHostEnvironment>();

        foreach (var role in new[] { RoleAdmin, RoleAnalyst, RoleViewer })
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));

        var admin = await EnsureUser(userManager, "admin@blazorviz.local", "Admin123!", RoleAdmin);
        await EnsureUser(userManager, "analyst@blazorviz.local", "Analyst123!", RoleAnalyst);
        await EnsureUser(userManager, "viewer@blazorviz.local", "Viewer123!", RoleViewer);

        if (!await db.ApiKeys.AnyAsync())
            db.ApiKeys.Add(new ApiKey { Name = "Demo key", Key = "bv-demo-" + Guid.NewGuid().ToString("N") });

        if (!await db.Datasets.AnyAsync())
        {
            var samplesDir = Path.Combine(env.ContentRootPath, "App_Data", "samples");
            Directory.CreateDirectory(samplesDir);
            WriteSampleFiles(samplesDir);

            var salesConn = new DataConnection { Name = "Sample: Sales CSV", Kind = "csv", ConfigJson = $$"""{"path": {{Json(Path.Combine(samplesDir, "sales.csv"))}}}""", CreatedBy = "system" };
            var worldConn = new DataConnection { Name = "Sample: World Indicators CSV", Kind = "csv", ConfigJson = $$"""{"path": {{Json(Path.Combine(samplesDir, "world_indicators.csv"))}}}""", CreatedBy = "system" };
            var webConn = new DataConnection { Name = "Sample: Web Analytics CSV", Kind = "csv", ConfigJson = $$"""{"path": {{Json(Path.Combine(samplesDir, "web_analytics.csv"))}}}""", CreatedBy = "system" };
            db.Connections.AddRange(salesConn, worldConn, webConn);
            await db.SaveChangesAsync();

            var dsSales = new Dataset { Name = "Sales", Description = "Sample retail sales transactions (2 years, 4 regions).", ConnectionId = salesConn.Id, SourceKind = "connection", CreatedBy = "system" };
            var dsWorld = new Dataset { Name = "World Indicators", Description = "Country-level population, GDP and life expectancy with coordinates.", ConnectionId = worldConn.Id, SourceKind = "connection", CreatedBy = "system" };
            var dsWeb = new Dataset { Name = "Web Analytics", Description = "Daily site traffic per page.", ConnectionId = webConn.Id, SourceKind = "connection", RefreshIntervalSeconds = 300, CreatedBy = "system" };
            var dsEtl = new Dataset
            {
                Name = "Sales by Region (ETL)",
                Description = "ETL example: aggregate revenue & quantity per region and category.",
                ConnectionId = salesConn.Id,
                SourceKind = "connection",
                EtlJson = EtlStep.ToJson(
                [
                    new EtlStep { Op = "aggregate", P = new() { ["groupBy"] = "Region,Category", ["aggs"] = "sum:Revenue,sum:Quantity,avg:UnitPrice" } },
                    new EtlStep { Op = "sort", P = new() { ["field"] = "sum_Revenue", ["desc"] = "true" } }
                ]),
                CreatedBy = "system"
            };
            db.Datasets.AddRange(dsSales, dsWorld, dsWeb, dsEtl);
            await db.SaveChangesAsync();

            var salesDash = BuildSalesDashboard(dsSales.Id, dsEtl.Id);
            var worldDash = BuildWorldDashboard(dsWorld.Id, dsWeb.Id);
            db.Dashboards.AddRange(
                NewDashboard("Sales Overview", "Revenue, orders and product performance.", salesDash, admin),
                NewDashboard("World & Web Analytics", "Geo map of world indicators plus site traffic.", worldDash, admin));
            await db.SaveChangesAsync();

            foreach (var dash in await db.Dashboards.ToListAsync())
                db.DashboardVersions.Add(new DashboardVersion { DashboardId = dash.Id, Version = 1, LayoutJson = dash.LayoutJson, CreatedBy = "system" });

            db.AuditLogs.Add(new AuditLog { UserName = "system", Category = "System", Action = "Seed", Details = "Sample users, datasets and dashboards created." });
            await db.SaveChangesAsync();
        }
        else
        {
            await db.SaveChangesAsync();
        }

        await EnsureExtraSamplesAsync(db, env, admin);
    }

    /// <summary>Adds the HR / Stocks / IoT sample datasets & dashboards when missing (runs on existing DBs too).</summary>
    private static async Task EnsureExtraSamplesAsync(ApplicationDbContext db, IWebHostEnvironment env, ApplicationUser admin)
    {
        if (await db.Datasets.AnyAsync(d => d.Name == "HR Employees")) return;

        var samplesDir = Path.Combine(env.ContentRootPath, "App_Data", "samples");
        Directory.CreateDirectory(samplesDir);
        WriteExtraSampleFiles(samplesDir);

        var hrConn = new DataConnection { Name = "Sample: HR CSV", Kind = "csv", ConfigJson = $$"""{"path": {{Json(Path.Combine(samplesDir, "hr_employees.csv"))}}}""", CreatedBy = "system" };
        var stockConn = new DataConnection { Name = "Sample: Stocks CSV", Kind = "csv", ConfigJson = $$"""{"path": {{Json(Path.Combine(samplesDir, "stocks.csv"))}}}""", CreatedBy = "system" };
        var iotConn = new DataConnection { Name = "Sample: Energy IoT CSV", Kind = "csv", ConfigJson = $$"""{"path": {{Json(Path.Combine(samplesDir, "energy_iot.csv"))}}}""", CreatedBy = "system" };
        db.Connections.AddRange(hrConn, stockConn, iotConn);
        await db.SaveChangesAsync();

        var dsHr = new Dataset { Name = "HR Employees", Description = "300 employees: department, role, salary, performance, satisfaction and attrition.", ConnectionId = hrConn.Id, SourceKind = "connection", CreatedBy = "system" };
        var dsStocks = new Dataset { Name = "Stock Prices", Description = "18 months of daily OHLCV prices for 3 fictional tickers.", ConnectionId = stockConn.Id, SourceKind = "connection", CreatedBy = "system" };
        var dsIot = new Dataset { Name = "Energy IoT", Description = "Hourly energy, temperature and humidity readings from 4 sites × 4 device types.", ConnectionId = iotConn.Id, SourceKind = "connection", CreatedBy = "system" };
        db.Datasets.AddRange(dsHr, dsStocks, dsIot);
        await db.SaveChangesAsync();

        var dashboards = new[]
        {
            NewDashboard("HR Analytics", "Workforce composition, compensation and attrition.", BuildHrDashboard(dsHr.Id), admin),
            NewDashboard("Stock Market", "OHLC candles, trends and volume for 3 tickers.", BuildStocksDashboard(dsStocks.Id), admin),
            NewDashboard("Energy IoT Monitor", "Site energy consumption, environment and device flows.", BuildIotDashboard(dsIot.Id), admin)
        };
        db.Dashboards.AddRange(dashboards);
        await db.SaveChangesAsync();
        foreach (var dash in dashboards)
            db.DashboardVersions.Add(new DashboardVersion { DashboardId = dash.Id, Version = 1, LayoutJson = dash.LayoutJson, CreatedBy = "system" });
        db.AuditLogs.Add(new AuditLog { UserName = "system", Category = "System", Action = "Seed", Details = "Extra samples added: HR Analytics, Stock Market, Energy IoT Monitor." });
        await db.SaveChangesAsync();
    }

    private static DashboardLayout BuildHrDashboard(int hrId) => new()
    {
        Filters =
        [
            new FilterDef { Type = "dropdown", DatasetId = hrId, Field = "Department", Label = "Department" },
            new FilterDef { Type = "slicer", DatasetId = hrId, Field = "Gender", Label = "Gender" },
            new FilterDef { Type = "slicer", DatasetId = hrId, Field = "Attrition", Label = "Attrition" }
        ],
        Tabs =
        [
            new DashboardTab
            {
                Title = "Workforce",
                Panels =
                [
                    new PanelDef { Title = "Headcount", ChartType = "kpi", DatasetId = hrId, Aggregation = "count", X = 0, Y = 0, W = 3, H = 2 },
                    new PanelDef { Title = "Avg Salary", ChartType = "kpi", DatasetId = hrId, YFields = ["Salary"], Aggregation = "avg", X = 3, Y = 0, W = 3, H = 2 },
                    new PanelDef { Title = "Avg Satisfaction", ChartType = "gauge", DatasetId = hrId, YFields = ["Satisfaction"], Aggregation = "avg", X = 6, Y = 0, W = 3, H = 2 },
                    new PanelDef { Title = "Gender Split", ChartType = "donut", DatasetId = hrId, XField = "Gender", Aggregation = "count", X = 9, Y = 0, W = 3, H = 2 },
                    new PanelDef { Title = "Headcount by Department", ChartType = "bar", DatasetId = hrId, XField = "Department", Aggregation = "count", SortBy = "count", SortDesc = true, X = 0, Y = 2, W = 6, H = 4 },
                    new PanelDef { Title = "Salary Distribution by Department", ChartType = "boxplot", DatasetId = hrId, XField = "Department", YFields = ["Salary"], Aggregation = "none", X = 6, Y = 2, W = 6, H = 4 },
                    new PanelDef { Title = "Age vs Salary", ChartType = "scatter", DatasetId = hrId, XField = "Age", YFields = ["Salary"], Aggregation = "none", X = 0, Y = 6, W = 6, H = 4 },
                    new PanelDef { Title = "Department → Role", ChartType = "sunburst", DatasetId = hrId, XField = "JobRole", SeriesField = "Department", YFields = ["Salary"], Aggregation = "sum", X = 6, Y = 6, W = 6, H = 4 }
                ]
            },
            new DashboardTab
            {
                Title = "Attrition",
                Panels =
                [
                    new PanelDef { Title = "Attrition Share", ChartType = "donut", DatasetId = hrId, XField = "Attrition", Aggregation = "count", X = 0, Y = 0, W = 4, H = 4 },
                    new PanelDef { Title = "Attrition by Department", ChartType = "stackedBar", DatasetId = hrId, XField = "Department", SeriesField = "Attrition", Aggregation = "count", X = 4, Y = 0, W = 8, H = 4 },
                    new PanelDef { Title = "Avg Satisfaction by Department", ChartType = "radar", DatasetId = hrId, XField = "Department", YFields = ["Satisfaction"], Aggregation = "avg", X = 0, Y = 4, W = 6, H = 4 },
                    new PanelDef { Title = "Longest-Serving Employees", ChartType = "table", DatasetId = hrId, XField = "JobRole", YFields = ["YearsAtCompany", "Salary"], Aggregation = "avg", SortBy = "YearsAtCompany", SortDesc = true, Limit = 12, X = 6, Y = 4, W = 6, H = 4 }
                ]
            }
        ]
    };

    private static DashboardLayout BuildStocksDashboard(int stockId) => new()
    {
        Filters =
        [
            new FilterDef { Type = "dropdown", DatasetId = stockId, Field = "Ticker", Label = "Ticker" },
            new FilterDef { Type = "daterange", DatasetId = stockId, Field = "Date", Label = "Date range" }
        ],
        Tabs =
        [
            new DashboardTab
            {
                Title = "Prices",
                Panels =
                [
                    new PanelDef { Title = "Max Close", ChartType = "kpi", DatasetId = stockId, YFields = ["Close"], Aggregation = "max", X = 0, Y = 0, W = 3, H = 2 },
                    new PanelDef { Title = "Min Close", ChartType = "kpi", DatasetId = stockId, YFields = ["Close"], Aggregation = "min", X = 3, Y = 0, W = 3, H = 2 },
                    new PanelDef { Title = "Total Volume", ChartType = "kpi", DatasetId = stockId, YFields = ["Volume"], Aggregation = "sum", X = 6, Y = 0, W = 3, H = 2 },
                    new PanelDef { Title = "Trading Days", ChartType = "kpi", DatasetId = stockId, Aggregation = "count", X = 9, Y = 0, W = 3, H = 2 },
                    new PanelDef { Title = "Candlestick (filter one ticker)", ChartType = "candlestick", DatasetId = stockId, XField = "Date", YFields = ["Open", "Close", "Low", "High"], Aggregation = "none", X = 0, Y = 2, W = 12, H = 5 },
                    new PanelDef { Title = "Close by Ticker", ChartType = "line", DatasetId = stockId, XField = "Date", SeriesField = "Ticker", YFields = ["Close"], Aggregation = "avg", X = 0, Y = 7, W = 8, H = 4 },
                    new PanelDef { Title = "Volume by Ticker", ChartType = "treemap", DatasetId = stockId, XField = "Ticker", YFields = ["Volume"], Aggregation = "sum", X = 8, Y = 7, W = 4, H = 4 }
                ]
            },
            new DashboardTab
            {
                Title = "Analysis",
                Panels =
                [
                    new PanelDef { Title = "Avg Close — Month × Ticker", ChartType = "heatmap", DatasetId = stockId, XField = "Month", SeriesField = "Ticker", YFields = ["Close"], Aggregation = "avg", X = 0, Y = 0, W = 8, H = 4 },
                    new PanelDef { Title = "Close Spread by Ticker", ChartType = "boxplot", DatasetId = stockId, XField = "Ticker", YFields = ["Close"], Aggregation = "none", X = 8, Y = 0, W = 4, H = 4 },
                    new PanelDef { Title = "Monthly Volume", ChartType = "area", DatasetId = stockId, XField = "Month", SeriesField = "Ticker", YFields = ["Volume"], Aggregation = "sum", X = 0, Y = 4, W = 12, H = 4 }
                ]
            }
        ]
    };

    private static DashboardLayout BuildIotDashboard(int iotId) => new()
    {
        Filters =
        [
            new FilterDef { Type = "slicer", DatasetId = iotId, Field = "Site", Label = "Site" },
            new FilterDef { Type = "multiselect", DatasetId = iotId, Field = "Device", Label = "Device" },
            new FilterDef { Type = "daterange", DatasetId = iotId, Field = "Date", Label = "Period" }
        ],
        Tabs =
        [
            new DashboardTab
            {
                Title = "Overview",
                Panels =
                [
                    new PanelDef { Title = "Total Energy (kWh)", ChartType = "kpi", DatasetId = iotId, YFields = ["EnergyKwh"], Aggregation = "sum", X = 0, Y = 0, W = 3, H = 2 },
                    new PanelDef { Title = "Avg Temperature (°C)", ChartType = "gauge", DatasetId = iotId, YFields = ["Temperature"], Aggregation = "avg", X = 3, Y = 0, W = 3, H = 2 },
                    new PanelDef { Title = "Avg Humidity (%)", ChartType = "kpi", DatasetId = iotId, YFields = ["Humidity"], Aggregation = "avg", X = 6, Y = 0, W = 3, H = 2 },
                    new PanelDef { Title = "Warnings", ChartType = "kpi", DatasetId = iotId, Aggregation = "count", X = 9, Y = 0, W = 3, H = 2, OptionsJson = null, SortBy = null },
                    new PanelDef { Title = "Daily Energy by Site", ChartType = "area", DatasetId = iotId, XField = "Date", SeriesField = "Site", YFields = ["EnergyKwh"], Aggregation = "sum", X = 0, Y = 2, W = 8, H = 4 },
                    new PanelDef { Title = "Site Map (energy)", ChartType = "map", DatasetId = iotId, LatField = "Lat", LngField = "Lng", ValueField = "EnergyKwh", LabelField = "Site", X = 8, Y = 2, W = 4, H = 4 },
                    new PanelDef { Title = "Energy Heatmap — Hour × Site", ChartType = "heatmap", DatasetId = iotId, XField = "Hour", SeriesField = "Site", YFields = ["EnergyKwh"], Aggregation = "sum", X = 0, Y = 6, W = 12, H = 4 }
                ]
            },
            new DashboardTab
            {
                Title = "Devices",
                Panels =
                [
                    new PanelDef { Title = "Energy Flow: Site → Device", ChartType = "sankey", DatasetId = iotId, XField = "Site", SeriesField = "Device", YFields = ["EnergyKwh"], Aggregation = "sum", X = 0, Y = 0, W = 8, H = 5 },
                    new PanelDef { Title = "Energy by Device", ChartType = "donut", DatasetId = iotId, XField = "Device", YFields = ["EnergyKwh"], Aggregation = "sum", X = 8, Y = 0, W = 4, H = 5 },
                    new PanelDef { Title = "Temperature Spread by Site", ChartType = "boxplot", DatasetId = iotId, XField = "Site", YFields = ["Temperature"], Aggregation = "none", X = 0, Y = 5, W = 6, H = 4 },
                    new PanelDef { Title = "Device Status", ChartType = "stackedBar", DatasetId = iotId, XField = "Device", SeriesField = "Status", Aggregation = "count", X = 6, Y = 5, W = 6, H = 4 }
                ]
            }
        ]
    };

    private static void WriteExtraSampleFiles(string dir)
    {
        var hr = Path.Combine(dir, "hr_employees.csv");
        if (!File.Exists(hr))
        {
            var rnd = new Random(11);
            var roles = new Dictionary<string, string[]>
            {
                ["Engineering"] = ["Software Engineer", "QA Engineer", "DevOps Engineer", "Engineering Manager"],
                ["Sales"] = ["Account Executive", "Sales Rep", "Sales Manager"],
                ["Marketing"] = ["Content Specialist", "SEO Analyst", "Marketing Manager"],
                ["HR"] = ["Recruiter", "HR Generalist"],
                ["Finance"] = ["Accountant", "Financial Analyst"],
                ["Operations"] = ["Logistics Coordinator", "Ops Manager"]
            };
            var baseSalary = new Dictionary<string, int>
            {
                ["Engineering"] = 95000, ["Sales"] = 70000, ["Marketing"] = 65000,
                ["HR"] = 60000, ["Finance"] = 75000, ["Operations"] = 62000
            };
            var sb = new StringBuilder("EmployeeId,Department,JobRole,Gender,Age,YearsAtCompany,Salary,PerformanceScore,Satisfaction,Attrition,HireDate\n");
            var departments = roles.Keys.ToArray();
            for (var i = 1; i <= 300; i++)
            {
                var dept = departments[rnd.Next(departments.Length)];
                var role = roles[dept][rnd.Next(roles[dept].Length)];
                var gender = rnd.NextDouble() < 0.45 ? "Female" : "Male";
                var years = rnd.Next(0, 18);
                var age = Math.Min(60, 22 + years + rnd.Next(0, 15));
                var salary = baseSalary[dept] + years * 2500 + rnd.Next(-8000, 15000) + (role.Contains("Manager") ? 25000 : 0);
                var performance = rnd.Next(1, 6);
                var satisfaction = Math.Clamp(performance + rnd.Next(-2, 5), 1, 10);
                var attrition = rnd.NextDouble() < (satisfaction <= 3 ? 0.55 : satisfaction <= 6 ? 0.18 : 0.06) ? "Yes" : "No";
                var hireDate = new DateTime(2026, 1, 1).AddYears(-years).AddDays(-rnd.Next(0, 364));
                sb.AppendLine(string.Join(",", $"E{i:0000}", dept, role, gender, age, years, salary, performance, satisfaction, attrition, hireDate.ToString("yyyy-MM-dd")));
            }
            File.WriteAllText(hr, sb.ToString());
        }

        var stocks = Path.Combine(dir, "stocks.csv");
        if (!File.Exists(stocks))
        {
            var rnd = new Random(23);
            var sb = new StringBuilder("Date,Month,Ticker,Open,Close,Low,High,Volume\n");
            (string Ticker, double Price, double Drift)[] tickers = [("BVZ", 120, 0.0007), ("DATA", 64, 0.0012), ("CHRT", 210, -0.0003)];
            var prices = tickers.ToDictionary(t => t.Ticker, t => t.Price);
            var start = new DateTime(2025, 1, 1);
            for (var d = 0; d < 548; d++)
            {
                var date = start.AddDays(d);
                if (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) continue;
                foreach (var (ticker, _, drift) in tickers)
                {
                    var open = prices[ticker];
                    var change = drift + (rnd.NextDouble() - 0.5) * 0.04;
                    var close = Math.Max(5, open * (1 + change));
                    var high = Math.Max(open, close) * (1 + rnd.NextDouble() * 0.015);
                    var low = Math.Min(open, close) * (1 - rnd.NextDouble() * 0.015);
                    var volume = (long)(800_000 + rnd.NextDouble() * 2_500_000 * (1 + Math.Abs(change) * 40));
                    sb.AppendLine(string.Join(",", date.ToString("yyyy-MM-dd"), date.ToString("yyyy-MM"), ticker,
                        Math.Round(open, 2).ToString(CultureInfo.InvariantCulture),
                        Math.Round(close, 2).ToString(CultureInfo.InvariantCulture),
                        Math.Round(low, 2).ToString(CultureInfo.InvariantCulture),
                        Math.Round(high, 2).ToString(CultureInfo.InvariantCulture), volume));
                    prices[ticker] = close;
                }
            }
            File.WriteAllText(stocks, sb.ToString());
        }

        var iot = Path.Combine(dir, "energy_iot.csv");
        if (!File.Exists(iot))
        {
            var rnd = new Random(37);
            (string Site, double Lat, double Lng, double Scale)[] sites =
            [
                ("Jakarta", -6.2, 106.8, 1.4), ("Surabaya", -7.25, 112.75, 1.1),
                ("Bandung", -6.9, 107.6, 0.8), ("Medan", 3.58, 98.67, 0.7)
            ];
            (string Device, double Base)[] devices = [("HVAC", 42), ("Lighting", 14), ("Servers", 30), ("Machinery", 55)];
            var sb = new StringBuilder("Timestamp,Date,Hour,Site,Lat,Lng,Device,EnergyKwh,Temperature,Humidity,Status\n");
            var start = DateTime.Today.AddDays(-14);
            for (var d = 0; d < 14; d++)
                for (var h = 0; h < 24; h++)
                {
                    var ts = start.AddDays(d).AddHours(h);
                    var dayFactor = h is >= 8 and <= 18 ? 1.0 : 0.45;
                    foreach (var (site, lat, lng, scale) in sites)
                        foreach (var (device, baseKwh) in devices)
                        {
                            var energy = Math.Round(baseKwh * scale * dayFactor * (0.8 + rnd.NextDouble() * 0.4), 2);
                            var temp = Math.Round(24 + (h is >= 10 and <= 16 ? 6 : 0) + rnd.NextDouble() * 4, 1);
                            var humidity = Math.Round(55 + rnd.NextDouble() * 30, 1);
                            var status = rnd.NextDouble() < 0.03 ? "Warning" : "OK";
                            sb.AppendLine(string.Join(",", ts.ToString("yyyy-MM-dd HH:mm"), ts.ToString("yyyy-MM-dd"), h,
                                site, lat.ToString(CultureInfo.InvariantCulture), lng.ToString(CultureInfo.InvariantCulture),
                                device, energy.ToString(CultureInfo.InvariantCulture),
                                temp.ToString(CultureInfo.InvariantCulture), humidity.ToString(CultureInfo.InvariantCulture), status));
                        }
                }
            File.WriteAllText(iot, sb.ToString());
        }
    }

    private static Dashboard NewDashboard(string name, string desc, DashboardLayout layout, ApplicationUser owner) => new()
    {
        Name = name,
        Description = desc,
        LayoutJson = layout.ToJson(),
        OwnerId = owner.Id,
        OwnerName = owner.UserName,
        IsPublic = true,
        ShareToken = Guid.NewGuid().ToString("N")
    };

    private static async Task<ApplicationUser> EnsureUser(UserManager<ApplicationUser> userManager, string email, string password, string role)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            user = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true };
            var result = await userManager.CreateAsync(user, password);
            if (!result.Succeeded)
                throw new InvalidOperationException($"Seed user {email} failed: {string.Join("; ", result.Errors.Select(e => e.Description))}");
        }
        if (!await userManager.IsInRoleAsync(user, role))
            await userManager.AddToRoleAsync(user, role);
        return user;
    }

    private static string Json(string s) => System.Text.Json.JsonSerializer.Serialize(s);

    private static DashboardLayout BuildSalesDashboard(int salesId, int etlId)
    {
        var layout = new DashboardLayout
        {
            RefreshIntervalSeconds = 0,
            Filters =
            [
                new FilterDef { Type = "dropdown", DatasetId = salesId, Field = "Region", Label = "Region" },
                new FilterDef { Type = "multiselect", DatasetId = salesId, Field = "Category", Label = "Category" },
                new FilterDef { Type = "daterange", DatasetId = salesId, Field = "OrderDate", Label = "Order date" }
            ],
            Tabs =
            [
                new DashboardTab
                {
                    Title = "Overview",
                    Panels =
                    [
                        new PanelDef { Title = "Total Revenue", ChartType = "kpi", DatasetId = salesId, YFields = ["Revenue"], Aggregation = "sum", X = 0, Y = 0, W = 3, H = 2 },
                        new PanelDef { Title = "Total Orders", ChartType = "kpi", DatasetId = salesId, YFields = ["Quantity"], Aggregation = "count", X = 3, Y = 0, W = 3, H = 2 },
                        new PanelDef { Title = "Avg Unit Price", ChartType = "kpi", DatasetId = salesId, YFields = ["UnitPrice"], Aggregation = "avg", X = 6, Y = 0, W = 3, H = 2 },
                        new PanelDef { Title = "Margin Gauge (%)", ChartType = "gauge", DatasetId = salesId, YFields = ["MarginPct"], Aggregation = "avg", X = 9, Y = 0, W = 3, H = 2 },
                        new PanelDef { Title = "Revenue by Month", ChartType = "line", DatasetId = salesId, XField = "Month", YFields = ["Revenue"], Aggregation = "sum", X = 0, Y = 2, W = 8, H = 4 },
                        new PanelDef { Title = "Revenue by Category", ChartType = "donut", DatasetId = salesId, XField = "Category", YFields = ["Revenue"], Aggregation = "sum", X = 8, Y = 2, W = 4, H = 4 },
                        new PanelDef { Title = "Revenue by Region", ChartType = "bar", DatasetId = salesId, XField = "Region", YFields = ["Revenue"], Aggregation = "sum", SortBy = "Revenue", SortDesc = true, X = 0, Y = 6, W = 6, H = 4 },
                        new PanelDef { Title = "Top Products", ChartType = "table", DatasetId = salesId, XField = "Product", YFields = ["Revenue", "Quantity"], Aggregation = "sum", SortBy = "Revenue", SortDesc = true, Limit = 10, X = 6, Y = 6, W = 6, H = 4 }
                    ]
                },
                new DashboardTab
                {
                    Title = "Breakdown",
                    Panels =
                    [
                        new PanelDef { Title = "Region × Category", ChartType = "stackedBar", DatasetId = salesId, XField = "Region", SeriesField = "Category", YFields = ["Revenue"], Aggregation = "sum", X = 0, Y = 0, W = 6, H = 4 },
                        new PanelDef { Title = "Monthly Heatmap", ChartType = "heatmap", DatasetId = salesId, XField = "Month", SeriesField = "Region", YFields = ["Revenue"], Aggregation = "sum", X = 6, Y = 0, W = 6, H = 4 },
                        new PanelDef { Title = "Product Treemap", ChartType = "treemap", DatasetId = salesId, XField = "Product", YFields = ["Revenue"], Aggregation = "sum", X = 0, Y = 4, W = 6, H = 4 },
                        new PanelDef { Title = "Revenue Waterfall by Month", ChartType = "waterfall", DatasetId = salesId, XField = "Month", YFields = ["Profit"], Aggregation = "sum", X = 6, Y = 4, W = 6, H = 4 },
                        new PanelDef { Title = "Aggregated (ETL) Result", ChartType = "table", DatasetId = etlId, X = 0, Y = 8, W = 12, H = 4 }
                    ]
                }
            ]
        };
        return layout;
    }

    private static DashboardLayout BuildWorldDashboard(int worldId, int webId)
    {
        return new DashboardLayout
        {
            RefreshIntervalSeconds = 0,
            Filters =
            [
                new FilterDef { Type = "slicer", DatasetId = worldId, Field = "Continent", Label = "Continent" }
            ],
            Tabs =
            [
                new DashboardTab
                {
                    Title = "World",
                    Panels =
                    [
                        new PanelDef { Title = "Population Map", ChartType = "map", DatasetId = worldId, LatField = "Lat", LngField = "Lng", ValueField = "Population", LabelField = "Country", X = 0, Y = 0, W = 8, H = 5 },
                        new PanelDef { Title = "GDP per Capita (Top 12)", ChartType = "horizontalBar", DatasetId = worldId, XField = "Country", YFields = ["GdpPerCapita"], Aggregation = "sum", SortBy = "GdpPerCapita", SortDesc = true, Limit = 12, X = 8, Y = 0, W = 4, H = 5 },
                        new PanelDef { Title = "GDP vs Life Expectancy", ChartType = "bubble", DatasetId = worldId, XField = "GdpPerCapita", YFields = ["LifeExpectancy"], SizeField = "Population", LabelField = "Country", Aggregation = "none", X = 0, Y = 5, W = 6, H = 4 },
                        new PanelDef { Title = "Population by Continent", ChartType = "treemap", DatasetId = worldId, XField = "Continent", YFields = ["Population"], Aggregation = "sum", X = 6, Y = 5, W = 6, H = 4 }
                    ]
                },
                new DashboardTab
                {
                    Title = "Web Traffic",
                    Panels =
                    [
                        new PanelDef { Title = "Visitors Trend", ChartType = "area", DatasetId = webId, XField = "Date", YFields = ["Visitors"], Aggregation = "sum", X = 0, Y = 0, W = 8, H = 4 },
                        new PanelDef { Title = "Views by Page", ChartType = "pie", DatasetId = webId, XField = "Page", YFields = ["PageViews"], Aggregation = "sum", X = 8, Y = 0, W = 4, H = 4 },
                        new PanelDef { Title = "Bounce Rate by Page", ChartType = "bar", DatasetId = webId, XField = "Page", YFields = ["BounceRate"], Aggregation = "avg", X = 0, Y = 4, W = 6, H = 4 },
                        new PanelDef { Title = "Traffic Funnel", ChartType = "funnel", DatasetId = webId, XField = "Page", YFields = ["Visitors"], Aggregation = "sum", Limit = 6, X = 6, Y = 4, W = 6, H = 4 }
                    ]
                }
            ]
        };
    }

    /// <summary>Generates deterministic sample CSV files (fixed seed) if they do not exist.</summary>
    private static void WriteSampleFiles(string dir)
    {
        var sales = Path.Combine(dir, "sales.csv");
        if (!File.Exists(sales))
        {
            var rnd = new Random(42);
            string[] regions = ["North", "South", "East", "West"];
            string[] categories = ["Electronics", "Furniture", "Apparel", "Groceries"];
            var products = new Dictionary<string, string[]>
            {
                ["Electronics"] = ["Laptop", "Phone", "Headphones", "Monitor"],
                ["Furniture"] = ["Desk", "Chair", "Bookshelf"],
                ["Apparel"] = ["Jacket", "Sneakers", "T-Shirt"],
                ["Groceries"] = ["Coffee", "Tea", "Snacks"]
            };
            var sb = new StringBuilder("OrderDate,Month,Region,Category,Product,Quantity,UnitPrice,Revenue,Cost,Profit,MarginPct\n");
            var start = new DateTime(2024, 1, 1);
            for (var i = 0; i < 730; i += 1)
            {
                var date = start.AddDays(i);
                var n = rnd.Next(1, 4);
                for (var j = 0; j < n; j++)
                {
                    var cat = categories[rnd.Next(categories.Length)];
                    var prod = products[cat][rnd.Next(products[cat].Length)];
                    var region = regions[rnd.Next(regions.Length)];
                    var qty = rnd.Next(1, 12);
                    var price = Math.Round(10 + rnd.NextDouble() * 990, 2);
                    var revenue = Math.Round(qty * price, 2);
                    var cost = Math.Round(revenue * (0.55 + rnd.NextDouble() * 0.25), 2);
                    var profit = Math.Round(revenue - cost, 2);
                    var margin = Math.Round(profit / revenue * 100, 1);
                    sb.AppendLine(string.Join(",",
                        date.ToString("yyyy-MM-dd"), date.ToString("yyyy-MM"), region, cat, prod, qty,
                        price.ToString(CultureInfo.InvariantCulture), revenue.ToString(CultureInfo.InvariantCulture),
                        cost.ToString(CultureInfo.InvariantCulture), profit.ToString(CultureInfo.InvariantCulture),
                        margin.ToString(CultureInfo.InvariantCulture)));
                }
            }
            File.WriteAllText(sales, sb.ToString());
        }

        var world = Path.Combine(dir, "world_indicators.csv");
        if (!File.Exists(world))
        {
            File.WriteAllText(world,
                """
                Country,Continent,Lat,Lng,Population,GdpPerCapita,LifeExpectancy
                Indonesia,Asia,-6.2,106.8,277000000,4940,72.0
                United States,Americas,38.9,-77.0,333000000,76399,77.2
                China,Asia,39.9,116.4,1412000000,12720,78.2
                India,Asia,28.6,77.2,1417000000,2410,67.7
                Brazil,Americas,-15.8,-47.9,215000000,8918,72.8
                Nigeria,Africa,9.1,7.4,218000000,2184,52.7
                Germany,Europe,52.5,13.4,84000000,48718,80.8
                Japan,Asia,35.7,139.7,125000000,33815,84.5
                United Kingdom,Europe,51.5,-0.1,67000000,45850,80.7
                France,Europe,48.9,2.4,68000000,40494,82.5
                Mexico,Americas,19.4,-99.1,127000000,11091,70.2
                Egypt,Africa,30.0,31.2,111000000,4295,70.2
                Russia,Europe,55.8,37.6,144000000,15271,69.4
                Australia,Oceania,-35.3,149.1,26000000,64492,83.3
                South Africa,Africa,-25.7,28.2,60000000,6766,62.3
                South Korea,Asia,37.6,127.0,52000000,32255,83.5
                Canada,Americas,45.4,-75.7,39000000,54966,81.3
                Italy,Europe,41.9,12.5,59000000,34158,82.8
                Spain,Europe,40.4,-3.7,48000000,29350,83.2
                Argentina,Americas,-34.6,-58.4,46000000,13651,75.4
                Turkey,Asia,39.9,32.9,85000000,10616,76.0
                Thailand,Asia,13.8,100.5,72000000,6910,79.3
                Vietnam,Asia,21.0,105.8,98000000,4164,73.6
                Malaysia,Asia,3.1,101.7,34000000,11972,74.9
                Singapore,Asia,1.35,103.8,5900000,82808,83.9
                Saudi Arabia,Asia,24.7,46.7,36000000,30436,76.9
                Kenya,Africa,-1.3,36.8,54000000,2099,61.4
                New Zealand,Oceania,-41.3,174.8,5100000,48781,82.1
                Netherlands,Europe,52.4,4.9,17700000,57025,81.7
                Sweden,Europe,59.3,18.1,10500000,56424,83.1
                """);
        }

        var web = Path.Combine(dir, "web_analytics.csv");
        if (!File.Exists(web))
        {
            var rnd = new Random(7);
            string[] pages = ["/home", "/products", "/pricing", "/blog", "/docs", "/checkout"];
            var sb = new StringBuilder("Date,Page,Visitors,PageViews,BounceRate,AvgSessionSec\n");
            var start = DateTime.Today.AddDays(-59);
            for (var d = 0; d < 60; d++)
            {
                foreach (var page in pages)
                {
                    var baseline = page switch { "/home" => 900, "/products" => 620, "/pricing" => 340, "/blog" => 280, "/docs" => 190, _ => 120 };
                    var visitors = baseline + rnd.Next(-80, 120);
                    sb.AppendLine(string.Join(",",
                        start.AddDays(d).ToString("yyyy-MM-dd"), page, visitors,
                        (int)(visitors * (1.4 + rnd.NextDouble())),
                        Math.Round(20 + rnd.NextDouble() * 45, 1).ToString(CultureInfo.InvariantCulture),
                        rnd.Next(35, 320)));
                }
            }
            File.WriteAllText(web, sb.ToString());
        }
    }
}
