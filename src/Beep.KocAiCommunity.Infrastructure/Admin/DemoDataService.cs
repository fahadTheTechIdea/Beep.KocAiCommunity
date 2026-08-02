using System.Text;
using Beep.KocAiCommunity.Application.Admin;
using Beep.KocAiCommunity.Application.Audit;
using Beep.KocAiCommunity.Application.Datasets;
using Beep.KocAiCommunity.Application.Storage;
using Beep.KocAiCommunity.Domain.Common;
using Beep.KocAiCommunity.Domain.Community;
using Beep.KocAiCommunity.Domain.Competitions;
using Beep.KocAiCommunity.Domain.Datasets;
using Beep.KocAiCommunity.Domain.Engagement;
using Beep.KocAiCommunity.Domain.Experiments;
using Beep.KocAiCommunity.Domain.Learning;
using Beep.KocAiCommunity.Domain.Notifications;
using Beep.KocAiCommunity.Domain.Organization;
using Beep.KocAiCommunity.Infrastructure.Datasets;
using Beep.KocAiCommunity.Infrastructure.Engagement;
using Beep.KocAiCommunity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using ExperimentRun = Beep.KocAiCommunity.Domain.Experiments.Run;

namespace Beep.KocAiCommunity.Infrastructure.Admin;

/// <summary>
/// Creates and removes a rich, self-contained demo of the whole platform: a demo group with three
/// teams and twelve colleagues (Barrels, streaks, badges, kudos), learning enrollments, two
/// competitions with real data + leaderboards + submissions, discussions, trainable datasets, an
/// experiment with trial metrics, and notifications. It also enrolls the dev personas so *their*
/// dashboards light up. Every row is stamped <c>CreatedByUserId = "demo-seed"</c>, which is exactly
/// what unseed deletes — real KOC records are never touched.
/// </summary>
public sealed class DemoDataService(KocDbContext db, IArtifactService artifacts, IAuditEnvelope audit) : IDemoDataService
{
    private const string Prefix = "demo-";
    private const string Marker = "demo-seed";
    private const string OrgPath = "/demo";

    // The dev personas whose dashboards should light up after seeding.
    private static readonly string[] DevUsers = ["dev-admin", "dev-user", "dev-lead", "dev-emp-1", "dev-emp-2"];

    private sealed record DemoPerson(string UserId, string Name, string Avatar, int Xp, int Streak, string Skills, int Team);

    private static readonly DemoPerson[] People =
    [
        new($"{Prefix}alice", "Alice Al-Sabah", "185-worker.png", 1420, 21, "ML.NET,Reservoir,Python", 0),
        new($"{Prefix}bob", "Bob Al-Rashid", "179-exploration.png", 980, 12, "Python,Production", 0),
        new($"{Prefix}carol", "Carol Mansour", "042-oil-pump.png", 815, 9, "HSE,Analytics", 2),
        new($"{Prefix}dana", "Dana Khalifa", "016-drill.png", 640, 7, "Geoscience,Well logs", 0),
        new($"{Prefix}eisa", "Eisa Al-Mutairi", "009-pump.png", 530, 5, "SCADA,Time series", 1),
        new($"{Prefix}fatima", "Fatima Al-Enezi", "023-gas.png", 455, 8, "Process,Refining", 1),
        new($"{Prefix}ghanim", "Ghanim Al-Otaibi", "031-valve.png", 390, 3, "Maintenance,SAP", 1),
        new($"{Prefix}huda", "Huda Al-Ajmi", "054-tank.png", 310, 4, "Storage,Logistics", 2),
        new($"{Prefix}ibrahim", "Ibrahim Nasser", "066-pipeline.png", 240, 2, "Pipelines,Integrity", 1),
        new($"{Prefix}jana", "Jana Al-Harbi", "078-refinery.png", 175, 6, "Downstream,Yield", 2),
        new($"{Prefix}khalid", "Khalid Salem", "090-platform.png", 95, 1, "Offshore,HSE", 2),
        new($"{Prefix}latifa", "Latifa Al-Fadhli", "101-helmet.png", 45, 1, "New joiner", 0),
    ];

