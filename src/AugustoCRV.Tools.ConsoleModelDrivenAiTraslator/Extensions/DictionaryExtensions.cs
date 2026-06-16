namespace AugustoCRV.Tools.ConsoleModelDrivenAiTraslator.Extensions;

internal static class DictionaryExtensions
{
    public static bool TryGetValueIgnoreCase<TValue>(this Dictionary<string, TValue> dictionary, string key, out TValue value)
        where TValue : notnull
    {
        if (dictionary.TryGetValue(key, out var directValue))
        {
            value = directValue;
            return true;
        }

        var match = dictionary.FirstOrDefault(x => x.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrEmpty(match.Key))
        {
            value = match.Value is null ? default! : match.Value;
            return true;
        }

        value = default!;
        return false;
    }

    public static bool TryGetNestedValueIgnoreCase<TValue>(
        this Dictionary<string, Dictionary<string, TValue>> dictionary,
        string key,
        out Dictionary<string, TValue> value)
        where TValue : notnull
    {
        if (dictionary.TryGetValue(key, out var directValue))
        {
            value = directValue;
            return true;
        }

        var match = dictionary.FirstOrDefault(x => x.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrEmpty(match.Key))
        {
            value = match.Value ?? new Dictionary<string, TValue>();
            return true;
        }

        value = new Dictionary<string, TValue>();
        return false;
    }
}
