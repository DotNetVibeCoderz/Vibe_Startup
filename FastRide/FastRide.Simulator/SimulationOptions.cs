using Microsoft.Extensions.Configuration;

namespace FastRide.Simulator;

/// <summary>Everything the run is configured with, from appsettings.json plus CLI overrides.</summary>
public sealed class SimulationOptions
{
    public string BaseUrl { get; init; } = "https://localhost:5001";
    public int RiderCount { get; init; } = 5;
    public int DriverCount { get; init; } = 3;

    /// <summary>Stop automatically after this many seconds. 0 means run until S is pressed.</summary>
    public int DurationSeconds { get; init; }

    public int RandomSeed { get; init; } = 42;

    /// <summary>Admin account used to approve the simulated drivers' documents.</summary>
    public string AdminEmail { get; init; } = "admin@fastride.com";
    public string AdminPassword { get; init; } = "Password123";

    /// <summary>Password given to every account the simulator creates.</summary>
    public string SimPassword { get; init; } = "SimPass123!";

    /// <summary>Share of trips a rider abandons before pickup, to exercise the cancel path.</summary>
    public double CancelRate { get; init; } = 0.08;

    public static SimulationOptions Load(IConfiguration config, string[] args)
    {
        var options = new SimulationOptions
        {
            BaseUrl = config["ApiSettings:BaseUrl"] ?? "https://localhost:5001",
            RiderCount = config.GetValue("Simulation:RiderCount", 5),
            DriverCount = config.GetValue("Simulation:DriverCount", 3),
            DurationSeconds = config.GetValue("Simulation:DurationSeconds", 0),
            RandomSeed = config.GetValue("Simulation:RandomSeed", 42),
            AdminEmail = config["Simulation:AdminEmail"] ?? "admin@fastride.com",
            AdminPassword = config["Simulation:AdminPassword"] ?? "Password123",
            SimPassword = config["Simulation:Password"] ?? "SimPass123!",
            CancelRate = config.GetValue("Simulation:CancelRate", 0.08)
        };

        return ApplyArguments(options, args);
    }

    /// <summary>--riders 20 --drivers 8 --duration 60 --url https://localhost:5001</summary>
    private static SimulationOptions ApplyArguments(SimulationOptions options, string[] args)
    {
        var riders = options.RiderCount;
        var drivers = options.DriverCount;
        var duration = options.DurationSeconds;
        var url = options.BaseUrl;

        for (var i = 0; i < args.Length - 1; i++)
        {
            switch (args[i].ToLowerInvariant())
            {
                case "--riders" when int.TryParse(args[i + 1], out var value):
                    riders = value;
                    break;
                case "--drivers" when int.TryParse(args[i + 1], out var value):
                    drivers = value;
                    break;
                case "--duration" when int.TryParse(args[i + 1], out var value):
                    duration = value;
                    break;
                case "--url":
                    url = args[i + 1];
                    break;
            }
        }

        return new SimulationOptions
        {
            BaseUrl = url,
            RiderCount = Math.Clamp(riders, 1, 200),
            DriverCount = Math.Clamp(drivers, 1, 100),
            DurationSeconds = Math.Max(0, duration),
            RandomSeed = options.RandomSeed,
            AdminEmail = options.AdminEmail,
            AdminPassword = options.AdminPassword,
            SimPassword = options.SimPassword,
            CancelRate = Math.Clamp(options.CancelRate, 0, 1)
        };
    }
}