    public async Task<DemoDataStatus> GetStatusAsync(CancellationToken ct = default)
    {
        var users = await db.Set<UserProfile>().CountAsync(p => p.UserId.StartsWith(Prefix), ct);
        var competitions = await db.Set<Competition>().CountAsync(c => c.CreatedByUserId == Marker, ct);
        var discussions = await db.Set<Discussion>().CountAsync(d => d.CreatedByUserId == Marker, ct);
        var datasets = await db.Set<Dataset>().CountAsync(d => d.CreatedByUserId == Marker, ct);
        return new DemoDataStatus(users > 0, users, competitions, discussions, datasets);
    }

    public async Task<DemoDataStatus> SeedAsync(string actorUserId, CancellationToken ct = default)
    {
        var status = await GetStatusAsync(ct);
        if (status.Seeded)
        {
            return status;   // idempotent — unseed first to refresh
        }

        // Hard timeout so a SQLite lock conflict fails loudly instead of stalling the request.
        // (Dev SQLite is shared with the Worker; WAL journaling — set at API startup — prevents the
        // writer-vs-writer stalls that made bulk seeding hang.)
        db.Database.SetCommandTimeout(TimeSpan.FromSeconds(15));

        var now = DateTime.UtcNow;

        var teams = SeedOrg(now);
        SeedPeople(teams, now);
        await SeedLearningAsync(now, ct);
        var active = await SeedCompetitionsAsync(now, ct);
        SeedDiscussions(now);
        await SeedDatasetsAsync(now, ct);
        SeedExperiment(now);
        SeedNotifications(active, now);

        await db.SaveChangesAsync(ct);
        await audit.WriteAsync(new AuditEntry("demo.seed", "demo-data", null, null, $"{{\"users\":{People.Length}}}"), ct);
        return await GetStatusAsync(ct);
    }

    public async Task<DemoDataStatus> UnseedAsync(string actorUserId, CancellationToken ct = default)
    {
        db.Database.SetCommandTimeout(TimeSpan.FromSeconds(15));

        // Every table the seeder writes to, keyed by the marker (children first, parents last).
        await db.Set<RunMetric>().Where(x => x.CreatedByUserId == Marker).ExecuteDeleteAsync(ct);
        await db.Set<ExperimentRun>().Where(x => x.CreatedByUserId == Marker).ExecuteDeleteAsync(ct);
        await db.Set<Experiment>().Where(x => x.CreatedByUserId == Marker).ExecuteDeleteAsync(ct);
        await db.Set<Notification>().Where(x => x.CreatedByUserId == Marker).ExecuteDeleteAsync(ct);
        await db.Set<DatasetProfileColumn>().Where(x => x.CreatedByUserId == Marker).ExecuteDeleteAsync(ct);
        await db.Set<DatasetProfile>().Where(x => x.CreatedByUserId == Marker).ExecuteDeleteAsync(ct);
        await db.Set<DatasetSchemaColumn>().Where(x => x.CreatedByUserId == Marker).ExecuteDeleteAsync(ct);
        await db.Set<DatasetFile>().Where(x => x.CreatedByUserId == Marker).ExecuteDeleteAsync(ct);
        await db.Set<DatasetVersion>().Where(x => x.CreatedByUserId == Marker).ExecuteDeleteAsync(ct);
        await db.Set<Dataset>().Where(x => x.CreatedByUserId == Marker || x.OwnerUserId.StartsWith(Prefix)).ExecuteDeleteAsync(ct);
        await db.Set<Reaction>().Where(x => x.CreatedByUserId == Marker || x.UserId.StartsWith(Prefix)).ExecuteDeleteAsync(ct);
        await db.Set<DiscussionReply>().Where(x => x.CreatedByUserId == Marker || x.AuthorUserId.StartsWith(Prefix)).ExecuteDeleteAsync(ct);
        await db.Set<Discussion>().Where(x => x.CreatedByUserId == Marker || x.AuthorUserId.StartsWith(Prefix)).ExecuteDeleteAsync(ct);
        await db.Set<LeaderboardEntry>().Where(x => x.CreatedByUserId == Marker || x.SubmitterUserId.StartsWith(Prefix)).ExecuteDeleteAsync(ct);
        await db.Set<Submission>().Where(x => x.CreatedByUserId == Marker || x.SubmitterUserId.StartsWith(Prefix)).ExecuteDeleteAsync(ct);
        await db.Set<Competition>().Where(x => x.CreatedByUserId == Marker || x.CreatedByUserId!.StartsWith(Prefix)).ExecuteDeleteAsync(ct);
        await db.Set<TrackCompletion>().Where(x => x.CreatedByUserId == Marker).ExecuteDeleteAsync(ct);
        await db.Set<TrackEnrollment>().Where(x => x.CreatedByUserId == Marker).ExecuteDeleteAsync(ct);
        await db.Set<Kudos>().Where(x => x.CreatedByUserId == Marker || x.FromUserId.StartsWith(Prefix)).ExecuteDeleteAsync(ct);
        await db.Set<ActivityEvent>().Where(x => x.CreatedByUserId == Marker || x.ActorUserId.StartsWith(Prefix)).ExecuteDeleteAsync(ct);
        await db.Set<UserBadge>().Where(x => x.CreatedByUserId == Marker || x.UserId.StartsWith(Prefix)).ExecuteDeleteAsync(ct);
        await db.Set<XpEvent>().Where(x => x.CreatedByUserId == Marker || x.UserId.StartsWith(Prefix)).ExecuteDeleteAsync(ct);
        await db.Set<UserProfile>().Where(x => x.CreatedByUserId == Marker || x.UserId.StartsWith(Prefix)).ExecuteDeleteAsync(ct);
        await db.Set<OrgMembership>().Where(x => x.UserId.StartsWith(Prefix)).ExecuteDeleteAsync(ct);
        // OrgUnit is self-referencing (ParentId), so remove the demo teams before the demo group root.
        await db.Set<OrgUnit>().Where(x => x.Path.StartsWith(OrgPath + "/")).ExecuteDeleteAsync(ct);
        await db.Set<OrgUnit>().Where(x => x.Path == OrgPath).ExecuteDeleteAsync(ct);

        await audit.WriteAsync(new AuditEntry("demo.unseed", "demo-data", null, null, null), ct);
        return await GetStatusAsync(ct);
    }

