
namespace AugustoCRV.Tools.ConsoleModelDrivenAiTraslator.Services.Translation;

/// <summary>Enum description.</summary>

public enum LogType
{
    Info,
    Warning,
    Error
}

internal class BaseRecordExporter
{
    public static int BulkCount = 10;
    private ExecuteMultipleRequest? request;

    protected void AddRequest(OrganizationRequest or)
    {
        if (request is null) InitMultipleRequest();

        request!.Requests.Add(or);
    }

    protected void ExecuteMultiple(IOrganizationService service, TranslationProgressEventArgs e, int total, bool forceUpdate = false)
    {
        if (request is null) return;

        if (request.Requests.Count % BulkCount != 0 && !forceUpdate) return;

        e.TotalItems = total;

        var bulkResponse = (ExecuteMultipleResponse)service.Execute(request);

        if (bulkResponse.IsFaulted)
        {
            e.FailureCount += bulkResponse.Responses.Count(r => r.Fault != null);
            e.SuccessCount += request.Requests.Count - bulkResponse.Responses.Count;
        }
        else
        {
            e.SuccessCount += request.Requests.Count;
        }

        InitMultipleRequest();
    }

    protected void InitMultipleRequest()
    {
        request = new ExecuteMultipleRequest
        {
            Requests = new OrganizationRequestCollection(),
            Settings = new ExecuteMultipleSettings
            {
                ContinueOnError = true,
                ReturnResponses = false
            }
        };
    }

    public string CalculateChecksum(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return string.Empty;
        }

        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    protected static bool IsManagedComponent(object? component)
    {
        if (component is null)
        {
            return false;
        }

        var isManagedProperty = component.GetType().GetProperty("IsManaged", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        if (isManagedProperty is null)
        {
            return false;
        }

        var isManagedValue = isManagedProperty.GetValue(component);
        if (isManagedValue is bool b)
        {
            return b;
        }

        var valueProperty = isManagedValue?.GetType().GetProperty("Value", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        var wrappedValue = valueProperty?.GetValue(isManagedValue);
        return wrappedValue is bool wrapped && wrapped;
    }
}

/// <summary>Class description.</summary>

public class LogEventArgs : EventArgs
{
    public LogEventArgs()
    {
    }

    public LogEventArgs(string message)
    {
        Message = message;
    }

    public string Message { get; set; } = string.Empty;
    public LogType Type { get; set; }
}

/// <summary>Class description.</summary>

public class TranslationProgressEventArgs : EventArgs
{
    public int FailureCount { get; set; }
    public string SheetName { get; set; } = string.Empty;
    public int SuccessCount { get; set; }
    public int TotalItems { get; set; }
}

