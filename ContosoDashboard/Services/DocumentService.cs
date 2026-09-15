using ContosoDashboard.Data;
using ContosoDashboard.Models;
using Microsoft.EntityFrameworkCore;

namespace ContosoDashboard.Services;

public class DocumentService : IDocumentService
{
    private readonly ApplicationDbContext _context;
    private readonly IFileStorageService _fileStorageService;
    private readonly IConfiguration _configuration;

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf",
        ".doc",
        ".docx",
        ".xls",
        ".xlsx",
        ".ppt",
        ".pptx",
        ".txt",
        ".jpg",
        ".jpeg",
        ".png"
    };

    public DocumentService(ApplicationDbContext context, IFileStorageService fileStorageService, IConfiguration configuration)
    {
        _context = context;
        _fileStorageService = fileStorageService;
        _configuration = configuration;
    }

    public async Task<Document?> GetDocumentByIdAsync(int documentId, int requestingUserId)
    {
        var document = await _context.Documents
            .Include(d => d.UploadedByUser)
            .Include(d => d.Project)
            .ThenInclude(p => p.ProjectMembers)
            .Include(d => d.Shares)
            .FirstOrDefaultAsync(d => d.DocumentId == documentId && !d.IsDeleted);

        if (document == null)
        {
            return null;
        }

        if (HasAccess(document, requestingUserId))
        {
            return document;
        }

        return null;
    }

    public async Task<List<Document>> GetUserDocumentsAsync(int userId, bool includeShared = true)
    {
        var owned = _context.Documents
            .Include(d => d.Project)
            .Include(d => d.UploadedByUser)
            .Where(d => d.UploadedByUserId == userId && !d.IsDeleted);

        IQueryable<Document> shared = _context.Documents
            .Include(d => d.Project)
            .Include(d => d.UploadedByUser)
            .Where(d => d.Shares.Any(s => s.UserId == userId && s.IsActive) && !d.IsDeleted);

        var query = includeShared ? owned.Union(shared) : owned;
        return await query.OrderByDescending(d => d.UploadDate).ToListAsync();
    }

    public async Task<List<Document>> GetProjectDocumentsAsync(int projectId, int requestingUserId)
    {
        var project = await _context.Projects
            .Include(p => p.ProjectMembers)
            .FirstOrDefaultAsync(p => p.ProjectId == projectId);

        if (project == null)
        {
            return new List<Document>();
        }

        var isMember = project.ProjectManagerId == requestingUserId || project.ProjectMembers.Any(pm => pm.UserId == requestingUserId);
        if (!isMember)
        {
            return new List<Document>();
        }

        return await _context.Documents
            .Include(d => d.UploadedByUser)
            .Include(d => d.Project)
            .Where(d => d.ProjectId == projectId && !d.IsDeleted)
            .OrderByDescending(d => d.UploadDate)
            .ToListAsync();
    }

    public async Task<Document> UploadDocumentAsync(
        int uploadedByUserId,
        string title,
        string category,
        string? description,
        string? tags,
        int? projectId,
        int? taskId,
        Stream fileStream,
        string fileName,
        string contentType,
        bool isProjectUpload = false)
    {
        if (fileStream == null) throw new ArgumentNullException(nameof(fileStream));
        if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("Title is required.", nameof(title));
        if (string.IsNullOrWhiteSpace(fileName)) throw new ArgumentException("File name is required.", nameof(fileName));

        var maxSize = _configuration.GetValue<long>("DocumentStorage:MaxFileSizeBytes", 25L * 1024 * 1024);
        var allowedExtensions = _configuration.GetSection("DocumentStorage:AllowedExtensions").Get<string[]>() ?? new[] { ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".txt", ".jpg", ".jpeg", ".png" };

        if (fileStream.Length == 0)
        {
            throw new InvalidOperationException("The uploaded file is empty.");
        }

        if (fileStream.Length > maxSize)
        {
            throw new InvalidOperationException("The uploaded file exceeds the maximum supported size.");
        }

        var extension = Path.GetExtension(fileName);
        if (string.IsNullOrWhiteSpace(extension) || !allowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The file type is not allowed for upload.");
        }

        if (projectId.HasValue)
        {
            var project = await _context.Projects
                .Include(p => p.ProjectMembers)
                .FirstOrDefaultAsync(p => p.ProjectId == projectId.Value);

            if (project == null)
            {
                throw new InvalidOperationException("The selected project was not found.");
            }

            var isProjectMember = project.ProjectManagerId == uploadedByUserId || project.ProjectMembers.Any(pm => pm.UserId == uploadedByUserId);
            if (!isProjectMember)
            {
                throw new UnauthorizedAccessException("You do not have access to upload documents for this project.");
            }
        }

        var safeFileName = Path.GetFileNameWithoutExtension(fileName);
        var uniqueName = $"{Guid.NewGuid():N}{extension}";
        var relativeDirectory = projectId.HasValue ? $"{uploadedByUserId}/{projectId.Value}" : $"{uploadedByUserId}/personal";
        var relativePath = Path.Combine(relativeDirectory, uniqueName).Replace('\\', '/');

        var filePath = await _fileStorageService.UploadAsync(fileStream, fileName, contentType, relativePath);

        var document = new Document
        {
            Title = title.Trim(),
            Description = description,
            Tags = tags,
            Category = string.IsNullOrWhiteSpace(category) ? "Other" : category,
            FileName = fileName,
            StoredFileName = uniqueName,
            FilePath = filePath,
            FileType = contentType,
            FileSize = fileStream.Length,
            UploadedByUserId = uploadedByUserId,
            ProjectId = projectId,
            TaskId = taskId,
            UploadDate = DateTime.UtcNow,
            UpdatedDate = DateTime.UtcNow,
            Status = DocumentStatus.Queued,
            IsDeleted = false
        };

        _context.Documents.Add(document);
        await _context.SaveChangesAsync();

        return document;
    }

    public async Task<bool> DeleteDocumentAsync(int documentId, int requestingUserId)
    {
        var document = await _context.Documents
            .Include(d => d.Project)
            .ThenInclude(p => p.ProjectMembers)
            .FirstOrDefaultAsync(d => d.DocumentId == documentId && !d.IsDeleted);

        if (document == null)
        {
            return false;
        }

        var isOwner = document.UploadedByUserId == requestingUserId;
        var isProjectManager = document.Project != null && document.Project.ProjectManagerId == requestingUserId;
        if (!isOwner && !isProjectManager)
        {
            return false;
        }

        document.IsDeleted = true;
        document.UpdatedDate = DateTime.UtcNow;

        await _fileStorageService.DeleteAsync(document.FilePath);
        await _context.SaveChangesAsync();

        return true;
    }

    public async Task<bool> ShareDocumentAsync(int documentId, int userId, int sharedByUserId)
    {
        var document = await _context.Documents
            .Include(d => d.Project)
            .ThenInclude(p => p.ProjectMembers)
            .FirstOrDefaultAsync(d => d.DocumentId == documentId && !d.IsDeleted);

        if (document == null) return false;

        var isOwner = document.UploadedByUserId == sharedByUserId;
        var isProjectManager = document.Project != null && document.Project.ProjectManagerId == sharedByUserId;
        if (!isOwner && !isProjectManager)
        {
            return false;
        }

        var existingShare = await _context.DocumentShares
            .FirstOrDefaultAsync(s => s.DocumentId == documentId && s.UserId == userId && s.IsActive);

        if (existingShare != null)
        {
            return true;
        }

        _context.DocumentShares.Add(new DocumentShare
        {
            DocumentId = documentId,
            UserId = userId,
            SharedByUserId = sharedByUserId,
            SharedDate = DateTime.UtcNow,
            IsActive = true
        });

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<List<Document>> SearchDocumentsAsync(int requestingUserId, string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return await GetUserDocumentsAsync(requestingUserId);
        }

        var normalizedQuery = query.Trim();

        return await _context.Documents
            .Include(d => d.UploadedByUser)
            .Include(d => d.Project)
            .Where(d => !d.IsDeleted && (d.Title.Contains(normalizedQuery) ||
                (d.Description != null && d.Description.Contains(normalizedQuery)) ||
                (d.Tags != null && d.Tags.Contains(normalizedQuery)) ||
                d.UploadedByUser.DisplayName.Contains(normalizedQuery) ||
                (d.Project != null && d.Project.Name.Contains(normalizedQuery))))
            .Where(d => d.UploadedByUserId == requestingUserId || d.Shares.Any(s => s.UserId == requestingUserId && s.IsActive) || (d.Project != null && (d.Project.ProjectManagerId == requestingUserId || d.Project.ProjectMembers.Any(pm => pm.UserId == requestingUserId))))
            .OrderByDescending(d => d.UploadDate)
            .ToListAsync();
    }

    private static bool HasAccess(Document document, int userId)
    {
        if (document.UploadedByUserId == userId)
        {
            return true;
        }

        if (document.Project != null &&
            (document.Project.ProjectManagerId == userId || document.Project.ProjectMembers.Any(pm => pm.UserId == userId)))
        {
            return true;
        }

        return document.Shares.Any(s => s.UserId == userId && s.IsActive);
    }
}