    // ---- Org: one demo group with three teams (rich enough for team leaderboards) ----

    private OrgUnit[] SeedOrg(DateTime now)
    {
        var group = NewUnit("[Demo] AI Champions", OrgUnitType.Group, null, OrgPath, People[0].UserId, now);
        var t0 = NewUnit("[Demo] Reservoir AI", OrgUnitType.Team, group.Id, $"{OrgPath}/reservoir-ai", People[0].UserId, now);
        var t1 = NewUnit("[Demo] Production Analytics", OrgUnitType.Team, group.Id, $"{OrgPath}/production-analytics", People[4].UserId, now);
        var t2 = NewUnit("[Demo] HSE Intelligence", OrgUnitType.Team, group.Id, $"{OrgPath}/hse-intelligence", People[2].UserId, now);
        db.AddRange(group, t0, t1, t2);
        return [t0, t1, t2];
    }

    private OrgUnit NewUnit(string name, OrgUnitType type, Guid? parent, string path, string leader, DateTime now) => new()
    {
        Name = name,
        Type = type,
        ParentId = parent,
        Path = path,
        LeaderUserId = leader,
        CreatedByUserId = Marker,
        CreatedUtc = now,
    };

    // ---- People: memberships, profiles, XP ledgers, badges, kudos, activity ----

