-- Numérotation transactionnelle des demandes document (REQ-YYYY-NNNNNN).
-- Appliquer sur documentation_db, ex. :
--   Get-Content .\init\sql\documentation_004_next_document_request_number.sql -Raw | docker compose exec -T postgres psql -U postgres -d documentation_db

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

ALTER FUNCTION documentation.next_document_request_number(text) OWNER TO documentation_user;

GRANT EXECUTE ON FUNCTION documentation.next_document_request_number(text) TO documentation_user;
