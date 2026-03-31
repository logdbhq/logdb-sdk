using System;
using com.logdb.logger;
using LogDB.Client.Models;
using com.logdb.LogDB;

namespace com.logdb.LogDB.LogBuilders
{
    [Obsolete("LogRelation is coming soon and is currently disabled in the public SDK.")]
    public sealed class LogRelationBuilder
    {
        private readonly ILogger _logger;
        private readonly LogRelation _entry;

        private LogRelationBuilder(ILogger logger, LogRelation entry)
        {
            _logger = logger;
            _entry = entry;
        }

        public LogRelationBuilder SetCollection(string collection)
        {
            var newEntry = CloneEntry(_entry);
            newEntry.Collection = collection;
            return new LogRelationBuilder(_logger, newEntry);
        }

        public static LogRelationBuilder Create(ILogger logger)
        {
            throw new NotSupportedException("LogRelationBuilder is marked [Soon] and is not available in this public SDK build yet.");
        }
        
        public LogRelationBuilder SetOrigin(string origin)
        {
            var newEntry = CloneEntry(_entry);
            newEntry.Origin = origin;
            return new LogRelationBuilder(_logger, newEntry);
        }

        public LogRelationBuilder SetRelation(string relation)
        {
            var newEntry = CloneEntry(_entry);
            newEntry.Relation = relation;
            return new LogRelationBuilder(_logger, newEntry);
        }

        public LogRelationBuilder SetSubject(string subject)
        {
            var newEntry = CloneEntry(_entry);
            newEntry.Subject = subject;
            return new LogRelationBuilder(_logger, newEntry);
        }

        public LogRelationBuilder SetDateIn(DateTime dateIn)
        {
            var newEntry = CloneEntry(_entry);
            newEntry.DateIn = dateIn;
            return new LogRelationBuilder(_logger, newEntry);
        }

        public LogRelationBuilder SetOriginProperty(string key, object value)
        {
            var newEntry = CloneEntry(_entry);
            newEntry.OriginProperties = new Dictionary<string, object>(newEntry.OriginProperties!) { [key] = value };
            return new LogRelationBuilder(_logger, newEntry);
        }

        public LogRelationBuilder SetSubjectProperty(string key, object value)
        {
            var newEntry = CloneEntry(_entry);
            newEntry.SubjectProperties = new Dictionary<string, object>(newEntry.SubjectProperties!) { [key] = value };
            return new LogRelationBuilder(_logger, newEntry);
        }

        public LogRelationBuilder SetRelationProperty(string key, object value)
        {
            var newEntry = CloneEntry(_entry);
            newEntry.RelationProperties = new Dictionary<string, object>(newEntry.RelationProperties!)
                { [key] = value };
            return new LogRelationBuilder(_logger, newEntry);
        }

        private static LogRelation CloneEntry(LogRelation original)
        {
            return new LogRelation()
            {
                ApiKey = original.ApiKey,
                Collection = original.Collection,
                Origin = original.Origin,
                CustomerId = original.CustomerId,
                Guid = original.Guid,
                Relation = original.Relation,
                Subject = original.Subject,
                DateIn = original.DateIn,
                OriginProperties = original.OriginProperties,
                SubjectProperties = original.SubjectProperties,
                RelationProperties = original.RelationProperties,
            };
        }


        public async Task Log()
        {
            await Task.FromException(new NotSupportedException(
                "LogRelationBuilder.Log() is marked [Soon] and is not available in this public SDK build yet."));
        }
    }
}
