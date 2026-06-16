namespace AugustoCRV.Tools.ConsoleModelDrivenAiTraslator.Extensions;

/// <summary>Class description.</summary>

public static class AnsiConsoleExtensions
{
    public static void WriteInfo(this IAnsiConsole console, string message)
    {
        ArgumentNullException.ThrowIfNull(console);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        console.MarkupLine($"[blue]{Markup.Escape(message)}[/]");
    }

    public static void WriteSuccess(this IAnsiConsole console, string message)
    {
        ArgumentNullException.ThrowIfNull(console);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        console.MarkupLine($"[green]{Markup.Escape(message)}[/]");
    }

    public static void WriteWarning(this IAnsiConsole console, string message)
    {
        ArgumentNullException.ThrowIfNull(console);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        console.MarkupLine($"[yellow]{Markup.Escape(message)}[/]");
    }

    public static void WriteError(this IAnsiConsole console, string message)
    {
        ArgumentNullException.ThrowIfNull(console);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        console.MarkupLine($"[red]{Markup.Escape(message)}[/]");
    }

    public static async Task<T> RunStatusAsync<T>(this IAnsiConsole console, string statusText, Func<Task<T>> operation)
    {
        ArgumentNullException.ThrowIfNull(console);
        ArgumentException.ThrowIfNullOrWhiteSpace(statusText);
        ArgumentNullException.ThrowIfNull(operation);

        T result = default!;
        await console.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync($"[blue]{Markup.Escape(statusText)}[/]", async _ =>
            {
                result = await operation().ConfigureAwait(false);
            }).ConfigureAwait(false);

        return result;
    }
}

