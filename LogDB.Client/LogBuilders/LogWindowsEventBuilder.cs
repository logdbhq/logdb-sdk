using com.logdb.LogDB;
using LogDB.Client.Models;
using LogDB.Client.Services;

namespace com.logdb.logger.LogBuilders;

public sealed class LogWindowsEventBuilder
{
    private static LoggerContext _context = new();
    private readonly LogWindowsEvent _entry;
    private static Logger? _logger;

    private LogWindowsEventBuilder(LogWindowsEvent entry)
    {
        _entry = entry;
    }

    public static string? Environment { get; set; }
    public static string? Application { get; set; }
    public static string? Collection { get; set; }

    public static string ApiKey
    {
        get => _context.ApiKey;
        set
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("ApiKey cannot be null or empty.", nameof(value));
            _context.ApiKey = value;
        }
    }

    private static Logger GetOrCreateLogger()
    {
        if (string.IsNullOrWhiteSpace(_context.ApiKey))
            throw new InvalidOperationException("LogWindowsEventBuilder.ApiKey must be set before logging.");

        return _logger ??= Logger.Create(_context.ApiKey);
    }

    public static LogWindowsEventBuilder Create()
    {
        if (string.IsNullOrWhiteSpace(_context.ApiKey))
            throw new InvalidOperationException("LogWindowsEventBuilder.ApiKey must be set before Create().");

        return new LogWindowsEventBuilder(new LogWindowsEvent(_context.ApiKey, Guid.NewGuid().ToString())
        {
            Timestamp = DateTime.UtcNow
        });
    }

    public LogWindowsEvent Build() => _entry;

    private static LogWindowsEvent CloneEntry(LogWindowsEvent original)
    {
        return new LogWindowsEvent(original.ApiKey, original.Guid)
        {
            Collection = original.Collection,
            Environment = original.Environment,
            Application = original.Application,
            Timestamp = original.Timestamp,
            EventId = original.EventId,
            ProviderName = original.ProviderName,
            Channel = original.Channel,
            Task = original.Task,
            Opcode = original.Opcode,
            Level = original.Level,
            Keywords = original.Keywords,
            Computer = original.Computer,
            UserId = original.UserId,
            Message = original.Message,
            XmlData = original.XmlData,
            IpAddress = original.IpAddress
        };
    }

    public LogWindowsEventBuilder SetTimestamp(DateTime timestamp)
    {
        var newEntry = CloneEntry(_entry);
        newEntry.Timestamp = timestamp;
        return new LogWindowsEventBuilder(newEntry);
    }

    public LogWindowsEventBuilder SetCollection(string? collection)
    {
        var newEntry = CloneEntry(_entry);
        newEntry.Collection = collection;
        return new LogWindowsEventBuilder(newEntry);
    }

    public LogWindowsEventBuilder SetEnvironment(string environment)
    {
        var newEntry = CloneEntry(_entry);
        newEntry.Environment = environment;
        return new LogWindowsEventBuilder(newEntry);
    }

    public LogWindowsEventBuilder SetApplication(string application)
    {
        var newEntry = CloneEntry(_entry);
        newEntry.Application = application;
        return new LogWindowsEventBuilder(newEntry);
    }

    public LogWindowsEventBuilder SetEventId(long eventId)
    {
        var newEntry = CloneEntry(_entry);
        newEntry.EventId = eventId;
        return new LogWindowsEventBuilder(newEntry);
    }

    public LogWindowsEventBuilder SetProviderName(string providerName)
    {
        var newEntry = CloneEntry(_entry);
        newEntry.ProviderName = providerName;
        return new LogWindowsEventBuilder(newEntry);
    }

    public LogWindowsEventBuilder SetChannel(string channel)
    {
        var newEntry = CloneEntry(_entry);
        newEntry.Channel = channel;
        return new LogWindowsEventBuilder(newEntry);
    }

    public LogWindowsEventBuilder SetTask(string task)
    {
        var newEntry = CloneEntry(_entry);
        newEntry.Task = task;
        return new LogWindowsEventBuilder(newEntry);
    }

    public LogWindowsEventBuilder SetOpcode(string opcode)
    {
        var newEntry = CloneEntry(_entry);
        newEntry.Opcode = opcode;
        return new LogWindowsEventBuilder(newEntry);
    }

    public LogWindowsEventBuilder SetLevel(string level)
    {
        var newEntry = CloneEntry(_entry);
        newEntry.Level = level;
        return new LogWindowsEventBuilder(newEntry);
    }

    public LogWindowsEventBuilder SetKeywords(string keywords)
    {
        var newEntry = CloneEntry(_entry);
        newEntry.Keywords = keywords;
        return new LogWindowsEventBuilder(newEntry);
    }

    public LogWindowsEventBuilder SetComputer(string computer, bool isEncrypted = false)
    {
        var newEntry = CloneEntry(_entry);
        newEntry.Computer = isEncrypted ? EncryptionService.Encrypt(computer) : computer;
        return new LogWindowsEventBuilder(newEntry);
    }

    public LogWindowsEventBuilder SetUserId(string userId, bool isEncrypted = false)
    {
        var newEntry = CloneEntry(_entry);
        newEntry.UserId = isEncrypted ? EncryptionService.Encrypt(userId) : userId;
        return new LogWindowsEventBuilder(newEntry);
    }

    public LogWindowsEventBuilder SetMessage(string message, bool isEncrypted = false)
    {
        var newEntry = CloneEntry(_entry);
        newEntry.Message = isEncrypted ? EncryptionService.Encrypt(message) : message;
        return new LogWindowsEventBuilder(newEntry);
    }

    public LogWindowsEventBuilder SetXmlData(string xmlData, bool isEncrypted = false)
    {
        var newEntry = CloneEntry(_entry);
        newEntry.XmlData = isEncrypted ? EncryptionService.Encrypt(xmlData) : xmlData;
        return new LogWindowsEventBuilder(newEntry);
    }

    public LogWindowsEventBuilder SetIpAddress(string ipAddress, bool isEncrypted = false)
    {
        var newEntry = CloneEntry(_entry);
        newEntry.IpAddress = isEncrypted ? EncryptionService.Encrypt(ipAddress) : ipAddress;
        return new LogWindowsEventBuilder(newEntry);
    }

    public LogWindowsEventBuilder Encrypt()
    {
        var newEntry = CloneEntry(_entry);
        newEntry._IsEncrypted = true;
        return new LogWindowsEventBuilder(newEntry);
    }

    public LogWindowsEventBuilder SetGuid(string guid)
    {
        var newEntry = CloneEntry(_entry);
        newEntry.Guid = guid;
        return new LogWindowsEventBuilder(newEntry);
    }

    public async Task Log()
    {
        if (string.IsNullOrEmpty(_entry.Environment) && !string.IsNullOrEmpty(Environment))
            _entry.Environment = Environment;
        if (string.IsNullOrEmpty(_entry.Application) && !string.IsNullOrEmpty(Application))
            _entry.Application = Application;
        if (string.IsNullOrEmpty(_entry.Collection))
            _entry.Collection = Collection ?? "default";

        _entry.ApiKey = ApiKey;

        if (string.IsNullOrEmpty(_entry.Guid))
            _entry.Guid = Guid.NewGuid().ToString();

        var logger = GetOrCreateLogger();
        await logger.Log(_entry.ToLog()).ConfigureAwait(false);
    }
}