    private void SeedPeople(OrgUnit[] teams, DateTime now)
    {
        string[] badgePool = [BadgeCatalog.FirstSubmission, BadgeCatalog.Streak7, BadgeCatalog.Podium, BadgeCatalog.DiscussionStarter, BadgeCatalog.FirstTrack];

        foreach (var (person, index) in People.Select((p, i) => (p, i)))
        {
            var team = teams[person.Team];
            db.Add(new OrgMembership
            {
                UserId = person.UserId,
                OrgUnitId = team.Id,
                PositionLevel = team.LeaderUserId == person.UserId ? PositionLevel.TeamLeader : PositionLevel.Employee,
                IsPrimary = true,
                FromUtc = now.AddMonths(-6),
                CreatedByUserId = Marker,
                CreatedUtc = now,
            });

            var level = KocLevels.ForXp(person.Xp);
            db.Add(new UserProfile
            {
                UserId = person.UserId,
                DisplayName = person.Name,
                Bio = $"{person.Skills.Split(',')[0]} specialist · demo colleague.",
                AvatarIcon = person.Avatar,
                SkillsCsv = person.Skills,
                XpTotal = person.Xp,
                Level = level.Level,
                CurrentStreakDays = person.Streak,
                LongestStreakDays = person.Streak + 4,
                LastActiveDate = DateOnly.FromDateTime(now),
                CreatedByUserId = Marker,
                CreatedUtc = now.AddMonths(-6),
            });

            // Ledger spread over recent days/weeks so the week/month/all leaderboards all differ.
            var chunks = new[] { (person.Xp / 2, 40), (person.Xp / 4, 12), (person.Xp - (person.Xp / 2) - (person.Xp / 4), 2) };
            var sources = new[] { XpSources.LessonCompleted, XpSources.SubmissionScored, XpSources.DiscussionCreated };
            foreach (var ((points, daysAgo), source) in chunks.Zip(sources))
            {
                if (points > 0)
                {
                    db.Add(new XpEvent { UserId = person.UserId, Source = source, Points = points, RefType = "demo", CreatedByUserId = Marker, CreatedUtc = now.AddDays(-daysAgo) });
                }
            }

            // Everyone has first-barrel; stronger players collect more.
            db.Add(new UserBadge { UserId = person.UserId, BadgeCode = BadgeCatalog.FirstBarrel, CreatedByUserId = Marker, CreatedUtc = now.AddMonths(-5) });
            for (var b = 0; b < Math.Min(badgePool.Length, (People.Length - index) / 3); b++)
            {
                db.Add(new UserBadge { UserId = person.UserId, BadgeCode = badgePool[b], CreatedByUserId = Marker, CreatedUtc = now.AddDays(-10 - (b * 7)) });
                db.Add(new ActivityEvent
                {
                    ActorUserId = person.UserId,
                    Type = "badge.earned",
                    RefType = "badge",
                    PayloadJson = $"{{\"badge\":\"{badgePool[b]}\"}}",
                    VisibilityScope = VisibilityScope.Company,
                    VisibilityOrgUnitId = Guid.Empty,
                    CreatedByUserId = Marker,
                    CreatedUtc = now.AddDays(-10 - (b * 7)),
                });
            }
        }

        // A web of kudos between colleagues (and to a dev persona so their bell rings).
        (string From, string To, string Msg, string Emoji)[] kudos =
        [
            (People[0].UserId, People[1].UserId, "Great catch on the ESP sensor drift — saved us a rerun.", "👏"),
            (People[1].UserId, People[3].UserId, "Your well-log features lifted everyone's scores.", "🚀"),
            (People[2].UserId, People[7].UserId, "Thanks for the HSE incident labelling session.", "🤝"),
            (People[4].UserId, People[0].UserId, "The split-before-fit tip fixed my leakage!", "🌟"),
            (People[5].UserId, People[9].UserId, "Refinery yield notebook was gold.", "🛢️"),
            (People[3].UserId, "dev-emp-1", "Welcome to the challenge — strong first submission!", "👏"),
        ];
        foreach (var k in kudos)
        {
            db.Add(new Kudos { FromUserId = k.From, ToUserId = k.To, Message = k.Msg, Emoji = k.Emoji, CreatedByUserId = Marker, CreatedUtc = now.AddDays(-3) });
            db.Add(new ActivityEvent
            {
                ActorUserId = k.From,
                Type = "kudos.given",
                RefType = "kudos",
                PayloadJson = $"{{\"to\":\"{k.To}\"}}",
                VisibilityScope = VisibilityScope.Company,
                VisibilityOrgUnitId = Guid.Empty,
                CreatedByUserId = Marker,
                CreatedUtc = now.AddDays(-3),
            });
        }
    }

    // ---- Learning: enroll demo people AND dev personas into the real seeded tracks ----

