using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using SCM3.FormBuilder.Core.Models;

namespace SCM3.FormBuilder.Data;

public interface IFormRepository
{
    // Form operations
    Task<FormVersion?> GetVersion(string toolType, string formKey, int? versionNumber = null);
    Task<FormVersion> SaveVersion(FormVersion version);
    Task DeleteDraft(string toolType, string formKey, int versionNumber);
    Task<List<(string ToolType, string FormKey)>> ListForms();
    Task<List<(int VersionNumber, bool IsPublished)>> ListVersions(string toolType, string formKey);
    
    // Artifact operations
    Task<Artifact?> GetArtifact(string artifactId);
    Task<Artifact> SaveArtifact(Artifact artifact);
    Task<List<Artifact>> ListArtifacts();
    
    // Submission operations (now supports multiple submissions per artifact per form)
    Task<List<FormSubmission>> GetSubmissions(string toolType, string formKey, string artifactId);
    Task<FormSubmission?> GetSubmissionById(Guid submissionId);
    Task<FormSubmission> SaveSubmission(FormSubmission submission);
    
    // Query helpers for searching submissions by field label + value
    Task<object?> GetSubmissionValueByLabel(string toolType, string formKey, string artifactId, string fieldLabel);
    Task<List<FormSubmission>> SearchSubmissionsByLabel(string toolType, string formKey, string fieldLabel, object? searchValue);
}

// Local-disk, file-based implementation. No SQL Server, no EF Core.
// Layout on disk:
//   {rootPath}/forms/{toolType}/{formKey}/v{N}.json
//   {rootPath}/artifacts/{artifactId}.json
//   {rootPath}/submissions/{toolType}/{formKey}/{artifactId}/{submissionId}.json
//
// Safe to zip up the whole rootPath folder and hand it off - it's the entire
// state of the system, no external database to also export.
public class JsonFileFormRepository : IFormRepository
{
    private readonly string _rootPath;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    public JsonFileFormRepository(string rootPath)
    {
        _rootPath = rootPath;
        Directory.CreateDirectory(Path.Combine(_rootPath, "forms"));
        Directory.CreateDirectory(Path.Combine(_rootPath, "artifacts"));
        Directory.CreateDirectory(Path.Combine(_rootPath, "submissions"));
    }

    private string FormFolder(string toolType, string formKey) =>
        Path.Combine(_rootPath, "forms", Sanitize(toolType), Sanitize(formKey));
    
    private string ArtifactsFolder() =>
        Path.Combine(_rootPath, "artifacts");

    private string SubmissionFolder(string toolType, string formKey, string artifactId) =>
        Path.Combine(_rootPath, "submissions", Sanitize(toolType), Sanitize(formKey), Sanitize(artifactId));

    private string ArtifactPath(string artifactId) =>
        Path.Combine(ArtifactsFolder(), $"{Sanitize(artifactId)}.json");

    private static string Sanitize(string value) =>
        string.Concat(value.Split(Path.GetInvalidFileNameChars()));

    public async Task<FormVersion?> GetVersion(string toolType, string formKey, int? versionNumber = null)
    {
        var folder = FormFolder(toolType, formKey);
        if (!Directory.Exists(folder)) return null;

        if (versionNumber.HasValue)
        {
            var path = Path.Combine(folder, $"v{versionNumber.Value}.json");
            if (!File.Exists(path)) return null;
            var json = await File.ReadAllTextAsync(path);
            return JsonSerializer.Deserialize<FormVersion>(json, JsonOptions);
        }

        // "Latest" = highest version number file present.
        var files = Directory.GetFiles(folder, "v*.json");
        if (files.Length == 0) return null;

        var latestPath = files
            .Select(f => new
            {
                Path = f,
                Num = int.TryParse(
                    Path.GetFileNameWithoutExtension(f).TrimStart('v'),
                    out var n) ? n : -1
            })
            .OrderByDescending(x => x.Num)
            .First()
            .Path;

        var latestJson = await File.ReadAllTextAsync(latestPath);
        return JsonSerializer.Deserialize<FormVersion>(latestJson, JsonOptions);
    }

