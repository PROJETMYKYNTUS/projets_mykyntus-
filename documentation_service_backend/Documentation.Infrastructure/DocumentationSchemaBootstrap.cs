using System.Data;
using Documentation.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Documentation.Infrastructure;

/// <summary>
/// Fonctions PostgreSQL absentes d'EF EnsureCreated (ex. numérotation REQ).
/// </summary>
internal static class DocumentationSchemaBootstrap
{
    private const string NextDocumentRequestNumberSql =
        """
        CREATE OR REPLACE FUNCTION documentation.next_document_request_number(p_tenant text)
        RETURNS text
        LANGUAGE plpgsql
        AS $$
        DECLARE
          v_year int := EXTRACT(YEAR FROM now())::int;
          v_next int;
        BEGIN
          PERFORM pg_advisory_xact_lock(hashtext(p_tenant), v_year);

          INSERT INTO documentation.document_request_sequences (tenant_id, year, last_value)
          VALUES (p_tenant, v_year, 1)
          ON CONFLICT (tenant_id, year) DO UPDATE
            SET last_value = documentation.document_request_sequences.last_value + 1
          RETURNING last_value INTO v_next;

          RETURN format('REQ-%s-%s', v_year, lpad(v_next::text, 6, '0'));
        END;
        $$;
        """;

    internal static async Task ApplyPostSchemaObjectsAsync(
        DocumentationDbContext db,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (await FunctionExistsAsync(db, cancellationToken).ConfigureAwait(false))
            {
                logger.LogInformation(
                    "Documentation schema bootstrap — documentation.next_document_request_number déjà présente (skip).");
                return;
            }

            await db.Database.ExecuteSqlRawAsync(NextDocumentRequestNumberSql, cancellationToken)
                .ConfigureAwait(false);
            logger.LogInformation(
                "Documentation schema bootstrap — fonction documentation.next_document_request_number créée.");
        }
        catch (PostgresException pgEx) when (pgEx.SqlState is "42501")
        {
            if (await FunctionExistsAsync(db, cancellationToken).ConfigureAwait(false))
            {
                logger.LogWarning(
                    "Documentation schema bootstrap — fonction REQ déjà provisionnée (droits CREATE insuffisants, ignoré).");
                return;
            }

            logger.LogError(
                pgEx,
                "Documentation schema bootstrap — fonction REQ absente et création refusée (42501). Exécutez init/sql/documentation_004_next_document_request_number.sql en superuser.");
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Documentation schema bootstrap — création next_document_request_number non appliquée (schéma absent ou droits limités).");
        }
    }

    private static async Task<bool> FunctionExistsAsync(
        DocumentationDbContext db,
        CancellationToken cancellationToken)
    {
        var connection = db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText =
            """
            SELECT 1
            FROM pg_proc p
            INNER JOIN pg_namespace n ON n.oid = p.pronamespace
            WHERE n.nspname = 'documentation'
              AND p.proname = 'next_document_request_number'
            LIMIT 1
            """;
        var result = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return result is not null and not DBNull;
    }
}
