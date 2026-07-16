-- Réaligner SubjectId formateur@gmail.com (Mes sessions Formation).
-- Exécuter sur auth_db si le compte existe déjà avec un GUID aléatoire.

UPDATE "Users"
SET "SubjectId" = '11111111-1111-4111-8111-111111111120'::uuid
WHERE lower("Email") IN (lower('formateur@gmail.com'), lower('formateur@kyntus.ma'))
  AND NOT EXISTS (
    SELECT 1 FROM "Users" u2
    WHERE u2."SubjectId" = '11111111-1111-4111-8111-111111111120'::uuid
      AND lower(u2."Email") NOT IN (lower('formateur@gmail.com'), lower('formateur@kyntus.ma'))
  );
