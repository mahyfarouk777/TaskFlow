
using System.ComponentModel.DataAnnotations;

namespace TaskFlow.Models;

public class TaskItem
{
    public int Id { get; set; }

    [Required]
    [StringLength(150)]
    public string Title { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; set; }

    [Required]
    public string Status { get; set; } = "To Do";

    [Required]
    public string Priority { get; set; } = "Medium";

    public DateTime? DueDate { get; set; }

    // each task --> proj
    public int ProjectId { get; set; }

    public Project? Project { get; set; }

    // assigned user
    public string? AssignedToUserId { get; set; }
}
