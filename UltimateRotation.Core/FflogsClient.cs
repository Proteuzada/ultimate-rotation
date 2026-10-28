using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace UltimateRotation;

public sealed class FflogsException : Exception
{
    public FflogsException(string message)
        : base(message)
    {
    }
}

/// <summary>
/// Official FFLogs API v2 only: client-credentials token, then the public GraphQL endpoint.
/// </summary>
public sealed class FflogsClient : IDisposable
{
    public const string TokenEndpoint = "https://www.fflogs.com/oauth/token";
    public const string GraphqlEndpoint = "https://www.fflogs.com/api/v2/client";

    private const int MaxEventPages = 30;

    private readonly HttpClient http;
    private readonly SemaphoreSlim tokenGate = new(1, 1);
    private string? token;
    private DateTime tokenExpiry = DateTime.MinValue;
    private string? tokenOwner;

    public FflogsClient(HttpMessageHandler? handler = null)
    {
        this.http = handler is null
            ? new HttpClient()
            : new HttpClient(handler, disposeHandler: false);
        this.http.Timeout = TimeSpan.FromSeconds(30);
        this.http.DefaultRequestHeaders.UserAgent.ParseAdd("ProteuUltimateRotation/0.12.5");
        this.http.DefaultRequestHeaders.Accept.ParseAdd("application/json");
    }

    public void ForgetToken()
    {
        this.token = null;
        this.tokenOwner = null;
        this.tokenExpiry = DateTime.MinValue;
    }

