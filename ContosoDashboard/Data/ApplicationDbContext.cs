using Microsoft.EntityFrameworkCore;
using ContosoDashboard.Models;

namespace ContosoDashboard.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users { get; set; } = null!;
    public DbSet<TaskItem> Tasks { get; set; } = null!;
    public DbSet<Project> Projects { get; set; } = null!;
    public DbSet<TaskComment> TaskComments { get; set; } = null!;
    public DbSet<Notification> Notifications { get; set; } = null!;
    public DbSet<ProjectMember> ProjectMembers { get; set; } = null!;
    public DbSet<Announcement> Announcements { get; set; } = null!;
    public DbSet<Document> Documents { get; set; } = null!;
    public DbSet<DocumentShare> DocumentShares { get; set; } = null!;
    public DbSet<DocumentTaskAssociation> DocumentTaskAssociations { get; set; } = null!;
    public DbSet<DocumentActivity> DocumentActivities { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure User relationships
        modelBuilder.Entity<User>()
            .HasMany(u => u.AssignedTasks)
            .WithOne(t => t.AssignedUser)
            .HasForeignKey(t => t.AssignedUserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<User>()
            .HasMany(u => u.CreatedTasks)
            .WithOne(t => t.CreatedByUser)
            .HasForeignKey(t => t.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<User>()
            .HasMany(u => u.ManagedProjects)
            .WithOne(p => p.ProjectManager)
            .HasForeignKey(p => p.ProjectManagerId)
            .OnDelete(DeleteBehavior.Restrict);

        // Configure indexes for performance
        modelBuilder.Entity<TaskItem>()
            .HasIndex(t => t.AssignedUserId);

        modelBuilder.Entity<TaskItem>()
            .HasIndex(t => t.Status);

        modelBuilder.Entity<TaskItem>()
            .HasIndex(t => t.DueDate);

        modelBuilder.Entity<Project>()
            .HasIndex(p => p.ProjectManagerId);

        modelBuilder.Entity<Project>()
            .HasIndex(p => p.Status);

        modelBuilder.Entity<Notification>()
            .HasIndex(n => new { n.UserId, n.IsRead });

        modelBuilder.Entity<User>()
            .HasIndex(u => u.Email)
            .IsUnique();

        modelBuilder.Entity<Document>().HasIndex(d => d.StorageName).IsUnique();
        modelBuilder.Entity<Document>().HasIndex(d => new { d.UploadedByUserId, d.UploadedAt });
        modelBuilder.Entity<Document>().HasIndex(d => new { d.ProjectId, d.UploadedAt });
        modelBuilder.Entity<Document>().HasIndex(d => new { d.Category, d.UploadedAt });
        modelBuilder.Entity<Document>().HasIndex(d => new { d.ScanStatus, d.UploadedAt });
        modelBuilder.Entity<Document>().HasOne(d => d.UploadedByUser).WithMany()
            .HasForeignKey(d => d.UploadedByUserId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Document>().HasOne(d => d.Project).WithMany()
            .HasForeignKey(d => d.ProjectId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<DocumentShare>().HasOne(s => s.Document).WithMany(d => d.Shares)
            .HasForeignKey(s => s.DocumentId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<DocumentShare>().HasOne(s => s.User).WithMany()
            .HasForeignKey(s => s.UserId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<DocumentShare>().HasOne(s => s.SharedByUser).WithMany()
            .HasForeignKey(s => s.SharedByUserId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<DocumentShare>().HasIndex(s => new { s.UserId, s.RevokedAt, s.DocumentId });
        modelBuilder.Entity<DocumentShare>().HasIndex(s => new { s.DocumentId, s.UserId })
            .IsUnique()
            .HasFilter("[RevokedAt] IS NULL");
        modelBuilder.Entity<DocumentTaskAssociation>().HasIndex(a => new { a.DocumentId, a.TaskId }).IsUnique();
        modelBuilder.Entity<DocumentTaskAssociation>().HasOne(a => a.Document).WithMany(d => d.TaskAssociations)
            .HasForeignKey(a => a.DocumentId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<DocumentTaskAssociation>().HasOne(a => a.Task).WithMany()
            .HasForeignKey(a => a.TaskId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<DocumentTaskAssociation>().HasOne(a => a.AttachedByUser).WithMany()
            .HasForeignKey(a => a.AttachedByUserId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<DocumentActivity>().HasOne(a => a.Document).WithMany(d => d.Activities)
            .HasForeignKey(a => a.DocumentId).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<DocumentActivity>().HasOne(a => a.ActorUser).WithMany()
            .HasForeignKey(a => a.ActorUserId).OnDelete(DeleteBehavior.Restrict);

        // Seed initial data
        SeedData(modelBuilder);
    }

    private void SeedData(ModelBuilder modelBuilder)
    {
        // Migration seed data must be deterministic. Using DateTime.UtcNow here causes every
        // model build to appear different from the migration snapshot.
        var seedNow = new DateTime(2026, 11, 2, 0, 0, 0, DateTimeKind.Utc);

        // Seed an admin user
        modelBuilder.Entity<User>().HasData(
            new User
            {
                UserId = 1,
                Email = "admin@contoso.com",
                DisplayName = "System Administrator",
                Department = "IT",
                JobTitle = "Administrator",
                Role = UserRole.Administrator,
                AvailabilityStatus = AvailabilityStatus.Available,
                CreatedDate = seedNow,
                EmailNotificationsEnabled = true,
                InAppNotificationsEnabled = true
            },
            new User
            {
                UserId = 2,
                Email = "camille.nicole@contoso.com",
                DisplayName = "Camille Nicole",
                Department = "Engineering",
                JobTitle = "Project Manager",
                Role = UserRole.ProjectManager,
                AvailabilityStatus = AvailabilityStatus.Available,
                CreatedDate = seedNow,
                EmailNotificationsEnabled = true,
                InAppNotificationsEnabled = true
            },
            new User
            {
                UserId = 3,
                Email = "floris.kregel@contoso.com",
                DisplayName = "Floris Kregel",
                Department = "Engineering",
                JobTitle = "Team Lead",
                Role = UserRole.TeamLead,
                AvailabilityStatus = AvailabilityStatus.Available,
                CreatedDate = seedNow,
                EmailNotificationsEnabled = true,
                InAppNotificationsEnabled = true
            },
            new User
            {
                UserId = 4,
                Email = "ni.kang@contoso.com",
                DisplayName = "Ni Kang",
                Department = "Engineering",
                JobTitle = "Software Engineer",
                Role = UserRole.Employee,
                AvailabilityStatus = AvailabilityStatus.Available,
                CreatedDate = seedNow,
                EmailNotificationsEnabled = true,
                InAppNotificationsEnabled = true
            }
        );

        // Seed a sample project
        modelBuilder.Entity<Project>().HasData(
            new Project
            {
                ProjectId = 1,
                Name = "ContosoDashboard Development",
                Description = "Internal employee productivity dashboard",
                ProjectManagerId = 2,
                StartDate = seedNow.AddDays(-30),
                TargetCompletionDate = seedNow.AddDays(60),
                Status = ProjectStatus.Active,
                CreatedDate = seedNow.AddDays(-30),
                UpdatedDate = seedNow
            }
        );

        // Seed sample tasks
        modelBuilder.Entity<TaskItem>().HasData(
            new TaskItem
            {
                TaskId = 1,
                Title = "Design database schema",
                Description = "Create entity relationship diagram and database design",
                Priority = TaskPriority.High,
                Status = Models.TaskStatus.Completed,
                DueDate = seedNow.AddDays(-20),
                AssignedUserId = 4,
                CreatedByUserId = 2,
                ProjectId = 1,
                CreatedDate = seedNow.AddDays(-30),
                UpdatedDate = seedNow.AddDays(-20)
            },
            new TaskItem
            {
                TaskId = 2,
                Title = "Implement authentication",
                Description = "Set up Microsoft Entra ID authentication",
                Priority = TaskPriority.Critical,
                Status = Models.TaskStatus.InProgress,
                DueDate = seedNow.AddDays(5),
                AssignedUserId = 4,
                CreatedByUserId = 2,
                ProjectId = 1,
                CreatedDate = seedNow.AddDays(-25),
                UpdatedDate = seedNow
            },
            new TaskItem
            {
                TaskId = 3,
                Title = "Create UI mockups",
                Description = "Design user interface mockups for all main pages",
                Priority = TaskPriority.Medium,
                Status = Models.TaskStatus.NotStarted,
                DueDate = seedNow.AddDays(10),
                AssignedUserId = 4,
                CreatedByUserId = 2,
                ProjectId = 1,
                CreatedDate = seedNow.AddDays(-20),
                UpdatedDate = seedNow.AddDays(-20)
            }
        );

        // Seed project members
        modelBuilder.Entity<ProjectMember>().HasData(
            new ProjectMember
            {
                ProjectMemberId = 1,
                ProjectId = 1,
                UserId = 3,
                Role = "TeamLead",
                AssignedDate = seedNow.AddDays(-30)
            },
            new ProjectMember
            {
                ProjectMemberId = 2,
                ProjectId = 1,
                UserId = 4,
                Role = "Developer",
                AssignedDate = seedNow.AddDays(-30)
            }
        );

        // Seed announcement
        modelBuilder.Entity<Announcement>().HasData(
            new Announcement
            {
                AnnouncementId = 1,
                Title = "Welcome to ContosoDashboard",
                Content = "Welcome to the new ContosoDashboard application. This platform will help you manage your tasks and projects more efficiently.",
                CreatedByUserId = 1,
                PublishDate = seedNow,
                ExpiryDate = seedNow.AddDays(30),
                IsActive = true
            }
        );
    }
}
