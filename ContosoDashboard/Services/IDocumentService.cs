using ContosoDashboard.Models;

namespace ContosoDashboard.Services;

public interface IDocumentService
{
    Task<Document?> GetDocumentByIdAsync(int documentId, int requestingUserId);
    Task<List<Document>> GetUserDocumentsAsync(int userId, bool includeShared = true);
    Task<List<Document>> GetProjectDocumentsAsync(int projectId, int requestingUserId);
    Task<Document> UploadDocumentAsync(
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
        bool isProjectUpload = false);
    Task<bool> DeleteDocumentAsync(int documentId, int requestingUserId);
    Task<bool> ShareDocumentAsync(int documentId, int userId, int sharedByUserId);
    Task<List<Document>> SearchDocumentsAsync(int requestingUserId, string query);
}