    public async Task<FormVersion> SaveVersion(FormVersion version)
    {
        var folder = FormFolder(version.ToolType, version.FormKey);
        Directory.CreateDirectory(folder);

        var path = Path.Combine(folder, $"v{version.VersionNumber}.json");

        // Immutability guard applies ONLY to published versions. A draft can
        // be saved repeatedly while being edited - it isn't locked until
        // IsPublished flips to true. Once published, it can never be
        // overwritten again, even by another "draft" save attempt.
        if (File.Exists(path))
        {
            var existingJson = await File.ReadAllTextAsync(path);
            var existing = JsonSerializer.Deserialize<FormVersion>(existingJson, JsonOptions);
            if (existing is not null && existing.IsPublished)
            {
                throw new InvalidOperationException(
                    $"Version {version.VersionNumber} of '{version.ToolType}/{version.FormKey}' " +
                    "is already published and immutable. Publish a new version number instead.");
            }
        }

        var json = JsonSerializer.Serialize(version, JsonOptions);
        await File.WriteAllTextAsync(path, json);
        return version;
    }

    // Discards an unpublished draft outright. Throws if the version is
    // already published - published versions can never be deleted, only
    // drafts can.
    public Task DeleteDraft(string toolType, string formKey, int versionNumber)
    {
        var folder = FormFolder(toolType, formKey);
        var path = Path.Combine(folder, $"v{versionNumber}.json");

        if (!File.Exists(path)) return Task.CompletedTask;

        var existingJson = File.ReadAllText(path);
        var existing = JsonSerializer.Deserialize<FormVersion>(existingJson, JsonOptions);
        if (existing is not null && existing.IsPublished)
        {
            throw new InvalidOperationException(
                $"Version {versionNumber} of '{toolType}/{formKey}' is published and cannot be deleted.");
        }

        File.Delete(path);
        return Task.CompletedTask;
    }

    // Lists every form (ToolType + FormKey pair) that has at least one
    // saved version, for the admin landing page.
    public Task<List<(string ToolType, string FormKey)>> ListForms()
    {
        var formsRoot = Path.Combine(_rootPath, "forms");
        var result = new List<(string ToolType, string FormKey)>();

        if (!Directory.Exists(formsRoot)) return Task.FromResult(result);

        foreach (var toolTypeDir in Directory.GetDirectories(formsRoot))
        {
            foreach (var formKeyDir in Directory.GetDirectories(toolTypeDir))
            {
                result.Add((Path.GetFileName(toolTypeDir), Path.GetFileName(formKeyDir)));
            }
        }

        return Task.FromResult(result);
    }

    // Lists all versions (number + published flag) for one form, for the
    // admin's per-form version history view.
    public async Task<List<(int VersionNumber, bool IsPublished)>> ListVersions(string toolType, string formKey)
    {
        var folder = FormFolder(toolType, formKey);
        var result = new List<(int VersionNumber, bool IsPublished)>();

        if (!Directory.Exists(folder)) return result;

        foreach (var file in Directory.GetFiles(folder, "v*.json"))
        {
            var json = await File.ReadAllTextAsync(file);
            var version = JsonSerializer.Deserialize<FormVersion>(json, JsonOptions);
            if (version is not null)
            {
                result.Add((version.VersionNumber, version.IsPublished));
            }
        }

        return result.OrderBy(v => v.VersionNumber).ToList();
    }

    // Artifact operations
    public async Task<Artifact?> GetArtifact(string artifactId)
    {
        var path = ArtifactPath(artifactId);
        if (!File.Exists(path)) return null;

        var json = await File.ReadAllTextAsync(path);
        return JsonSerializer.Deserialize<Artifact>(json, JsonOptions);
    }

    public async Task<Artifact> SaveArtifact(Artifact artifact)
    {
        var folder = ArtifactsFolder();
        Directory.CreateDirectory(folder);
        
        var path = ArtifactPath(artifact.ArtifactId);
        var json = JsonSerializer.Serialize(artifact, JsonOptions);
        await File.WriteAllTextAsync(path, json);
        return artifact;
    }

