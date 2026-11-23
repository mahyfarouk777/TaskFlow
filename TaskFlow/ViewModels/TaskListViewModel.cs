
using TaskFlow.Models;
using TaskStatusModel = TaskFlow.Models.TaskStatus;

namespace TaskFlow.ViewModels;

public class TaskListViewModel
{
    // Tasks displayed on the current page
    public List<TaskItem> Tasks { get; set; } = new();

    // Available statuses for the filter dropdown
    public List<TaskStatusModel> TaskStatuses { get; set; } = new();


    // Search and filtering
    public string? SearchTerm { get; set; }

    public int? TaskStatusId { get; set; }

    public string? Priority { get; set; }

    // Sorting
    public string SortOrder { get; set; } = "title";

    // Pagination
    public int PageNumber { get; set; } = 1;

    public int PageSize { get; set; } = 5;

    public int TotalItems { get; set; }

    public int TotalPages =>
        (int)Math.Ceiling(TotalItems / (double)PageSize);
}
