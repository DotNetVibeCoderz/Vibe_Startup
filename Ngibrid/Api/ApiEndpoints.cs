using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ngibrid.Data;
using Ngibrid.Models;
using Ngibrid.Services;

namespace Ngibrid.Api;

/// <summary>
/// Minimal API endpoints for external integration (marketplace, ERP, CRM, courier apps).
/// Documented at /api/docs via Swagger.
/// </summary>
public static class ApiEndpoints
{
    public static void MapApiEndpoints(this WebApplication app)
    {
        var api = app.MapGroup("/api/v1");

        MapMasterDataEndpoints(api);
        MapOrderEndpoints(api);
        MapTrackingEndpoints(api);
        MapFinanceEndpoints(api);
        MapOperationsEndpoints(api);
        MapAnalyticsEndpoints(api);
        MapIntegrationEndpoints(api);
        MapComplianceEndpoints(api);
        MapChatEndpoints(api);
        MapLabelEndpoints(api);
        MapConfigEndpoints(api);
    }

    // ─── Master data: provinces & cities ───
    // Public and unauthenticated on purpose: this is a reference table, and the order form,
    // marketplace importers, and the chat bot all need it before anyone signs in.
    private static void MapMasterDataEndpoints(RouteGroupBuilder api)
    {
        api.MapGet("/provinces", async (CityService svc) =>
            Results.Ok(await svc.GetProvincesAsync()))
            .WithTags("MasterData");

        api.MapGet("/cities", async (CityService svc, string? province, string? q) =>
        {
            var source = string.IsNullOrWhiteSpace(province)
                ? await svc.GetAllAsync()
                : await svc.GetCitiesAsync(province);

            if (!string.IsNullOrWhiteSpace(q))
                source = source.Where(c => c.Name.Contains(q, StringComparison.OrdinalIgnoreCase)
                                        || (c.SeatName?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false))
                               .ToList();

            return Results.Ok(source.Select(c => new
            {
                c.Id, c.Country, c.Province, c.Type, c.Name, c.FullName,
                Seat = c.SeatName, c.Latitude, c.Longitude
            }));
        }).WithTags("MasterData");

        api.MapGet("/cities/{id:long}", async (long id, CityService svc) =>
            (await svc.GetAllAsync()).FirstOrDefault(c => c.Id == id) is { } city
                ? Results.Ok(new
                {
                    city.Id, city.Country, city.Province, city.Type, city.Name, city.FullName,
                    Seat = city.SeatName, city.Latitude, city.Longitude
                })
                : Results.NotFound())
            .WithTags("MasterData");

        api.MapGet("/cities/distance", (string from, string to,
                string? fromProvince, string? toProvince) =>
        {
            if (!CityCoordinates.TryResolve(fromProvince, from, out var a))
                return Results.BadRequest(new { error = $"Kota '{from}' tidak ada di master data." });
            if (!CityCoordinates.TryResolve(toProvince, to, out var b))
                return Results.BadRequest(new { error = $"Kota '{to}' tidak ada di master data." });

            var km = RouteOptimizationService.HaversineDistance(a.Lat, a.Lng, b.Lat, b.Lng);
            return Results.Ok(new
            {
                From = new { City = from, Province = fromProvince, a.Lat, a.Lng },
                To = new { City = to, Province = toProvince, b.Lat, b.Lng },
                StraightLineKm = Math.Round(km, 2)
            });
        }).WithTags("MasterData");
    }

