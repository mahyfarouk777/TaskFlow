
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Data;
using TaskFlow.Models;

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
    public async Task<IActionResult> Index(
        string? searchString,
        string? status,
        string? priority)
    {
        var user = await _userManager.GetUserAsync(User);

        if (user == null)
            return Challenge();

        IQueryable<TaskItem> tasks = _context.Tasks
            .Include(t => t.Project);

        if (User.IsInRole("Admin"))
        {
            // Admin can see all tasks.
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

        if (!string.IsNullOrWhiteSpace(searchString))
        {
            tasks = tasks.Where(t =>
                t.Title.Contains(searchString) ||
                (t.Description != null &&
                 t.Description.Contains(searchString)));
        }

        if (!string.IsNullOrWhiteSpace(status))
            tasks = tasks.Where(t => t.Status == status);

        if (!string.IsNullOrWhiteSpace(priority))
            tasks = tasks.Where(t => t.Priority == priority);

        ViewData["CurrentFilter"] = searchString;
        ViewData["CurrentStatus"] = status;
        ViewData["CurrentPriority"] = priority;

        return View(await tasks
            .OrderBy(t => t.DueDate)
            .ToListAsync());
    }

    // GET: Tasks/Details/5
    public async Task<IActionResult> Details(int? id)
    {
        if (id == null)
            return NotFound();

        var task = await _context.Tasks
            .Include(t => t.Project)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (task == null)
            return NotFound();

        if (!await CanViewTaskAsync(task))
            return Forbid();

        return View(task);
    }

    // GET: Tasks/Create
    [Authorize(Roles = "Admin,ProjectManager")]
    public async Task<IActionResult> Create()
    {
        await LoadProjectsAsync();
        return View(new TaskItem());
    }

    // POST: Tasks/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,ProjectManager")]
    public async Task<IActionResult> Create(TaskItem input)
    {
        ModelState.Remove(nameof(TaskItem.Project));
        ModelState.Remove(nameof(TaskItem.AssignedToUserId));

        if (!ModelState.IsValid)
        {
            await LoadProjectsAsync(input.ProjectId);
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
            return View(input);
        }

        if (!await CanManageProjectAsync(project))
            return Forbid();

        var task = new TaskItem
        {
            Title = input.Title,
            Description = input.Description,
            Status = input.Status,
            Priority = input.Priority,
            DueDate = input.DueDate,
            ProjectId = project.Id,
            AssignedToUserId = null
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
            .FirstOrDefaultAsync(t => t.Id == id);

        if (task == null)
            return NotFound();

        if (User.IsInRole("TeamMember"))
        {
            if (task.AssignedToUserId != _userManager.GetUserId(User))
                return Forbid();

            // Team members can update status only.
            return View("UpdateStatus", task);
        }

        if (!await CanManageTaskAsync(task))
            return Forbid();

        await LoadProjectsAsync(task.ProjectId);
        await LoadUsersAsync(task.AssignedToUserId);

        return View(task);
    }

    // POST: Tasks/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        int id,
        TaskItem input)
    {
        if (id != input.Id)
            return NotFound();

        var task = await _context.Tasks
            .Include(t => t.Project)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (task == null)
            return NotFound();

        if (User.IsInRole("TeamMember"))
            return Forbid();

        if (!await CanManageTaskAsync(task))
            return Forbid();

        ModelState.Remove(nameof(TaskItem.Project));
        ModelState.Remove(nameof(TaskItem.AssignedToUserId));

        if (!ModelState.IsValid)
        {
            await LoadProjectsAsync(input.ProjectId);
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
            await LoadUsersAsync(input.AssignedToUserId);
            return View(input);
        }

        // Managers cannot move tasks into projects they don't manage.
        if (!await CanManageProjectAsync(project))
            return Forbid();

        if (input.AssignedToUserId != null &&
            !await _context.Users.AnyAsync(
                u => u.Id == input.AssignedToUserId))
        {
            ModelState.AddModelError(
                nameof(input.AssignedToUserId),
                "Select a valid user.");

            await LoadProjectsAsync(input.ProjectId);
            await LoadUsersAsync(input.AssignedToUserId);
            return View(input);
        }

        task.Title = input.Title;
        task.Description = input.Description;
        task.Status = input.Status;
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
            .FirstOrDefaultAsync(t => t.Id == id);

        if (task == null)
            return NotFound();

        if (task.AssignedToUserId != _userManager.GetUserId(User))
            return Forbid();

        return View(task);
    }

    // POST: Tasks/UpdateStatus/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "TeamMember")]
    public async Task<IActionResult> UpdateStatus(
        int id,
        string status)
    {
        var task = await _context.Tasks
            .FirstOrDefaultAsync(t => t.Id == id);

        if (task == null)
            return NotFound();

        if (task.AssignedToUserId != _userManager.GetUserId(User))
            return Forbid();

        var allowedStatuses = new[]
        {
            "To Do",
            "In Progress",
            "Done"
        };

        if (!allowedStatuses.Contains(status))
        {
            ModelState.AddModelError(
                nameof(status),
                "Select a valid status.");

            return View(task);
        }

        task.Status = status;

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

    private async Task LoadUsersAsync(string? selectedUser = null)
    {
        var users = await _context.Users
            .OrderBy(u => u.UserName)
            .ToListAsync();

        ViewData["AssignedToUserId"] = new SelectList(
            users,
            "Id",
            "UserName",
            selectedUser);
    }
}