    private async Task SeedLearningAsync(DateTime now, CancellationToken ct)
    {
        var tracks = await db.Set<LearningTrack>().AsNoTracking().OrderBy(t => t.OrderNo).Select(t => t.Id).ToListAsync(ct);
        if (tracks.Count == 0)
        {
            return;
        }

        // The dev personas may already be enrolled (DevOrgSeeder, or the user clicking around) —
        // (TrackId, UserId) is unique, so skip pairs that exist instead of violating the index.
        var enrolled = (await db.Set<TrackEnrollment>().AsNoTracking().Select(e => new { e.TrackId, e.UserId }).ToListAsync(ct))
            .Select(e => (e.TrackId, e.UserId)).ToHashSet();
        var completedAlready = (await db.Set<TrackCompletion>().AsNoTracking().Select(c => new { c.TrackId, c.UserId }).ToListAsync(ct))
            .Select(c => (c.TrackId, c.UserId)).ToHashSet();

        var learners = People.Select(p => p.UserId).Concat(DevUsers).ToArray();
        foreach (var (userId, index) in learners.Select((u, i) => (u, i)))
        {
            // Everyone starts track 1; roughly half completed it; stronger profiles progress further.
            var completedFirst = index % 2 == 0;
            AddEnrollment(enrolled, completedAlready, tracks[0], userId, completedFirst, now.AddDays(-30));
            if (tracks.Count > 1 && index % 3 == 0)
            {
                AddEnrollment(enrolled, completedAlready, tracks[1], userId, completed: index % 6 == 0, now.AddDays(-14));
            }

            if (tracks.Count > 2 && index % 4 == 0)
            {
                AddEnrollment(enrolled, completedAlready, tracks[2], userId, completed: false, now.AddDays(-5));
            }
        }
    }

    private void AddEnrollment(HashSet<(Guid, string)> enrolled, HashSet<(Guid, string)> completedAlready, Guid trackId, string userId, bool completed, DateTime started)
    {
        if (enrolled.Add((trackId, userId)))
        {
            db.Add(new TrackEnrollment
            {
                TrackId = trackId,
                UserId = userId,
                Status = completed ? "completed" : "active",
                StartedUtc = started,
                CompletedUtc = completed ? started.AddDays(7) : null,
                CreatedByUserId = Marker,
                CreatedUtc = started,
            });
        }

        if (completed && completedAlready.Add((trackId, userId)))
        {
            db.Add(new TrackCompletion { TrackId = trackId, UserId = userId, CompletedUtc = started.AddDays(7), CreatedByUserId = Marker, CreatedUtc = started.AddDays(7) });
        }
    }

    // ---- Competitions: one live (full leaderboard incl. dev personas) + one concluded ----

