using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;

namespace AIScriptToMediaDotNet.Services.ComfyUI;

/// <summary>
/// Provides workflow templates for ComfyUI image generation.
/// </summary>
public interface IWorkflowTemplateProvider
{
    /// <summary>
    /// Gets the workflow template.
    /// </summary>
    /// <returns>The workflow template as a JsonNode.</returns>
    Task<JsonNode> GetWorkflowTemplateAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the workflow name.
    /// </summary>
    /// <returns>The workflow name.</returns>
    string GetWorkflowName();
}

/// <summary>
/// File-based implementation of IWorkflowTemplateProvider.
/// </summary>
public class FileWorkflowTemplateProvider : IWorkflowTemplateProvider
{
    private readonly string _workflowPath;
    private readonly WorkflowNodeMapping _nodeMapping;
    private readonly ILogger<FileWorkflowTemplateProvider> _logger;
    private JsonNode? _cachedTemplate;

    /// <summary>
    /// Initializes a new instance of the FileWorkflowTemplateProvider class.
    /// </summary>
    /// <param name="workflowPath">Path to the workflow JSON file.</param>
    /// <param name="nodeMapping">Optional node mapping configuration.</param>
    /// <param name="logger">The logger instance.</param>
    public FileWorkflowTemplateProvider(
        string workflowPath,
        WorkflowNodeMapping? nodeMapping = null,
        ILogger<FileWorkflowTemplateProvider>? logger = null)
    {
        _workflowPath = workflowPath;
        _nodeMapping = nodeMapping ?? new WorkflowNodeMapping();
        _logger = logger;
    }

    /// <summary>
    /// Gets the workflow template from the file.
    /// </summary>
    public Task<JsonNode> GetWorkflowTemplateAsync(CancellationToken cancellationToken = default)
    {
        if (_cachedTemplate != null)
        {
            return Task.FromResult(_cachedTemplate);
        }

        if (!File.Exists(_workflowPath))
        {
            _logger?.LogError("Workflow file not found: {WorkflowPath}", _workflowPath);
            throw new FileNotFoundException($"Workflow file not found at {_workflowPath}");
        }

        var workflowJson = File.ReadAllText(_workflowPath);
        var workflow = JsonNode.Parse(workflowJson)
            ?? throw new InvalidOperationException($"Failed to parse workflow JSON from {_workflowPath}");

        _cachedTemplate = workflow;
        _logger?.LogDebug("Loaded workflow template from {WorkflowPath}", _workflowPath);

        return Task.FromResult(workflow);
    }

    /// <summary>
    /// Gets the workflow name from the file path.
    /// </summary>
    public string GetWorkflowName() => Path.GetFileNameWithoutExtension(_workflowPath);

    /// <summary>
    /// Gets the node mapping for this workflow.
    /// </summary>
    /// <returns>The node mapping.</returns>
    public WorkflowNodeMapping GetNodeMapping() => _nodeMapping;
}

/// <summary>
/// In-memory implementation of IWorkflowTemplateProvider for testing.
/// </summary>
public class InMemoryWorkflowTemplateProvider : IWorkflowTemplateProvider
{
    private readonly JsonNode _workflowTemplate;
    private readonly string _workflowName;
    private readonly WorkflowNodeMapping _nodeMapping;

    /// <summary>
    /// Initializes a new instance of the InMemoryWorkflowTemplateProvider class.
    /// </summary>
    /// <param name="workflowTemplate">The workflow template.</param>
    /// <param name="workflowName">The workflow name.</param>
    /// <param name="nodeMapping">Optional node mapping configuration.</param>
    public InMemoryWorkflowTemplateProvider(
        JsonNode workflowTemplate,
        string workflowName = "Custom",
        WorkflowNodeMapping? nodeMapping = null)
    {
        _workflowTemplate = workflowTemplate;
        _workflowName = workflowName;
        _nodeMapping = nodeMapping ?? new WorkflowNodeMapping();
    }

    /// <summary>
    /// Gets the workflow template.
    /// </summary>
    public Task<JsonNode> GetWorkflowTemplateAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(_workflowTemplate);

    /// <summary>
    /// Gets the workflow name.
    /// </summary>
    public string GetWorkflowName() => _workflowName;

    /// <summary>
    /// Gets the node mapping for this workflow.
    /// </summary>
    public WorkflowNodeMapping GetNodeMapping() => _nodeMapping;
}
