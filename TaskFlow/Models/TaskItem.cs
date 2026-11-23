
using System.ComponentModel.DataAnnotations;
using TaskStatusModel = TaskFlow.Models.TaskStatus;

namespace TaskFlow.Models;

public class TaskItem
{
    public int Id { get; set; }

    [Required]
    [StringLength(150)]
    public string Title { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; set; }

    // Relationship with TaskStatus
    [Required]
    public int TaskStatusId { get; set; }

    public TaskStatusModel? TaskStatus { get; set; }

    [Required]
    [StringLength(50)]
    public string Priority { get; set; } = "Medium";

    public DateTime? DueDate { get; set; }

    // Relationship with Project
    [Required]
    public int ProjectId { get; set; }

    public Project? Project { get; set; }

    // Assigned user
    public string? AssignedToUserId { get; set; }
}
