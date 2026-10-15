
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Data;
using TaskFlow.Models;

namespace TaskFlow.Controllers;

[Authorize]
public class ProjectsController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<IdentityUser> _userManager;

    public ProjectsController(
        ApplicationDbContext context,
        UserManager<IdentityUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    // GET: Projects
    public async Task<IActionResult> Index(string? searchString)
    {
        var user = await _userManager.GetUserAsync(User);

        if (user == null)
            return Challenge();

        IQueryable<Project> projects = _context.Projects
            .Include(p => p.Tasks);

        if (User.IsInRole("Admin"))
        {
            // Admins can view all projects.
        }
        else if (User.IsInRole("ProjectManager"))
        {
            projects = projects.Where(
                p => p.ProjectManagerId == user.Id);
        }
        else if (User.IsInRole("TeamMember"))
        {
            projects = projects.Where(
                p => p.Tasks.Any(t => t.AssignedToUserId == user.Id));
        }
        else
        {
            return Forbid();
        }

        if (!string.IsNullOrWhiteSpace(searchString))
        {
            projects = projects.Where(p =>
                p.Name.Contains(searchString) ||
                (p.Description != null &&
                 p.Description.Contains(searchString)));
        }

        ViewData["CurrentFilter"] = searchString;

        var result = await projects
            .OrderByDescending(p => p.StartDate)
            .ToListAsync();

        foreach (var project in result)
        {
            project.TaskCount = project.Tasks.Count;
        }

        return View(result);
    }

    // GET: Projects/Details/5
    public async Task<IActionResult> Details(int? id)
    {
        if (id == null)
            return NotFound();

        var project = await _context.Projects
            .Include(p => p.Tasks)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (project == null)
            return NotFound();

        if (!await CanViewProjectAsync(project))
            return Forbid();

        project.TaskCount = project.Tasks.Count;

        return View(project);
    }

    // GET: Projects/Create
    [Authorize(Roles = "Admin,ProjectManager")]
    public IActionResult Create()
    {
        return View(new Project());
    }

    // POST: Projects/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,ProjectManager")]
    public async Task<IActionResult> Create(Project project)
    {
        ModelState.Remove(nameof(Project.ProjectManagerId));
        ModelState.Remove(nameof(Project.Tasks));
        ModelState.Remove(nameof(Project.TaskCount));

        if (!ModelState.IsValid)
            return View(project);

        if (User.IsInRole("ProjectManager"))
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
                return Challenge();

            project.ProjectManagerId = user.Id;
        }
        else
        {
            project.ProjectManagerId = null;
        }

        project.TaskCount = 0;

        _context.Projects.Add(project);
        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }

    // GET: Projects/Edit/5
    [Authorize(Roles = "Admin,ProjectManager")]
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null)
            return NotFound();

        var project = await _context.Projects.FindAsync(id);

        if (project == null)
            return NotFound();

        if (!await CanManageProjectAsync(project))
            return Forbid();

        return View(project);
    }

    // POST: Projects/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,ProjectManager")]
    public async Task<IActionResult> Edit(
        int id,
        [Bind("Id,Name,Description,StartDate,Deadline")]
        Project input)
    {
        if (id != input.Id)
            return NotFound();

        var project = await _context.Projects.FindAsync(id);

        if (project == null)
            return NotFound();

        if (!await CanManageProjectAsync(project))
            return Forbid();

        ModelState.Remove(nameof(Project.ProjectManagerId));
        ModelState.Remove(nameof(Project.Tasks));
        ModelState.Remove(nameof(Project.TaskCount));

        if (!ModelState.IsValid)
            return View(input);

        project.Name = input.Name;
        project.Description = input.Description;
        project.StartDate = input.StartDate;
        project.Deadline = input.Deadline;

        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }

    // GET: Projects/Delete/5
    [Authorize(Roles = "Admin,ProjectManager")]
    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null)
            return NotFound();

        var project = await _context.Projects
            .Include(p => p.Tasks)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (project == null)
            return NotFound();

        if (!await CanManageProjectAsync(project))
            return Forbid();

        project.TaskCount = project.Tasks.Count;

        return View(project);
    }

    // POST: Projects/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,ProjectManager")]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var project = await _context.Projects.FindAsync(id);

        if (project == null)
            return NotFound();

        if (!await CanManageProjectAsync(project))
            return Forbid();

        _context.Projects.Remove(project);
        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }

    // Permission checks
    private async Task<bool> CanViewProjectAsync(Project project)
    {
        if (User.IsInRole("Admin"))
            return true;

        var user = await _userManager.GetUserAsync(User);

        if (user == null)
            return false;

        if (User.IsInRole("ProjectManager"))
            return project.ProjectManagerId == user.Id;

        if (User.IsInRole("TeamMember"))
        {
            return await _context.Tasks.AnyAsync(t =>
                t.ProjectId == project.Id &&
                t.AssignedToUserId == user.Id);
        }

        return false;
    }

    private async Task<bool> CanManageProjectAsync(Project project)
    {
        if (User.IsInRole("Admin"))
            return true;

        var user = await _userManager.GetUserAsync(User);

        return user != null &&
               User.IsInRole("ProjectManager") &&
               project.ProjectManagerId == user.Id;
    }
}
