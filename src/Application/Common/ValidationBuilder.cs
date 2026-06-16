namespace Scrobblint.Application.Common;

/// <summary>
/// Tiny fluent-free helper for accumulating field validation errors by hand.
/// Keeps us free of a validation framework while staying readable.
/// </summary>
public sealed class ValidationBuilder
{
    private readonly Dictionary<string, List<string>> _errors = new(StringComparer.OrdinalIgnoreCase);

    public bool HasErrors => _errors.Count > 0;

    public ValidationBuilder Add(string field, string message)
    {
        if (!_errors.TryGetValue(field, out var list))
        {
            list = new List<string>();
            _errors[field] = list;
        }
        list.Add(message);
        return this;
    }

    /// <summary>Adds <paramref name="message"/> for <paramref name="field"/> when <paramref name="condition"/> is true.</summary>
    public ValidationBuilder AddIf(bool condition, string field, string message)
    {
        if (condition) Add(field, message);
        return this;
    }

    public void Required(string field, string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) Add(field, $"{field} is required.");
    }

    public void Length(string field, string? value, int min, int max)
    {
        if (value is null) return;
        if (value.Length < min) Add(field, $"{field} must be at least {min} characters.");
        if (value.Length > max) Add(field, $"{field} must be at most {max} characters.");
    }

    public IReadOnlyDictionary<string, string[]> Build() =>
        _errors.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToArray(), StringComparer.OrdinalIgnoreCase);
}