    // ─── Orders ───
    private static void MapOrderEndpoints(RouteGroupBuilder api)
    {
        api.MapGet("/orders", async (OrderService svc, ClaimsPrincipal user,
                string? search, string? status, int page = 1, int size = 20) =>
        {
            // Customers only ever see their own orders; staff see everything.
            if (IsStaff(user))
                return Results.Ok(await svc.GetOrdersAsync(search, status, page, size));

            var userId = GetUserId(user);
            if (userId is null) return Results.Unauthorized();
            return Results.Ok(await svc.GetCustomerOrdersAsync(userId.Value, page, size));
        })
        .RequireAuthorization().WithName("GetOrders").WithTags("Orders");

        api.MapGet("/orders/{id:long}", async (long id, OrderService svc, ClaimsPrincipal user) =>
        {
            var order = await svc.GetOrderAsync(id);
            if (order == null) return Results.NotFound();
            if (!IsStaff(user) && order.CustomerId != GetUserId(user)) return Results.Forbid();
            return Results.Ok(order);
        }).RequireAuthorization().WithTags("Orders");

        // Public: tracking a parcel by its number is intentionally anonymous, like every courier's site.
        api.MapGet("/orders/track/{trackingNumber}", async (string trackingNumber, OrderService svc) =>
            await svc.GetByTrackingNumberAsync(trackingNumber) is { } o
                ? Results.Ok(ToPublicTracking(o))
                : Results.NotFound(new { message = "Nomor resi tidak ditemukan." }))
            .WithName("TrackOrder").WithTags("Orders");

        api.MapPost("/orders", async (CreateOrderRequest request, OrderService svc, ClaimsPrincipal user) =>
        {
            var userId = GetUserId(user);
            if (userId is null) return Results.Unauthorized();

            var order = new Order
            {
                CustomerId = userId.Value,
                ExternalOrderId = request.ExternalOrderId,
                SenderName = request.SenderName,
                SenderPhone = request.SenderPhone,
                SenderAddress = request.SenderAddress,
                SenderCity = request.SenderCity,
                SenderProvince = request.SenderProvince
                                 ?? CityCoordinates.ProvinceOf(request.SenderCity),
                SenderPostalCode = request.SenderPostalCode,
                RecipientName = request.RecipientName,
                RecipientPhone = request.RecipientPhone,
                RecipientAddress = request.RecipientAddress,
                RecipientCity = request.RecipientCity,
                RecipientProvince = request.RecipientProvince
                                    ?? CityCoordinates.ProvinceOf(request.RecipientCity),
                RecipientPostalCode = request.RecipientPostalCode,
                PackageDescription = request.PackageDescription,
                WeightKg = request.WeightKg,
                LengthCm = request.LengthCm,
                WidthCm = request.WidthCm,
                HeightCm = request.HeightCm,
                ServiceType = request.ServiceType ?? "REG",
                HasInsurance = request.HasInsurance,
                DeclaredValue = request.DeclaredValue,
                IsEcoDelivery = request.IsEcoDelivery
            };

            var created = await svc.CreateOrderAsync(order);
            return Results.Created($"/api/v1/orders/{created.Id}", created);
        }).RequireAuthorization().WithTags("Orders");

        api.MapPut("/orders/{id:long}/status", async (long id, UpdateStatusRequest request,
                OrderService svc, ClaimsPrincipal user) =>
        {
            var order = await svc.UpdateStatusAsync(id, request.Status, request.Notes,
                request.Latitude, request.Longitude, user.Identity?.Name);
            return Results.Ok(order);
        }).RequireAuthorization("CourierArea").WithTags("Orders");

        api.MapPost("/orders/{id:long}/cancel", async (long id, [FromBody] CancelRequest request,
                OrderService svc, ClaimsPrincipal user) =>
        {
            var order = await svc.GetOrderAsync(id);
            if (order == null) return Results.NotFound();
            if (!IsStaff(user) && order.CustomerId != GetUserId(user)) return Results.Forbid();

            await svc.CancelOrderAsync(id, request.Reason ?? "Dibatalkan oleh pengguna");
            return Results.Ok(new { message = "Pesanan dibatalkan." });
        }).RequireAuthorization().WithTags("Orders");
    }

