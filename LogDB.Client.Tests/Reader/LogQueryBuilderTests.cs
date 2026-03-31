using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using LogDB.Client.Tests.TestDoubles;
using LogDB.Extensions.Logging;

namespace LogDB.Client.Tests.Reader;

public class LogQueryBuilderTests
{
    [Fact]
    public async Task QueryLogs_ExecuteAsync_ForwardsFiltersAndPaging()
    {
        var reader = new FakeLogDBReader
        {
            NextLogsResult = new PagedResult<LogDto> { TotalCount = 7 }
        };

        var result = await new LogQueryBuilder(reader)
            .FromApplication("Orders.Api")
            .InEnvironment("prod")
            .WithLevel("Error")
            .InCollection("app-logs")
            .WithCorrelationId("corr-1")
            .FromSource("OrdersController")
            .ByUser("user@example.com")
            .ByUserId(42)
            .WithHttpMethod("POST")
            .WithRequestPath("/orders")
            .FromIpAddress("127.0.0.1")
            .WithStatusCode(500)
            .Containing("failed")
            .OnlyExceptions()
            .WithLabel("critical")
            .WithLabels("api", "orders")
            .WithAttribute("tenant", "acme")
            .Skip(10)
            .Take(25)
            .OrderBy("Timestamp", ascending: true)
            .ExecuteAsync();

        Assert.Equal(7, result.TotalCount);
        Assert.NotNull(reader.LastLogQuery);
        Assert.Equal("Orders.Api", reader.LastLogQuery!.Application);
        Assert.Equal("prod", reader.LastLogQuery.Environment);
        Assert.Equal("Error", reader.LastLogQuery.Level);
        Assert.Equal("app-logs", reader.LastLogQuery.Collection);
        Assert.Equal("corr-1", reader.LastLogQuery.CorrelationId);
        Assert.Equal("OrdersController", reader.LastLogQuery.Source);
        Assert.Equal("user@example.com", reader.LastLogQuery.UserEmail);
        Assert.Equal(42, reader.LastLogQuery.UserId);
        Assert.Equal("POST", reader.LastLogQuery.HttpMethod);
        Assert.Equal("/orders", reader.LastLogQuery.RequestPath);
        Assert.Equal("127.0.0.1", reader.LastLogQuery.IpAddress);
        Assert.Equal(500, reader.LastLogQuery.StatusCode);
        Assert.Equal("failed", reader.LastLogQuery.SearchString);
        Assert.True(reader.LastLogQuery.IsException);
        Assert.Equal(10, reader.LastLogQuery.Skip);
        Assert.Equal(25, reader.LastLogQuery.Take);
        Assert.Equal("Timestamp", reader.LastLogQuery.SortField);
        Assert.True(reader.LastLogQuery.SortAscending);
        Assert.Contains("critical", reader.LastLogQuery.Labels!);
        Assert.Contains("api", reader.LastLogQuery.Labels!);
        Assert.Contains("orders", reader.LastLogQuery.Labels!);
        Assert.Equal("acme", reader.LastLogQuery.AttributeFilters!["tenant"]);
    }

    [Fact]
    public async Task QueryLogs_FirstOrDefaultAsync_UsesTakeOneAndReturnsFirstItem()
    {
        var reader = new FakeLogDBReader
        {
            NextLogsResult = new PagedResult<LogDto>
            {
                Items = new List<LogDto> { new() { Id = "first" }, new() { Id = "second" } }
            }
        };

        var item = await new LogQueryBuilder(reader)
            .Take(99)
            .FirstOrDefaultAsync();

        Assert.Equal("first", item!.Id);
        Assert.Equal(1, reader.LastLogQuery!.Take);
    }

