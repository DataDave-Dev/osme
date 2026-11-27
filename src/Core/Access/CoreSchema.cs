// SPDX-License-Identifier: LGPL-3.0-or-later
using Npgsql;

namespace Osme.Core.Access;

/// <summary>
/// The tables the core owns: company, accounts, roles, sessions and settings. Written by hand
/// following documents/arquitectura/base-de-datos.md, because the document engine does not
/// generate them (change 0-2-1, decision 1). Creates what is missing and never drops or alters.
/// </summary>
public static class CoreSchema
{
    /// <summary>Origin and internal name of the one system role. The interface translates it.</summary>
    public const string AdministratorRole = "core.administrator";

    // Arbitrary, fixed number: two servers starting at once on the same database take turns.
    private const long StartupLock = 0x4F534D45; // "OSME"

    private const string Structure = """
        CREATE SCHEMA IF NOT EXISTS core;

        CREATE TABLE IF NOT EXISTS core.user_account (
            id                   uuid        NOT NULL,
            username             text        NOT NULL,
            display_name         text        NOT NULL,
            password_hash        text        NOT NULL,
            is_active            boolean     NOT NULL DEFAULT true,
            must_change_password boolean     NOT NULL DEFAULT false,
            failed_attempts      integer     NOT NULL DEFAULT 0,
            locked_until         timestamptz,
            created_at           timestamptz NOT NULL,
            created_by           uuid,
            updated_at           timestamptz NOT NULL,
            updated_by           uuid,
            version              integer     NOT NULL DEFAULT 1,
            custom               jsonb       NOT NULL DEFAULT '{}',
            CONSTRAINT pk_user_account PRIMARY KEY (id),
            CONSTRAINT uq_user_account__username UNIQUE (username),
            CONSTRAINT ck_user_account__username_lowercase CHECK (username = lower(username)),
            CONSTRAINT fk_user_account__created_by FOREIGN KEY (created_by) REFERENCES core.user_account (id),
            CONSTRAINT fk_user_account__updated_by FOREIGN KEY (updated_by) REFERENCES core.user_account (id)
        );
        CREATE INDEX IF NOT EXISTS ix_user_account__created_by ON core.user_account (created_by);
        CREATE INDEX IF NOT EXISTS ix_user_account__updated_by ON core.user_account (updated_by);

        CREATE TABLE IF NOT EXISTS core.company (
            id         uuid        NOT NULL,
            name       text        NOT NULL,
            is_active  boolean     NOT NULL DEFAULT true,
            created_at timestamptz NOT NULL,
            created_by uuid,
            updated_at timestamptz NOT NULL,
            updated_by uuid,
            version    integer     NOT NULL DEFAULT 1,
            custom     jsonb       NOT NULL DEFAULT '{}',
            CONSTRAINT pk_company PRIMARY KEY (id),
            CONSTRAINT fk_company__created_by FOREIGN KEY (created_by) REFERENCES core.user_account (id),
            CONSTRAINT fk_company__updated_by FOREIGN KEY (updated_by) REFERENCES core.user_account (id)
        );
        CREATE INDEX IF NOT EXISTS ix_company__created_by ON core.company (created_by);
        CREATE INDEX IF NOT EXISTS ix_company__updated_by ON core.company (updated_by);

        CREATE TABLE IF NOT EXISTS core.role (
            id         uuid        NOT NULL,
            name       text        NOT NULL,
            is_active  boolean     NOT NULL DEFAULT true,
            is_system  boolean     NOT NULL DEFAULT false,
            origin     text,
            created_at timestamptz NOT NULL,
            created_by uuid,
            updated_at timestamptz NOT NULL,
            updated_by uuid,
            version    integer     NOT NULL DEFAULT 1,
            custom     jsonb       NOT NULL DEFAULT '{}',
            CONSTRAINT pk_role PRIMARY KEY (id),
            CONSTRAINT uq_role__origin UNIQUE (origin),
            CONSTRAINT fk_role__created_by FOREIGN KEY (created_by) REFERENCES core.user_account (id),
            CONSTRAINT fk_role__updated_by FOREIGN KEY (updated_by) REFERENCES core.user_account (id)
        );
        -- Unique whatever the case, active or not. An expression needs an index, not a constraint.
        CREATE UNIQUE INDEX IF NOT EXISTS uq_role__name ON core.role (lower(name));
        CREATE INDEX IF NOT EXISTS ix_role__created_by ON core.role (created_by);
        CREATE INDEX IF NOT EXISTS ix_role__updated_by ON core.role (updated_by);

        CREATE TABLE IF NOT EXISTS core.role_permission (
            id         uuid        NOT NULL,
            role_id    uuid        NOT NULL,
            -- A document of some package (itdesk.ticket.read): text, no foreign key across packages.
            permission text        NOT NULL,
            scope      text        NOT NULL DEFAULT 'all',
            created_at timestamptz NOT NULL,
            created_by uuid,
            CONSTRAINT pk_role_permission PRIMARY KEY (id),
            CONSTRAINT uq_role_permission__role_id_permission UNIQUE (role_id, permission),
            CONSTRAINT ck_role_permission__scope CHECK (scope IN ('all', 'own')),
            CONSTRAINT fk_role_permission__role_id FOREIGN KEY (role_id) REFERENCES core.role (id),
            CONSTRAINT fk_role_permission__created_by FOREIGN KEY (created_by) REFERENCES core.user_account (id)
        );
        CREATE INDEX IF NOT EXISTS ix_role_permission__created_by ON core.role_permission (created_by);

        CREATE TABLE IF NOT EXISTS core.role_user_account (
            id              uuid        NOT NULL,
            role_id         uuid        NOT NULL,
            user_account_id uuid        NOT NULL,
            created_at      timestamptz NOT NULL,
            created_by      uuid,
            CONSTRAINT pk_role_user_account PRIMARY KEY (id),
            CONSTRAINT uq_role_user_account__role_id_user_account_id UNIQUE (role_id, user_account_id),
            CONSTRAINT fk_role_user_account__role_id FOREIGN KEY (role_id) REFERENCES core.role (id),
            CONSTRAINT fk_role_user_account__user_account_id FOREIGN KEY (user_account_id) REFERENCES core.user_account (id),
            CONSTRAINT fk_role_user_account__created_by FOREIGN KEY (created_by) REFERENCES core.user_account (id)
        );
        CREATE INDEX IF NOT EXISTS ix_role_user_account__user_account_id ON core.role_user_account (user_account_id);
        CREATE INDEX IF NOT EXISTS ix_role_user_account__created_by ON core.role_user_account (created_by);

        CREATE TABLE IF NOT EXISTS core.session (
            id              uuid        NOT NULL,
            user_account_id uuid        NOT NULL,
            -- SHA-256 of the token; the token itself only lives in the cookie.
            token_hash      bytea       NOT NULL,
            created_at      timestamptz NOT NULL,
            last_seen_at    timestamptz NOT NULL,
            CONSTRAINT pk_session PRIMARY KEY (id),
            CONSTRAINT uq_session__token_hash UNIQUE (token_hash),
            CONSTRAINT fk_session__user_account_id FOREIGN KEY (user_account_id) REFERENCES core.user_account (id)
        );
        CREATE INDEX IF NOT EXISTS ix_session__user_account_id ON core.session (user_account_id);

        CREATE TABLE IF NOT EXISTS core.setting (
            id         uuid        NOT NULL,
            key        text        NOT NULL,
            value      jsonb       NOT NULL,
            updated_at timestamptz NOT NULL,
            updated_by uuid,
            CONSTRAINT pk_setting PRIMARY KEY (id),
            CONSTRAINT uq_setting__key UNIQUE (key),
            CONSTRAINT fk_setting__updated_by FOREIGN KEY (updated_by) REFERENCES core.user_account (id)
        );
        CREATE INDEX IF NOT EXISTS ix_setting__updated_by ON core.setting (updated_by);
        """;