    // ─── Tracking ───
    private static void MapTrackingEndpoints(RouteGroupBuilder api)
    {
        api.MapGet("/tracking/{orderId:long}", async (long orderId, TrackingService svc) =>
            Results.Ok(await svc.GetTrackingHistoryAsync(orderId)))
            .WithTags("Tracking");

        api.MapGet("/tracking/{orderId:long}/latest", async (long orderId, TrackingService svc) =>
            await svc.GetLatestPositionAsync(orderId) is { } t ? Results.Ok(t) : Results.NotFound())
            .WithTags("Tracking");

        api.MapPost("/tracking/{orderId:long}", async (long orderId, TrackingPointRequest request,
                TrackingService svc, ClaimsPrincipal user) =>
        {
            var point = await svc.AddTrackingPointAsync(orderId, request.Latitude, request.Longitude,
                request.SpeedKmh, request.Heading, request.Description, GetUserId(user));
            return Results.Created($"/api/v1/tracking/{orderId}/latest", point);
        }).RequireAuthorization("CourierArea").WithTags("Tracking");

        // originProvince/destProvince are optional but recommended — without them a bare
        // "Bandung" cannot be told apart from "Kabupaten Bandung".
        api.MapGet("/pricing/calculate", async (string origin, string dest, double weight,
                string? service, string? originProvince, string? destProvince,
                DynamicPricingService svc) =>
            Results.Ok(await svc.CalculatePriceAsync(origin, dest, weight, service ?? "REG",
                originProvince, destProvince)))
            .WithTags("Pricing");

        api.MapGet("/pricing/compare", async (string origin, string dest, double weight,
                string? originProvince, string? destProvince, DynamicPricingService svc) =>
        {
            var results = new Dictionary<string, PriceResult>();
            foreach (var s in new[] { "ECO", "REG", "EXP", "SAMEDAY" })
                results[s] = await svc.CalculatePriceAsync(origin, dest, weight, s,
                    originProvince, destProvince);
            return Results.Ok(results);
        }).WithTags("Pricing");
    }

    // ─── Payments, invoices, insurance ───
    private static void MapFinanceEndpoints(RouteGroupBuilder api)
    {
        api.MapGet("/payments", async (PaymentService svc, int page = 1, int size = 20) =>
            Results.Ok(await svc.GetPaymentsAsync(page, size)))
            .RequireAuthorization().WithTags("Payments");

        api.MapPost("/payments/{orderId:long}", async (long orderId, [FromBody] CreatePaymentRequest request,
                PaymentService svc) =>
        {
            var payment = await svc.CreatePaymentAsync(orderId, request.Method, request.Channel);
            return Results.Created($"/api/v1/payments/{payment.Id}", payment);
        }).RequireAuthorization().WithTags("Payments");

        api.MapPost("/payments/{paymentId:long}/confirm", async (long paymentId,
                [FromBody] ConfirmPaymentRequest request, PaymentService svc) =>
            Results.Ok(await svc.ConfirmPaymentAsync(paymentId,
                request.TransactionId ?? $"TXN-{Guid.NewGuid().ToString("N")[..10].ToUpper()}")))
            .RequireAuthorization().WithTags("Payments");

        api.MapGet("/invoices", async (InvoiceService svc, int page = 1, int size = 50) =>
            Results.Ok(await svc.GetInvoicesAsync(page, size)))
            .RequireAuthorization().WithTags("Invoices");

        api.MapGet("/invoices/{orderId:long}", async (long orderId, InvoiceService svc) =>
            Results.Ok(await svc.GetOrGenerateInvoiceAsync(orderId)))
            .RequireAuthorization().WithTags("Invoices");

        api.MapGet("/invoices/{invoiceId:long}/html", async (long invoiceId, InvoiceService svc) =>
            Results.Content(await svc.RenderInvoiceHtmlAsync(invoiceId), "text/html"))
            .RequireAuthorization().WithTags("Invoices");

        api.MapGet("/insurance/claims", async (InsuranceService svc, string? status) =>
            Results.Ok(await svc.GetClaimsAsync(status)))
            .RequireAuthorization().WithTags("Insurance");

        api.MapPost("/insurance/claims", async ([FromBody] ClaimRequest request, InsuranceService svc) =>
            Results.Ok(await svc.SubmitClaimAsync(request.OrderId, request.Amount, request.Reason, request.DocumentUrl)))
            .RequireAuthorization().WithTags("Insurance");

        api.MapPost("/insurance/claims/{claimId:long}/review", async (long claimId,
                [FromBody] ReviewClaimRequest request, InsuranceService svc) =>
            Results.Ok(await svc.ReviewClaimAsync(claimId, request.Approve, request.ApprovedAmount, request.Reason)))
            .RequireAuthorization("AdminOrManager").WithTags("Insurance");
    }

