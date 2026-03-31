using com.logdb.LogDB;
using LogDB.Client.Models;
using LogDB.Client.Services;
using Newtonsoft.Json;

namespace com.logdb.LogDB.LogBuilders;

public sealed class LogEventBuilder
{
    /// <summary>
    /// Static defaults so callers can configure global metadata once (e.g. in startup).
    /// </summary>
    public static string? Collection { get; set; }
    public static string? Environment { get; set; }
    public static string? Application { get; set; }
    public static string? ApiKey { get; set; }

    /// <summary>
    /// Optional default logger used when Create() is called without an explicit logger.
    /// If not set, a default Logger is created from ApiKey.
    /// </summary>
    public static ILogger? DefaultLogger { get; set; }

    private readonly ILogger _logger;
    private readonly Log _entry;

    internal LogEventBuilder(ILogger logger, Log entry)
    {
        _logger = logger;
        _entry = entry;
    }

    public static LogEventBuilder Create(ILogger? logger = null)
    {
        var effectiveLogger = logger ?? DefaultLogger;
        if (effectiveLogger == null)
        {
            if (string.IsNullOrWhiteSpace(ApiKey))
            {
                throw new InvalidOperationException(
                    "LogEventBuilder.ApiKey must be set, or pass an ILogger to LogEventBuilder.Create().");
            }

            effectiveLogger = com.logdb.logger.Logger.Create(ApiKey);
        }

        return new LogEventBuilder(effectiveLogger, new Log
        {
            ApiKey = ApiKey,
            Collection = Collection,
            Application = Application ?? string.Empty,
            Environment = Environment ?? string.Empty,
            Guid = Guid.NewGuid().ToString(),
            Timestamp = DateTime.UtcNow,
            Level = LogLevel.Info,
            AttributesS = new Dictionary<string, string>(),
            AttributesN = new Dictionary<string, double>(),
            AttributesB = new Dictionary<string, bool>(),
            AttributesD = new Dictionary<string, DateTime>(),
            Label = [],
        });
    }

    public LogEventBuilder SetUserEmail(string userEmail, bool isEncrypted = false)
    {
        var newEntry = CloneEntry(_entry);
        newEntry.UserEmail = isEncrypted ? EncryptionService.Encrypt(userEmail) : userEmail;
        return new LogEventBuilder(_logger, newEntry);
    }

    public LogEventBuilder SetIpAddress(string ipAddress, bool isEncrypted = false)
    {
        var newEntry = CloneEntry(_entry);
        newEntry.IpAddress = isEncrypted ? EncryptionService.Encrypt(ipAddress) : ipAddress;
        return new LogEventBuilder(_logger, newEntry);
    }

    public LogEventBuilder SetHttpMethod(string httpMethod)
    {
        var newEntry = CloneEntry(_entry);
        newEntry.HttpMethod = httpMethod;
        return new LogEventBuilder(_logger, newEntry);
    }

    public LogEventBuilder SetSource(string source, bool isEncrypted = false)
    {
        var newEntry = CloneEntry(_entry);
        newEntry.Source = isEncrypted ? EncryptionService.Encrypt(source) : source;
        return new LogEventBuilder(_logger, newEntry);
    }

    public LogEventBuilder SetStackTrace(string stackTrace, bool isEncrypted = false)
    {
        var newEntry = CloneEntry(_entry);
        newEntry.StackTrace = isEncrypted ? EncryptionService.Encrypt(stackTrace) : stackTrace;
        return new LogEventBuilder(_logger, newEntry);
    }

    public LogEventBuilder SetApplication(string application, bool isEncrypted = false)
    {
        var newEntry = CloneEntry(_entry);
        newEntry.Application = isEncrypted ? EncryptionService.Encrypt(application) : application;
        return new LogEventBuilder(_logger, newEntry);
    }

    public LogEventBuilder SetEnvironment(string environment, bool isEncrypted = false)
    {
        var newEntry = CloneEntry(_entry);
        newEntry.Environment = isEncrypted ? EncryptionService.Encrypt(environment) : environment;
        return new LogEventBuilder(_logger, newEntry);
    }

    public LogEventBuilder SetLogLevel(LogLevel logLevel)
    {
        var newEntry = CloneEntry(_entry);
        newEntry.Level = logLevel;
        return new LogEventBuilder(_logger, newEntry);
    }

    public LogEventBuilder SetMessage(string message, bool isEncrypted = false)
    {
        var newEntry = CloneEntry(_entry);
        newEntry.Message = isEncrypted ? EncryptionService.Encrypt(message) : message;
        return new LogEventBuilder(_logger, newEntry);
    }

    private LogEventBuilder SetUserId(int userId)
    {
        var newEntry = CloneEntry(_entry);
        newEntry.UserId = userId;
        return new LogEventBuilder(_logger, newEntry);
    }

