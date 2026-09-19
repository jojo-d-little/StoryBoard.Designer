using Microsoft.Win32;

namespace StoryboardDesigner.App.Services;

internal static class UserEnvironmentVariableRefreshService
{
    private const string UserEnvironmentKeyPath = "Environment";

    public static void RefreshMissingProcessVariables()
    {
        using var environmentKey = Registry.CurrentUser.OpenSubKey(UserEnvironmentKeyPath);
        if (environmentKey is null)
        {
            return;
        }

        foreach (var variableName in environmentKey.GetValueNames())
        {
            if (string.IsNullOrWhiteSpace(variableName)
                || Environment.GetEnvironmentVariable(variableName) is not null)
            {
                continue;
            }

            var value = environmentKey.GetValue(variableName, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
            if (value is string stringValue && !string.IsNullOrWhiteSpace(stringValue))
            {
                Environment.SetEnvironmentVariable(variableName, ExpandUserEnvironmentValue(stringValue));
            }
        }
    }

    private static string ExpandUserEnvironmentValue(string value)
    {
        return Environment.ExpandEnvironmentVariables(value);
    }
}
