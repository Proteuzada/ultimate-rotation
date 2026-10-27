using System.Net;
using System.Text;
using Xunit;

namespace UltimateRotation.Tests;

public class FflogsClientTests
{
    private const string Secret = "super-secret-value";

    [Fact]
    public async Task UsesOfficialEndpointsAndDoesNotSendTheSecretToGraphql()
    {
        var handler = new ScriptedHandler();
        handler.When(FflogsClient.TokenEndpoint, _ => Json(HttpStatusCode.OK, """
            {"access_token":"token-1","expires_in":3600,"token_type":"Bearer"}
            """));
        handler.When(FflogsClient.GraphqlEndpoint, request =>
        {
            Assert.Equal("Bearer", request.Authorization?.Scheme);
            Assert.Equal("token-1", request.Authorization?.Parameter);
            Assert.DoesNotContain(Secret, request.Body, StringComparison.Ordinal);
            Assert.Contains("characterRankings", request.Body, StringComparison.Ordinal);
            Assert.Contains("metric: rdps", request.Body, StringComparison.Ordinal);
            Assert.DoesNotContain("playerspeed", request.Body, StringComparison.Ordinal);
            Assert.Contains("specName", request.Body, StringComparison.Ordinal);
            Assert.Contains("Reaper", request.Body, StringComparison.Ordinal);
            Assert.DoesNotContain("fightRankings", request.Body, StringComparison.Ordinal);
            Assert.DoesNotContain("/zone/rankings", request.Body, StringComparison.Ordinal);
            return Json(HttpStatusCode.OK, """
                {"data":{"worldData":{"encounter":{"name":"Futures Rewritten","characterRankings":{"rankings":[
                  {"rank":40,"rankPercent":99.4,"name":"Example","report":{"code":"abcdefghijklmnop","fightID":12},"server":{"name":"Leviathan","region":"NA"}},
                  {"rank":3,"rankPercent":100,"name":"Other","report":{"code":"bbcdefghijklmnop","fightID":4},"server":{"name":"Sargatanas","region":"NA"}},
                  {"rank":80,"rankPercent":98,"name":"Third","report":{"code":"cbcdefghijklmnop","fightID":8},"server":{"name":"Exodus","region":"NA"}}
                ]}}}}}
                """);
        });

        using var client = new FflogsClient(handler);
        var rankings = await client.GetJobParseRankingsAsync("client-id", Secret, 1079, "Reaper", CancellationToken.None);

        Assert.Equal(FflogsClient.TokenEndpoint, handler.Requests[0].Url);
        Assert.Equal("Basic", handler.Requests[0].Authorization?.Scheme);
        Assert.Contains("grant_type=client_credentials", handler.Requests[0].Body, StringComparison.Ordinal);
        Assert.DoesNotContain(Secret, handler.Requests[0].Url, StringComparison.Ordinal);
        Assert.Equal(FflogsClient.GraphqlEndpoint, handler.Requests[1].Url);

        Assert.Equal([100, 99, 98], rankings.Select(row => row.Parse).ToArray());
        Assert.Equal(["Other", "Example", "Third"], rankings.Select(row => row.Player).ToArray());

        var row = rankings[1];
        Assert.Equal(40, row.Rank);
        Assert.Equal(99, row.Parse);
        Assert.Equal("abcdefghijklmnop", row.Code);
        Assert.Equal(12, row.FightId);
        Assert.Equal("Example", row.Player);
        Assert.Equal("Leviathan (NA)", row.Server);
    }

    [Fact]
    public void ParsePercentFloorsToTheDisplayedNumber()
    {
        Assert.Equal(100, RankedReport.ParsePercent(100));
        Assert.Equal(99, RankedReport.ParsePercent(99.4));
        Assert.Equal(98, RankedReport.ParsePercent(98));
        Assert.Equal(-1, RankedReport.ParsePercent(null));
    }

