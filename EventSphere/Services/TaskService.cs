using Microsoft.EntityFrameworkCore;
using EventSphere.Data.Context;
using EventSphere.Data.Models;

namespace EventSphere.Services;

public class TaskService
{
    private readonly AppDbContext _db;
    public TaskService(AppDbContext db) => _db = db;

    public async Task<List<TaskItem>> GetTasksForEventAsync(Guid eventId) =>
        await _db.TaskItems.Where(t => t.EventId == eventId).Include(t => t.AssignedTo).OrderBy(t => t.SortOrder).ToListAsync();

    public async Task<List<TaskItem>> GetTasksForUserAsync(string userId) =>
        await _db.TaskItems.Where(t => t.AssignedToId == userId && t.Status != TaskItemStatus.Done)
            .Include(t => t.Event).OrderBy(t => t.DueDate).ToListAsync();

    public async Task<TaskItem> CreateAsync(TaskItem task) { task.Id = Guid.NewGuid(); task.CreatedAt = DateTime.UtcNow; _db.TaskItems.Add(task); await _db.SaveChangesAsync(); return task; }

    public async Task<bool> UpdateAsync(TaskItem task)
    {
        var e = await _db.TaskItems.FindAsync(task.Id);
        if (e == null) return false;
        e.Title = task.Title;
        e.Description = task.Description;
        e.Category = task.Category;
        e.Priority = task.Priority;
        e.Status = task.Status;
        e.DueDate = task.DueDate;
        e.AssignedToId = task.AssignedToId;
        e.Progress = task.Progress;
        if (task.Status == TaskItemStatus.Done && e.CompletedAt == null)
            e.CompletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteAsync(Guid id) { var t = await _db.TaskItems.FindAsync(id); if (t == null) return false; _db.TaskItems.Remove(t); await _db.SaveChangesAsync(); return true; }

    public async Task<bool> MarkCompleteAsync(Guid taskId, string userId)
    {
        var t = await _db.TaskItems.FindAsync(taskId);
        if (t == null) return false;
        t.Status = TaskItemStatus.Done;
        t.CompletedAt = DateTime.UtcNow;
        t.CompletedById = userId;
        t.Progress = 100;
        await _db.SaveChangesAsync();
        return true;
    }
}