    public LogEventBuilder SetStatusCode(int statusCode)
    {
        var newEntry = CloneEntry(_entry);
        newEntry.StatusCode = statusCode;
        return new LogEventBuilder(_logger, newEntry);
    }

    private LogEventBuilder SetDescription(string description, bool isEncrypted = false)
    {
        var newEntry = CloneEntry(_entry);
        newEntry.Description = isEncrypted ? EncryptionService.Encrypt(description) : description;
        return new LogEventBuilder(_logger, newEntry);
    }

    public LogEventBuilder SetRequestPath(string requestPath, bool isEncrypted = false)
    {
        var newEntry = CloneEntry(_entry);
        newEntry.RequestPath = isEncrypted ? EncryptionService.Encrypt(requestPath) : requestPath;
        return new LogEventBuilder(_logger, newEntry);
    }

    public LogEventBuilder SetCorrelationId(string correlationId, bool isEncrypted = false)
    {
        var newEntry = CloneEntry(_entry);
        newEntry.CorrelationId = isEncrypted ? EncryptionService.Encrypt(correlationId) : correlationId;
        return new LogEventBuilder(_logger, newEntry);
    }

    public LogEventBuilder SetAdditionalData(string additionalData, bool isEncrypted = false)
    {
        var newEntry = CloneEntry(_entry);
        newEntry.AdditionalData = isEncrypted ? EncryptionService.Encrypt(additionalData) : additionalData;
        return new LogEventBuilder(_logger, newEntry);
    }

    public LogEventBuilder SetException(Exception exception, bool isEncrypted = false)
    {
        var newEntry = CloneEntry(_entry);
        newEntry.Message = isEncrypted
            ? EncryptionService.Encrypt($"{exception.Message} {exception.InnerException}")
            : $"{exception.Message} {exception.InnerException}";
        newEntry.AdditionalData = isEncrypted
            ? EncryptionService.Encrypt(JsonConvert.SerializeObject(exception, Formatting.Indented))
            : JsonConvert.SerializeObject(exception, Formatting.Indented);
        newEntry.Level = LogLevel.Exception;
        return new LogEventBuilder(_logger, newEntry);
    }

    public LogEventBuilder AddLabel(string label, bool isEncrypted = false)
    {
        var newEntry = CloneEntry(_entry);
        newEntry.Label.Add(isEncrypted ? EncryptionService.Encrypt(label) : label);
        return new LogEventBuilder(_logger, newEntry);
    }

    public LogEventBuilder AddAttribute(string key, string value, bool isEncrypted = false)
    {
        var newEntry = CloneEntry(_entry);
        var fkey = isEncrypted ? EncryptionService.Encrypt(key) : key;
        var fvalue = isEncrypted ? EncryptionService.Encrypt(value) : value;
        newEntry.AttributesS[fkey] = fvalue;
        return new LogEventBuilder(_logger, newEntry);
    }

    public LogEventBuilder AddAttribute(string key, int value, bool isEncrypted = false)
    {
        var newEntry = CloneEntry(_entry);
        var fkey = isEncrypted ? EncryptionService.Encrypt(key) : key;
        newEntry.AttributesN[fkey] = value;
        return new LogEventBuilder(_logger, newEntry);
    }

    public LogEventBuilder AddAttribute(string key, bool value, bool isEncrypted = false)
    {
        var newEntry = CloneEntry(_entry);
        var fkey = isEncrypted ? EncryptionService.Encrypt(key) : key;
        newEntry.AttributesB[fkey] = value;
        return new LogEventBuilder(_logger, newEntry);
    }

    public LogEventBuilder AddAttribute(string key, DateTime value, bool isEncrypted = false)
    {
        var newEntry = CloneEntry(_entry);
        var fkey = isEncrypted ? EncryptionService.Encrypt(key) : key;
        newEntry.AttributesD[fkey] = value;
        return new LogEventBuilder(_logger, newEntry);
    }

    public LogEventBuilder SetGuid(string guid)
    {
        var newEntry = CloneEntry(_entry);
        newEntry.Guid = guid;
        return new LogEventBuilder(_logger, newEntry);
    }

    private static Log CloneEntry(Log original)
    {
        return new Log
        {
            ID = original.ID,
            ApiKey = original.ApiKey,
            Guid = original.Guid, // Fix: Include Guid in clone
            Timestamp = original.Timestamp,
            Collection = original.Collection,
            Application = original.Application,
            Environment = original.Environment,
            Level = original.Level,
            Message = original.Message,
            Exception = original.Exception,
            StackTrace = original.StackTrace,
            Source = original.Source,
            UserId = original.UserId,
            UserEmail = original.UserEmail,
            CorrelationId = original.CorrelationId,
            RequestPath = original.RequestPath,
            HttpMethod = original.HttpMethod,
            AdditionalData = original.AdditionalData,
            IpAddress = original.IpAddress,
            StatusCode = original.StatusCode,
            Description = original.Description,
            Label = [.. original.Label],
            AttributesS = new Dictionary<string, string>(original.AttributesS),
            AttributesN = new Dictionary<string, double>(original.AttributesN),
            AttributesB = new Dictionary<string, bool>(original.AttributesB),
            AttributesD = new Dictionary<string, DateTime>(original.AttributesD),
        };
    }