    [Fact]
    public async Task PagesCastsUntilTheCursorStops()
    {
        var handler = new ScriptedHandler();
        handler.When(FflogsClient.TokenEndpoint, _ => Json(HttpStatusCode.OK, """
            {"access_token":"token-1","expires_in":3600}
            """));
        var graphqlCalls = 0;
        handler.When(FflogsClient.GraphqlEndpoint, request =>
        {
            graphqlCalls++;
            if (request.Body.Contains("fights(encounterID", StringComparison.Ordinal))
            {
                return Json(HttpStatusCode.OK, """
                    {"data":{"reportData":{"report":{
                      "title":"Reclear","visibility":"public",
                      "masterData":{
                        "actors":[{"id":5,"name":"Exemplo","subType":"White Mage","server":"Leviathan"}],
                        "abilities":[{"gameID":25865,"name":"Broil IV"},{"gameID":3571,"name":"Assize"}]
                      },
                      "phases":[{"encounterID":1079,"phases":[{"id":1,"name":"Fatebreaker","isIntermission":false}]}],
                      "fights":[{"id":3,"name":"Futures Rewritten","kill":true,"inProgress":false,"fightPercentage":100,
                        "startTime":100000,"endTime":200000,"friendlyPlayers":[5],
                        "phaseTransitions":[{"id":1,"startTime":100000}]}]
                    }}}}
                    """);
            }

            if (graphqlCalls == 2)
            {
                Assert.Contains("\"startTime\":85000", request.Body, StringComparison.Ordinal);
                return Json(HttpStatusCode.OK, """
                    {"data":{"reportData":{"report":{"events":{
                      "data":[{"timestamp":110000,"type":"begincast","sourceID":5,"abilityGameID":25865}],
                      "nextPageTimestamp":111000
                    }}}}}
                    """);
            }

            Assert.Contains("\"startTime\":111000", request.Body, StringComparison.Ordinal);
            return Json(HttpStatusCode.OK, """
                {"data":{"reportData":{"report":{"events":{
                  "data":[
                    {"timestamp":111200,"type":"cast","sourceID":5,"abilityGameID":3571},
                    {"timestamp":112500,"type":"cast","sourceID":5,"abilityGameID":25865}
                  ],
                  "nextPageTimestamp":null
                }}}}}
                """);
        });

        using var client = new FflogsClient(handler);
        var report = await client.GetReportAsync("client-id", Secret, "abcdefghijklmnop", 1079, CancellationToken.None);
        var fight = Assert.Single(report.Fights);
        var casts = await client.GetCastsAsync("client-id", Secret, report, fight, 5, CancellationToken.None);

        Assert.Equal(["Broil IV", "Assize", "Broil IV"], casts.Select(cast => cast.Name ?? string.Empty).ToArray());
        var steps = TimelineNormalizer.Normalize(
            new TimelineInput(report.Title, "Exemplo", report.Code, fight.Id, fight.StartMs, fight.EndMs, report.Phases, fight.Transitions, casts),
            id => id == 3571 ? new AbilityVerdict(false, StepKind.OffGcd) : new AbilityVerdict(false, StepKind.Gcd));
        Assert.Equal(["Broil IV", "Assize"], steps.Select(step => step.Name).ToArray());
    }

    [Fact]
    public async Task AuthenticationFailureDoesNotEchoTheSecret()
    {
        var handler = new ScriptedHandler();
        handler.When(FflogsClient.TokenEndpoint, _ => Json(HttpStatusCode.Unauthorized, $$"""
            {"error":"invalid_client","error_description":"rejected {{Secret}}"}
            """));

        using var client = new FflogsClient(handler);
        var error = await Assert.ThrowsAsync<FflogsException>(() =>
            client.GetJobParseRankingsAsync("client-id", Secret, 1079, "Reaper", CancellationToken.None));

        Assert.DoesNotContain(Secret, error.Message, StringComparison.Ordinal);
        Assert.Contains("authenticate", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string body)
        => new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private sealed class ScriptedHandler : HttpMessageHandler
    {
        private readonly Dictionary<string, Func<CapturedRequest, HttpResponseMessage>> responders = new(StringComparer.Ordinal);

        public List<CapturedRequest> Requests { get; } = [];

        public void When(string url, Func<CapturedRequest, HttpResponseMessage> respond)
            => this.responders[url] = respond;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);
            var captured = new CapturedRequest(request.RequestUri!.ToString(), request.Headers.Authorization, body);
            this.Requests.Add(captured);
            if (!this.responders.TryGetValue(captured.Url, out var respond))
                throw new InvalidOperationException($"URL inesperada: {captured.Url}");
            return respond(captured);
        }
    }

    private sealed record CapturedRequest(string Url, System.Net.Http.Headers.AuthenticationHeaderValue? Authorization, string Body);
}
