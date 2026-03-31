namespace LogDB.NLog
{
    /// <summary>
    /// Defines which LogDB record type should be written for a log event.
    /// </summary>
    public enum LogDBPayloadType
    {
        Log = 0,
        Cache = 1,
        Beat = 2
    }
}
