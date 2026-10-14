
using System.ComponentModel.DataAnnotations;

namespace TaskFlow.Models;

public class Project
{
    public int Id { get; set; }

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; set; }

    public DateTime StartDate { get; set; } = DateTime.Today;

    public DateTime? Deadline { get; set; }

    // the user 
    public string? ProjectManagerId { get; set; }

    public int TaskCount { get; set; }

    public ICollection<TaskItem> Tasks { get; set; }
        = new List<TaskItem>();
}
