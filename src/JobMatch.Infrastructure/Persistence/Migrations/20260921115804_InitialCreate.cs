using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace JobMatch.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:citext", ",,");

            migrationBuilder.CreateTable(
                name: "company",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    city = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_company", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "skill",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "citext", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_skill", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    user_name = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    normalized_user_name = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    email = table.Column<string>(type: "citext", maxLength: 320, nullable: false),
                    normalized_email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    email_confirmed = table.Column<bool>(type: "boolean", nullable: false),
                    password_hash = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    security_stamp = table.Column<string>(type: "text", nullable: true),
                    concurrency_stamp = table.Column<string>(type: "text", nullable: true),
                    phone_number = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    phone_number_confirmed = table.Column<bool>(type: "boolean", nullable: false),
                    two_factor_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    lockout_end = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    lockout_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    access_failed_count = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.id);
                    table.CheckConstraint("ck_users_role", "role IN ('Candidate', 'Employer')");
                });

            migrationBuilder.CreateTable(
                name: "vacancy",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    requirements = table.Column<string>(type: "text", nullable: false),
                    city = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    work_format = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    salary_from = table.Column<int>(type: "integer", nullable: true),
                    salary_to = table.Column<int>(type: "integer", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    published_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    closed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_vacancy", x => x.id);
                    table.CheckConstraint("ck_vacancy_salary", "(salary_from IS NULL OR salary_from >= 0) AND (salary_to IS NULL OR salary_to >= 0) AND (salary_from IS NULL OR salary_to IS NULL OR salary_from <= salary_to)");
                    table.CheckConstraint("ck_vacancy_status", "status IN ('Draft', 'Published', 'Closed')");
                    table.CheckConstraint("ck_vacancy_work_format", "work_format IN ('Office', 'Remote', 'Hybrid')");
                    table.ForeignKey(
                        name: "fk_vacancy_company",
                        column: x => x.company_id,
                        principalTable: "company",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "candidate_profile",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    full_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    phone = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    city = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_candidate_profile", x => x.id);
                    table.ForeignKey(
                        name: "fk_candidate_profile_users",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "company_member",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    member_role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_company_member", x => x.id);
                    table.CheckConstraint("ck_company_member_role", "member_role IN ('Owner')");
                    table.ForeignKey(
                        name: "fk_company_member_company",
                        column: x => x.company_id,
                        principalTable: "company",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_company_member_users",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "user_claim",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    claim_type = table.Column<string>(type: "text", nullable: true),
                    claim_value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_claim", x => x.id);
                    table.ForeignKey(
                        name: "FK_user_claim_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_login",
                columns: table => new
                {
                    login_provider = table.Column<string>(type: "text", nullable: false),
                    provider_key = table.Column<string>(type: "text", nullable: false),
                    provider_display_name = table.Column<string>(type: "text", nullable: true),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_login", x => new { x.login_provider, x.provider_key });
                    table.ForeignKey(
                        name: "FK_user_login_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_token",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    login_provider = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_token", x => new { x.user_id, x.login_provider, x.name });
                    table.ForeignKey(
                        name: "FK_user_token_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "vacancy_skill",
                columns: table => new
                {
                    vacancy_id = table.Column<Guid>(type: "uuid", nullable: false),
                    skill_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_vacancy_skill", x => new { x.vacancy_id, x.skill_id });
                    table.ForeignKey(
                        name: "fk_vacancy_skill_skill",
                        column: x => x.skill_id,
                        principalTable: "skill",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_vacancy_skill_vacancy",
                        column: x => x.vacancy_id,
                        principalTable: "vacancy",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "credit_account",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    candidate_profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    period_date = table.Column<DateOnly>(type: "date", nullable: false),
                    balance = table.Column<int>(type: "integer", nullable: false),
                    daily_limit = table.Column<int>(type: "integer", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_credit_account", x => x.id);
                    table.CheckConstraint("ck_credit_account_balance", "daily_limit > 0 AND balance >= 0 AND balance <= daily_limit");
                    table.ForeignKey(
                        name: "fk_credit_account_candidate_profile",
                        column: x => x.candidate_profile_id,
                        principalTable: "candidate_profile",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "resume",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    candidate_profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    desired_position = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    summary = table.Column<string>(type: "text", nullable: false),
                    contact_email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    contact_phone = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_resume", x => x.id);
                    table.ForeignKey(
                        name: "fk_resume_candidate_profile",
                        column: x => x.candidate_profile_id,
                        principalTable: "candidate_profile",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "application",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    candidate_profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    resume_id = table.Column<Guid>(type: "uuid", nullable: false),
                    vacancy_id = table.Column<Guid>(type: "uuid", nullable: false),
                    idempotency_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    cover_letter = table.Column<string>(type: "text", nullable: true),
                    resume_snapshot = table.Column<JsonDocument>(type: "jsonb", nullable: false),
                    snapshot_version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    submitted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_application", x => x.id);
                    table.CheckConstraint("ck_application_snapshot_version", "snapshot_version > 0");
                    table.CheckConstraint("ck_application_status", "status IN ('Submitted', 'Viewed', 'Invited', 'Rejected', 'Withdrawn')");
                    table.ForeignKey(
                        name: "fk_application_candidate_profile",
                        column: x => x.candidate_profile_id,
                        principalTable: "candidate_profile",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_application_resume",
                        column: x => x.resume_id,
                        principalTable: "resume",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_application_vacancy",
                        column: x => x.vacancy_id,
                        principalTable: "vacancy",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "education",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    resume_id = table.Column<Guid>(type: "uuid", nullable: false),
                    institution = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    specialty = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    degree = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    graduation_year = table.Column<short>(type: "smallint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_education", x => x.id);
                    table.CheckConstraint("ck_education_graduation_year", "graduation_year IS NULL OR graduation_year BETWEEN 1900 AND 2100");
                    table.ForeignKey(
                        name: "fk_education_resume",
                        column: x => x.resume_id,
                        principalTable: "resume",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "resume_skill",
                columns: table => new
                {
                    resume_id = table.Column<Guid>(type: "uuid", nullable: false),
                    skill_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_resume_skill", x => new { x.resume_id, x.skill_id });
                    table.ForeignKey(
                        name: "fk_resume_skill_resume",
                        column: x => x.resume_id,
                        principalTable: "resume",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_resume_skill_skill",
                        column: x => x.skill_id,
                        principalTable: "skill",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "work_experience",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    resume_id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    position = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    started_on = table.Column<DateOnly>(type: "date", nullable: false),
                    ended_on = table.Column<DateOnly>(type: "date", nullable: true),
                    description = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_work_experience", x => x.id);
                    table.CheckConstraint("ck_work_experience_dates", "ended_on IS NULL OR ended_on >= started_on");
                    table.ForeignKey(
                        name: "fk_work_experience_resume",
                        column: x => x.resume_id,
                        principalTable: "resume",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "credit_transaction",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    credit_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    application_id = table.Column<Guid>(type: "uuid", nullable: true),
                    type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    amount = table.Column<int>(type: "integer", nullable: false),
                    reason = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_credit_transaction", x => x.id);
                    table.CheckConstraint("ck_credit_transaction_amount", "amount > 0");
                    table.CheckConstraint("ck_credit_transaction_application", "(type = 'Debit' AND application_id IS NOT NULL) OR (type = 'DailyReset' AND application_id IS NULL)");
                    table.CheckConstraint("ck_credit_transaction_type", "type IN ('DailyReset', 'Debit')");
                    table.ForeignKey(
                        name: "fk_credit_transaction_application",
                        column: x => x.application_id,
                        principalTable: "application",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_credit_transaction_credit_account",
                        column: x => x.credit_account_id,
                        principalTable: "credit_account",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_application_resume_id",
                table: "application",
                column: "resume_id");

            migrationBuilder.CreateIndex(
                name: "ix_application_vacancy_id",
                table: "application",
                column: "vacancy_id");

            migrationBuilder.CreateIndex(
                name: "ux_application_candidate_idempotency",
                table: "application",
                columns: new[] { "candidate_profile_id", "idempotency_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_application_candidate_vacancy",
                table: "application",
                columns: new[] { "candidate_profile_id", "vacancy_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_candidate_profile_user_id",
                table: "candidate_profile",
                column: "user_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_company_member_company_id",
                table: "company_member",
                column: "company_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_company_member_user_id",
                table: "company_member",
                column: "user_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_credit_account_candidate_profile_id",
                table: "credit_account",
                column: "candidate_profile_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_credit_transaction_credit_account_id",
                table: "credit_transaction",
                column: "credit_account_id");

            migrationBuilder.CreateIndex(
                name: "ux_credit_transaction_debit_application_id",
                table: "credit_transaction",
                column: "application_id",
                unique: true,
                filter: "application_id IS NOT NULL AND type = 'Debit'");

            migrationBuilder.CreateIndex(
                name: "ix_education_resume_id",
                table: "education",
                column: "resume_id");

            migrationBuilder.CreateIndex(
                name: "ux_resume_candidate_profile_id",
                table: "resume",
                column: "candidate_profile_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_resume_skill_skill_id",
                table: "resume_skill",
                column: "skill_id");

            migrationBuilder.CreateIndex(
                name: "ux_skill_name",
                table: "skill",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_user_claim_user_id",
                table: "user_claim",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_login_user_id",
                table: "user_login",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_users_normalized_email",
                table: "users",
                column: "normalized_email");

            migrationBuilder.CreateIndex(
                name: "ux_users_email",
                table: "users",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_users_normalized_user_name",
                table: "users",
                column: "normalized_user_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_vacancy_city",
                table: "vacancy",
                column: "city");

            migrationBuilder.CreateIndex(
                name: "ix_vacancy_company_id",
                table: "vacancy",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "ix_vacancy_status_published_at",
                table: "vacancy",
                columns: new[] { "status", "published_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_vacancy_work_format",
                table: "vacancy",
                column: "work_format");

            migrationBuilder.CreateIndex(
                name: "ix_vacancy_skill_skill_id",
                table: "vacancy_skill",
                column: "skill_id");

            migrationBuilder.CreateIndex(
                name: "ix_work_experience_resume_id",
                table: "work_experience",
                column: "resume_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "company_member");

            migrationBuilder.DropTable(
                name: "credit_transaction");

            migrationBuilder.DropTable(
                name: "education");

            migrationBuilder.DropTable(
                name: "resume_skill");

            migrationBuilder.DropTable(
                name: "user_claim");

            migrationBuilder.DropTable(
                name: "user_login");

            migrationBuilder.DropTable(
                name: "user_token");

            migrationBuilder.DropTable(
                name: "vacancy_skill");

            migrationBuilder.DropTable(
                name: "work_experience");

            migrationBuilder.DropTable(
                name: "application");

            migrationBuilder.DropTable(
                name: "credit_account");

            migrationBuilder.DropTable(
                name: "skill");

            migrationBuilder.DropTable(
                name: "resume");

            migrationBuilder.DropTable(
                name: "vacancy");

            migrationBuilder.DropTable(
                name: "candidate_profile");

            migrationBuilder.DropTable(
                name: "company");

            migrationBuilder.DropTable(
                name: "users");
        }
    }
}
