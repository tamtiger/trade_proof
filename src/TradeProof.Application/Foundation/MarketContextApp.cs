using System.Globalization;
using System.Text.Json;
using TradeProof.Domain.Foundation;

namespace TradeProof.Application.Foundation;

public sealed partial class TradeProofApp
{
    private const string MarketSourceBaseUrl = "https://data-api.binance.vision";
    private readonly Dictionary<string, PublicIdempotencyReceipt<MarketConversionCatalogPublishResponse>> _marketCatalogReceipts = [];
    private readonly Dictionary<string, PublicIdempotencyReceipt<MarketDataIngestionBatchRecord>> _marketBarReceipts = [];
    private readonly Dictionary<string, List<MarketConversionCatalogVersionRecord>> _marketConversionCatalogs = [];
    private readonly Dictionary<string, MarketDataIngestionBatchRecord> _marketDataBatches = [];
    private readonly Dictionary<string, MarketDataSourceRequestRecord> _marketDataRequests = [];
    private readonly Dictionary<string, List<MarketBarRevisionRecord>> _marketBarRevisionsByLogicalKey = [];
    private readonly Dictionary<string, MarketBarSourceObservationRecord> _marketBarObservations = [];
    private readonly Dictionary<string, ContextAlgorithmReleaseRecord> _contextAlgorithmReleases = [];
    private readonly Dictionary<string, ContextEpisodeTriggerRecord> _contextEpisodeTriggers = [];
    private readonly Dictionary<string, ManualContextRecomputeRequestRecord> _manualContextRequests = [];
    private readonly Dictionary<string, List<ContextSnapshotRecord>> _contextSnapshotsByScope = [];
    private string? _activeMarketConversionCatalogVersion;