    // ─── Warehouse, courier, pickup, support, lockers ───
    private static void MapOperationsEndpoints(RouteGroupBuilder api)
    {
        api.MapGet("/warehouses", async (WarehouseService svc) =>
            Results.Ok(await svc.GetAllWarehousesAsync())).WithTags("Warehouse");

        api.MapGet("/warehouses/{id:long}", async (long id, WarehouseService svc) =>
            await svc.GetWarehouseAsync(id) is { } w ? Results.Ok(w) : Results.NotFound())
            .WithTags("Warehouse");

        api.MapGet("/inventory", async (string? query, long? warehouseId, WarehouseService svc) =>
            Results.Ok(await svc.SearchInventoryAsync(query, warehouseId))).WithTags("Warehouse");

        api.MapPost("/inventory/{itemId:long}/movement", async (long itemId,
                [FromBody] MovementRequest request, WarehouseService svc) =>
            Results.Ok(await svc.RecordMovementAsync(itemId, request.Type, request.Quantity, request.Notes)))
            .RequireAuthorization("StaffArea").WithTags("Warehouse");

        api.MapGet("/packaging/recommend", (double length, double width, double height, WarehouseService svc) =>
        {
            var (box, wasted) = svc.RecommendBox(length, width, height);
            var volume = length * width * height;
            return Results.Ok(new
            {
                recommendedBox = box,
                wastedVolumePercent = wasted,
                volumeCm3 = volume,
                volumetricWeightKg = Math.Round(volume / 6000.0, 2)
            });
        }).WithTags("Warehouse");

        api.MapGet("/couriers", async (CourierService svc) =>
            Results.Ok(await svc.GetAllCouriersAsync()))
            .RequireAuthorization().WithTags("Courier");

        api.MapGet("/couriers/available", async (CourierService svc) =>
            Results.Ok(await svc.GetAvailableCouriersAsync())).WithTags("Courier");

        api.MapPost("/couriers/{courierId:long}/location", async (long courierId,
                [FromBody] LocationRequest request, CourierService svc) =>
        {
            await svc.UpdateLocationAsync(courierId, request.Latitude, request.Longitude);
            return Results.Ok(new { message = "Lokasi diperbarui." });
        }).RequireAuthorization("CourierArea").WithTags("Courier");

        api.MapGet("/couriers/{courierId:long}/route", async (long courierId, DateTime? date,
                RouteOptimizationService svc) =>
            Results.Ok(await svc.PlanCourierRouteAsync(courierId, date ?? DateTime.UtcNow)))
            .RequireAuthorization("CourierArea").WithTags("Courier");

        api.MapPost("/pickup", async (PickupRequest request, PickupService svc, ClaimsPrincipal user) =>
        {
            var userId = GetUserId(user);
            if (userId is null) return Results.Unauthorized();
            request.CustomerId = userId.Value;
            var created = await svc.RequestPickupAsync(request);
            return Results.Created($"/api/v1/pickup/{created.Id}", created);
        }).RequireAuthorization().WithTags("Pickup");

        api.MapGet("/pickup/pending", async (PickupService svc) =>
            Results.Ok(await svc.GetPendingPickupsAsync()))
            .RequireAuthorization().WithTags("Pickup");

        api.MapGet("/support/tickets", async (SupportTicketService svc, ClaimsPrincipal user, string? status) =>
            Results.Ok(await svc.GetTicketsAsync(IsStaff(user) ? null : GetUserId(user), status)))
            .RequireAuthorization().WithTags("Support");

        api.MapPost("/support/tickets", async (SupportTicket ticket, SupportTicketService svc,
                ClaimsPrincipal user) =>
        {
            var userId = GetUserId(user);
            if (userId is null) return Results.Unauthorized();
            ticket.UserId = userId.Value;
            return Results.Ok(await svc.CreateTicketAsync(ticket));
        }).RequireAuthorization().WithTags("Support");

        // Projected, not returned raw: the entity carries AccessPin, and this endpoint is public.
        api.MapGet("/lockers", async (SmartLockerService svc, string? city) =>
            Results.Ok((await svc.GetLockersAsync(city)).Select(l => new
            {
                l.Id, l.Code, l.Name, l.Address, l.City, l.Latitude, l.Longitude,
                l.Status, l.BatteryPercent, l.TemperatureCelsius, l.LastHeartbeat,
                TotalCompartments = l.Compartments?.Count ?? 0,
                AvailableCompartments = l.Compartments?.Count(c => c.Status == "EMPTY") ?? 0,
                Compartments = l.Compartments?
                    .OrderBy(c => c.CompartmentNumber)
                    .Select(c => new { c.Id, c.CompartmentNumber, c.Size, c.Status, c.ExpiresAt })
            }))).WithTags("SmartLocker");

        api.MapPost("/lockers/{lockerId:long}/assign", async (long lockerId,
                [FromBody] AssignLockerRequest request, SmartLockerService svc) =>
            await svc.AssignCompartmentAsync(lockerId, request.OrderId, request.Size ?? "M") is { } c
                ? Results.Ok(new { c.Id, c.CompartmentNumber, c.AccessPin, c.ExpiresAt })
                : Results.BadRequest(new { message = "Tidak ada pintu loker yang tersedia." }))
            .RequireAuthorization("CourierArea").WithTags("SmartLocker");

        api.MapPost("/lockers/compartments/{compartmentId:long}/collect", async (long compartmentId,
                [FromBody] CollectRequest request, SmartLockerService svc) =>
            await svc.CollectAsync(compartmentId, request.Pin ?? "")
                ? Results.Ok(new { message = "Loker terbuka. Silakan ambil paket Anda." })
                : Results.BadRequest(new { message = "PIN salah atau loker kosong." }))
            .WithTags("SmartLocker");
    }

