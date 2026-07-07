using Microsoft.EntityFrameworkCore;
using FitnessCenter.Data;
using FitnessCenter.Models;

namespace FitnessCenter.Services;

public class TrainerService
{
    private readonly AppDbContext _db;
    public TrainerService(AppDbContext db) => _db = db;

    public async Task<List<Trainer>> GetAllAsync() =>
        await _db.Trainers.Where(t => t.IsActive).OrderBy(t => t.FullName).ToListAsync();

    public async Task<Trainer?> GetByIdAsync(int id) =>
        await _db.Trainers.Include(t => t.Classes).FirstOrDefaultAsync(t => t.Id == id);

    public async Task<Trainer> CreateAsync(Trainer t) { _db.Trainers.Add(t); await _db.SaveChangesAsync(); return t; }
    public async Task UpdateAsync(Trainer t) { _db.Trainers.Update(t); await _db.SaveChangesAsync(); }
    public async Task DeleteAsync(int id) { var t = await _db.Trainers.FindAsync(id); if (t != null) { t.IsActive = false; await _db.SaveChangesAsync(); } }

    public async Task UpdateRatingAsync(int trainerId)
    {
        var ratings = await _db.Feedbacks.Where(f => f.Type == FeedbackType.Trainer && f.ReferenceId == trainerId).Select(f => (double)f.Rating).ToListAsync();
        var trainer = await _db.Trainers.FindAsync(trainerId);
        if (trainer != null) { trainer.Rating = ratings.Any() ? Math.Round(ratings.Average(), 1) : 0; await _db.SaveChangesAsync(); }
    }
}
