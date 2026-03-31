using System;
using System.Collections.Generic;

namespace LogDB.Client.Tests.TestDoubles;

internal sealed class EnvironmentVariableScope : IDisposable
{
    private readonly Dictionary<string, string?> _originalValues = new(StringComparer.Ordinal);

    public EnvironmentVariableScope(params (string Name, string? Value)[] updates)
    {
        foreach (var update in updates)
        {
            if (_originalValues.ContainsKey(update.Name))
            {
                continue;
            }

            _originalValues[update.Name] = Environment.GetEnvironmentVariable(update.Name);
            Environment.SetEnvironmentVariable(update.Name, update.Value);
        }
    }

    public void Dispose()
    {
        foreach (var pair in _originalValues)
        {
            Environment.SetEnvironmentVariable(pair.Key, pair.Value);
        }
    }
}