    // ─── Analytics ───
    private static void MapAnalyticsEndpoints(RouteGroupBuilder api)
    {
        api.MapGet("/dashboard/revenue", async (AnalyticsService svc, int days = 30) =>
            Results.Ok(await svc.GetRevenueSummaryAsync(days)))
            .RequireAuthorization().WithTags("Dashboard");

        api.MapGet("/dashboard/delivery-volume", async (AnalyticsService svc, int days = 30) =>
            Results.Ok(await svc.GetDeliveryVolumeAsync("DAILY", days))).WithTags("Dashboard");

        api.MapGet("/dashboard/status-breakdown", async (AnalyticsService svc, int days = 30) =>
            Results.Ok(await svc.GetStatusBreakdownAsync(days))).WithTags("Dashboard");

        api.MapGet("/dashboard/sla", async (AnalyticsService svc, int days = 30) =>
            Results.Ok(new { compliance = await svc.GetSlaComplianceAsync(days) })).WithTags("Dashboard");

        api.MapGet("/dashboard/snapshot", async (AnalyticsService svc) =>
            Results.Ok(await svc.GetOperationalSnapshotAsync()))
            .RequireAuthorization().WithTags("Dashboard");

        api.MapGet("/dashboard/couriers", async (AnalyticsService svc) =>
            Results.Ok(await svc.GetCourierPerformanceAsync()))
            .RequireAuthorization().WithTags("Dashboard");

        api.MapGet("/analytics/forecast", async (ForecastService svc, string? city, int days = 14) =>
            Results.Ok(await svc.ForecastDemandAsync(days, 90, city)))
            .RequireAuthorization().WithTags("Analytics");

        api.MapGet("/analytics/trend", async (ForecastService svc, int months = 12) =>
            Results.Ok(await svc.AnalyzeTrendAsync(months)))
            .RequireAuthorization().WithTags("Analytics");

        api.MapGet("/analytics/cost-insights", async (ForecastService svc) =>
            Results.Ok(await svc.GetCostInsightsAsync()))
            .RequireAuthorization().WithTags("Analytics");

        api.MapGet("/analytics/emissions", async (AnalyticsService svc, int days = 30) =>
        {
            var (total, avg, eco) = await svc.GetEmissionSummaryAsync(days);
            return Results.Ok(new { totalGramCo2 = total, avgGramPerOrder = avg, ecoOrders = eco });
        }).RequireAuthorization().WithTags("Analytics");
    }

