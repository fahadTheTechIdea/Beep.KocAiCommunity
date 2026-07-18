# Data Model Catalog

Entity-by-entity table for the EF Core model. Each entry: table name, schema, primary purpose, columns of interest, indexes, and the plan phase that creates it.

Schemas:
- `dbo` — Identity tables (managed by `AddIdentity`)
- `koc` — application tables (community, datasets, projects, workflows, experiments, models, competitions)
- `platform` — admin/settings/audit tables

All entities inherit `AuditableEntity` unless noted (CreatedUtc, CreatedByUserId, LastModifiedUtc, LastModifiedByUserId, IsDeleted, RowVersion).

## Identity (schema: dbo)

Standard ASP.NET Identity tables managed by `AddIdentity<IdentityUser, IdentityRole>`.

| Table | Purpose | Phase |
|---|---|---|
| AspNetUsers | Identity users | 02 |
| AspNetRoles | Identity roles | 02 |
| AspNetUserRoles | User-role mapping | 02 |
| AspNetUserClaims | Claims | 02 |
| AspNetUserLogins | External login providers | 02 |
| AspNetUserTokens | Token storage | 02 |
| AspNetRoleClaims | Role claims | 02 |

## Application entities (schema: koc)

### Identity supplement

| Table | Purpose | Phase |
|---|---|---|
| UserProfile | KOC user profile (JobTitle, Bio, Skills, Interests) | 06 |
| UserSkill | User skill tag | 06 |
| UserInterest | User interest tag | 06 |

### Org hierarchy and RBAC (Phase 02)

| Table | Purpose |
|---|---|
| OrgUnit | KOC org node: Type (Company/Directorate/Group/Team), ParentId, Path (materialized), LeaderUserId |
| OrgMembership | UserId, OrgUnitId, PositionLevel (Employee/TeamLeader/Manager/DCEO/CEO), IsPrimary, From/To |
| UserEntityPermission | UserId, ResourceType, ResourceId, PermissionKey, Granted/Expires |

`VisibilityScope` (Team/Group/Directorate/Company) is a shared enum, not a table; it is stored as columns (`VisibilityScope`, `VisibilityOrgUnitId`) on Dataset, Project, Competition, and LearningTrack.

### Collaboration (Phase 06)

| Table | Purpose |
|---|---|
| Discussion | Title, body, scope (project/well/field/facility/hse), tags, locked, pinned |
| DiscussionReply | Body, parent reply id, attachments |
| DiscussionVote | UserId, Value (+1/-1) |
| DiscussionAttachment | DiscussionId, ArtifactReferenceId |
| Mention | ReplyId, MentionedUserId |
| Notification | UserId, Type, PayloadJson, ReadUtc |
| ActivityEvent | ActorUserId, Type, ResourceType, ResourceId, PayloadJson, Visibility |

### Datasets and projects (Phase 07)

| Table | Purpose |
|---|---|
| Dataset | Name, description, owner, VisibilityScope + VisibilityOrgUnitId, classification, license, domain |
| DatasetVersion | VersionNumber, status (draft/published/archived), notes, source connector info |
| DatasetFile | LogicalPath, ArtifactReferenceId |
| DatasetSchema | ColumnName, DataType, Nullable, Description |
| DatasetProfile | sampleRowCount, generatedUtc |
| DatasetProfileColumn | nullCount, distinctCount, min, max, mean, stdDev |
| Project | Name, description, owner, VisibilityScope + VisibilityOrgUnitId, classification, domain |
| ProjectMember | ProjectId, UserId, Role (owner/contributor/viewer) |
| ProjectActivity | ProjectId, ActorUserId, Type, PayloadJson |
| ProjectTemplate | Name, PayloadJson |
| DatasetImportJob | Status, payload, started, completed |
| DatasetImportLog | JobId, message, severity |

### Connectors (Phase 07a)

