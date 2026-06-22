using Microsoft.EntityFrameworkCore;
using JuraganKost.Data.Context;
using JuraganKost.Data.Models;
using JuraganKost.Services;
using JuraganKost.Services.Storage;
using JuraganKost.Services.Chat;

namespace JuraganKost.Api;

public static class ApiEndpoints
{
    public static void Map(WebApplication app)
    {
        var api = app.MapGroup("/api/v1");

        api.MapGet("/kost", async (AppDbContext db) => await db.Kost.Include(k=>k.Kamar).OrderByDescending(k=>k.CreatedAt).ToListAsync()).WithTags("Kost");
        api.MapGet("/kost/{id}", async (AppDbContext db, int id) => await db.Kost.Include(k=>k.Kamar).FirstOrDefaultAsync(k=>k.Id==id) is Kost k ? Results.Ok(k) : Results.NotFound()).WithTags("Kost");
        api.MapPost("/kost", async (AppDbContext db, Kost k) => { db.Kost.Add(k); await db.SaveChangesAsync(); return Results.Created($"/api/v1/kost/{k.Id}",k); }).WithTags("Kost");
        api.MapPut("/kost/{id}", async (AppDbContext db, int id, Kost k) => { var e=await db.Kost.FindAsync(id); if(e==null)return Results.NotFound(); e.Nama=k.Nama;e.Alamat=k.Alamat;e.Kota=k.Kota;e.Provinsi=k.Provinsi;e.KodePos=k.KodePos;e.Telepon=k.Telepon;e.Email=k.Email;e.Jenis=k.Jenis;e.Status=k.Status;e.Deskripsi=k.Deskripsi;e.UpdatedAt=DateTime.UtcNow;await db.SaveChangesAsync();return Results.Ok(e); }).WithTags("Kost");
        api.MapDelete("/kost/{id}", async (AppDbContext db, int id) => { var k=await db.Kost.FindAsync(id); if(k==null)return Results.NotFound(); db.Kost.Remove(k); await db.SaveChangesAsync(); return Results.NoContent(); }).WithTags("Kost");

        api.MapGet("/kamar", async (AppDbContext db, int? kostId) => await db.Kamar.Where(k=>!kostId.HasValue||k.KostId==kostId.Value).ToListAsync()).WithTags("Kamar");
        api.MapGet("/kamar/{id}", async (AppDbContext db, int id) => await db.Kamar.FindAsync(id) is Kamar k ? Results.Ok(k) : Results.NotFound()).WithTags("Kamar");
        api.MapPost("/kamar", async (AppDbContext db, Kamar k) => { db.Kamar.Add(k); await db.SaveChangesAsync(); return Results.Created($"/api/v1/kamar/{k.Id}",k); }).WithTags("Kamar");
        api.MapPut("/kamar/{id}", async (AppDbContext db, int id, Kamar k) => { var e=await db.Kamar.FindAsync(id); if(e==null)return Results.NotFound(); e.NomorKamar=k.NomorKamar;e.HargaSewa=k.HargaSewa;e.Status=k.Status;e.Jenis=k.Jenis;e.Deskripsi=k.Deskripsi;e.UpdatedAt=DateTime.UtcNow;await db.SaveChangesAsync();return Results.Ok(e); }).WithTags("Kamar");
        api.MapDelete("/kamar/{id}", async (AppDbContext db, int id) => { var k=await db.Kamar.FindAsync(id); if(k==null)return Results.NotFound(); db.Kamar.Remove(k);await db.SaveChangesAsync();return Results.NoContent(); }).WithTags("Kamar");

        api.MapGet("/penghuni", async (AppDbContext db) => await db.Penghuni.Include(p=>p.Kamar).ToListAsync()).WithTags("Penghuni");
        api.MapGet("/penghuni/{id}", async (AppDbContext db, int id) => await db.Penghuni.Include(p=>p.Kamar).Include(p=>p.Kontrak).FirstOrDefaultAsync(p=>p.Id==id) is Penghuni p ? Results.Ok(p) : Results.NotFound()).WithTags("Penghuni");
        api.MapPost("/penghuni", async (AppDbContext db, Penghuni p) => { p.CreatedAt=DateTime.UtcNow; db.Penghuni.Add(p); await db.SaveChangesAsync(); return Results.Created($"/api/v1/penghuni/{p.Id}",p); }).WithTags("Penghuni");

        api.MapGet("/tagihan", async (AppDbContext db) => await db.Tagihan.Include(t=>t.Penghuni).ToListAsync()).WithTags("Tagihan");
        api.MapGet("/tagihan/{id}", async (AppDbContext db, int id) => await db.Tagihan.FindAsync(id) is Tagihan t ? Results.Ok(t) : Results.NotFound()).WithTags("Tagihan");

        api.MapGet("/pembayaran", async (AppDbContext db) => await db.Pembayaran.Include(p=>p.Penghuni).ToListAsync()).WithTags("Pembayaran");
        api.MapPost("/pembayaran", async (AppDbContext db, PembayaranService svc, Pembayaran p) => { var r=await svc.CreateAsync(p); return Results.Created($"/api/v1/pembayaran/{r.Id}",r); }).WithTags("Pembayaran");
        api.MapPost("/pembayaran/{id}/verifikasi", async (AppDbContext db, int id, bool diterima) => { var p=await db.Pembayaran.FindAsync(id); if(p==null)return Results.NotFound(); p.Status=diterima?StatusPembayaran.Diverifikasi:StatusPembayaran.Ditolak; if(diterima&&p.TagihanId.HasValue){ var t=await db.Tagihan.FindAsync(p.TagihanId.Value); if(t!=null){t.Status=StatusTagihan.Dibayar;t.TanggalBayar=DateTime.UtcNow;}} await db.SaveChangesAsync(); return Results.Ok(p); }).WithTags("Pembayaran");

        api.MapGet("/komplain", async (AppDbContext db) => await db.Komplain.Include(k=>k.Penghuni).ToListAsync()).WithTags("Komplain");
        api.MapPost("/komplain", async (AppDbContext db, Komplain k) => { k.NomorKomplain=$"CMP-{DateTime.Now:yyyyMMdd}-{new Random().Next(1000,9999)}"; k.CreatedAt=DateTime.UtcNow; db.Komplain.Add(k); await db.SaveChangesAsync(); return Results.Created($"/api/v1/komplain/{k.Id}",k); }).WithTags("Komplain");

        api.MapGet("/iot/latest", async (IoTService svc, int? kamarId) => Results.Ok(await svc.GetLatestAsync(kamarId))).WithTags("IoT");
        api.MapGet("/iot/history/{deviceId}", async (IoTService svc, string deviceId, int? hours) => Results.Ok(await svc.GetHistoryAsync(deviceId,hours??24))).WithTags("IoT");
        api.MapPost("/iot/record", async (IoTService svc, IoTSensorData data) => Results.Ok(await svc.RecordAsync(data))).WithTags("IoT");
        api.MapPost("/iot/simulate/{kostId}", async (IoTService svc, int kostId) => Results.Ok(await svc.SimulateAsync(kostId))).WithTags("IoT");

        api.MapGet("/dashboard/{kostId}", async (KostService svc, int kostId) => Results.Ok(await svc.GetDashboardAsync(kostId))).WithTags("Dashboard");
        api.MapGet("/marketplace", async (AppDbContext db) => await db.MarketplaceListing.Include(m=>m.Kost).ThenInclude(k=>k.Kamar).Where(m=>m.IsPublic).ToListAsync()).WithTags("Marketplace");

        api.MapPost("/storage/upload", async (IStorageProvider storage, HttpRequest request) => { var f=request.Form.Files.FirstOrDefault(); if(f==null)return Results.BadRequest("No file"); await using var s=f.OpenReadStream(); var url=await storage.UploadAsync(f.FileName,s,f.ContentType); return Results.Ok(new{url,fileName=f.FileName,contentType=f.ContentType}); }).WithTags("Storage").DisableAntiforgery();
        api.MapGet("/storage/list", async (IStorageProvider storage, string? prefix) => Results.Ok(await storage.ListAsync(prefix))).WithTags("Storage");
        api.MapDelete("/storage/{*fileKey}", async (IStorageProvider storage, string fileKey) => { var d=await storage.DeleteAsync(fileKey); return d?Results.NoContent():Results.NotFound(); }).WithTags("Storage");

        api.MapPost("/chat/send", async (ChatService chatSvc, ChatRequest req) => { var r=await chatSvc.SendMessageAsync(req.SessionId??Guid.NewGuid().ToString("N")[..8],req.Message??"",req.ImageUrl,req.DocumentUrl); return Results.Ok(new{r, sessionId=req.SessionId}); }).WithTags("Chat");
        api.MapPost("/chat/reset", (ChatService chatSvc, string sessionId) => { chatSvc.ResetSession(sessionId); return Results.Ok(new{message="Session reset"}); }).WithTags("Chat");

        api.MapGet("/export/kamar/csv", async (ExportService svc) => Results.File(await svc.ExportKamarToCsvAsync(),"text/csv","kamar.csv")).WithTags("Export");
        api.MapGet("/export/kamar/excel", async (ExportService svc) => Results.File(await svc.ExportKamarToExcelAsync(),"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet","kamar.xlsx")).WithTags("Export");
        api.MapGet("/export/penghuni/csv", async (ExportService svc) => Results.File(await svc.ExportPenghuniToCsvAsync(),"text/csv","penghuni.csv")).WithTags("Export");
        api.MapGet("/export/penghuni/excel", async (ExportService svc) => Results.File(await svc.ExportPenghuniToExcelAsync(),"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet","penghuni.xlsx")).WithTags("Export");
        api.MapGet("/export/tagihan/csv", async (ExportService svc) => Results.File(await svc.ExportTagihanToCsvAsync(),"text/csv","tagihan.csv")).WithTags("Export");
        api.MapGet("/export/tagihan/excel", async (ExportService svc) => Results.File(await svc.ExportTagihanToExcelAsync(),"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet","tagihan.xlsx")).WithTags("Export");
        api.MapGet("/export/kost/csv", async (ExportService svc) => Results.File(await svc.ExportKostToCsvAsync(),"text/csv","kost.csv")).WithTags("Export");
        api.MapGet("/export/kost/excel", async (ExportService svc) => Results.File(await svc.ExportKostToExcelAsync(),"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet","kost.xlsx")).WithTags("Export");
    }
}

public class ChatRequest { public string? SessionId{get;set;} public string? Message{get;set;} public string? ImageUrl{get;set;} public string? DocumentUrl{get;set;} }