    // ─── Marketplace / 3PL integrations ───
    private static void MapIntegrationEndpoints(RouteGroupBuilder api)
    {
        api.MapGet("/integrations", async (MarketplaceService svc) =>
            Results.Ok(await svc.GetIntegrationsAsync()))
            .RequireAuthorization("AdminOrManager").WithTags("Integrations");

        api.MapPost("/integrations/{id:long}/sync", async (long id, MarketplaceService svc) =>
            Results.Ok(await svc.SyncOrdersAsync(id)))
            .RequireAuthorization("AdminOrManager").WithTags("Integrations");

        api.MapGet("/integrations/logs", async (MarketplaceService svc, long? integrationId) =>
            Results.Ok(await svc.GetSyncLogsAsync(integrationId)))
            .RequireAuthorization("AdminOrManager").WithTags("Integrations");

        api.MapGet("/partners", async (PartnerLogisticsService svc) =>
            Results.Ok(await svc.GetPartnersAsync())).WithTags("Partners");

        api.MapGet("/partners/quotes", async (PartnerLogisticsService svc, string destination,
                double weight, bool? crossBorder) =>
            Results.Ok(await svc.GetQuotesAsync(destination, weight, crossBorder ?? false)))
            .WithTags("Partners");

        api.MapPost("/partners/handover", async ([FromBody] HandoverRequest request,
                PartnerLogisticsService svc) =>
            Results.Ok(await svc.HandoverAsync(request.OrderId, request.PartnerId,
                request.CrossBorder, request.DestinationCountry)))
            .RequireAuthorization("StaffArea").WithTags("Partners");
    }

    // ─── Compliance ───
    private static void MapComplianceEndpoints(RouteGroupBuilder api)
    {
        api.MapGet("/compliance/tax", async (ComplianceService svc, DateTime? period) =>
            Results.Ok(await svc.GetTaxRecordsAsync(period)))
            .RequireAuthorization("AdminOrManager").WithTags("Compliance");

        api.MapGet("/compliance/tax/summary", async (ComplianceService svc, int months = 12) =>
            Results.Ok(await svc.GetTaxSummaryAsync(months)))
            .RequireAuthorization("AdminOrManager").WithTags("Compliance");

        api.MapGet("/compliance/customs", async (ComplianceService svc, string? status) =>
            Results.Ok(await svc.GetDeclarationsAsync(status)))
            .RequireAuthorization("StaffArea").WithTags("Compliance");

        api.MapPost("/compliance/customs", async ([FromBody] DeclarationRequest request,
                ComplianceService svc) =>
            Results.Ok(await svc.CreateDeclarationAsync(request.OrderId, request.DeclarationType,
                request.DestinationCountry, request.DeclaredValue, request.Currency ?? "USD",
                request.HsCode, request.Incoterm ?? "DAP")))
            .RequireAuthorization("StaffArea").WithTags("Compliance");

        api.MapPost("/compliance/customs/{id:long}/status", async (long id,
                [FromBody] DeclarationStatusRequest request, ComplianceService svc) =>
        {
            var (ok, message) = await svc.AdvanceDeclarationAsync(id, request.Status);
            return ok ? Results.Ok(new { message }) : Results.BadRequest(new { message });
        }).RequireAuthorization("StaffArea").WithTags("Compliance");

        api.MapGet("/loyalty/balance", async (LoyaltyService svc, ClaimsPrincipal user) =>
        {
            var userId = GetUserId(user);
            if (userId is null) return Results.Unauthorized();
            var balance = await svc.GetBalanceAsync(userId.Value);
            var tier = LoyaltyService.GetTier(balance);
            return Results.Ok(new { balance, tier = tier.Name, multiplier = tier.Multiplier });
        }).RequireAuthorization().WithTags("Loyalty");

        api.MapPost("/loyalty/redeem", async ([FromBody] RedeemRequest request, LoyaltyService svc,
                ClaimsPrincipal user) =>
        {
            var userId = GetUserId(user);
            if (userId is null) return Results.Unauthorized();
            var (ok, message, discount) = await svc.RedeemAsync(userId.Value, request.Points);
            return ok ? Results.Ok(new { message, discount }) : Results.BadRequest(new { message });
        }).RequireAuthorization().WithTags("Loyalty");
    }