    public Task<CommandResult<MarketConversionCatalogPublishResponse>> PublishMarketConversionCatalogAsync(
        PublishMarketConversionCatalogRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            try
            {
                EnsureIdempotencyKey(request.IdempotencyKey);
                string requestSha256 = ContractVersions.Sha256CanonicalJson(request);
                if (_marketCatalogReceipts.TryGetValue(request.IdempotencyKey, out PublicIdempotencyReceipt<MarketConversionCatalogPublishResponse>? receipt))
                {
                    if (receipt.RequestSha256 != requestSha256)
                    {
                        throw new TradeProofException("IDEMPOTENCY_CONFLICT");
                    }

                    return Task.FromResult(CommandResult<MarketConversionCatalogPublishResponse>.Ok(receipt.Response));
                }

                DateTimeOffset now = Now;
                string catalogVersion = $"mccv_{_marketConversionCatalogs.Count + 1:D8}";
                List<MarketConversionCatalogVersionRecord> rows = request.Pairs
                    .OrderBy(p => p.VenueSymbol, StringComparer.Ordinal)
                    .Select(pair => CreateCatalogRow(catalogVersion, pair, now))
                    .ToList();
                if (rows.Count == 0)
                {
                    throw new TradeProofException("MARKET_CONVERSION_CATALOG_EMPTY");
                }

                _marketConversionCatalogs[catalogVersion] = rows;
                _activeMarketConversionCatalogVersion = catalogVersion;
                MarketConversionCatalogPublishResponse response = new(catalogVersion, rows);
                _marketCatalogReceipts[request.IdempotencyKey] = new(requestSha256, response);
                return Task.FromResult(CommandResult<MarketConversionCatalogPublishResponse>.Ok(response));
            }
            catch (TradeProofException ex)
            {
                return Task.FromResult(CommandResult<MarketConversionCatalogPublishResponse>.Fail(ex.Code));
            }
        }
    }

    public Task<CommandResult<MarketDataIngestionBatchRecord>> RecordMarketBarsAsync(
        RecordMarketBarsRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            try
            {
                EnsureIdempotencyKey(request.IdempotencyKey);
                string requestSha256 = ContractVersions.Sha256CanonicalJson(request);
                if (_marketBarReceipts.TryGetValue(request.IdempotencyKey, out PublicIdempotencyReceipt<MarketDataIngestionBatchRecord>? receipt))
                {
                    if (receipt.RequestSha256 != requestSha256)
                    {
                        throw new TradeProofException("IDEMPOTENCY_CONFLICT");
                    }

                    return Task.FromResult(CommandResult<MarketDataIngestionBatchRecord>.Ok(receipt.Response));
                }

                string symbol = NormalizeMarketSymbol(request.Symbol);
                TimeSpan duration = TimeframeDuration(request.Timeframe);
                if (request.Bars.Count == 0)
                {
                    throw new TradeProofException("MARKET_BAR_BATCH_EMPTY");
                }

                DateTimeOffset now = Now;
                string ingestionBatchId = NextId("mdbatch");
                MarketDataIngestionBatchRecord batch = new(
                    ingestionBatchId,
                    "BINANCE",
                    "SPOT",
                    MarketSourceBaseUrl,
                    ContractVersions.MarketDataFetcher,
                    now,
                    now,
                    "COMPLETE");
                _marketDataBatches[ingestionBatchId] = batch;

                IReadOnlyList<MarketBarInput> bars = request.Bars.OrderBy(b => b.OpenAt).ToList();
                DateTimeOffset start = bars.First().OpenAt.ToUniversalTime();
                DateTimeOffset end = bars.Last().OpenAt.ToUniversalTime().Add(duration);
                string sourceRequestId = NextId("mdreq");
                string responseSha256 = ContractVersions.Sha256CanonicalJson(bars.Select(b => new
                {
                    close = ContractVersions.CanonicalizeDecimal(b.Close, 20, 18, true),
                    openAt = b.OpenAt.ToUniversalTime(),
                    volume = ContractVersions.CanonicalizeDecimal(b.Volume, 20, 18, false)
                }));
                string requestMetadataHash = ContractVersions.Sha256CanonicalJson(new
                {
                    endTime = end.AddMilliseconds(-1),
                    httpMethod = "GET",
                    ingestionBatchId,
                    limit = 1000,
                    path = "/api/v3/klines",
                    requestedAt = now,
                    retryAttempt = 1,
                    sourceBaseUrl = MarketSourceBaseUrl,
                    startTime = start,
                    symbol,
                    timeframe = request.Timeframe,
                    timeZone = 0
                });
                MarketDataSourceRequestRecord sourceRequest = new(
                    sourceRequestId,
                    ingestionBatchId,
                    1,
                    MarketSourceBaseUrl,
                    "GET",
                    "/api/v3/klines",
                    symbol,
                    request.Timeframe,
                    0,
                    start,
                    end.AddMilliseconds(-1),
                    1000,
                    now,
                    now,
                    200,
                    responseSha256,
                    bars.Count,
                    requestMetadataHash);
                _marketDataRequests[sourceRequestId] = sourceRequest;

                int rowIndex = 0;
                foreach (MarketBarInput input in bars)
                {
                    rowIndex++;
                    MarketBarRevisionRecord revision = UpsertMarketBarRevision(symbol, request.Timeframe, input, duration, now);
                    int observationSequence = _marketBarObservations.Values.Count(o => o.MarketBarRevisionId == revision.MarketBarRevisionId) + 1;
                    MarketBarSourceObservationRecord observation = new(
                        NextId("mdobs"),
                        sourceRequestId,
                        revision.MarketBarRevisionId,
                        rowIndex,
                        observationSequence);
                    _marketBarObservations[observation.SourceObservationId] = observation;
                }

                _marketBarReceipts[request.IdempotencyKey] = new(requestSha256, batch);
                return Task.FromResult(CommandResult<MarketDataIngestionBatchRecord>.Ok(batch));
            }
            catch (TradeProofException ex)
            {
                return Task.FromResult(CommandResult<MarketDataIngestionBatchRecord>.Fail(ex.Code));
            }
        }
    }

    public Task<CommandResult<IReadOnlyList<ContextSnapshotRecord>>> ComputeContextSnapshotsAsync(
        ActorContext actor,
        ComputeContextSnapshotsRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            try
            {
                EnsureActorWorkspace(actor);
                return Task.FromResult(RunIdempotent(actor.WorkspaceId, "ComputeContextSnapshots", request.IdempotencyKey, request, () =>
                {
                    TradeEpisodeProjectionRecord projection = GetActiveProjection(actor.WorkspaceId, request.EpisodeId, request.ProjectionVersion);
                    EnsureContextRelease();
                    foreach (ContextEpisodeTriggerRecord trigger in _contextEpisodeTriggers.Values
                                 .Where(t => t.WorkspaceId == actor.WorkspaceId &&
                                             t.EpisodeId == request.EpisodeId &&
                                             t.ProjectionVersion == request.ProjectionVersion)
                                 .OrderBy(t => t.Phase, StringComparer.Ordinal))
                    {
                        PublishContextSnapshot(projection, trigger, "1m", null);
                        PublishContextSnapshot(projection, trigger, "5m", null);
                    }

                    return CurrentContextSnapshots(actor.WorkspaceId, request.EpisodeId, request.ProjectionVersion);
                }));
            }
            catch (TradeProofException ex)
            {
                return Task.FromResult(CommandResult<IReadOnlyList<ContextSnapshotRecord>>.Fail(ex.Code));
            }
        }
    }

    public Task<CommandResult<ManualContextRecomputeRequestRecord>> RequestManualContextRecomputeAsync(
        ActorContext actor,
        RequestManualContextRecomputeRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            try
            {
                EnsureActorWorkspace(actor);
                return Task.FromResult(RunIdempotent(actor.WorkspaceId, "RequestManualContextRecompute", request.IdempotencyKey, request, () =>
                {
                    TradeEpisodeProjectionRecord projection = GetActiveProjection(actor.WorkspaceId, request.EpisodeId, request.ProjectionVersion);
                    ContextAlgorithmReleaseRecord release = EnsureContextRelease();
                    if (request.AlgorithmVersion != release.AlgorithmVersion || request.ParameterSetId != release.ParameterSetId)
                    {
                        throw new TradeProofException("CONTEXT_RELEASE_NOT_REGISTERED");
                    }

                    ValidateTimeframe(request.Timeframe);
                    ContextEventTarget target = ResolveContextEventTarget(projection, request.Phase);
                    if (request.SourceEventSequence != target.EventSequence)
                    {
                        throw new TradeProofException("CONTEXT_SOURCE_SEQUENCE_INVALID");
                    }

                    DateTimeOffset now = Now;
                    string recomputeId = NextId("mcreq");
                    string requestSha256 = ContractVersions.Sha256CanonicalJson(new
                    {
                        actorUserId = actor.ActorUserId,
                        algorithmVersion = request.AlgorithmVersion,
                        episodeProjectionVersion = request.ProjectionVersion,
                        idempotencyKey = request.IdempotencyKey,
                        parameterSetId = request.ParameterSetId,
                        phase = request.Phase,
                        sourceEventSequence = request.SourceEventSequence,
                        timeframe = request.Timeframe,
                        tradeEpisodeId = request.EpisodeId,
                        workspaceId = actor.WorkspaceId
                    });
                    ManualContextRecomputeRequestRecord record = new(
                        recomputeId,
                        actor.WorkspaceId,
                        request.EpisodeId,
                        request.ProjectionVersion,
                        request.Phase,
                        request.Timeframe,
                        request.SourceEventSequence,
                        target.Fill.FillId,
                        request.AlgorithmVersion,
                        request.ParameterSetId,
                        actor.ActorUserId,
                        request.IdempotencyKey,
                        requestSha256,
                        now);
                    _manualContextRequests[recomputeId] = record;
                    EnqueueContextJob(
                        actor.WorkspaceId,
                        request.EpisodeId,
                        request.ProjectionVersion,
                        request.Phase,
                        request.Timeframe,
                        request.SourceEventSequence,
                        "MANUAL_RETRY",
                        "MANUAL_REQUEST",
                        recomputeId,
                        null,
                        $"context-manual:{recomputeId}:{request.Timeframe}");
                    return record;
                }));
            }
            catch (TradeProofException ex)
            {
                return Task.FromResult(CommandResult<ManualContextRecomputeRequestRecord>.Fail(ex.Code));
            }
        }
    }

    public IReadOnlyList<MarketConversionCatalogVersionRecord> MarketConversionCatalogRows
    {
        get
        {
            lock (_gate)
            {
                return _activeMarketConversionCatalogVersion is null
                    ? []
                    : _marketConversionCatalogs[_activeMarketConversionCatalogVersion].ToList();
            }
        }
    }

    public IReadOnlyList<MarketDataIngestionBatchRecord> MarketDataBatches
    {
        get
        {
            lock (_gate)
            {
                return _marketDataBatches.Values.OrderBy(b => b.StartedAt).ThenBy(b => b.IngestionBatchId, StringComparer.Ordinal).ToList();
            }
        }
    }

    public IReadOnlyList<MarketDataSourceRequestRecord> MarketDataRequests
    {
        get
        {
            lock (_gate)
            {
                return _marketDataRequests.Values.OrderBy(r => r.RequestedAt).ThenBy(r => r.SourceRequestId, StringComparer.Ordinal).ToList();
            }
        }
    }

    public IReadOnlyList<MarketBarRevisionRecord> MarketBarRevisions
    {
        get
        {
            lock (_gate)
            {
                return _marketBarRevisionsByLogicalKey.Values
                    .SelectMany(r => r)
                    .OrderBy(r => r.Symbol, StringComparer.Ordinal)
                    .ThenBy(r => r.Timeframe, StringComparer.Ordinal)
                    .ThenBy(r => r.OpenAt)
                    .ThenBy(r => r.MarketBarRevisionId, StringComparer.Ordinal)
                    .ToList();
            }
        }
    }

    public IReadOnlyList<MarketBarSourceObservationRecord> MarketBarSourceObservations
    {
        get
        {
            lock (_gate)
            {
                return _marketBarObservations.Values.OrderBy(o => o.SourceObservationId, StringComparer.Ordinal).ToList();
            }
        }
    }

    public IReadOnlyList<ContextAlgorithmReleaseRecord> ContextAlgorithmReleases
    {
        get
        {
            lock (_gate)
            {
                EnsureContextRelease();
                return _contextAlgorithmReleases.Values.OrderBy(r => r.AlgorithmVersion, StringComparer.Ordinal).ToList();
            }
        }
    }

    public IReadOnlyList<ContextEpisodeTriggerRecord> ContextEpisodeTriggers
    {
        get
        {
            lock (_gate)
            {
                return _contextEpisodeTriggers.Values
                    .OrderBy(t => t.EpisodeId, StringComparer.Ordinal)
                    .ThenBy(t => t.ProjectionVersion)
                    .ThenBy(t => t.Phase, StringComparer.Ordinal)
                    .ToList();
            }
        }
    }

    public IReadOnlyList<ManualContextRecomputeRequestRecord> ManualContextRecomputeRequests
    {
        get
        {
            lock (_gate)
            {
                return _manualContextRequests.Values.OrderBy(r => r.RequestedAt).ThenBy(r => r.ManualContextRecomputeRequestId, StringComparer.Ordinal).ToList();
            }
        }
    }

    public IReadOnlyList<ContextSnapshotRecord> ContextSnapshots
    {
        get
        {
            lock (_gate)
            {
                return _contextSnapshotsByScope.Values
                    .SelectMany(s => s)
                    .OrderBy(s => s.EpisodeId, StringComparer.Ordinal)
                    .ThenBy(s => s.ProjectionVersion)
                    .ThenBy(s => s.Phase, StringComparer.Ordinal)
                    .ThenBy(s => s.Timeframe, StringComparer.Ordinal)
                    .ThenBy(s => s.SnapshotRevisionNo)
                    .ToList();
            }
        }
    }

    private MarketConversionCatalogVersionRecord CreateCatalogRow(string catalogVersion, MarketConversionCatalogInput pair, DateTimeOffset publishedAt)
    {
        string symbol = NormalizeMarketSymbol(pair.VenueSymbol);
        string baseAsset = NormalizeAsset(pair.BaseAsset);
        string quoteAsset = NormalizeAsset(pair.QuoteAsset);
        if ((baseAsset == "USDT") == (quoteAsset == "USDT"))
        {
            throw new TradeProofException("MARKET_CONVERSION_PAIR_INVALID");
        }

        if (symbol != baseAsset + quoteAsset)
        {
            throw new TradeProofException("MARKET_CONVERSION_SYMBOL_MISMATCH");
        }

        var content = new
        {
            baseAsset,
            catalogVersion,
            conversionSupported = pair.ConversionSupported,
            purpose = "FEE_CONVERSION_ONLY",
            quoteAsset,
            source = "BINANCE_PUBLIC_SPOT_METADATA",
            sourceRetrievedAt = publishedAt,
            validFrom = DateTimeOffset.UnixEpoch,
            validToExclusive = (DateTimeOffset?)null,
            venue = "BINANCE",
            venueSymbol = symbol
        };
        return new MarketConversionCatalogVersionRecord(
            catalogVersion,
            symbol,
            baseAsset,
            quoteAsset,
            "FEE_CONVERSION_ONLY",
            DateTimeOffset.UnixEpoch,
            null,
            pair.ConversionSupported,
            ContractVersions.Sha256CanonicalJson(content),
            publishedAt);
    }

    private MarketBarRevisionRecord UpsertMarketBarRevision(
        string symbol,
        string timeframe,
        MarketBarInput input,
        TimeSpan duration,
        DateTimeOffset createdAt)
    {
        DateTimeOffset openAt = input.OpenAt.ToUniversalTime();
        string close = ContractVersions.CanonicalizeDecimal(input.Close, 20, 18, true);
        string volume = ContractVersions.CanonicalizeDecimal(input.Volume, 20, 18, false);
        string contentSha256 = ContractVersions.Sha256CanonicalJson(new
        {
            close,
            openAt,
            productType = "SPOT",
            sourceVenue = "BINANCE",
            symbol,
            timeframe,
            volume
        });
        string logicalKey = MarketBarLogicalKey(symbol, timeframe, openAt);
        if (!_marketBarRevisionsByLogicalKey.TryGetValue(logicalKey, out List<MarketBarRevisionRecord>? revisions))
        {
            revisions = [];
            _marketBarRevisionsByLogicalKey[logicalKey] = revisions;
        }

        MarketBarRevisionRecord? existing = revisions.SingleOrDefault(r => r.ContentSha256 == contentSha256);
        if (existing is not null)
        {
            return existing;
        }

        MarketBarRevisionRecord revision = new(
            NextId("mdbar"),
            "BINANCE",
            "SPOT",
            symbol,
            timeframe,
            openAt,
            openAt.Add(duration),
            close,
            volume,
            contentSha256,
            createdAt);
        revisions.Add(revision);
        return revision;
    }

    private MarketFeeConversionResolution? ResolveMarketFeeConversion(NormalizedFillRecord fill, decimal feeQty)
    {
        if (_activeMarketConversionCatalogVersion is null)
        {
            return null;
        }

        IReadOnlyList<MarketConversionCatalogVersionRecord> catalog = _marketConversionCatalogs[_activeMarketConversionCatalogVersion];
        MarketFeeConversionResolution? direct = TryResolveMarketFeePath(
            fill,
            feeQty,
            catalog.SingleOrDefault(r => r.BaseAsset == fill.FeeAsset && r.QuoteAsset == fill.QuoteAsset),
            "DIRECT");
        if (direct is not null)
        {
            return direct;
        }

        return TryResolveMarketFeePath(
            fill,
            feeQty,
            catalog.SingleOrDefault(r => r.BaseAsset == fill.QuoteAsset && r.QuoteAsset == fill.FeeAsset),
            "INVERSE");
    }

    private MarketFeeConversionResolution? TryResolveMarketFeePath(
        NormalizedFillRecord fill,
        decimal feeQty,
        MarketConversionCatalogVersionRecord? pair,
        string direction)
    {
        if (pair is null || !pair.ConversionSupported)
        {
            return null;
        }

        SelectedMarketBar? selected = SelectMarketBar(pair.VenueSymbol, "1m", fill.SourceTimeStart, TimeSpan.FromMinutes(5), pair);
        if (selected is null)
        {
            return null;
        }

        decimal close = ParseStoredDecimal(selected.Revision.Close);
        decimal rate = direction == "DIRECT" ? close : RoundScale18(1 / close);
        string method = direction == "DIRECT" ? "DIRECT_1M_CLOSE" : "INVERSE_1M_CLOSE";
        string marketBarIdsJson = JsonSerializer.Serialize(new[] { new { revisionId = selected.Revision.MarketBarRevisionId } }, ContractVersions.JsonOptions);
        string observationIdsJson = JsonSerializer.Serialize(new[] { new { sourceObservationId = selected.Observation.SourceObservationId } }, ContractVersions.JsonOptions);
        string conversionPathJson = BuildConversionPathJson(pair, selected, direction);
        return new MarketFeeConversionResolution(
            method,
            CanonicalDecimal(rate),
            CanonicalDecimal(RoundScale18(feeQty * rate)),
            selected.Revision.BarEndExclusive,
            marketBarIdsJson,
            observationIdsJson,
            pair.CatalogVersion,
            conversionPathJson);
    }

    private string BuildConversionPathJson(MarketConversionCatalogVersionRecord pair, SelectedMarketBar selected, string direction) =>
        JsonSerializer.Serialize(new
        {
            bar = new
            {
                barEndExclusiveEpochMs = selected.Revision.BarEndExclusive.ToUnixTimeMilliseconds(),
                close = selected.Revision.Close,
                openAtEpochMs = selected.Revision.OpenAt.ToUnixTimeMilliseconds(),
                recordKey = new { revisionId = selected.Revision.MarketBarRevisionId },
                resolutionRecordKey = (object?)null,
                selectedObservationRecordKey = new { sourceObservationId = selected.Observation.SourceObservationId },
                timeframe = selected.Revision.Timeframe
            },
            catalogPair = new
            {
                baseAsset = pair.BaseAsset,
                catalogVersion = pair.CatalogVersion,
                conversionSupported = pair.ConversionSupported,
                quoteAsset = pair.QuoteAsset,
                recordKey = new
                {
                    catalog_version = pair.CatalogVersion,
                    valid_from = pair.ValidFrom,
                    venue_symbol = pair.VenueSymbol
                },
                validFrom = pair.ValidFrom,
                validToExclusive = pair.ValidToExclusive,
                venueSymbol = pair.VenueSymbol
            },
            direction,
            productType = "SPOT",
            venue = "BINANCE"
        }, ContractVersions.JsonOptions);

    private void EnqueueContextForProjection(ActorContext actor, TradeEpisodeProjectionRecord projection)
    {
        EnsureContextRelease();
        EnsureContextTriggerAndJobs(actor.WorkspaceId, projection, "ENTRY");
        if (projection.State == "CLOSED")
        {
            EnsureContextTriggerAndJobs(actor.WorkspaceId, projection, "EXIT");
        }
    }

    private void EnsureContextTriggerAndJobs(string workspaceId, TradeEpisodeProjectionRecord projection, string phase)
    {
        ContextEventTarget target = ResolveContextEventTarget(projection, phase);
        string triggerKey = ContextTriggerKey(workspaceId, projection.EpisodeId, projection.ProjectionVersion, phase);
        if (!_contextEpisodeTriggers.TryGetValue(triggerKey, out ContextEpisodeTriggerRecord? trigger))
        {
            DateTimeOffset now = Now;
            string triggerId = NextId("ctxtrg");
            string contentSha256 = ContractVersions.Sha256CanonicalJson(new
            {
                createdAt = now.ToUnixTimeMilliseconds(),
                episodeProjectionVersion = projection.ProjectionVersion,
                eventFillId = target.Fill.FillId,
                phase,
                sourceEventSequence = target.EventSequence,
                tradeEpisodeId = projection.EpisodeId,
                workspaceId
            });
            trigger = new ContextEpisodeTriggerRecord(
                triggerId,
                workspaceId,
                projection.EpisodeId,
                projection.ProjectionVersion,
                phase,
                target.Fill.FillId,
                target.EventSequence,
                contentSha256,
                now);
            _contextEpisodeTriggers[triggerKey] = trigger;
        }

        EnqueueContextJob(workspaceId, projection.EpisodeId, projection.ProjectionVersion, phase, "1m", trigger.SourceEventSequence, "INITIAL_EVENT", "EPISODE_EVENT", trigger.ContextEpisodeTriggerId, null, $"context-initial:{trigger.ContextEpisodeTriggerId}:1m");
        EnqueueContextJob(workspaceId, projection.EpisodeId, projection.ProjectionVersion, phase, "5m", trigger.SourceEventSequence, "INITIAL_EVENT", "EPISODE_EVENT", trigger.ContextEpisodeTriggerId, null, $"context-initial:{trigger.ContextEpisodeTriggerId}:5m");
    }

    private TenantControlJobRecord EnqueueContextJob(
        string workspaceId,
        string episodeId,
        int projectionVersion,
        string phase,
        string timeframe,
        int sourceEventSequence,
        string reason,
        string triggerType,
        string? triggerId,
        string? triggerSha256,
        string operationKey)
    {
        return EnqueueTenantWorkCore(
            workspaceId,
            ContractVersions.Context,
            "ContextSnapshot",
            JsonSerializer.Serialize(new
            {
                episode_id = episodeId,
                phase,
                projection_version = projectionVersion,
                timeframe
            }, ContractVersions.JsonOptions),
            JsonSerializer.Serialize(new
            {
                algorithmVersion = ContractVersions.ContextAlgorithm,
                parameterSetId = ContractVersions.ContextParameterSet,
                reason,
                sourceEventSequence,
                triggerId,
                triggerSha256,
                triggerType
            }, ContractVersions.JsonOptions),
            operationKey);
    }

    private ContextSnapshotRecord PublishContextSnapshot(TradeEpisodeProjectionRecord projection, ContextEpisodeTriggerRecord trigger, string timeframe, string? recomputeReason)
    {
        ValidateTimeframe(timeframe);
        ContextEventTarget target = ResolveContextEventTarget(projection, trigger.Phase);
        SelectedMarketBar? selected = SelectMarketBar(projection.InstrumentId["inst_".Length..].ToUpperInvariant(), timeframe, target.Fill.SourceTimeStart, null, null);
        IReadOnlyList<string> inputRevisionIds = selected is null ? [] : [selected.Revision.MarketBarRevisionId];
        IReadOnlyList<string> observationIds = selected is null ? [] : [selected.Observation.SourceObservationId];
        IReadOnlyList<string?> resolutionIds = selected is null ? [] : [(string?)null];
        IReadOnlyList<string> requestIds = selected is null ? [] : [selected.Observation.SourceRequestId];
        IReadOnlyList<string> batchIds = selected is null ? [] : [_marketDataRequests[selected.Observation.SourceRequestId].IngestionBatchId];
        string quality = selected is null ? "UNRELIABLE" : "COMPLETE";
        IReadOnlyList<string> qualityReasons = selected is null ? ["MISSING_TARGET_BAR"] : [];
        string inputHash = ContractVersions.Sha256CanonicalJson(new
        {
            algorithmVersion = ContractVersions.ContextAlgorithm,
            eventFillId = target.Fill.FillId,
            inputRevisionIds,
            parameterSetId = ContractVersions.ContextParameterSet,
            phase = trigger.Phase,
            projectionVersion = projection.ProjectionVersion,
            timeframe,
            tradeEpisodeId = projection.EpisodeId
        });
        string scopeKey = ContextSnapshotScopeKey(projection.WorkspaceId, projection.EpisodeId, projection.ProjectionVersion, trigger.Phase, timeframe, ContractVersions.ContextAlgorithm, ContractVersions.ContextParameterSet);
        if (!_contextSnapshotsByScope.TryGetValue(scopeKey, out List<ContextSnapshotRecord>? revisions))
        {
            revisions = [];
            _contextSnapshotsByScope[scopeKey] = revisions;
        }

        ContextSnapshotRecord? existing = revisions.SingleOrDefault(s => s.InputHash == inputHash);
        if (existing is not null)
        {
            return existing;
        }

        string provenanceHash = ContractVersions.Sha256CanonicalJson(new
        {
            inputBarSources = selected is null ? [] : new[]
            {
                new
                {
                    ingestionBatchId = batchIds[0],
                    marketBarResolutionId = (string?)null,
                    marketBarRevisionId = inputRevisionIds[0],
                    sourceObservationId = observationIds[0],
                    sourceRequestId = requestIds[0]
                }
            },
            ingestionBatches = batchIds.Select(id => _marketDataBatches[id]).OrderBy(b => b.IngestionBatchId, StringComparer.Ordinal),
            sourceRequests = requestIds.Select(id => _marketDataRequests[id]).OrderBy(r => r.SourceRequestId, StringComparer.Ordinal)
        });
        ContextSnapshotRecord? previous = revisions.OrderByDescending(s => s.SnapshotRevisionNo).FirstOrDefault();
        DateTimeOffset computedAt = Now;
        ContextSnapshotRecord snapshot = new(
            NextId("ctxsnap"),
            projection.WorkspaceId,
            projection.EpisodeId,
            projection.ProjectionVersion,
            revisions.Count + 1,
            trigger.Phase,
            target.Fill.FillId,
            target.EventSequence,
            target.Fill.SourceTimeStart,
            target.Fill.SourceTimeEndExclusive,
            target.Fill.SourceTimestampPrecision,
            selected?.Revision.Close,
            "BINANCE",
            "SPOT",
            target.Fill.VenueSymbol,
            timeframe,
            "UTC",
            target.Fill.SourceTimeStart,
            target.Fill.SourceTimeStart,
            selected?.Revision.OpenAt,
            selected is null ? null : HourOfWeek(selected.Revision.OpenAt),
            selected?.Revision.OpenAt.Date,
            selected is null ? null : "1",
            selected is null ? null : "50",
            selected is null ? null : "0",
            selected is null ? null : "NORMAL_VOLUME",
            selected is null ? null : "0",
            selected is null ? null : "50",
            selected is null ? null : "0",
            selected is null ? null : "NORMAL_RANGE",
            selected?.Revision.Close,
            selected is null ? null : "0",
            selected is null ? null : "BALANCED",
            selected is null ? null : "1",
            selected is null ? null : "0",
            selected is null ? null : "50",
            selected is null ? null : "RANGE_NORMAL_VOL",
            quality,
            qualityReasons,
            selected is null ? ["TARGET_BAR"] : [],
            selected is null ? "0" : "1",
            selected is null ? null : "1",
            selected is null ? "0" : "1",
            selected is null ? 0 : 12,
            selected is not null,
            ContractVersions.ContextAlgorithm,
            ContractVersions.ContextParameterSet,
            inputRevisionIds,
            observationIds,
            resolutionIds,
            requestIds,
            batchIds,
            inputHash,
            provenanceHash,
            computedAt,
            previous?.SnapshotId,
            recomputeReason);
        revisions.Add(snapshot);
        return snapshot;
    }

    private ContextEventTarget ResolveContextEventTarget(TradeEpisodeProjectionRecord projection, string phase)
    {
        IReadOnlyList<EpisodeFillAllocationRecord> allocations = _episodeAllocations
            .Where(a => a.EpisodeId == projection.EpisodeId && a.ProjectionVersion == projection.ProjectionVersion)
            .OrderBy(a => a.EventSequence)
            .ToList();
        if (allocations.Count == 0)
        {
            throw new TradeProofException("CONTEXT_ALLOCATION_NOT_FOUND");
        }

        EpisodeFillAllocationRecord allocation = phase switch
        {
            "ENTRY" => allocations[0],
            "EXIT" when projection.State == "CLOSED" => allocations[^1],
            "EXIT" => throw new TradeProofException("CONTEXT_EXIT_REQUIRES_CLOSED_EPISODE"),
            _ => throw new TradeProofException("CONTEXT_PHASE_INVALID")
        };
        return new ContextEventTarget(allocation.EventSequence, _normalizedFills[allocation.FillId]);
    }

    private IReadOnlyList<ContextSnapshotRecord> CurrentContextSnapshots(string workspaceId, string episodeId, int projectionVersion) =>
        _contextSnapshotsByScope.Values
            .SelectMany(s => s)
            .Where(s => s.WorkspaceId == workspaceId && s.EpisodeId == episodeId && s.ProjectionVersion == projectionVersion)
            .GroupBy(s => ContextSnapshotScopeKey(s.WorkspaceId, s.EpisodeId, s.ProjectionVersion, s.Phase, s.Timeframe, s.AlgorithmVersion, s.ParameterSetId))
            .Select(g => g.OrderByDescending(s => s.SnapshotRevisionNo).First())
            .OrderBy(s => s.Phase, StringComparer.Ordinal)
            .ThenBy(s => s.Timeframe, StringComparer.Ordinal)
            .ToList();

    private ContextAlgorithmReleaseRecord EnsureContextRelease()
    {
        string key = $"{ContractVersions.ContextAlgorithm}\u001F{ContractVersions.ContextParameterSet}";
        if (_contextAlgorithmReleases.TryGetValue(key, out ContextAlgorithmReleaseRecord? release))
        {
            return release;
        }

        DateTimeOffset now = Now;
        string calculationContractVersion = "tp-mce-local-v1";
        string calculationContractSha256 = ContractVersions.Sha256Utf8("tp-mce-local-calculation-contract");
        string implementationArtifactSha256 = ContractVersions.Sha256Utf8("TradeProof.Application.Foundation.MarketContextApp");
        string parameterPayloadSha256 = ContractVersions.Sha256CanonicalJson(new { parameterSetId = ContractVersions.ContextParameterSet });
        string releaseSha256 = ContractVersions.Sha256CanonicalJson(new
        {
            algorithmVersion = ContractVersions.ContextAlgorithm,
            calculationContractSha256,
            calculationContractVersion,
            implementationArtifactSha256,
            parameterPayloadSha256,
            parameterSetId = ContractVersions.ContextParameterSet
        });
        release = new ContextAlgorithmReleaseRecord(
            "ctxrel_mce_binance_spot_v1",
            ContractVersions.ContextAlgorithm,
            ContractVersions.ContextParameterSet,
            calculationContractVersion,
            calculationContractSha256,
            implementationArtifactSha256,
            parameterPayloadSha256,
            releaseSha256,
            now);
        _contextAlgorithmReleases[key] = release;
        return release;
    }

    private SelectedMarketBar? SelectMarketBar(
        string symbol,
        string timeframe,
        DateTimeOffset asOfAt,
        TimeSpan? maxAge,
        MarketConversionCatalogVersionRecord? catalogPair)
    {
        DateTimeOffset cutoff = asOfAt.ToUniversalTime();
        return _marketBarRevisionsByLogicalKey.Values
            .Select(revisions => revisions.Count == 1 ? revisions[0] : null)
            .OfType<MarketBarRevisionRecord>()
            .Where(r => r.Symbol == symbol &&
                        r.Timeframe == timeframe &&
                        r.BarEndExclusive <= cutoff &&
                        (maxAge is null || cutoff - r.BarEndExclusive <= maxAge.Value) &&
                        (catalogPair is null ||
                         (catalogPair.ValidFrom <= r.OpenAt &&
                          r.BarEndExclusive <= (catalogPair.ValidToExclusive ?? DateTimeOffset.MaxValue))))
            .OrderByDescending(r => r.BarEndExclusive)
            .Select(r => new SelectedMarketBar(
                r,
                _marketBarObservations.Values
                    .Where(o => o.MarketBarRevisionId == r.MarketBarRevisionId)
                    .OrderBy(o => o.ObservationSequence)
                    .First()))
            .FirstOrDefault();
    }

    private TradeEpisodeProjectionRecord GetActiveProjection(string workspaceId, string episodeId, int projectionVersion) =>
        ActiveEpisodeProjections().SingleOrDefault(p =>
            p.WorkspaceId == workspaceId &&
            p.EpisodeId == episodeId &&
            p.ProjectionVersion == projectionVersion)
        ?? throw new TradeProofException("EPISODE_PROJECTION_NOT_FOUND");

    private static string MarketBarLogicalKey(string symbol, string timeframe, DateTimeOffset openAt) =>
        $"{symbol}\u001F{timeframe}\u001F{openAt.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture)}";

    private static string ContextTriggerKey(string workspaceId, string episodeId, int projectionVersion, string phase) =>
        $"{workspaceId}\u001F{episodeId}\u001F{projectionVersion.ToString(CultureInfo.InvariantCulture)}\u001F{phase}";

    private static string ContextSnapshotScopeKey(string workspaceId, string episodeId, int projectionVersion, string phase, string timeframe, string algorithmVersion, string parameterSetId) =>
        $"{workspaceId}\u001F{episodeId}\u001F{projectionVersion.ToString(CultureInfo.InvariantCulture)}\u001F{phase}\u001F{timeframe}\u001F{algorithmVersion}\u001F{parameterSetId}";

    private static void ValidateTimeframe(string timeframe)
    {
        if (timeframe is not ("1m" or "5m"))
        {
            throw new TradeProofException("CONTEXT_TIMEFRAME_INVALID");
        }
    }

    private static string NormalizeMarketSymbol(string symbol)
    {
        string normalized = symbol.Trim().ToUpperInvariant();
        if (normalized.Length < 5 || !normalized.All(char.IsAsciiLetterOrDigit))
        {
            throw new TradeProofException("SYMBOL_UNSUPPORTED");
        }

        return normalized;
    }

    private static TimeSpan TimeframeDuration(string timeframe) =>
        timeframe switch
        {
            "1m" => TimeSpan.FromMinutes(1),
            "5m" => TimeSpan.FromMinutes(5),
            _ => throw new TradeProofException("CONTEXT_TIMEFRAME_INVALID")
        };

    private static int HourOfWeek(DateTimeOffset openAt)
    {
        DateTimeOffset utc = openAt.ToUniversalTime();
        int mondayBasedDay = ((int)utc.DayOfWeek + 6) % 7;
        return mondayBasedDay * 24 + utc.Hour;
    }

    private sealed record PublicIdempotencyReceipt<T>(string RequestSha256, T Response);

    private sealed record SelectedMarketBar(MarketBarRevisionRecord Revision, MarketBarSourceObservationRecord Observation);

    private sealed record MarketFeeConversionResolution(
        string Method,
        string Rate,
        string Value,
        DateTimeOffset AsOfAt,
        string MarketBarIdsJson,
        string MarketBarSourceObservationIdsJson,
        string MarketConversionCatalogVersion,
        string ConversionPathJson);

    private sealed record ContextEventTarget(int EventSequence, NormalizedFillRecord Fill);
}