    [Fact]
    public async Task QueryLogs_CountAsync_UsesTakeOneAndReturnsTotalCount()
    {
        var reader = new FakeLogDBReader
        {
            NextLogsResult = new PagedResult<LogDto> { TotalCount = 123 }
        };

        var count = await new LogQueryBuilder(reader).Take(250).CountAsync();

        Assert.Equal(123, count);
        Assert.Equal(1, reader.LastLogQuery!.Take);
    }

    [Fact]
    public async Task QueryCache_ExecuteAndGet_ForwardArguments()
    {
        var reader = new FakeLogDBReader
        {
            NextCachesResult = new PagedResult<LogCacheDto> { TotalCount = 2 },
            NextCacheResult = new LogCacheDto { Key = "session:1" }
        };

        var query = new LogCacheQueryBuilder(reader)
            .WithKeyPattern("session:*")
            .InCollection("sessions")
            .Skip(5)
            .Take(10)
            .OrderBy("UpdatedAt", ascending: true);

        var paged = await query.ExecuteAsync();
        var item = await query.GetAsync("session:1");

        Assert.Equal(2, paged.TotalCount);
        Assert.Equal("session:*", reader.LastCacheQuery!.KeyPattern);
        Assert.Equal("sessions", reader.LastCacheQuery.Collection);
        Assert.Equal(5, reader.LastCacheQuery.Skip);
        Assert.Equal(10, reader.LastCacheQuery.Take);
        Assert.Equal("UpdatedAt", reader.LastCacheQuery.SortField);
        Assert.True(reader.LastCacheQuery.SortAscending);
        Assert.Equal("session:1", reader.LastCacheKey);
        Assert.Equal("session:1", item!.Key);
    }

    [Fact]
    public async Task QueryLogPoints_ExecuteAsync_ForwardsMeasurementAndTagFilter()
    {
        var reader = new FakeLogDBReader
        {
            NextPointsResult = new PagedResult<LogPointDto> { TotalCount = 1 }
        };

        var result = await new LogPointQueryBuilder(reader)
            .ForMeasurement("cpu_usage")
            .InCollection("metrics")
            .WithTag("host", "api-1")
            .Skip(2)
            .Take(3)
            .ExecuteAsync();

        Assert.Equal(1, result.TotalCount);
        Assert.Equal("cpu_usage", reader.LastPointQuery!.Measurement);
        Assert.Equal("metrics", reader.LastPointQuery.Collection);
        Assert.Equal("api-1", reader.LastPointQuery.TagFilters!["host"]);
        Assert.Equal(2, reader.LastPointQuery.Skip);
        Assert.Equal(3, reader.LastPointQuery.Take);
    }

    [Fact]
    public async Task QueryRelations_ExecuteAndGetRelated_ForwardArguments()
    {
        var reader = new FakeLogDBReader
        {
            NextRelationsResult = new PagedResult<LogRelationDto> { TotalCount = 4 },
            NextRelatedEntities = new List<RelatedEntityDto> { new() { Entity = "product:1" } }
        };

        var query = new LogRelationQueryBuilder(reader)
            .FromOrigin("user:1")
            .ToSubject("product:1")
            .WithRelation("purchased")
            .InCollection("orders")
            .Skip(7)
            .Take(8);

        var paged = await query.ExecuteAsync();
        var related = await query.GetRelatedAsync("user:1", direction: "outgoing", depth: 2);

        Assert.Equal(4, paged.TotalCount);
        Assert.Equal("user:1", reader.LastRelationQuery!.Origin);
        Assert.Equal("product:1", reader.LastRelationQuery.Subject);
        Assert.Equal("purchased", reader.LastRelationQuery.Relation);
        Assert.Equal("orders", reader.LastRelationQuery.Collection);
        Assert.Equal(7, reader.LastRelationQuery.Skip);
        Assert.Equal(8, reader.LastRelationQuery.Take);
        Assert.Equal(("user:1", "outgoing", "purchased", 2), reader.LastRelatedEntitiesRequest);
        Assert.Single(related);
        Assert.Equal("product:1", related.Single().Entity);
    }
}