    // ─── Chat bot ───
    private static void MapChatEndpoints(RouteGroupBuilder api)
    {
        api.MapPost("/chat/sessions", async ([FromBody] CreateSessionRequest request,
                ChatBotService svc, ClaimsPrincipal user) =>
        {
            var userId = GetUserId(user);
            if (userId is null) return Results.Unauthorized();
            var session = await svc.CreateSessionAsync(userId.Value, request.Title ?? "New Chat", request.Model);
            return Results.Created($"/api/v1/chat/sessions/{session.Id}", session);
        }).RequireAuthorization().WithTags("Chat");

        api.MapGet("/chat/sessions", async (ChatBotService svc, ClaimsPrincipal user) =>
        {
            var userId = GetUserId(user);
            if (userId is null) return Results.Unauthorized();
            return Results.Ok(await svc.GetUserSessionsAsync(userId.Value));
        }).RequireAuthorization().WithTags("Chat");

        api.MapGet("/chat/sessions/{sessionId:long}/messages", async (long sessionId, ChatBotService svc) =>
            Results.Ok(await svc.GetSessionMessagesAsync(sessionId)))
            .RequireAuthorization().WithTags("Chat");

        api.MapPost("/chat/sessions/{sessionId:long}/messages",
            async (long sessionId, ChatMessageRequest req, ChatBotService svc) =>
                Results.Ok(await svc.SendMessageAsync(sessionId, req.Message, req.AttachmentsJson)))
            .RequireAuthorization().WithTags("Chat");

        api.MapDelete("/chat/sessions/{sessionId:long}", async (long sessionId, ChatBotService svc) =>
        {
            await svc.DeleteSessionAsync(sessionId);
            return Results.Ok(new { message = "Sesi dihapus." });
        }).RequireAuthorization().WithTags("Chat");

        api.MapGet("/chat/functions", (ChatBotService svc) =>
            Results.Ok(svc.GetAvailableFunctions())).WithTags("Chat");
    }

    // ─── Shipping labels ───
    private static void MapLabelEndpoints(RouteGroupBuilder api)
    {
        api.MapGet("/labels/{trackingNumber}/qr", async (string trackingNumber,
                OrderService orders, BarcodeService barcodes) =>
        {
            var order = await orders.GetByTrackingNumberAsync(trackingNumber);
            if (order == null) return Results.NotFound();
            return Results.File(barcodes.GenerateQrPng(trackingNumber, 8), "image/png");
        }).WithTags("Labels");

        api.MapGet("/labels/{trackingNumber}/barcode", async (string trackingNumber,
                OrderService orders, BarcodeService barcodes) =>
        {
            var order = await orders.GetByTrackingNumberAsync(trackingNumber);
            if (order == null) return Results.NotFound();
            return Results.Content(barcodes.GenerateBarcodeSvg(trackingNumber), "image/svg+xml");
        }).WithTags("Labels");
    }