    private async Task<Competition> SeedCompetitionsAsync(DateTime now, CancellationToken ct)
    {
        // Real, usable data files so the challenge is playable end to end.
        var (training, evaluation, answerKey) = BuildEspData();
        var trainingRef = await SaveCsvAsync(training, "demo/esp/training.csv", ct);
        var evalRef = await SaveCsvAsync(evaluation, "demo/esp/evaluation.csv", ct);
        var keyRef = await SaveCsvAsync(answerKey, "demo/esp/answer-key.csv", ct);

        var active = new Competition
        {
            Title = "[Demo] ESP Failure Challenge",
            Description = "Predict electric submersible pump failures from pressure and vibration readings. "
                + "Download the training data, build a pipeline in Studio, and submit — you're scored on a hidden test set.",
            Status = "active",
            VisibilityScope = VisibilityScope.Company,
            VisibilityOrgUnitId = Guid.Empty,
            ScorerCode = "accuracy",
            TrainingDatasetArtifactId = trainingRef.Id,
            EvaluationArtifactId = evalRef.Id,
            AnswerKeyArtifactId = keyRef.Id,
            LabelColumn = "label",
            IdColumn = "id",
            TaskType = "BinaryClassification",
            SubmissionQuotaPerDay = 10,
            CreatedByUserId = Marker,
            CreatedUtc = now.AddDays(-14),
        };
        db.Add(active);

        // A leaderboard of 8 demo people + 3 dev personas, with plausible score decay and submissions.
        var contenders = People.Take(8).Select(p => p.UserId).Concat(["dev-admin", "dev-user", "dev-emp-1"]).ToArray();
        for (var i = 0; i < contenders.Length; i++)
        {
            var score = Math.Round(0.96 - (i * 0.023), 3);
            var submission = new Submission
            {
                CompetitionId = active.Id,
                SubmitterUserId = contenders[i],
                PredictionArtifactId = evalRef.Id,   // placeholder artifact; scoring already happened
                SubmittedUtc = now.AddDays(-1 - i),
                Status = "scored",
                Score = score,
                CreatedByUserId = Marker,
                CreatedUtc = now.AddDays(-1 - i),
            };
            db.Add(submission);
            // A second, earlier (worse) attempt for the top half — so "My submissions" shows history.
            if (i < 5)
            {
                db.Add(new Submission
                {
                    CompetitionId = active.Id,
                    SubmitterUserId = contenders[i],
                    PredictionArtifactId = evalRef.Id,
                    SubmittedUtc = now.AddDays(-4 - i),
                    Status = "scored",
                    Score = Math.Round(score - 0.05, 3),
                    CreatedByUserId = Marker,
                    CreatedUtc = now.AddDays(-4 - i),
                });
            }

            db.Add(new LeaderboardEntry
            {
                CompetitionId = active.Id,
                SubmitterUserId = contenders[i],
                BestSubmissionId = submission.Id,
                Score = score,
                Rank = i + 1,
                CreatedByUserId = Marker,
                CreatedUtc = now,
            });
        }

        // A concluded challenge with revealed final standings — shows the full lifecycle.
        var concluded = new Competition
        {
            Title = "[Demo] Well-Log Facies (concluded)",
            Description = "Classify well-log intervals into rock facies. Concluded — final standings revealed.",
            Status = "concluded",
            VisibilityScope = VisibilityScope.Company,
            VisibilityOrgUnitId = Guid.Empty,
            ScorerCode = "accuracy",
            LabelColumn = "label",
            IdColumn = "id",
            TaskType = "MulticlassClassification",
            RevealUtc = now.AddDays(-2),
            SubmissionQuotaPerDay = 5,
            CreatedByUserId = Marker,
            CreatedUtc = now.AddDays(-45),
        };
        db.Add(concluded);
        var winners = new[] { People[3].UserId, People[0].UserId, People[6].UserId, "dev-emp-1", People[9].UserId };
        for (var i = 0; i < winners.Length; i++)
        {
            db.Add(new LeaderboardEntry
            {
                CompetitionId = concluded.Id,
                SubmitterUserId = winners[i],
                Score = Math.Round(0.90 - (i * 0.04), 3),
                Rank = i + 1,
                CreatedByUserId = Marker,
                CreatedUtc = now.AddDays(-3),
            });
        }

        db.Add(new UserBadge { UserId = winners[0], BadgeCode = BadgeCatalog.CompetitionWinner, CreatedByUserId = Marker, CreatedUtc = now.AddDays(-2) });
        return active;
    }

    // ---- Discussions: three threads with replies and reactions ----

    private void SeedDiscussions(DateTime now)
    {
        (string Author, string Title, string Body, (string By, string Text)[] Replies)[] threads =
        [
            (People[0].UserId, "[Demo] ESP challenge — share your feature ideas",
             "What features moved your score? Remember: split before you fit, or the compiler will reject your publish!",
             [(People[1].UserId, "Rolling averages on vibration helped me most."),
              (People[4].UserId, "Pressure deltas over 6h windows — big lift."),
              ("dev-emp-1", "One-hot on pump model + robust scaling worked for me.")]),
            (People[2].UserId, "[Demo] Reading a dataset profile before you train",
             "The profile tab shows nulls, distincts and ranges per column — check it before wiring your pipeline.",
             [(People[7].UserId, "The null counts saved me from a broken sensor column.")]),
            (People[5].UserId, "[Demo] From experiment to registered model — my walkthrough",
             "Ran the refinery-yield workflow, tracked trials under Experiments, then registered the best run. Two approvals later it's serving predictions.",
             []),
        ];

        foreach (var t in threads)
        {
            var d = new Discussion
            {
                Title = t.Title,
                Body = t.Body,
                AuthorUserId = t.Author,
                VisibilityScope = VisibilityScope.Company,
                VisibilityOrgUnitId = Guid.Empty,
                IsPinned = t == threads[0],
                CreatedByUserId = Marker,
                CreatedUtc = now.AddDays(-6),
            };
            db.Add(d);
            foreach (var (reply, r) in t.Replies.Select((x, i) => (x, i)))
            {
                db.Add(new DiscussionReply { DiscussionId = d.Id, AuthorUserId = reply.By, Body = reply.Text, CreatedByUserId = Marker, CreatedUtc = now.AddDays(-5 + r) });
            }

            foreach (var (emoji, user) in new[] { ("👍", People[3].UserId), ("💡", People[8].UserId), ("🚀", People[10].UserId) })
            {
                db.Add(new Reaction { TargetType = "discussion", TargetId = d.Id, UserId = user, Emoji = emoji, CreatedByUserId = Marker, CreatedUtc = now.AddDays(-4) });
            }
        }
    }