    public LogEventBuilder SetCollection(string collection, bool isEncrypted = false)
    {
        var newEntry = CloneEntry(_entry);
        newEntry.Collection = isEncrypted ? EncryptionService.Encrypt(collection) : collection;
        return new LogEventBuilder(_logger, newEntry);
    }

    public LogEventBuilder Encrypt()
    {
        var newEntry = CloneEntry(_entry);
        newEntry._IsEncrypted = true;
        return new LogEventBuilder(_logger, newEntry);
    }

    public async Task Log()
    {
        if (_entry._IsEncrypted)
        {
            if (!String.IsNullOrEmpty(_entry.Message) && !_entry.Message.StartsWith("encrypted_by_logdb"))
                _entry.Message = EncryptionService.Encrypt(_entry.Message);
            if (!String.IsNullOrEmpty(_entry.AdditionalData) && !_entry.AdditionalData.StartsWith("encrypted_by_logdb"))
                _entry.AdditionalData = EncryptionService.Encrypt(_entry.AdditionalData);
            if (!String.IsNullOrEmpty(_entry.Application) && !_entry.Application.StartsWith("encrypted_by_logdb"))
                _entry.Application = EncryptionService.Encrypt(_entry.Application);
            if (!String.IsNullOrEmpty(_entry.CorrelationId) && !_entry.CorrelationId.StartsWith("encrypted_by_logdb"))
                _entry.CorrelationId = EncryptionService.Encrypt(_entry.CorrelationId);
            if (!String.IsNullOrEmpty(_entry.Description) && !_entry.Description.StartsWith("encrypted_by_logdb"))
                _entry.Description = EncryptionService.Encrypt(_entry.Description);
            if (!String.IsNullOrEmpty(_entry.Environment) && !_entry.Environment.StartsWith("encrypted_by_logdb"))
                _entry.Environment = EncryptionService.Encrypt(_entry.Environment);
            if (!String.IsNullOrEmpty(_entry.RequestPath) && !_entry.RequestPath.StartsWith("encrypted_by_logdb"))
                _entry.RequestPath = EncryptionService.Encrypt(_entry.RequestPath);
            if (!String.IsNullOrEmpty(_entry.Source) && !_entry.Source.StartsWith("encrypted_by_logdb"))
                _entry.Source = EncryptionService.Encrypt(_entry.Source);
            if (!String.IsNullOrEmpty(_entry.Collection) && !_entry.Collection.StartsWith("encrypted_by_logdb"))
                _entry.Collection = EncryptionService.Encrypt(_entry.Collection);
            if (!String.IsNullOrEmpty(_entry.IpAddress) && !_entry.IpAddress.StartsWith("encrypted_by_logdb"))
                _entry.IpAddress = EncryptionService.Encrypt(_entry.IpAddress);
            for (var i = 0;i < _entry.Label.Count;i++)
            {
                if (!_entry.Label[i].StartsWith("encrypted_by_logdb"))
                    _entry.Label[i] = EncryptionService.Encrypt(_entry.Label[i]);
            }
            
            foreach (var key in _entry.AttributesS.Keys.ToList())
            {
                if (!key.StartsWith("encrypted_by_logdb"))
                {
                    var value = _entry.AttributesS[key];
                    _entry.AttributesS.Remove(key);
                    _entry.AttributesS[EncryptionService.Encrypt(key)] = EncryptionService.Encrypt(value);
                }
            }
            
            foreach (var key in _entry.AttributesB.Keys.ToList())
            {
                if (!key.StartsWith("encrypted_by_logdb"))
                {
                    var value = _entry.AttributesB[key];
                    _entry.AttributesB.Remove(key);
                    _entry.AttributesB[EncryptionService.Encrypt(key)] = value;
                }
            }
            
            foreach (var key in _entry.AttributesD.Keys.ToList())
            {
                if (!key.StartsWith("encrypted_by_logdb"))
                {
                    var value = _entry.AttributesD[key];
                    _entry.AttributesD.Remove(key);
                    _entry.AttributesD[EncryptionService.Encrypt(key)] = value;
                }
            }
            
            foreach (var key in _entry.AttributesN.Keys.ToList())
            {
                if (!key.StartsWith("encrypted_by_logdb"))
                {
                    var value = _entry.AttributesN[key];
                    _entry.AttributesN.Remove(key);
                    _entry.AttributesN[EncryptionService.Encrypt(key)] = value;
                }
            }
        }
        
        await _logger.Log(_entry);
    }
}
