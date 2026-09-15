using Microsoft.EntityFrameworkCore;
using ContosoDashboard.Data;
using ContosoDashboard.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components.Authorization;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

// Add authentication state provider for Blazor
builder.Services.AddScoped<AuthenticationStateProvider, CustomAuthenticationStateProvider>();

// Configure Database
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// Configure Mock Authentication (Cookie-based for training purposes)
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/login";
        options.LogoutPath = "/logout";
        options.AccessDeniedPath = "/login";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
    });

// Add authorization
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Employee", policy => policy.RequireRole("Employee", "TeamLead", "ProjectManager", "Administrator"));
    options.AddPolicy("TeamLead", policy => policy.RequireRole("TeamLead", "ProjectManager", "Administrator"));
    options.AddPolicy("ProjectManager", policy => policy.RequireRole("ProjectManager", "Administrator"));
    options.AddPolicy("Administrator", policy => policy.RequireRole("Administrator"));
});

// Register application services
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<ITaskService, TaskService>();
builder.Services.AddScoped<IProjectService, ProjectService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<IFileStorageService, LocalFileStorageService>();
builder.Services.AddScoped<IDocumentService, DocumentService>();

// Add HttpContextAccessor for accessing user claims
builder.Services.AddHttpContextAccessor();

var app = builder.Build();

// Initialize database
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<ApplicationDbContext>();
        context.Database.EnsureCreated(); // For development - use migrations in production
        context.Database.ExecuteSqlRaw(@"
            CREATE TABLE IF NOT EXISTS Documents (
                DocumentId INTEGER NOT NULL CONSTRAINT PK_Documents PRIMARY KEY AUTOINCREMENT,
                Title TEXT NOT NULL,
                Description TEXT NULL,
                Category TEXT NOT NULL,
                FileName TEXT NOT NULL,
                StoredFileName TEXT NOT NULL,
                FilePath TEXT NOT NULL,
                FileType TEXT NOT NULL,
                FileSize INTEGER NOT NULL,
                UploadedByUserId INTEGER NOT NULL,
                ProjectId INTEGER NULL,
                TaskId INTEGER NULL,
                UploadDate TEXT NOT NULL,
                UpdatedDate TEXT NOT NULL,
                IsDeleted INTEGER NOT NULL,
                Status INTEGER NOT NULL,
                Tags TEXT NULL,
                CONSTRAINT FK_Documents_Users_UploadedByUserId FOREIGN KEY (UploadedByUserId) REFERENCES Users (UserId) ON DELETE RESTRICT,
                CONSTRAINT FK_Documents_Projects_ProjectId FOREIGN KEY (ProjectId) REFERENCES Projects (ProjectId) ON DELETE SET NULL,
                CONSTRAINT FK_Documents_Tasks_TaskId FOREIGN KEY (TaskId) REFERENCES Tasks (TaskId) ON DELETE SET NULL
            );
            CREATE TABLE IF NOT EXISTS DocumentShares (
                DocumentShareId INTEGER NOT NULL CONSTRAINT PK_DocumentShares PRIMARY KEY AUTOINCREMENT,
                DocumentId INTEGER NOT NULL,
                UserId INTEGER NOT NULL,
                SharedByUserId INTEGER NOT NULL,
                SharedDate TEXT NOT NULL,
                IsActive INTEGER NOT NULL,
                CONSTRAINT FK_DocumentShares_Documents_DocumentId FOREIGN KEY (DocumentId) REFERENCES Documents (DocumentId) ON DELETE CASCADE,
                CONSTRAINT FK_DocumentShares_Users_UserId FOREIGN KEY (UserId) REFERENCES Users (UserId) ON DELETE CASCADE,
                CONSTRAINT FK_DocumentShares_Users_SharedByUserId FOREIGN KEY (SharedByUserId) REFERENCES Users (UserId) ON DELETE RESTRICT
            );
            CREATE INDEX IF NOT EXISTS IX_Documents_UploadedByUserId ON Documents (UploadedByUserId);
            CREATE INDEX IF NOT EXISTS IX_Documents_ProjectId ON Documents (ProjectId);
            CREATE INDEX IF NOT EXISTS IX_Documents_TaskId ON Documents (TaskId);
            CREATE INDEX IF NOT EXISTS IX_Documents_Category ON Documents (Category);
            CREATE INDEX IF NOT EXISTS IX_DocumentShares_DocumentId_UserId ON DocumentShares (DocumentId, UserId);");
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred creating the database.");
    }
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}
else
{
    // Use HSTS even in development for training purposes
    app.UseHsts();
}

// Add security headers
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["X-XSS-Protection"] = "1; mode=block";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    
    // Content Security Policy for Blazor Server
    context.Response.Headers["Content-Security-Policy"] = 
        "default-src 'self'; " +
        "script-src 'self' 'unsafe-inline' 'unsafe-eval' https://cdn.jsdelivr.net; " +
        "style-src 'self' 'unsafe-inline' https://cdn.jsdelivr.net; " +
        "font-src 'self' https://cdn.jsdelivr.net; " +
        "img-src 'self' data: https:; " +
        "connect-src 'self' wss: ws:;";
    
    await next();
});

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

// Enable authentication and authorization
app.UseAuthentication();
app.UseAuthorization();

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();
