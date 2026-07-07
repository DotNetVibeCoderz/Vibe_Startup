using Microsoft.EntityFrameworkCore;
using FitnessCenter.Data;
using FitnessCenter.Models;

namespace FitnessCenter.Services;

public class FeedbackService
{
    private readonly AppDbContext _db;
    private readonly TrainerService _trainerService;
    public FeedbackService(AppDbContext db, TrainerService trainerService) { _db = db; _trainerService = trainerService; }

    public async Task<List<Feedback>> GetAllAsync() =>
        await _db.Feedbacks.Include(f => f.User).OrderByDescending(f => f.CreatedAt).ToListAsync();

    public async Task<Feedback> CreateAsync(Feedback feedback)
    {
        _db.Feedbacks.Add(feedback);
        if (feedback.Type == FeedbackType.Trainer && feedback.ReferenceId.HasValue)
            await _trainerService.UpdateRatingAsync(feedback.ReferenceId.Value);
        await _db.SaveChangesAsync();
        return feedback;
    }

    public async Task<double> GetAverageRatingAsync(FeedbackType type, int? referenceId = null)
    {
        var query = _db.Feedbacks.Where(f => f.Type == type);
        if (referenceId.HasValue) query = query.Where(f => f.ReferenceId == referenceId);
        var ratings = await query.Select(f => f.Rating).ToListAsync();
        return ratings.Any() ? Math.Round(ratings.Average(), 1) : 0;
    }

    public async Task<List<Feedback>> GetByTypeAsync(FeedbackType type) =>
        await _db.Feedbacks.Include(f => f.User).Where(f => f.Type == type).OrderByDescending(f => f.CreatedAt).ToListAsync();
}