    // ─── System configuration ───
    private static void MapConfigEndpoints(RouteGroupBuilder api)
    {
        api.MapGet("/config", async (NgibridDbContext db) =>
            Results.Ok(await db.SystemConfigurations.ToListAsync()))
            .RequireAuthorization("AdminOrManager").WithTags("Config");

        api.MapPut("/config/{key}", async (string key, [FromBody] ConfigValueRequest request,
                NgibridDbContext db) =>
        {
            var config = await db.SystemConfigurations.FirstOrDefaultAsync(c => c.Key == key);
            if (config == null) return Results.NotFound();
            if (!config.IsEditable) return Results.BadRequest(new { message = "Konfigurasi ini tidak dapat diubah." });

            config.Value = request.Value ?? "";
            await db.SaveChangesAsync();
            return Results.Ok(config);
        }).RequireAuthorization("AdminOnly").WithTags("Config");

        api.MapGet("/health", async (NgibridDbContext db, IConfiguration config) =>
        {
            var canConnect = await db.Database.CanConnectAsync();
            return Results.Ok(new
            {
                status = canConnect ? "healthy" : "degraded",
                database = config["Database:Provider"],
                storage = config["Storage:Provider"],
                chatModel = config["ChatBot:DefaultModel"],
                timestamp = DateTime.UtcNow
            });
        }).WithTags("System");
    }

    // ─── Helpers ───

    private static long? GetUserId(ClaimsPrincipal user)
    {
        var value = user.FindFirstValue(ClaimTypes.NameIdentifier);
        return long.TryParse(value, out var id) ? id : null;
    }

    private static bool IsStaff(ClaimsPrincipal user) =>
        user.IsInRole("Admin") || user.IsInRole("Manager") || user.IsInRole("WarehouseStaff");

    /// <summary>Public tracking view — excludes pricing and full addresses.</summary>
    private static object ToPublicTracking(Order o) => new
    {
        o.OrderNumber,
        o.TrackingNumber,
        o.Status,
        o.ServiceType,
        o.WeightKg,
        SenderCity = o.SenderCity,
        SenderProvince = o.SenderProvince,
        RecipientCity = o.RecipientCity,
        RecipientProvince = o.RecipientProvince,
        o.EstimatedDeliveryDate,
        o.ActualDeliveryDate,
        History = o.StatusHistory?
            .OrderByDescending(h => h.CreatedAt)
            .Select(h => new { h.Status, h.Notes, h.CreatedAt, h.Location })
    };

    // ─── DTOs ───

    public record CreateOrderRequest(
        string SenderName, string SenderPhone, string SenderAddress, string? SenderCity, string? SenderPostalCode,
        string RecipientName, string RecipientPhone, string RecipientAddress, string? RecipientCity,
        string? RecipientPostalCode, string PackageDescription, double WeightKg,
        double LengthCm = 20, double WidthCm = 15, double HeightCm = 10,
        string? ServiceType = "REG", bool HasInsurance = false, decimal? DeclaredValue = null,
        bool IsEcoDelivery = false, string? ExternalOrderId = null,
        // Optional so older clients keep working; when omitted the province is looked up from
        // the city name in the master table.
        string? SenderProvince = null, string? RecipientProvince = null);

    public record UpdateStatusRequest(string Status, string? Notes, double? Latitude, double? Longitude);
    public record CancelRequest(string? Reason);
    public record TrackingPointRequest(double Latitude, double Longitude, double? SpeedKmh, double? Heading, string? Description);
    public record CreatePaymentRequest(string Method, string Channel);
    public record ConfirmPaymentRequest(string? TransactionId);
    public record ClaimRequest(long OrderId, decimal Amount, string Reason, string? DocumentUrl);
    public record ReviewClaimRequest(bool Approve, decimal? ApprovedAmount, string? Reason);
    public record MovementRequest(string Type, int Quantity, string? Notes);
    public record LocationRequest(double Latitude, double Longitude);
    public record AssignLockerRequest(long OrderId, string? Size);
    public record CollectRequest(string? Pin);
    public record HandoverRequest(long OrderId, long PartnerId, bool CrossBorder, string? DestinationCountry);
    public record DeclarationRequest(long OrderId, string DeclarationType, string DestinationCountry,
        decimal DeclaredValue, string? Currency, string? HsCode, string? Incoterm);
    public record DeclarationStatusRequest(string Status);
    public record RedeemRequest(int Points);
    public record CreateSessionRequest(string? Title, string? Model);
    public record ConfigValueRequest(string? Value);
}

/// <summary>
/// Chat message request DTO
/// </summary>
public class ChatMessageRequest
{
    public string Message { get; set; } = string.Empty;
    public string? AttachmentsJson { get; set; }
}
