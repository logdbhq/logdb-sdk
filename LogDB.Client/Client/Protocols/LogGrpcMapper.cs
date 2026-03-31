using System;
using System.Collections.Generic;
using System.Linq;
using com.logdb.grpc.logger;
using Google.Protobuf.WellKnownTypes;
using LogDB.Client.Models;

namespace LogDB.Extensions.Logging
{
    /// <summary>
    /// Zero-dependency static mapper from SDK models to protobuf gRPC request types.
    /// Replaces AutoMapper to eliminate the external dependency and its vulnerability warnings.
    /// </summary>
    internal static class LogGrpcMapper
    {
        public static LogGrpcRequest ToGrpc(this Log src)
        {
            var dest = new LogGrpcRequest
            {
                // ApiKey is set by the caller after mapping (protobuf field quirk)
                Guid        = src.Guid        ?? Guid.NewGuid().ToString(),
                Timestamp   = src.Timestamp.ToString("o"),
                Collection  = src.Collection  ?? string.Empty,
                Application = src.Application ?? string.Empty,
                Environment = src.Environment ?? string.Empty,
                Level       = src.Level.ToString(),
                Message     = src.Message     ?? string.Empty,
                Exception   = src.Exception   ?? string.Empty,
                StackTrace  = src.StackTrace  ?? string.Empty,
                Source      = src.Source      ?? string.Empty,
                UserId      = src.UserId      ?? 0,
                UserEmail   = src.UserEmail   ?? string.Empty,
                CorrelationId = src.CorrelationId ?? string.Empty,
                RequestPath = src.RequestPath ?? string.Empty,
                HttpMethod  = src.HttpMethod  ?? string.Empty,
                AdditionalData = src.AdditionalData ?? string.Empty,
                IpAddress   = src.IpAddress   ?? string.Empty,
                StatusCode  = src.StatusCode  ?? 0,
                Description = src.Description ?? string.Empty,
            };

            if (src.Label != null)
                dest.Label.AddRange(src.Label);

            if (src.AttributesB != null)
                foreach (var kvp in src.AttributesB)
                    dest.AttributesB[kvp.Key] = kvp.Value;

            if (src.AttributesS != null)
                foreach (var kvp in src.AttributesS)
                    dest.AttributesS[kvp.Key] = kvp.Value;

            if (src.AttributesD != null)
                foreach (var kvp in src.AttributesD)
                    dest.AttributesD[kvp.Key] = Timestamp.FromDateTime(
                        DateTime.SpecifyKind(kvp.Value, DateTimeKind.Utc));

            if (src.AttributesN != null)
                foreach (var kvp in src.AttributesN)
                    dest.AttributesN[kvp.Key] = kvp.Value;

            return dest;
        }

        public static LogPointGrpcRequest ToGrpc(this LogPoint src)
        {
            var dest = new LogPointGrpcRequest
            {
                Collection  = src.Collection  ?? string.Empty,
                Apikey      = src.ApiKey      ?? string.Empty,
                Guid        = src.Guid        ?? string.Empty,
                Measurement = src.Measurement ?? string.Empty,
                Timestamp   = src.Timestamp.ToString("o"),
            };

            if (src.Tag != null)
                dest.Tag.AddRange(src.Tag.Select(m => m.ToGrpc()));

            if (src.Field != null)
                dest.Field.AddRange(src.Field.Select(m => m.ToGrpc()));

            return dest;
        }

        public static LogPointGrpcRequest ToGrpc(this LogBeat src)
        {
            var dest = new LogPointGrpcRequest
            {
                Collection  = src.Collection  ?? string.Empty,
                Apikey      = src.ApiKey      ?? string.Empty,
                Guid        = src.Guid        ?? string.Empty,
                Measurement = src.Measurement ?? string.Empty,
                Timestamp   = src.Timestamp.ToString("o"),
            };

            if (src.Tag != null)
                dest.Tag.AddRange(src.Tag.Select(m => m.ToGrpc()));

            if (src.Field != null)
                dest.Field.AddRange(src.Field.Select(m => m.ToGrpc()));

            return dest;
        }

        public static LogCacheGrpcRequest ToGrpc(this LogCache src)
        {
            return new LogCacheGrpcRequest
            {
                Key    = src.Key    ?? string.Empty,
                Value  = src.Value  ?? string.Empty,
                Guid   = src.Guid   ?? string.Empty,
                Apikey = src.ApiKey ?? string.Empty,
            };
        }

        public static LogMetaGrpc ToGrpc(this LogMeta src)
        {
            return new LogMetaGrpc
            {
                Key   = src.Key   ?? string.Empty,
                Value = src.Value ?? string.Empty,
            };
        }

        public static LogRelationGrpcRequest ToGrpc(this LogRelation src)
        {
            var dest = new LogRelationGrpcRequest
            {
                Apikey     = src.ApiKey     ?? string.Empty,
                Collection = src.Collection ?? string.Empty,
                Origin     = src.Origin     ?? string.Empty,
                Relation   = src.Relation   ?? string.Empty,
                Subject    = src.Subject    ?? string.Empty,
                Guid       = src.Guid       ?? string.Empty,
            };

            if (src.DateIn.HasValue)
                dest.DateIn = Timestamp.FromDateTime(src.DateIn.Value.ToUniversalTime());

            if (src.OriginProperties != null)
                dest.OriginProperties.AddRange(
                    src.OriginProperties.Select(kvp => new LogMetaGrpc { Key = kvp.Key, Value = kvp.Value?.ToString() ?? string.Empty }));

            if (src.SubjectProperties != null)
                dest.SubjectProperties.AddRange(
                    src.SubjectProperties.Select(kvp => new LogMetaGrpc { Key = kvp.Key, Value = kvp.Value?.ToString() ?? string.Empty }));

            if (src.RelationProperties != null)
                dest.RelationProperties.AddRange(
                    src.RelationProperties.Select(kvp => new LogMetaGrpc { Key = kvp.Key, Value = kvp.Value?.ToString() ?? string.Empty }));

            return dest;
        }
    }
}