    public async Task<List<Artifact>> ListArtifacts()
    {
        var folder = ArtifactsFolder();
        var result = new List<Artifact>();

        if (!Directory.Exists(folder)) return result;

        foreach (var file in Directory.GetFiles(folder, "*.json"))
        {
            var json = await File.ReadAllTextAsync(file);
            var artifact = JsonSerializer.Deserialize<Artifact>(json, JsonOptions);
            if (artifact is not null)
            {
                result.Add(artifact);
            }
        }

        return result.OrderByDescending(a => a.CreatedDate).ToList();
    }

    // Submission operations - now supports multiple submissions per artifact per form
    public async Task<List<FormSubmission>> GetSubmissions(string toolType, string formKey, string artifactId)
    {
        var folder = SubmissionFolder(toolType, formKey, artifactId);
        var result = new List<FormSubmission>();

        if (!Directory.Exists(folder)) return result;

        foreach (var file in Directory.GetFiles(folder, "*.json"))
        {
            var json = await File.ReadAllTextAsync(file);
            var submission = JsonSerializer.Deserialize<FormSubmission>(json, JsonOptions);
            if (submission is not null)
            {
                result.Add(submission);
            }
        }

        return result.OrderByDescending(s => s.SubmittedDate).ToList();
    }

    public async Task<FormSubmission?> GetSubmissionById(Guid submissionId)
    {
        var submissionsRoot = Path.Combine(_rootPath, "submissions");
        if (!Directory.Exists(submissionsRoot)) return null;

        // Search through all submission files to find the one with matching ID
        foreach (var file in Directory.GetFiles(submissionsRoot, "*.json", SearchOption.AllDirectories))
        {
            var json = await File.ReadAllTextAsync(file);
            var submission = JsonSerializer.Deserialize<FormSubmission>(json, JsonOptions);
            if (submission?.SubmissionId == submissionId)
            {
                return submission;
            }
        }

        return null;
    }

    public async Task<FormSubmission> SaveSubmission(FormSubmission submission)
    {
        var folder = SubmissionFolder(submission.ToolType, submission.FormKey, submission.RecordPrimaryKey);
        Directory.CreateDirectory(folder);

        // Each submission gets a unique file with its ID
        var path = Path.Combine(folder, $"{submission.SubmissionId}.json");

        // Always create a new file - never overwrite, so we keep submission history
        var json = JsonSerializer.Serialize(submission, JsonOptions);
        await File.WriteAllTextAsync(path, json);
        return submission;
    }

    // Query a single submission's value by field label (not field ID).
    // Returns null if the label is not found in this submission.
    public async Task<object?> GetSubmissionValueByLabel(string toolType, string formKey, string artifactId, string fieldLabel)
    {
        var submissions = await GetSubmissions(toolType, formKey, artifactId);
        if (submissions.Count == 0) return null;

        // Return the most recent submission's value for this label
        var latestSubmission = submissions.FirstOrDefault();
        var field = latestSubmission?.Values.Values.FirstOrDefault(v => v.Label == fieldLabel);
        return field?.Value;
    }

    // Query all submissions for a form that have a specific field label with a specific value.
    // Example: "Find all submissions where 'Customer Name' == 'John'"
    public async Task<List<FormSubmission>> SearchSubmissionsByLabel(string toolType, string formKey, string fieldLabel, object? searchValue)
    {
        var submissionsRoot = Path.Combine(_rootPath, "submissions", Sanitize(toolType), Sanitize(formKey));
        var result = new List<FormSubmission>();

        if (!Directory.Exists(submissionsRoot)) return result;

        // Search across all artifact folders
        foreach (var artifactFolder in Directory.GetDirectories(submissionsRoot))
        {
            foreach (var file in Directory.GetFiles(artifactFolder, "*.json"))
            {
                var json = await File.ReadAllTextAsync(file);
                var submission = JsonSerializer.Deserialize<FormSubmission>(json, JsonOptions);
                if (submission is null) continue;

                // Check if this submission has a field with the matching label and value
                var matchingField = submission.Values.Values.FirstOrDefault(v => 
                    v.Label == fieldLabel && 
                    (searchValue is null ? v.Value is null : v.Value?.Equals(searchValue) == true));

                if (matchingField is not null)
                {
                    result.Add(submission);
                }
            }
        }

        return result.OrderByDescending(s => s.SubmittedDate).ToList();
    }
}