    public async Task<IReadOnlyList<RankedReport>> GetJobParseRankingsAsync(
        string clientId,
        string clientSecret,
        int encounterId,
        string specName,
        CancellationToken cancellationToken)
    {
        var job = JobCatalog.Find(specName) ?? throw new FflogsException("Choose a job.");
        var kills = await this.LoadKillsAsync(clientId, clientSecret, encounterId, includeSize: true, cancellationToken)
            .ConfigureAwait(false);
        if (kills.Count == 0)
        {
            kills = await this.LoadKillsAsync(clientId, clientSecret, encounterId, includeSize: false, cancellationToken)
                .ConfigureAwait(false);
        }

        if (kills.Count == 0)
            throw new FflogsException("FFLogs did not return kills for this ultimate.");

        var groups = new Dictionary<int, List<RankedReport>>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var total = 0;

        for (var offset = 0; offset < kills.Count; offset += 8)
        {
            var batch = kills.Skip(offset).Take(8).ToList();
            var data = await this.QueryAsync(
                clientId,
                clientSecret,
                BuildClassRankingsQuery(batch),
                new Dictionary<string, object?> { ["encounterID"] = encounterId },
                cancellationToken).ConfigureAwait(false);

            var reportData = Require(data, "reportData");
            var lowest = int.MaxValue;
            for (var index = 0; index < batch.Count; index++)
            {
                if (!reportData.TryGetProperty($"r{index}", out var report)
                    || report.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined
                    || !report.TryGetProperty("rankings", out var rankings)
                    || rankings.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
                    continue;

                foreach (var character in EnumerateRankingCharacters(Unwrap(rankings)))
                {
                    if (!TryReadClassRank(character, job, batch[index], out var ranked))
                        continue;

                    lowest = Math.Min(lowest, ranked.Parse);
                    var key = $"{ranked.Code}:{ranked.FightId}:{ranked.Player}";
                    if (!seen.Add(key))
                        continue;
                    if (!groups.TryGetValue(ranked.Parse, out var bucket))
                    {
                        bucket = [];
                        groups[ranked.Parse] = bucket;
                    }

                    if (bucket.Count >= UltimateCatalog.RowsPerParse)
                        continue;

                    bucket.Add(ranked);
                    total++;
                    if (total >= UltimateCatalog.RankingLimit)
                        break;
                }

                if (total >= UltimateCatalog.RankingLimit)
                    break;
            }

            if (total >= UltimateCatalog.RankingLimit)
                break;
            if (ParseGroupsFilled(groups, lowest == int.MaxValue ? 0 : lowest))
                break;
        }

        return groups
            .SelectMany(group => group.Value)
            .OrderByDescending(row => row.Parse)
            .ThenBy(row => row.Rank)
            .ToList();
    }

    private async Task<List<EncounterKill>> LoadKillsAsync(
        string clientId,
        string clientSecret,
        int encounterId,
        bool includeSize,
        CancellationToken cancellationToken)
    {
        var sizeArgument = includeSize ? $"\n                    size: {UltimateCatalog.PartySize}" : string.Empty;
        var query = $$"""
            query EncounterKills($encounterID: Int!, $page: Int!) {
              worldData {
                encounter(id: $encounterID) {
                  name
                  fightRankings(
                    difficulty: {{UltimateCatalog.Difficulty}}{{sizeArgument}}
                    page: $page
                  )
                }
              }
            }
            """;

        var kills = new List<EncounterKill>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var page = 1; page <= 3 && kills.Count < 24; page++)
        {
            var data = await this.QueryAsync(
                clientId,
                clientSecret,
                query,
                new Dictionary<string, object?>
                {
                    ["encounterID"] = encounterId,
                    ["page"] = page,
                },
                cancellationToken).ConfigureAwait(false);

            var encounter = Require(Require(data, "worldData"), "encounter");
            if (encounter.ValueKind is JsonValueKind.Null)
                throw new FflogsException("FFLogs does not know this ultimate.");

            var rankings = Unwrap(Require(encounter, "fightRankings"));
            if (rankings.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
                break;
            if (!rankings.TryGetProperty("rankings", out var rows) || rows.ValueKind != JsonValueKind.Array)
                break;

            var rowCount = 0;
            foreach (var row in rows.EnumerateArray())
            {
                rowCount++;
                if (row.ValueKind != JsonValueKind.Object)
                    continue;
                if (row.TryGetProperty("hidden", out var hidden) && hidden.ValueKind is JsonValueKind.True)
                    continue;
                if (!row.TryGetProperty("report", out var report) || report.ValueKind != JsonValueKind.Object)
                    continue;

                var code = ReadString(report, "code");
                if (!ReportCode.TryExtract(code, out var parsed, out _))
                    continue;

                var fightId = ReadInt(report, "fightID", "fightId");
                if (fightId <= 0 || !seen.Add($"{parsed}:{fightId}"))
                    continue;

                kills.Add(new EncounterKill(parsed, fightId, ReadDouble(row, "duration")));
                if (kills.Count >= 24)
                    break;
            }

            if (rowCount == 0 || !HasMorePages(rankings, rowCount))
                break;
        }

        return kills;
    }

    private static string BuildClassRankingsQuery(IReadOnlyList<EncounterKill> kills)
    {
        var builder = new StringBuilder();
        builder.AppendLine("query ClassRankings($encounterID: Int!) {");
        builder.AppendLine("  reportData {");
        for (var index = 0; index < kills.Count; index++)
        {
            var kill = kills[index];
            builder.AppendLine($"    r{index}: report(code: \"{kill.Code}\") {{");
            builder.AppendLine($"      rankings(encounterID: $encounterID, fightIDs: [{kill.FightId}], playerMetric: rdps)");
            builder.AppendLine("    }");
        }

        builder.AppendLine("  }");
        builder.Append('}');
        return builder.ToString();
    }

    private static bool TryReadClassRank(
        JsonElement row,
        CombatJob job,
        EncounterKill kill,
        out RankedReport ranked)
    {
        ranked = null!;
        if (row.ValueKind != JsonValueKind.Object)
            return false;
        if (row.TryGetProperty("hidden", out var hidden) && hidden.ValueKind is JsonValueKind.True)
            return false;

        var spec = ReadString(row, "spec");
        var jobName = ReadString(row, "class");
        if (!JobCatalog.Matches(job, spec) && !JobCatalog.Matches(job, jobName))
            return false;

        var player = ReadString(row, "name");
        if (string.IsNullOrWhiteSpace(player))
            return false;

        var parse = RankedReport.ParsePercent(
            ReadDouble(row, "rankPercent")
            ?? ReadDouble(row, "bracketPercent")
            ?? ReadDouble(row, "percentile"));
        if (parse < 0)
            return false;

        var rank = ReadRank(row);
        var fightId = ReadInt(row, "fightID", "fightId");
        if (fightId <= 0)
            fightId = kill.FightId;

        ranked = new RankedReport(
            rank,
            player,
            kill.Code,
            fightId,
            ReadDouble(row, "duration") ?? kill.DurationMs,
            JoinServer(Child(row, "server")),
            parse);
        return true;
    }

    private static IEnumerable<JsonElement> EnumerateRankingCharacters(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("data", out var data))
        {
            foreach (var character in EnumerateRankingCharacters(data))
                yield return character;
            yield break;
        }

        if (root.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in root.EnumerateArray())
            {
                foreach (var character in EnumerateRankingCharacters(item))
                    yield return character;
            }

            yield break;
        }

        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("roles", out var roles))
            yield break;

        if (roles.ValueKind == JsonValueKind.Array)
        {
            foreach (var role in roles.EnumerateArray())
            {
                foreach (var character in CharactersOf(role))
                    yield return character;
            }

            yield break;
        }

        if (roles.ValueKind != JsonValueKind.Object)
            yield break;

        foreach (var role in roles.EnumerateObject())
        {
            foreach (var character in CharactersOf(role.Value))
                yield return character;
        }
    }

    private static IEnumerable<JsonElement> CharactersOf(JsonElement role)
    {
        var characters = role;
        if (characters.ValueKind == JsonValueKind.Object
            && characters.TryGetProperty("characters", out var nested))
            characters = nested;
        if (characters.ValueKind != JsonValueKind.Array)
            yield break;

        foreach (var character in characters.EnumerateArray())
        {
            if (character.ValueKind == JsonValueKind.Object)
                yield return character;
        }
    }

    private static int ReadRank(JsonElement row)
    {
        if (!row.TryGetProperty("rank", out var rank))
            return 0;
        if (rank.ValueKind == JsonValueKind.Number)
            return rank.TryGetInt32(out var whole) ? whole : (int)rank.GetDouble();
        if (rank.ValueKind != JsonValueKind.String)
            return 0;

        var text = rank.GetString();
        if (string.IsNullOrWhiteSpace(text))
            return 0;

        var length = 0;
        while (length < text.Length && char.IsDigit(text[length]))
            length++;

        return length > 0 && int.TryParse(text[..length], out var parsed) ? parsed : 0;
    }

    private readonly record struct EncounterKill(string Code, int FightId, double? DurationMs);

    private static bool HasMorePages(JsonElement rankings, int rowCount)
    {
        if (rankings.TryGetProperty("hasMorePages", out var more)
            && more.ValueKind is JsonValueKind.True or JsonValueKind.False)
            return more.ValueKind == JsonValueKind.True;

        return rowCount >= 100;
    }

    private static bool ParseGroupsFilled(Dictionary<int, List<RankedReport>> groups, int lowestOnPage)
    {
        if (groups.Count == 0)
            return false;

        var best = groups.Keys.Max();
        for (var parse = best; parse > best - UltimateCatalog.ParseGroupDepth && parse >= 0; parse--)
        {
            if (!groups.TryGetValue(parse, out var rows) || rows.Count == 0)
                return false;
            if (rows.Count < UltimateCatalog.RowsPerParse && lowestOnPage >= parse)
                return false;
        }

        return true;
    }

    public async Task<LoadedReport> GetReportAsync(
        string clientId,
        string clientSecret,
        string code,
        int encounterId,
        CancellationToken cancellationToken)
    {
        const string query = """
            query Report($code: String!, $encounterID: Int!) {
              reportData {
                report(code: $code) {
                  title
                  visibility
                  masterData {
                    actors(type: "Player") { id name subType server }
                    abilities { gameID name }
                  }
                  phases {
                    encounterID
                    phases { id name isIntermission }
                  }
                  fights(encounterID: $encounterID, killType: Encounters) {
                    id
                    name
                    kill
                    inProgress
                    fightPercentage
                    startTime
                    endTime
                    friendlyPlayers
                    phaseTransitions { id startTime }
                  }
                }
              }
            }
            """;

        var data = await this.QueryAsync(
            clientId,
            clientSecret,
            query,
            new Dictionary<string, object?>
            {
                ["code"] = code,
                ["encounterID"] = encounterId,
            },
            cancellationToken).ConfigureAwait(false);

        var report = Require(Require(data, "reportData"), "report");
        if (report.ValueKind is JsonValueKind.Null)
            throw new FflogsException("Report not found.");

        var visibility = ReadString(report, "visibility");
        if (string.Equals(visibility, "private", StringComparison.OrdinalIgnoreCase))
            throw new FflogsException("Private report or missing permission. The public API only reads public reports.");

        var master = Require(report, "masterData");
        var actors = new List<ReportActorInfo>();
        if (master.TryGetProperty("actors", out var actorRows) && actorRows.ValueKind == JsonValueKind.Array)
        {
            foreach (var actor in actorRows.EnumerateArray())
            {
                var id = ReadInt(actor, "id");
                var name = ReadString(actor, "name");
                if (id == 0 || string.IsNullOrWhiteSpace(name))
                    continue;

                actors.Add(new ReportActorInfo(id, name, ReadString(actor, "subType") ?? string.Empty, ReadString(actor, "server")));
            }
        }

        var abilityNames = new Dictionary<uint, string>();
        if (master.TryGetProperty("abilities", out var abilityRows) && abilityRows.ValueKind == JsonValueKind.Array)
        {
            foreach (var ability in abilityRows.EnumerateArray())
            {
                var id = ReadUInt(ability, "gameID");
                var name = ReadString(ability, "name");
                if (id == 0 || string.IsNullOrWhiteSpace(name))
                    continue;
                abilityNames[id] = name;
            }
        }

        var phases = new List<PhaseLabel>();
        if (report.TryGetProperty("phases", out var phaseGroups) && phaseGroups.ValueKind == JsonValueKind.Array)
        {
            foreach (var group in phaseGroups.EnumerateArray())
            {
                if (ReadInt(group, "encounterID") != encounterId)
                    continue;
                if (!group.TryGetProperty("phases", out var phaseRows) || phaseRows.ValueKind != JsonValueKind.Array)
                    continue;

                foreach (var phase in phaseRows.EnumerateArray())
                {
                    phases.Add(new PhaseLabel(
                        ReadInt(phase, "id"),
                        ReadString(phase, "name") ?? string.Empty,
                        phase.TryGetProperty("isIntermission", out var intermission) && intermission.ValueKind == JsonValueKind.True));
                }
            }
        }

        var fights = new List<ReportFightInfo>();
        if (report.TryGetProperty("fights", out var fightRows) && fightRows.ValueKind == JsonValueKind.Array)
        {
            foreach (var fight in fightRows.EnumerateArray())
            {
                var playerIds = new List<int>();
                if (fight.TryGetProperty("friendlyPlayers", out var players) && players.ValueKind == JsonValueKind.Array)
                {
                    foreach (var player in players.EnumerateArray())
                    {
                        if (player.ValueKind == JsonValueKind.Number)
                            playerIds.Add((int)player.GetDouble());
                    }
                }

                var transitions = new List<PhaseTransitionMark>();
                if (fight.TryGetProperty("phaseTransitions", out var transitionRows) && transitionRows.ValueKind == JsonValueKind.Array)
                {
                    foreach (var transition in transitionRows.EnumerateArray())
                    {
                        var start = ReadDouble(transition, "startTime");
                        if (start is null)
                            continue;
                        transitions.Add(new PhaseTransitionMark(ReadInt(transition, "id"), start.Value));
                    }
                }

                var startMs = ReadDouble(fight, "startTime") ?? 0;
                var endMs = ReadDouble(fight, "endTime") ?? startMs;
                fights.Add(new ReportFightInfo(
                    ReadInt(fight, "id"),
                    ReadString(fight, "name") ?? "Pull",
                    fight.TryGetProperty("kill", out var kill) && kill.ValueKind == JsonValueKind.True,
                    ReadDouble(fight, "fightPercentage"),
                    startMs,
                    endMs,
                    fight.TryGetProperty("inProgress", out var inProgress) && inProgress.ValueKind == JsonValueKind.True,
                    playerIds,
                    transitions));
            }
        }

        return new LoadedReport(
            code,
            ReadString(report, "title") ?? code,
            encounterId,
            fights.OrderBy(fight => fight.StartMs).ToList(),
            actors,
            phases,
            abilityNames);
    }

    public async Task<IReadOnlyList<CastEvent>> GetCastsAsync(
        string clientId,
        string clientSecret,
        LoadedReport report,
        ReportFightInfo fight,
        int sourceId,
        CancellationToken cancellationToken)
    {
        const string query = """
            query Casts($code: String!, $fightIDs: [Int]!, $sourceID: Int!, $startTime: Float!, $endTime: Float!) {
              reportData {
                report(code: $code) {
                  events(
                    fightIDs: $fightIDs
                    sourceID: $sourceID
                    dataType: Casts
                    startTime: $startTime
                    endTime: $endTime
                    limit: 10000
                    translate: true
                  ) {
                    data
                    nextPageTimestamp
                  }
                }
              }
            }
            """;

        var cursor = Math.Max(0, fight.StartMs - TimelineNormalizer.PrepullWindowMs);
        var end = fight.EndMs;
        var seen = new HashSet<(long Timestamp, string Type, uint AbilityId)>();
        var casts = new List<CastEvent>();

        for (var page = 0; page < MaxEventPages; page++)
        {
            var data = await this.QueryAsync(
                clientId,
                clientSecret,
                query,
                new Dictionary<string, object?>
                {
                    ["code"] = report.Code,
                    ["fightIDs"] = new[] { fight.Id },
                    ["sourceID"] = sourceId,
                    ["startTime"] = cursor,
                    ["endTime"] = end,
                },
                cancellationToken).ConfigureAwait(false);

            var events = Require(Require(Require(data, "reportData"), "report"), "events");
            if (events.TryGetProperty("data", out var payload))
                ReadCasts(Unwrap(payload), sourceId, report.AbilityNames, seen, casts);

            if (!events.TryGetProperty("nextPageTimestamp", out var nextElement)
                || nextElement.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
                break;

            var next = nextElement.GetDouble();
            if (next <= cursor)
                break;

            cursor = next;
        }

        return casts;
    }

    public void Dispose()
    {
        this.http.Dispose();
        this.tokenGate.Dispose();
    }

    private async Task<JsonElement> QueryAsync(
        string clientId,
        string clientSecret,
        string query,
        Dictionary<string, object?> variables,
        CancellationToken cancellationToken)
    {
        var accessToken = await this.GetTokenAsync(clientId, clientSecret, cancellationToken).ConfigureAwait(false);
        var body = JsonSerializer.Serialize(new { query, variables });
        using var request = new HttpRequestMessage(HttpMethod.Post, GraphqlEndpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = new StringContent(body, Encoding.UTF8, "application/json");

        using var response = await this.http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var text = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            throw new FflogsException($"FFLogs rejected the query (HTTP {(int)response.StatusCode}).");

        using var document = JsonDocument.Parse(text);
        if (document.RootElement.TryGetProperty("errors", out var errors)
            && errors.ValueKind == JsonValueKind.Array
            && errors.GetArrayLength() > 0)
        {
            var message = errors[0].TryGetProperty("message", out var errorMessage)
                ? errorMessage.GetString()
                : null;
            throw new FflogsException(string.IsNullOrWhiteSpace(message) ? "FFLogs rejected the query." : message);
        }

        if (!document.RootElement.TryGetProperty("data", out var data))
            throw new FflogsException("FFLogs response had no data.");

        return data.Clone();
    }

    private async Task<string> GetTokenAsync(string clientId, string clientSecret, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
            throw new FflogsException("Enter the FFLogs API v2 client id and secret.");

        await this.tokenGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var owner = clientId.Trim();
            if (this.token is not null
                && this.tokenOwner == owner
                && DateTime.UtcNow < this.tokenExpiry)
                return this.token;

            var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{owner}:{clientSecret}"));
            using var request = new HttpRequestMessage(HttpMethod.Post, TokenEndpoint);
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", basic);
            request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
            });

            using var response = await this.http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            var text = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                throw new FflogsException("Could not authenticate with FFLogs. Check the client id and secret.");

            using var document = JsonDocument.Parse(text);
            var accessToken = ReadString(document.RootElement, "access_token");
            if (string.IsNullOrWhiteSpace(accessToken))
                throw new FflogsException("Could not authenticate with FFLogs. Check the client id and secret.");

            var expiresIn = ReadDouble(document.RootElement, "expires_in") ?? 3600;
            this.token = accessToken;
            this.tokenOwner = owner;
            this.tokenExpiry = DateTime.UtcNow.AddSeconds(Math.Max(30, expiresIn - 60));
            return accessToken;
        }
        finally
        {
            this.tokenGate.Release();
        }
    }

    private static void ReadCasts(
        JsonElement payload,
        int sourceId,
        IReadOnlyDictionary<uint, string> abilityNames,
        HashSet<(long Timestamp, string Type, uint AbilityId)> seen,
        List<CastEvent> casts)
    {
        if (payload.ValueKind != JsonValueKind.Array)
            return;

        foreach (var ev in payload.EnumerateArray())
        {
            if (ev.ValueKind != JsonValueKind.Object)
                continue;

            if (ev.TryGetProperty("sourceID", out var source) && source.ValueKind == JsonValueKind.Number)
            {
                var id = (int)source.GetDouble();
                if (id != 0 && id != sourceId)
                    continue;
            }

            var type = ReadString(ev, "type");
            var timestamp = ReadDouble(ev, "timestamp");
            if (type is null || timestamp is null)
                continue;

            var abilityId = ReadUInt(ev, "abilityGameID", "guid");
            string? name = null;
            if (abilityId == 0 && ev.TryGetProperty("ability", out var ability) && ability.ValueKind == JsonValueKind.Object)
            {
                abilityId = ReadUInt(ability, "guid", "gameID", "gameId");
                name = ReadString(ability, "name");
            }
            else if (ev.TryGetProperty("ability", out var named) && named.ValueKind == JsonValueKind.Object)
            {
                name = ReadString(named, "name");
            }

            if (abilityId == 0)
                continue;
            if (string.IsNullOrWhiteSpace(name) && abilityNames.TryGetValue(abilityId, out var known))
                name = known;

            var key = ((long)Math.Round(timestamp.Value), type, abilityId);
            if (!seen.Add(key))
                continue;

            casts.Add(new CastEvent(timestamp.Value, type, abilityId, name));
        }
    }

    private static JsonElement Unwrap(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.String)
            return element;

        var text = element.GetString();
        if (string.IsNullOrWhiteSpace(text))
            return element;

        using var document = JsonDocument.Parse(text);
        return document.RootElement.Clone();
    }

    private static JsonElement Require(JsonElement element, string name)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(name, out var child))
            throw new FflogsException($"FFLogs response is missing the {name} field.");

        return child;
    }

    private static JsonElement? Child(JsonElement element, string name)
        => element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var child)
            ? child
            : null;

    private static string? ReadString(JsonElement? element, string name)
    {
        if (element is not { } value || value.ValueKind != JsonValueKind.Object)
            return null;
        if (!value.TryGetProperty(name, out var property) || property.ValueKind != JsonValueKind.String)
            return null;
        return property.GetString();
    }

    private static int ReadInt(JsonElement element, params string[] names)
    {
        var value = ReadDouble(element, names);
        return value is null ? 0 : (int)value.Value;
    }

    private static uint ReadUInt(JsonElement element, params string[] names)
    {
        var value = ReadDouble(element, names);
        return value is null or < 0 ? 0 : (uint)value.Value;
    }

    private static double? ReadDouble(JsonElement element, params string[] names)
    {
        if (element.ValueKind != JsonValueKind.Object)
            return null;

        foreach (var name in names)
        {
            if (!element.TryGetProperty(name, out var property))
                continue;
            if (property.ValueKind == JsonValueKind.Number)
                return property.GetDouble();
            if (property.ValueKind == JsonValueKind.String
                && double.TryParse(property.GetString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var parsed))
                return parsed;
        }

        return null;
    }

    private static string? JoinServer(JsonElement? server)
    {
        var name = ReadString(server, "name");
        var region = ReadString(server, "region");
        if (string.IsNullOrWhiteSpace(name))
            return null;
        return string.IsNullOrWhiteSpace(region) ? name : $"{name} ({region})";
    }
}