    /// <summary>Runs at every start, before any package is loaded.</summary>
    public static async Task EnsureAsync(NpgsqlDataSource dataSource, CancellationToken cancel = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancel);
        await using var transaction = await connection.BeginTransactionAsync(cancel);

        await using (var guard = new NpgsqlCommand("SELECT pg_advisory_xact_lock($1)", connection, transaction))
        {
            guard.Parameters.AddWithValue(StartupLock);
            await guard.ExecuteNonQueryAsync(cancel);
        }

        await using (var ddl = new NpgsqlCommand(Structure, connection, transaction))
            await ddl.ExecuteNonQueryAsync(cancel);

        await using (var administrator = new NpgsqlCommand("""
            INSERT INTO core.role (id, name, is_system, origin, created_at, updated_at)
            VALUES ($1, $2, true, $2, $3, $3)
            ON CONFLICT ON CONSTRAINT uq_role__origin DO NOTHING
            """, connection, transaction))
        {
            var now = DateTimeOffset.UtcNow;
            administrator.Parameters.AddWithValue(Guid.CreateVersion7(now));
            administrator.Parameters.AddWithValue(AdministratorRole);
            administrator.Parameters.AddWithValue(now);
            await administrator.ExecuteNonQueryAsync(cancel);
        }

        await transaction.CommitAsync(cancel);
    }
}