| Table | Purpose |
|---|---|
| ConnectorDefinition | Code, DisplayName, Version, CapabilitiesJson |
| ConnectorInstance | ConnectorDefinitionId, Endpoint, AuthMode, DefaultClassification, HealthProbeIntervalSeconds |
| CredentialVaultEntry | ConnectorInstanceId, Key, EncryptedValue, ProtectionDescriptor, LastRotatedUtc, ExpiresUtc |
| ConnectorHealthSnapshot | ConnectorInstanceId, Status, LatencyMs, DetailJson, MeasuredUtc |

### Workflow (Phase 09)

| Table | Purpose |
|---|---|
| Workflow | Name, description, owner, project id, dataset id, classification |
| WorkflowVersion | VersionNumber, status, SchemaVersion, DefinitionJson, SnapshotHash, Notes |
| WorkflowTemplate | Code, DisplayName, Domain, DefinitionJson, SchemaVersion, SnapshotHash |

### ML entities (Phase 11)

| Table | Purpose |
|---|---|
| Experiment | Name, description, owner, project, status, LatestBestRunId |
| Run | ExperimentId, workflow/dataset references, status, parent run, snapshots |
| RunMetric | RunId, Name, Value, Dataset (train/validation/test), Phase, Step, LoggedUtc |
| RunParameter | RunId, Name, ValueJson |
| RunTag | RunId, Key, Value |
| RunLog | RunId, Severity, Message, LoggedUtc |
| RunArtifact | RunId, ArtifactReferenceId, Type |
| RunSnapshot | RunId, Type (workflow/dataset/environment/dependency) |

### Models (Phase 12)

| Table | Purpose |
|---|---|
| Model | Name, description, owner, project, classification, task type |
| ModelVersion | ModelId, SemVer, status (staging/production/archived/rejected), ArtifactReferenceId, Sha256, SignatureJson, SourceRunId |
| ModelApproval | ModelVersionId, Decision, ApproverUserId, Notes |
| ModelInferenceLog | ModelVersionId, CallerUserId, Endpoint, LatencyMs, CalledUtc, Success, ErrorJson |

### Competitions (Phase 13)

| Table | Purpose |
|---|---|
| Competition | Name, description, status, dates, RevealUtc, sponsor, VisibilityScope + VisibilityOrgUnitId, RecommendedTrackId, dataset reference, classification, rules, quotas, scoring plugin |
| CompetitionPhase | CompetitionId, Name, FromUtc, ToUtc, Type |
| Submission | CompetitionId, submitter, status, type, prediction/model, live/final scores, breakdown |
| LeaderboardEntry | CompetitionId, submitter, best submission, score, rank, IsLive (visible vs concealed-final) |

### Learning tracks (Phase 13a)

| Table | Purpose |
|---|---|
| LearningTrack | Title, Summary, Level (Beginner/Intermediate/Advanced), OrderNo, Status, Domain, VisibilityScope + VisibilityOrgUnitId, RecommendedCompetitionId |
| Lesson | TrackId, OrderNo, Title, ContentRef (markdown artifact), EstimatedMinutes, HandsOnKind, HandsOnRefId |
| TrackEnrollment | TrackId, UserId, Status (active/completed/abandoned), Started/Completed |
| LessonProgress | EnrollmentId, LessonId, Status (not-started/in-progress/completed), CompletedUtc |
| TrackCompletion | TrackId, UserId, CompletedUtc |

### Templates (Phase 14)

| Table | Purpose |
|---|---|
| IndustryTemplateDefinition | Code, DisplayName, Subdomain (upstream/midstream/downstream/hse), description, visibility |
| IndustryTemplateVersion | TemplateDefinitionId, VersionNumber, status, SchemaVersion, DefinitionJson, SnapshotHash |

## Platform entities (schema: platform)

### Settings and audit (Phase 14a)