    // ---- Datasets: two real, trainable CSVs with version + schema + profile ----

    private async Task SeedDatasetsAsync(DateTime now, CancellationToken ct)
    {
        await AddDatasetAsync("[Demo] ESP sensor readings",
            "Pressure/vibration sensor readings with failure labels — train the ESP challenge on this.",
            "upstream", BuildEspData().Training, now, ct);

        var rateCsv = new StringBuilder("choke,tubing_pressure,water_cut,oil_rate\n");
        for (var i = 0; i < 150; i++)
        {
            var choke = 1 + (i % 8);
            var tubing = 20 + (i % 25);
            var wc = (i % 10) / 10.0;
            rateCsv.Append($"{choke},{tubing},{wc:0.0},{(40 * choke) + (6 * tubing) - (int)(50 * wc)}\n");
        }

        await AddDatasetAsync("[Demo] Well production rates",
            "Choke size, tubing pressure and water cut vs daily oil rate — a ready-made regression dataset.",
            "upstream", rateCsv.ToString(), now, ct);
    }

    private async Task AddDatasetAsync(string name, string description, string domain, string csv, DateTime now, CancellationToken ct)
    {
        var owner = People[0].UserId;
        var fileRef = await SaveCsvAsync(csv, $"demo/datasets/{Guid.NewGuid():N}.csv", ct);

        var dataset = new Dataset
        {
            Name = name,
            Description = description,
            OwnerUserId = owner,
            VisibilityScope = VisibilityScope.Company,
            VisibilityOrgUnitId = Guid.Empty,
            Classification = KocDataClassification.Internal,
            Domain = domain,
            LatestVersionNumber = 1,
            FileArtifactId = fileRef.Id,
            CreatedByUserId = Marker,
            CreatedUtc = now.AddDays(-10),
        };
        db.Add(dataset);

        var version = new DatasetVersion
        {
            DatasetId = dataset.Id,
            VersionNumber = 1,
            Status = "published",
            TotalSizeBytes = fileRef.SizeBytes,
            Sha256 = fileRef.Sha256,
            PublishedUtc = now.AddDays(-9),
            PublishedByUserId = owner,
            CreatedByUserId = Marker,
            CreatedUtc = now.AddDays(-10),
        };
        db.Add(version);
        db.Add(new DatasetFile
        {
            DatasetVersionId = version.Id,
            ArtifactReferenceId = fileRef.Id,
            LogicalPath = "data.csv",
            ContentType = "text/csv",
            SizeBytes = fileRef.SizeBytes,
            RowCount = csv.Count(c => c == '\n') - 1,
            CreatedByUserId = Marker,
            CreatedUtc = now.AddDays(-10),
        });

        // Real inferred schema + profile so the "Manage data" dialog is fully populated.
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        var result = CsvProfiler.Profile(stream);
        var profile = new DatasetProfile { DatasetVersionId = version.Id, SampledRows = result.SampledRows, TotalRows = result.TotalRows, GeneratedUtc = now, CreatedByUserId = Marker, CreatedUtc = now };
        db.Add(profile);
        foreach (var (col, ordinal) in result.Columns.Select((c, i) => (c, i)))
        {
            db.Add(new DatasetSchemaColumn { DatasetVersionId = version.Id, Ordinal = ordinal, ColumnName = col.Name, DataType = col.DataType, Nullable = col.Nullable, CreatedByUserId = Marker, CreatedUtc = now });
            db.Add(new DatasetProfileColumn { DatasetProfileId = profile.Id, ColumnName = col.Name, NullCount = col.NullCount, DistinctCount = col.DistinctCount, Min = col.Min, Max = col.Max, Mean = col.Mean, CreatedByUserId = Marker, CreatedUtc = now });
        }
    }

    // ---- Experiment: one experiment with three completed runs and a trial curve ----

