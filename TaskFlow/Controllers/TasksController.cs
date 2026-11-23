
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Data;
using TaskFlow.Models;
using TaskFlow.ViewModels;

namespace TaskFlow.Controllers;

[Authorize]
public class TasksController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<IdentityUser> _userManager;

    public TasksController(
        ApplicationDbContext context,
        UserManager<IdentityUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    // GET: Tasks
    public async Task<IActionResult> Index(TaskListViewModel model)
    {
        var user = await _userManager.GetUserAsync(User);

        if (user == null)
            return Challenge();

        IQueryable<TaskItem> tasks = _context.Tasks
            .Include(t => t.Project)
            .Include(t => t.TaskStatus)
            .AsNoTracking();

        // Role-based filtering
        if (User.IsInRole("Admin"))
        {
            // Admins can see all tasks.
        }
        else if (User.IsInRole("ProjectManager"))
        {
            tasks = tasks.Where(t =>
                t.Project != null &&
                t.Project.ProjectManagerId == user.Id);
        }
        else if (User.IsInRole("TeamMember"))
        {
            tasks = tasks.Where(t =>
                t.AssignedToUserId == user.Id);
        }
        else
        {
            return Forbid();
        }

        // Search by title and description
        if (!string.IsNullOrWhiteSpace(model.SearchTerm))
        {
            tasks = tasks.Where(t =>
                t.Title.Contains(model.SearchTerm) ||
                (t.Description != null &&
                 t.Description.Contains(model.SearchTerm)));
        }

        // Filter by status
        if (model.TaskStatusId.HasValue)
        {
            tasks = tasks.Where(t =>
                t.TaskStatusId == model.TaskStatusId.Value);
        }

        // Filter by priority
        if (!string.IsNullOrWhiteSpace(model.Priority))
        {
            tasks = tasks.Where(t =>
                t.Priority == model.Priority);
        }

        // Sorting
        if (string.Equals(
            model.SortOrder,
            "dueDate",
            StringComparison.OrdinalIgnoreCase))
        {
            tasks = tasks
                .OrderBy(t => t.DueDate == null)
                .ThenBy(t => t.DueDate);
        }
        else
        {
            model.SortOrder = "title";
            tasks = tasks.OrderBy(t => t.Title);
        }

        // Pagination
        model.PageSize = 5;
        model.PageNumber = Math.Max(1, model.PageNumber);

        model.TotalItems = await tasks.CountAsync();

        model.PageNumber = Math.Min(
            model.PageNumber,
            Math.Max(1, model.TotalPages));

        model.Tasks = await tasks
            .Skip((model.PageNumber - 1) * model.PageSize)
            .Take(model.PageSize)
            .ToListAsync();

        // Load statuses for the filter dropdown
        model.TaskStatuses = await _context.TaskStatuses
            .AsNoTracking()
            .OrderBy(s => s.Name)
            .ToListAsync();

        return View(model);
    }

    // GET: Tasks/Details/5
    public async Task<IActionResult> Details(int? id)
    {
        if (id == null)
            return NotFound();

        var task = await _context.Tasks
            .Include(t => t.Project)
            .Include(t => t.TaskStatus)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (task == null)
            return NotFound();

        if (!await CanViewTaskAsync(task))
            return Forbid();

        return View(task);
    }

    // GET: Tasks/Create
    [Authorize(Roles = "Admin,ProjectManager")]
    public async Task<IActionResult> Create(int? projectId)
    {
        await LoadProjectsAsync(projectId);
        await LoadTaskStatusesAsync();
        await LoadUsersAsync();

        return View(new TaskItem
        {
            ProjectId = projectId ?? 0
        });
    }

    // POST: Tasks/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,ProjectManager")]
    public async Task<IActionResult> Create(TaskItem input)
    {
        ModelState.Remove(nameof(TaskItem.Project));
        ModelState.Remove(nameof(TaskItem.TaskStatus));

        if (!ModelState.IsValid)
        {
            await LoadProjectsAsync(input.ProjectId);
            await LoadTaskStatusesAsync(input.TaskStatusId);
            await LoadUsersAsync(input.AssignedToUserId);

            return View(input);
        }

        var project = await _context.Projects
            .FirstOrDefaultAsync(p => p.Id == input.ProjectId);

        if (project == null)
        {
            ModelState.AddModelError(
                nameof(input.ProjectId),
                "Select a valid project.");

            await LoadProjectsAsync(input.ProjectId);
            await LoadTaskStatusesAsync(input.TaskStatusId);
            await LoadUsersAsync(input.AssignedToUserId);

            return View(input);
        }

        if (!await CanManageProjectAsync(project))
            return Forbid();

        var statusExists = await _context.TaskStatuses
            .AnyAsync(s => s.Id == input.TaskStatusId);

        if (!statusExists)
        {
            ModelState.AddModelError(
                nameof(input.TaskStatusId),
                "Select a valid status.");

            await LoadProjectsAsync(input.ProjectId);
            await LoadTaskStatusesAsync(input.TaskStatusId);
            await LoadUsersAsync(input.AssignedToUserId);

            return View(input);
        }

        // Only TeamMembers can be assigned to tasks
        if (input.AssignedToUserId != null &&
            !await IsTeamMemberAsync(input.AssignedToUserId))
        {
            ModelState.AddModelError(
                nameof(input.AssignedToUserId),
                "Only team members can be assigned to tasks.");

            await LoadProjectsAsync(input.ProjectId);
            await LoadTaskStatusesAsync(input.TaskStatusId);
            await LoadUsersAsync(input.AssignedToUserId);

            return View(input);
        }

        var task = new TaskItem
        {
            Title = input.Title,
            Description = input.Description,
            TaskStatusId = input.TaskStatusId,
            Priority = input.Priority,
            DueDate = input.DueDate,
            ProjectId = project.Id,
            AssignedToUserId = input.AssignedToUserId
        };

        _context.Tasks.Add(task);
        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }

    // GET: Tasks/Edit/5
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null)
            return NotFound();

        var task = await _context.Tasks
            .Include(t => t.Project)
            .Include(t => t.TaskStatus)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (task == null)
            return NotFound();

        if (User.IsInRole("TeamMember") &&
            !User.IsInRole("Admin") &&
            !User.IsInRole("ProjectManager"))
        {
            if (task.AssignedToUserId != _userManager.GetUserId(User))
                return Forbid();

            await LoadTaskStatusesAsync(task.TaskStatusId);
            return View("UpdateStatus", task);
        }

        if (!await CanManageTaskAsync(task))
            return Forbid();

        await LoadProjectsAsync(task.ProjectId);
        await LoadTaskStatusesAsync(task.TaskStatusId);
        await LoadUsersAsync(task.AssignedToUserId);

        return View(task);
    }

    // POST: Tasks/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, TaskItem input)
    {
        if (id != input.Id)
            return NotFound();

        var task = await _context.Tasks
            .Include(t => t.Project)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (task == null)
            return NotFound();

        if (User.IsInRole("TeamMember") &&
            !User.IsInRole("Admin") &&
            !User.IsInRole("ProjectManager"))
            return Forbid();

        if (!await CanManageTaskAsync(task))
            return Forbid();

        ModelState.Remove(nameof(TaskItem.Project));
        ModelState.Remove(nameof(TaskItem.TaskStatus));

        if (!ModelState.IsValid)
        {
            await LoadProjectsAsync(input.ProjectId);
            await LoadTaskStatusesAsync(input.TaskStatusId);
            await LoadUsersAsync(input.AssignedToUserId);

            return View(input);
        }

        var project = await _context.Projects
            .FirstOrDefaultAsync(p => p.Id == input.ProjectId);

        if (project == null)
        {
            ModelState.AddModelError(
                nameof(input.ProjectId),
                "Select a valid project.");

            await LoadProjectsAsync(input.ProjectId);
            await LoadTaskStatusesAsync(input.TaskStatusId);
            await LoadUsersAsync(input.AssignedToUserId);

            return View(input);
        }

        if (!await CanManageProjectAsync(project))
            return Forbid();

        var statusExists = await _context.TaskStatuses
            .AnyAsync(s => s.Id == input.TaskStatusId);

        if (!statusExists)
        {
            ModelState.AddModelError(
                nameof(input.TaskStatusId),
                "Select a valid status.");

            await LoadProjectsAsync(input.ProjectId);
            await LoadTaskStatusesAsync(input.TaskStatusId);
            await LoadUsersAsync(input.AssignedToUserId);

            return View(input);
        }

        // Only TeamMembers can be assigned to tasks
        if (input.AssignedToUserId != null &&
            !await IsTeamMemberAsync(input.AssignedToUserId))
        {
            ModelState.AddModelError(
                nameof(input.AssignedToUserId),
                "Only team members can be assigned to tasks.");

            await LoadProjectsAsync(input.ProjectId);
            await LoadTaskStatusesAsync(input.TaskStatusId);
            await LoadUsersAsync(input.AssignedToUserId);

            return View(input);
        }

        task.Title = input.Title;
        task.Description = input.Description;
        task.TaskStatusId = input.TaskStatusId;
        task.Priority = input.Priority;
        task.DueDate = input.DueDate;
        task.ProjectId = project.Id;
        task.AssignedToUserId = input.AssignedToUserId;

        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }

    // GET: Tasks/UpdateStatus/5
    [Authorize(Roles = "TeamMember")]
    public async Task<IActionResult> UpdateStatus(int? id)
    {
        if (id == null)
            return NotFound();

        var task = await _context.Tasks
            .Include(t => t.Project)
            .Include(t => t.TaskStatus)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (task == null)
            return NotFound();

        if (task.AssignedToUserId != _userManager.GetUserId(User))
            return Forbid();

        await LoadTaskStatusesAsync(task.TaskStatusId);

        return View(task);
    }

    // POST: Tasks/UpdateStatus/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "TeamMember")]
    public async Task<IActionResult> UpdateStatus(
        int id,
        int TaskStatusId)
    {
        var task = await _context.Tasks
            .Include(t => t.Project)
            .Include(t => t.TaskStatus)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (task == null)
            return NotFound();

        if (task.AssignedToUserId != _userManager.GetUserId(User))
            return Forbid();

        var statusExists = await _context.TaskStatuses
            .AnyAsync(s => s.Id == TaskStatusId);

        if (!statusExists)
        {
            ModelState.AddModelError(
                nameof(TaskStatusId),
                "Select a valid status.");

            await LoadTaskStatusesAsync(task.TaskStatusId);
            return View(task);
        }

        task.TaskStatusId = TaskStatusId;

        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }

    // GET: Tasks/Delete/5
    [Authorize(Roles = "Admin,ProjectManager")]
    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null)
            return NotFound();

        var task = await _context.Tasks
            .Include(t => t.Project)
            .Include(t => t.TaskStatus)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (task == null)
            return NotFound();

        if (!await CanManageTaskAsync(task))
            return Forbid();

        return View(task);
    }

    // POST: Tasks/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,ProjectManager")]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var task = await _context.Tasks
            .Include(t => t.Project)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (task == null)
            return NotFound();

        if (!await CanManageTaskAsync(task))
            return Forbid();

        _context.Tasks.Remove(task);
        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }

    // Permission helpers

    private async Task<bool> CanViewTaskAsync(TaskItem task)
    {
        if (User.IsInRole("Admin"))
            return true;

        var userId = _userManager.GetUserId(User);

        if (userId == null)
            return false;

        if (User.IsInRole("ProjectManager"))
        {
            return await _context.Projects.AnyAsync(p =>
                p.Id == task.ProjectId &&
                p.ProjectManagerId == userId);
        }

        if (User.IsInRole("TeamMember"))
            return task.AssignedToUserId == userId;

        return false;
    }

    private async Task<bool> CanManageTaskAsync(TaskItem task)
    {
        if (User.IsInRole("Admin"))
            return true;

        if (!User.IsInRole("ProjectManager"))
            return false;

        var userId = _userManager.GetUserId(User);

        return userId != null &&
            await _context.Projects.AnyAsync(p =>
                p.Id == task.ProjectId &&
                p.ProjectManagerId == userId);
    }

    private async Task<bool> CanManageProjectAsync(Project project)
    {
        if (User.IsInRole("Admin"))
            return true;

        if (!User.IsInRole("ProjectManager"))
            return false;

        return project.ProjectManagerId ==
            _userManager.GetUserId(User);
    }

    // Check whether a user exists and has the TeamMember role
    private async Task<bool> IsTeamMemberAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);

        return user != null &&
            await _userManager.IsInRoleAsync(user, "TeamMember");
    }

    // Dropdown helpers

    private async Task LoadProjectsAsync(int? selectedProject = null)
    {
        IQueryable<Project> projects = _context.Projects;

        if (User.IsInRole("ProjectManager"))
        {
            var userId = _userManager.GetUserId(User);

            projects = projects.Where(
                p => p.ProjectManagerId == userId);
        }

        ViewData["ProjectId"] = new SelectList(
            await projects.OrderBy(p => p.Name).ToListAsync(),
            "Id",
            "Name",
            selectedProject);
    }

    private async Task LoadTaskStatusesAsync(int? selectedStatus = null)
    {
        ViewData["TaskStatusId"] = new SelectList(
            await _context.TaskStatuses
                .OrderBy(s => s.Name)
                .ToListAsync(),
            "Id",
            "Name",
            selectedStatus);
    }

    // Load only users who have the TeamMember role
    private async Task LoadUsersAsync(string? selectedUser = null)
    {
        var teamMembers = await _userManager.GetUsersInRoleAsync("TeamMember");

        var users = teamMembers
            .OrderBy(u => u.UserName)
            .ToList();

        ViewData["AssignedToUserId"] = new SelectList(
            users,
            "Id",
            "UserName",
            selectedUser);
    }
}
