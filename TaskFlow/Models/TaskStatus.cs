
using System.ComponentModel.DataAnnotations;

namespace TaskFlow.Models;

public class TaskStatus
{
    public int Id { get; set; }

    [Required]
    [StringLength(50)]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    // One status can belong to many tasks
    public ICollection<TaskItem> Tasks { get; set; }
        = new List<TaskItem>();
}