    private void SeedExperiment(DateTime now)
    {
        var experiment = new Experiment
        {
            Name = "[Demo] ESP failure — algorithm shoot-out",
            Description = "Three tracked runs comparing algorithms on the ESP dataset.",
            OwnerUserId = "dev-admin",
            Status = "active",
            CreatedByUserId = Marker,
            CreatedUtc = now.AddDays(-4),
        };
        db.Add(experiment);

        (string Algorithm, double Score, int Trials)[] runs = [("FastTree", 0.947, 9), ("SdcaLogisticRegression", 0.921, 7), ("FastForest", 0.902, 6)];
        foreach (var (spec, index) in runs.Select((r, i) => (r, i)))
        {
            var run = new ExperimentRun
            {
                ExperimentId = experiment.Id,
                RunByUserId = "dev-admin",
                Status = "completed",
                Task = "BinaryClassification",
                Algorithm = spec.Algorithm,
                PrimaryMetric = "Accuracy",
                PrimaryValue = spec.Score,
                SecondaryMetric = "AUC",
                SecondaryValue = spec.Score + 0.02,
                RowCount = 180,
                TrialCount = spec.Trials,
                IsBest = index == 0,
                StartedUtc = now.AddDays(-4).AddHours(index),
                CompletedUtc = now.AddDays(-4).AddHours(index + 1),
                CreatedByUserId = Marker,
                CreatedUtc = now.AddDays(-4),
            };
            db.Add(run);

            // A rising trial curve toward the final score.
            for (var t = 1; t <= spec.Trials; t++)
            {
                db.Add(new RunMetric
                {
                    RunId = run.Id,
                    Name = "Accuracy",
                    Value = Math.Round(spec.Score - ((spec.Trials - t) * 0.012), 4),
                    Dataset = "validation",
                    Phase = "trial",
                    Step = t,
                    LoggedUtc = now.AddDays(-4).AddMinutes(t * 5),
                    CreatedByUserId = Marker,
                    CreatedUtc = now.AddDays(-4),
                });
            }
        }

        experiment.BestRunId = null;   // recomputed lazily by the service; IsBest flag drives the UI
    }

    // ---- Notifications: make the dev personas' bells ring ----

    private void SeedNotifications(Competition active, DateTime now)
    {
        foreach (var user in new[] { "dev-admin", "dev-user" })
        {
            db.Add(new Notification { UserId = user, Type = "welcome", Title = "Demo data is live", Message = "Explore the ESP Failure Challenge, the leaderboards, and the demo colleagues.", LinkUrl = "/compete", CreatedByUserId = Marker, CreatedUtc = now });
            db.Add(new Notification { UserId = user, Type = "submission-scored", Title = "Submission scored", Message = "Your ESP challenge entry landed on the leaderboard.", LinkUrl = $"/compete/{active.Id}", CreatedByUserId = Marker, CreatedUtc = now.AddHours(-2) });
        }

        db.Add(new Notification { UserId = "dev-emp-1", Type = "kudos", Title = "You received kudos 👏", Message = $"{People[3].Name}: Welcome to the challenge — strong first submission!", LinkUrl = "/profile", CreatedByUserId = Marker, CreatedUtc = now.AddDays(-3) });
    }

    // ---- Data builders ----

    private static (string Training, string Evaluation, string AnswerKey) BuildEspData()
    {
        var training = new StringBuilder("id,pressure,vibration,label\n");
        for (var i = 0; i < 90; i++)
        {
            training.Append($"tr{i}p,{8 + (i % 3)},{8 + ((i / 3) % 3)},true\n");
            training.Append($"tr{i}n,{i % 3},{(i / 3) % 3},false\n");
        }

        var evaluation = new StringBuilder("id,pressure,vibration\n");
        var answerKey = new StringBuilder("id,label\n");
        for (var k = 0; k < 40; k++)
        {
            var failing = k % 2 == 0;
            evaluation.Append($"e{k},{(failing ? 8 + (k % 3) : k % 3)},{(failing ? 8 + (k % 2) : k % 2)}\n");
            answerKey.Append($"e{k},{(failing ? "true" : "false")}\n");
        }

        return (training.ToString(), evaluation.ToString(), answerKey.ToString());
    }

    private async Task<Domain.Storage.ArtifactReference> SaveCsvAsync(string csv, string path, CancellationToken ct)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        return await artifacts.SaveAsync(stream, path, "text/csv", KocDataClassification.Internal, ct);
    }
}
