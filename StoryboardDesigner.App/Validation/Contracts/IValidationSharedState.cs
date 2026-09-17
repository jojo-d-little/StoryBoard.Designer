namespace StoryboardDesigner.App.Validation.Contracts;

public interface IValidationSharedState
{
    bool TryGet<T>(string key, out T? value);
    T GetOrAdd<T>(string key, Func<T> factory);
}
