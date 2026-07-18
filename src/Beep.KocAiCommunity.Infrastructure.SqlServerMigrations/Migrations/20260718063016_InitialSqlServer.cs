using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Beep.KocAiCommunity.Infrastructure.SqlServerMigrations.Migrations;

/// <inheritdoc />
public partial class InitialSqlServer : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(
            name: "platform");

        migrationBuilder.EnsureSchema(
            name: "koc");

        migrationBuilder.CreateTable(
            name: "AdminAuditLog",
            schema: "platform",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ActorUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                ActorRole = table.Column<string>(type: "nvarchar(max)", nullable: true),
                Action = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                Resource = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                ResourceId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                BeforeJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                AfterJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                IpAddress = table.Column<string>(type: "nvarchar(max)", nullable: true),
                UserAgent = table.Column<string>(type: "nvarchar(max)", nullable: true),
                RequestId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                OccurredUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AdminAuditLog", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "ArtifactReferences",
            schema: "koc",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                StorageKey = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                LogicalPath = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: false),
                ContentType = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                Sha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                Classification = table.Column<int>(type: "int", nullable: false),
                CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                CreatedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                LastModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                LastModifiedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ArtifactReferences", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "AspNetRoles",
            columns: table => new
            {
                Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                NormalizedName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AspNetRoles", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "AspNetUsers",
            columns: table => new
            {
                Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                UserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                NormalizedUserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                NormalizedEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                EmailConfirmed = table.Column<bool>(type: "bit", nullable: false),
                PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: true),
                SecurityStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                PhoneNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                PhoneNumberConfirmed = table.Column<bool>(type: "bit", nullable: false),
                TwoFactorEnabled = table.Column<bool>(type: "bit", nullable: false),
                LockoutEnd = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                LockoutEnabled = table.Column<bool>(type: "bit", nullable: false),
                AccessFailedCount = table.Column<int>(type: "int", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AspNetUsers", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Competitions",
            schema: "koc",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Title = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                Description = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                VisibilityScope = table.Column<int>(type: "int", nullable: false),
                VisibilityOrgUnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                RevealUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                SubmissionQuotaPerDay = table.Column<int>(type: "int", nullable: false),
                ScorerCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                AnswerKeyArtifactId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                RecommendedTrackId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                CreatedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                LastModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                LastModifiedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Competitions", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Datasets",
            schema: "koc",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                Description = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                OwnerUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                VisibilityScope = table.Column<int>(type: "int", nullable: false),
                VisibilityOrgUnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Classification = table.Column<int>(type: "int", nullable: false),
                Domain = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                Tags = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                FileArtifactId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                CreatedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                LastModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                LastModifiedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Datasets", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Discussions",
            schema: "koc",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Title = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                Body = table.Column<string>(type: "nvarchar(max)", maxLength: 8192, nullable: false),
                AuthorUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                VisibilityScope = table.Column<int>(type: "int", nullable: false),
                VisibilityOrgUnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                CreatedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                LastModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                LastModifiedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Discussions", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "LearningTracks",
            schema: "koc",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Title = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                Summary = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: false),
                Level = table.Column<int>(type: "int", nullable: false),
                OrderNo = table.Column<int>(type: "int", nullable: false),
                Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                Domain = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                VisibilityScope = table.Column<int>(type: "int", nullable: false),
                VisibilityOrgUnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                RecommendedCompetitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                CreatedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                LastModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                LastModifiedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_LearningTracks", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "LessonProgress",
            schema: "koc",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                EnrollmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                LessonId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                CompletedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                CreatedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                LastModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                LastModifiedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_LessonProgress", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "ModelRuns",
            schema: "koc",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                DatasetName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                LabelColumn = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                Algorithm = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                Accuracy = table.Column<double>(type: "float", nullable: false),
                AreaUnderRoc = table.Column<double>(type: "float", nullable: false),
                RowCount = table.Column<long>(type: "bigint", nullable: false),
                RunByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                CompletedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                CreatedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                LastModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                LastModifiedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ModelRuns", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "OrgUnits",
            schema: "koc",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                Type = table.Column<int>(type: "int", nullable: false),
                ParentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                Path = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: false),
                LeaderUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                CreatedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                LastModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                LastModifiedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_OrgUnits", x => x.Id);
                table.ForeignKey(
                    name: "FK_OrgUnits_OrgUnits_ParentId",
                    column: x => x.ParentId,
                    principalSchema: "koc",
                    principalTable: "OrgUnits",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "OutboxMessages",
            schema: "platform",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Type = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                ProcessedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                RetryCount = table.Column<int>(type: "int", nullable: false),
                LastError = table.Column<string>(type: "nvarchar(max)", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_OutboxMessages", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "TrackCompletions",
            schema: "koc",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                TrackId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                CompletedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                CreatedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                LastModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                LastModifiedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_TrackCompletions", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "TrackEnrollments",
            schema: "koc",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                TrackId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                StartedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                CompletedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                CreatedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                LastModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                LastModifiedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_TrackEnrollments", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "UserEntityPermissions",
            schema: "koc",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                ResourceType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                ResourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                PermissionKey = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                GrantedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                GrantedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                ExpiresUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_UserEntityPermissions", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "AspNetRoleClaims",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                RoleId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AspNetRoleClaims", x => x.Id);
                table.ForeignKey(
                    name: "FK_AspNetRoleClaims_AspNetRoles_RoleId",
                    column: x => x.RoleId,
                    principalTable: "AspNetRoles",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "AspNetUserClaims",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AspNetUserClaims", x => x.Id);
                table.ForeignKey(
                    name: "FK_AspNetUserClaims_AspNetUsers_UserId",
                    column: x => x.UserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "AspNetUserLogins",
            columns: table => new
            {
                LoginProvider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                ProviderKey = table.Column<string>(type: "nvarchar(450)", nullable: false),
                ProviderDisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                UserId = table.Column<string>(type: "nvarchar(450)", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AspNetUserLogins", x => new { x.LoginProvider, x.ProviderKey });
                table.ForeignKey(
                    name: "FK_AspNetUserLogins_AspNetUsers_UserId",
                    column: x => x.UserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "AspNetUserRoles",
            columns: table => new
            {
                UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                RoleId = table.Column<string>(type: "nvarchar(450)", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AspNetUserRoles", x => new { x.UserId, x.RoleId });
                table.ForeignKey(
                    name: "FK_AspNetUserRoles_AspNetRoles_RoleId",
                    column: x => x.RoleId,
                    principalTable: "AspNetRoles",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_AspNetUserRoles_AspNetUsers_UserId",
                    column: x => x.UserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "AspNetUserTokens",
            columns: table => new
            {
                UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                LoginProvider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                Name = table.Column<string>(type: "nvarchar(450)", nullable: false),
                Value = table.Column<string>(type: "nvarchar(max)", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AspNetUserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                table.ForeignKey(
                    name: "FK_AspNetUserTokens_AspNetUsers_UserId",
                    column: x => x.UserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "LeaderboardEntries",
            schema: "koc",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CompetitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                SubmitterUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                BestSubmissionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                Score = table.Column<double>(type: "float", nullable: false),
                Rank = table.Column<int>(type: "int", nullable: false),
                CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                CreatedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                LastModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                LastModifiedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_LeaderboardEntries", x => x.Id);
                table.ForeignKey(
                    name: "FK_LeaderboardEntries_Competitions_CompetitionId",
                    column: x => x.CompetitionId,
                    principalSchema: "koc",
                    principalTable: "Competitions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "Submissions",
            schema: "koc",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CompetitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                SubmitterUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                PredictionArtifactId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                SubmittedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                Score = table.Column<double>(type: "float", nullable: true),
                Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                CreatedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                LastModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                LastModifiedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Submissions", x => x.Id);
                table.ForeignKey(
                    name: "FK_Submissions_Competitions_CompetitionId",
                    column: x => x.CompetitionId,
                    principalSchema: "koc",
                    principalTable: "Competitions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "DiscussionReplies",
            schema: "koc",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                DiscussionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                AuthorUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                Body = table.Column<string>(type: "nvarchar(max)", maxLength: 8192, nullable: false),
                CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                CreatedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                LastModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                LastModifiedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_DiscussionReplies", x => x.Id);
                table.ForeignKey(
                    name: "FK_DiscussionReplies_Discussions_DiscussionId",
                    column: x => x.DiscussionId,
                    principalSchema: "koc",
                    principalTable: "Discussions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "Lessons",
            schema: "koc",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                TrackId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                OrderNo = table.Column<int>(type: "int", nullable: false),
                Title = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                ContentRef = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                EstimatedMinutes = table.Column<int>(type: "int", nullable: false),
                HandsOnKind = table.Column<string>(type: "nvarchar(max)", nullable: true),
                HandsOnRefId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                CreatedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                LastModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                LastModifiedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Lessons", x => x.Id);
                table.ForeignKey(
                    name: "FK_Lessons_LearningTracks_TrackId",
                    column: x => x.TrackId,
                    principalSchema: "koc",
                    principalTable: "LearningTracks",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "OrgMemberships",
            schema: "koc",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                OrgUnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                PositionLevel = table.Column<int>(type: "int", nullable: false),
                IsPrimary = table.Column<bool>(type: "bit", nullable: false),
                FromUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                ToUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                CreatedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                LastModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                LastModifiedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_OrgMemberships", x => x.Id);
                table.ForeignKey(
                    name: "FK_OrgMemberships_OrgUnits_OrgUnitId",
                    column: x => x.OrgUnitId,
                    principalSchema: "koc",
                    principalTable: "OrgUnits",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_AdminAuditLog_ActorUserId_OccurredUtc",
            schema: "platform",
            table: "AdminAuditLog",
            columns: new[] { "ActorUserId", "OccurredUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_AdminAuditLog_OccurredUtc",
            schema: "platform",
            table: "AdminAuditLog",
            column: "OccurredUtc");

        migrationBuilder.CreateIndex(
            name: "IX_ArtifactReferences_Sha256",
            schema: "koc",
            table: "ArtifactReferences",
            column: "Sha256",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_ArtifactReferences_StorageKey",
            schema: "koc",
            table: "ArtifactReferences",
            column: "StorageKey");

        migrationBuilder.CreateIndex(
            name: "IX_AspNetRoleClaims_RoleId",
            table: "AspNetRoleClaims",
            column: "RoleId");

        migrationBuilder.CreateIndex(
            name: "RoleNameIndex",
            table: "AspNetRoles",
            column: "NormalizedName",
            unique: true,
            filter: "[NormalizedName] IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "IX_AspNetUserClaims_UserId",
            table: "AspNetUserClaims",
            column: "UserId");

        migrationBuilder.CreateIndex(
            name: "IX_AspNetUserLogins_UserId",
            table: "AspNetUserLogins",
            column: "UserId");

        migrationBuilder.CreateIndex(
            name: "IX_AspNetUserRoles_RoleId",
            table: "AspNetUserRoles",
            column: "RoleId");

        migrationBuilder.CreateIndex(
            name: "EmailIndex",
            table: "AspNetUsers",
            column: "NormalizedEmail");

        migrationBuilder.CreateIndex(
            name: "UserNameIndex",
            table: "AspNetUsers",
            column: "NormalizedUserName",
            unique: true,
            filter: "[NormalizedUserName] IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "IX_Competitions_Status_VisibilityScope",
            schema: "koc",
            table: "Competitions",
            columns: new[] { "Status", "VisibilityScope" });

        migrationBuilder.CreateIndex(
            name: "IX_Datasets_OwnerUserId",
            schema: "koc",
            table: "Datasets",
            column: "OwnerUserId");

        migrationBuilder.CreateIndex(
            name: "IX_Datasets_VisibilityScope_VisibilityOrgUnitId",
            schema: "koc",
            table: "Datasets",
            columns: new[] { "VisibilityScope", "VisibilityOrgUnitId" });

        migrationBuilder.CreateIndex(
            name: "IX_DiscussionReplies_DiscussionId_CreatedUtc",
            schema: "koc",
            table: "DiscussionReplies",
            columns: new[] { "DiscussionId", "CreatedUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_Discussions_VisibilityScope_VisibilityOrgUnitId",
            schema: "koc",
            table: "Discussions",
            columns: new[] { "VisibilityScope", "VisibilityOrgUnitId" });

        migrationBuilder.CreateIndex(
            name: "IX_LeaderboardEntries_CompetitionId_Rank",
            schema: "koc",
            table: "LeaderboardEntries",
            columns: new[] { "CompetitionId", "Rank" });

        migrationBuilder.CreateIndex(
            name: "IX_LeaderboardEntries_CompetitionId_SubmitterUserId",
            schema: "koc",
            table: "LeaderboardEntries",
            columns: new[] { "CompetitionId", "SubmitterUserId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_LearningTracks_Status_OrderNo",
            schema: "koc",
            table: "LearningTracks",
            columns: new[] { "Status", "OrderNo" });

        migrationBuilder.CreateIndex(
            name: "IX_LessonProgress_EnrollmentId_LessonId",
            schema: "koc",
            table: "LessonProgress",
            columns: new[] { "EnrollmentId", "LessonId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Lessons_TrackId_OrderNo",
            schema: "koc",
            table: "Lessons",
            columns: new[] { "TrackId", "OrderNo" });

        migrationBuilder.CreateIndex(
            name: "IX_ModelRuns_RunByUserId_CompletedUtc",
            schema: "koc",
            table: "ModelRuns",
            columns: new[] { "RunByUserId", "CompletedUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_OrgMemberships_OrgUnitId",
            schema: "koc",
            table: "OrgMemberships",
            column: "OrgUnitId");

        migrationBuilder.CreateIndex(
            name: "IX_OrgMemberships_UserId_IsPrimary",
            schema: "koc",
            table: "OrgMemberships",
            columns: new[] { "UserId", "IsPrimary" });

        migrationBuilder.CreateIndex(
            name: "IX_OrgUnits_ParentId",
            schema: "koc",
            table: "OrgUnits",
            column: "ParentId");

        migrationBuilder.CreateIndex(
            name: "IX_OrgUnits_Path",
            schema: "koc",
            table: "OrgUnits",
            column: "Path");

        migrationBuilder.CreateIndex(
            name: "IX_OrgUnits_Type_LeaderUserId",
            schema: "koc",
            table: "OrgUnits",
            columns: new[] { "Type", "LeaderUserId" });

        migrationBuilder.CreateIndex(
            name: "IX_OutboxMessages_ProcessedUtc_CreatedUtc",
            schema: "platform",
            table: "OutboxMessages",
            columns: new[] { "ProcessedUtc", "CreatedUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_Submissions_CompetitionId_SubmitterUserId_SubmittedUtc",
            schema: "koc",
            table: "Submissions",
            columns: new[] { "CompetitionId", "SubmitterUserId", "SubmittedUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_TrackCompletions_TrackId_UserId",
            schema: "koc",
            table: "TrackCompletions",
            columns: new[] { "TrackId", "UserId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_TrackCompletions_UserId_CompletedUtc",
            schema: "koc",
            table: "TrackCompletions",
            columns: new[] { "UserId", "CompletedUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_TrackEnrollments_TrackId_UserId",
            schema: "koc",
            table: "TrackEnrollments",
            columns: new[] { "TrackId", "UserId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_UserEntityPermissions_UserId_ResourceType_ResourceId",
            schema: "koc",
            table: "UserEntityPermissions",
            columns: new[] { "UserId", "ResourceType", "ResourceId" });

        migrationBuilder.CreateIndex(
            name: "IX_UserEntityPermissions_UserId_ResourceType_ResourceId_PermissionKey",
            schema: "koc",
            table: "UserEntityPermissions",
            columns: new[] { "UserId", "ResourceType", "ResourceId", "PermissionKey" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "AdminAuditLog",
            schema: "platform");

        migrationBuilder.DropTable(
            name: "ArtifactReferences",
            schema: "koc");

        migrationBuilder.DropTable(
            name: "AspNetRoleClaims");

        migrationBuilder.DropTable(
            name: "AspNetUserClaims");

        migrationBuilder.DropTable(
            name: "AspNetUserLogins");

        migrationBuilder.DropTable(
            name: "AspNetUserRoles");

        migrationBuilder.DropTable(
            name: "AspNetUserTokens");

        migrationBuilder.DropTable(
            name: "Datasets",
            schema: "koc");

        migrationBuilder.DropTable(
            name: "DiscussionReplies",
            schema: "koc");

        migrationBuilder.DropTable(
            name: "LeaderboardEntries",
            schema: "koc");

        migrationBuilder.DropTable(
            name: "LessonProgress",
            schema: "koc");

        migrationBuilder.DropTable(
            name: "Lessons",
            schema: "koc");

        migrationBuilder.DropTable(
            name: "ModelRuns",
            schema: "koc");

        migrationBuilder.DropTable(
            name: "OrgMemberships",
            schema: "koc");

        migrationBuilder.DropTable(
            name: "OutboxMessages",
            schema: "platform");

        migrationBuilder.DropTable(
            name: "Submissions",
            schema: "koc");

        migrationBuilder.DropTable(
            name: "TrackCompletions",
            schema: "koc");

        migrationBuilder.DropTable(
            name: "TrackEnrollments",
            schema: "koc");

        migrationBuilder.DropTable(
            name: "UserEntityPermissions",
            schema: "koc");

        migrationBuilder.DropTable(
            name: "AspNetRoles");

        migrationBuilder.DropTable(
            name: "AspNetUsers");

        migrationBuilder.DropTable(
            name: "Discussions",
            schema: "koc");

        migrationBuilder.DropTable(
            name: "LearningTracks",
            schema: "koc");

        migrationBuilder.DropTable(
            name: "OrgUnits",
            schema: "koc");

        migrationBuilder.DropTable(
            name: "Competitions",
            schema: "koc");
    }
}
