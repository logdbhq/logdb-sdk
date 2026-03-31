using com.logdb.logger;
using LogDB.Client.Models;
using LogDB.Client.Services;
using Newtonsoft.Json;

namespace com.logdb.LogDB.LogBuilders;

public sealed class LogCacheBuilder
{
    private static LoggerContext _context = new();
    private readonly LogCache _entry;
    private static Logger? _logger;


    private LogCacheBuilder(LogCache entry)
    {
        _entry = entry;
    }

    public static LogCacheBuilder Create()
    {
        return new LogCacheBuilder(new LogCache()
        {
            Key = string.Empty,
            Value = string.Empty
        });
    }
    
    public static string ApiKey
    {
        get
        {
            if (string.IsNullOrWhiteSpace(_context.ApiKey))
                throw new InvalidOperationException("LogCacheBuilder.ApiKey must be set before accessing it.");
            return _context.ApiKey;
        }
        set
        {
            if (string.IsNullOrEmpty(value))
                throw new ArgumentException("ApiKey cannot be null or empty.", nameof(value));

            _context.ApiKey = value;
            _logger = Logger.Create(_context.ApiKey);
        }
    }

    public LogCacheBuilder SetKey(string key, bool isEncrypted = false)
    {
        var newEntry = CloneEntry(_entry);
        newEntry.Key = isEncrypted ? EncryptionService.Encrypt(key) : key;
        return new LogCacheBuilder(newEntry);
    }

    public LogCacheBuilder SetValue(object value)
    {
        var newEntry = CloneEntry(_entry);
        newEntry.Value = JsonConvert.SerializeObject(value);
        return new LogCacheBuilder(newEntry);
    }

    public LogCacheBuilder SetTimestamp(DateTime timestamp)
    {
        var newEntry = CloneEntry(_entry);
        newEntry.Timestamp = timestamp;
        return new LogCacheBuilder(newEntry);
    }

    public LogCache Build()
    {
        if (_entry._IsEncrypted)
        {
            _entry.Key = EncryptionService.Encrypt(_entry.Key);
            _entry.Value = EncryptionService.Encrypt(_entry.Value);
        }
        return _entry;
    }

    private static LogCache CloneEntry(LogCache original)
    {
        // Don't copy Timestamp - it's not used in gRPC requests and the getter may be missing
        // This avoids the "Method not found: get_Timestamp()" error
        return new LogCache()
        {
            ApiKey = original.ApiKey,
            Guid = original.Guid,
            Key = original.Key,
            Value = original.Value
            // Timestamp is intentionally not copied - not used in LogCacheGrpcRequest
        };
    }

    public LogCacheBuilder Encrypt()
    {
        var newEntry = CloneEntry(_entry);
        newEntry._IsEncrypted = true;
        return new LogCacheBuilder(newEntry);
    }

    public async Task Log()
    {
        /*if (String.IsNullOrEmpty(_entry.Collection) && !String.IsNullOrEmpty(Collection))
            _entry.Collection = Collection;
        if (String.IsNullOrEmpty(_entry.Environment) && !String.IsNullOrEmpty(Environment))
            _entry.Environment = Environment;
        if (String.IsNullOrEmpty(_entry.Application) && !String.IsNullOrEmpty(Application))
            _entry.Application = Application;*/

        if (_logger == null)
        {
            if (string.IsNullOrWhiteSpace(_context.ApiKey))
                throw new InvalidOperationException("LogCacheBuilder.ApiKey must be set before calling Log().");
            _logger = Logger.Create(_context.ApiKey);
        }

        _entry.ApiKey = ApiKey;
        _entry.Guid = Guid.NewGuid().ToString();
        
        await _logger.Log(_entry).ConfigureAwait(false);
    }
}
