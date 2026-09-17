using StoryboardDesigner.App.Validation.Contracts;

namespace StoryboardDesigner.App.Validation.Execution;

public sealed class ValidationSharedState : IValidationSharedState
{
    private readonly Dictionary<string, object?> _values = new(StringComparer.Ordinal);

    public bool TryGet<T>(string key, out T? value)
    {
        if (_values.TryGetValue(key, out var boxed) && boxed is T typed)
        {
            value = typed;
            return true;
        }

        value = default;
        return false;
    }

    public T GetOrAdd<T>(string key, Func<T> factory)
    {
        if (_values.TryGetValue(key, out var boxed) && boxed is T typed)
        {
            return typed;
        }

        var created = factory();
        _values[key] = created;
        return created;
    }
}