| Table | Purpose |
|---|---|
| SettingsCategory | Code, Name, Description, IsSensitive |
| SettingDefinition | CategoryId, Key, DataType, IsRequired, DefaultValueJson, Description |
| SettingValue | DefinitionId, ValueJson, EncryptedSecret, Version, EffectiveFromUtc, EffectiveToUtc, ChangedByUserId, ChangedAtUtc |
| SettingOverride | DefinitionId, Scope, ScopeId, ValueJson, EncryptedSecret, ExpiresAtUtc |
| SettingAudit | DefinitionId, OldValueJson, NewValueJson, ChangedByUserId, ChangedAtUtc, Reason |
| FeatureFlag | Key, Description, IsEnabled, RolloutPercent, AllowedRoles |
| PlatformRole | Code, Name, Description |
| PlatformRolePermission | RoleId, PermissionKey |
| UserPlatformRole | UserId, RoleId, GrantedUtc, GrantedByUserId, ExpiresUtc |
| AdminAuditLog | ActorUserId, ActorRole, Action, Resource, ResourceId, BeforeJson, AfterJson, IpAddress, UserAgent, RequestId, OccurredUtc |
| AdminSession | UserId, StartedUtc, LastSeenUtc, IpAddress, UserAgent, SignOutUtc, RevokedUtc, RevokedByUserId |
| SystemHealthSnapshot | Component, Status, DetailJson, MeasuredUtc |
| MaintenanceTask | Name, Description, ScheduleCron, LastRunUtc, LastRunStatus, LastRunDurationMs, LastError |
| RateLimitPolicy | Scope, EndpointPattern, PermitLimit, WindowSeconds, QueueLimit, PartitionKey |
| EmailTemplate | Code, SubjectTemplate, BodyTemplate, Language, Version, IsActive |
| Notification | UserId, Severity, Category, Title, Body, LinkUrl, CreatedUtc, ReadUtc |

### Background and outbox (Phases 04, 10)

| Table | Purpose |
|---|---|
| OutboxMessage | Id, Type, PayloadJson, CreatedUtc, ProcessedUtc, RetryCount, LastError |
| Job | Id, Type, PayloadJson, Status, AttemptCount, MaxAttempts, LeaseExpiresUtc, LeaseOwnerId, LastHeartbeatUtc, NextAttemptUtc, StartedUtc, CompletedUtc, LastError, Priority |
| JobAttempt | JobId, AttemptNumber, StartedUtc, CompletedUtc, Status, ErrorJson, WorkerId |
| JobLog | JobId, LoggedUtc, Severity, Message, PayloadJson |

## Indexing strategy

| Index | Purpose |
|---|---|
| `OutboxMessage(ProcessedUtc, CreatedUtc)` | Dispatcher scan |
| `Job(Status, NextAttemptUtc)` | Worker claim scan |
| `Job(LeaseExpiresUtc)` | Lease recovery scan |
| `RunMetric(RunId, Name, Step)` | Metric timeline queries |
| `Discussion(ScopeType, ScopeId, CreatedUtc DESC)` | Threaded discussion listing |
| `DiscussionVote(DiscussionId)` | Vote aggregation |
| `Notification(UserId, ReadUtc)` | Unread count |
| `LeaderboardEntry(CompetitionId, IsLive, Rank)` | Leaderboard rendering |
| `OrgUnit(Path)` prefix | Supervisory subtree + visibility subtree scans |
| `OrgMembership(UserId, IsPrimary)` unique (filtered) | Resolve a user's home unit + position |
| `TrackEnrollment(TrackId, UserId)` unique | Idempotent enrollment |
| `LessonProgress(EnrollmentId, LessonId)` unique | Progress upsert |
| `Dataset(VisibilityScope, VisibilityOrgUnitId)` | Visibility-filtered listing |
| `Dataset(Name)` full-text | Dataset search |
| `Project(Name)` full-text | Project search |
| `ActivityEvent(ActorUserId, OccurredUtc DESC)` | Activity feed |
| `ArtifactReference(Sha256)` | Deduplication |
| `AdminAuditLog(OccurredUtc DESC)` | Audit feed |
| `AdminAuditLog(ActorUserId, OccurredUtc DESC)` | Actor timeline |
| `AdminSession(UserId, SignOutUtc, RevokedUtc)` | Active session query |
| `SystemHealthSnapshot(Component, MeasuredUtc DESC)` | Component history |
| `SettingDefinition(CategoryId, Key)` unique | Settings lookup |
| `SettingValue(DefinitionId, EffectiveFromUtc, EffectiveToUtc)` | Settings resolution |
