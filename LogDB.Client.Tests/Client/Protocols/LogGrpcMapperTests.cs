using System;
using System.Collections.Generic;
using Xunit;
using LogDB.Client.Models;
using LogDB.Extensions.Logging;

namespace LogDB.Client.Tests.Client.Protocols;

public class LogGrpcMapperTests
{
    [Fact]
    public void ToGrpc_MapsLogFieldsAndAttributes()
    {
        var timestamp = new DateTime(2026, 3, 31, 12, 34, 56, DateTimeKind.Utc);
        var log = new Log
        {
            Guid = "log-guid",
            Timestamp = timestamp,
            Collection = "orders",
            Application = "Orders.Api",
            Environment = "prod",
            Level = LogLevel.Warning,
            Message = "Order processed",
            Exception = "none",
            StackTrace = "stack",
            Source = "OrderService",
            UserId = 42,
            UserEmail = "user@example.com",
            CorrelationId = "corr-1",
            RequestPath = "/api/orders",
            HttpMethod = "POST",
            AdditionalData = "details",
            IpAddress = "127.0.0.1",
            StatusCode = 201,
            Description = "desc",
            Label = new List<string> { "audit", "order" },
            AttributesS = new Dictionary<string, string> { ["tenant"] = "acme" },
            AttributesN = new Dictionary<string, double> { ["duration_ms"] = 12.5 },
            AttributesB = new Dictionary<string, bool> { ["success"] = true },
            AttributesD = new Dictionary<string, DateTime> { ["created_at"] = timestamp }
        };

        var grpc = log.ToGrpc();

        Assert.Equal("log-guid", grpc.Guid);
        Assert.Equal(timestamp.ToString("o"), grpc.Timestamp);
        Assert.Equal("orders", grpc.Collection);
        Assert.Equal("Orders.Api", grpc.Application);
        Assert.Equal("prod", grpc.Environment);
        Assert.Equal("Warning", grpc.Level);
        Assert.Equal("Order processed", grpc.Message);
        Assert.Equal("none", grpc.Exception);
        Assert.Equal("stack", grpc.StackTrace);
        Assert.Equal("OrderService", grpc.Source);
        Assert.Equal(42, grpc.UserId);
        Assert.Equal("user@example.com", grpc.UserEmail);
        Assert.Equal("corr-1", grpc.CorrelationId);
        Assert.Equal("/api/orders", grpc.RequestPath);
        Assert.Equal("POST", grpc.HttpMethod);
        Assert.Equal("details", grpc.AdditionalData);
        Assert.Equal("127.0.0.1", grpc.IpAddress);
        Assert.Equal(201, grpc.StatusCode);
        Assert.Equal("desc", grpc.Description);
        Assert.Contains("audit", grpc.Label);
        Assert.Equal("acme", grpc.AttributesS["tenant"]);
        Assert.Equal(12.5, grpc.AttributesN["duration_ms"]);
        Assert.True(grpc.AttributesB["success"]);
        Assert.Equal(timestamp, grpc.AttributesD["created_at"].ToDateTime());
    }

    [Fact]
    public void ToGrpc_MapsRelationPropertiesAndDate()
    {
        var relationDate = new DateTime(2026, 3, 31, 9, 0, 0, DateTimeKind.Utc);
        var relation = new LogRelation
        {
            Collection = "orders",
            Origin = "user:1",
            Relation = "purchased",
            Subject = "product:2",
            Guid = "rel-guid",
            DateIn = relationDate,
            OriginProperties = new Dictionary<string, object> { ["tier"] = "premium" },
            SubjectProperties = new Dictionary<string, object> { ["sku"] = 12345 },
            RelationProperties = new Dictionary<string, object> { ["quantity"] = 2 }
        };
        relation.ApiKey = "secret-key";

        var grpc = relation.ToGrpc();

        Assert.Equal("secret-key", grpc.Apikey);
        Assert.Equal("orders", grpc.Collection);
        Assert.Equal("user:1", grpc.Origin);
        Assert.Equal("purchased", grpc.Relation);
        Assert.Equal("product:2", grpc.Subject);
        Assert.Equal("rel-guid", grpc.Guid);
        Assert.Equal(relationDate, grpc.DateIn.ToDateTime());
        Assert.Contains(grpc.OriginProperties, item => item.Key == "tier" && item.Value == "premium");
        Assert.Contains(grpc.SubjectProperties, item => item.Key == "sku" && item.Value == "12345");
        Assert.Contains(grpc.RelationProperties, item => item.Key == "quantity" && item.Value == "2");
    }
}
