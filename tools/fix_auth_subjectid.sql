-- Répare auth_db : colonne SubjectId (idempotent, gère plusieurs utilisateurs)
DO $$
BEGIN
  IF NOT EXISTS (
    SELECT 1 FROM information_schema.columns
    WHERE table_schema = 'public' AND table_name = 'Users' AND column_name = 'SubjectId'
  ) THEN
    ALTER TABLE "Users" ADD COLUMN "SubjectId" uuid;
  END IF;
END $$;

UPDATE "Users" SET "SubjectId" = '11111111-1111-4111-8111-111111111103'::uuid WHERE lower("Email") = lower('employee@kyntus.ma');
UPDATE "Users" SET "SubjectId" = '11111111-1111-4111-8111-111111111104'::uuid WHERE lower("Email") = lower('rh@kyntus.ma');
UPDATE "Users" SET "SubjectId" = '11111111-1111-4111-8111-111111111105'::uuid WHERE lower("Email") = lower('manager@kyntus.ma');
UPDATE "Users" SET "SubjectId" = '11111111-1111-4111-8111-111111111106'::uuid WHERE lower("Email") = lower('coach@kyntus.ma');
UPDATE "Users" SET "SubjectId" = '11111111-1111-4111-8111-111111111107'::uuid WHERE lower("Email") = lower('rp@kyntus.ma');
UPDATE "Users" SET "SubjectId" = '11111111-1111-4111-8111-111111111108'::uuid WHERE lower("Email") = lower('admin@kyntus.ma');
UPDATE "Users" SET "SubjectId" = '11111111-1111-4111-8111-111111111109'::uuid WHERE lower("Email") = lower('audit@kyntus.ma');
UPDATE "Users" SET "SubjectId" = '11111111-1111-4111-8111-111111111110'::uuid WHERE lower("Email") = lower('formation@kyntus.ma');
UPDATE "Users" SET "SubjectId" = '11111111-1111-4111-8111-111111111101'::uuid WHERE lower("Email") = lower('yasmine.elamrani@atlas-tech-demo.dev');
UPDATE "Users" SET "SubjectId" = '11111111-1111-4111-8111-111111111102'::uuid WHERE lower("Email") = lower('fatima.alaoui@atlas-tech-demo.dev');

UPDATE "Users" SET "SubjectId" = gen_random_uuid() WHERE "SubjectId" IS NULL;

ALTER TABLE "Users" ALTER COLUMN "SubjectId" SET NOT NULL;

DROP INDEX IF EXISTS "IX_Users_SubjectId";
CREATE UNIQUE INDEX "IX_Users_SubjectId" ON "Users" ("SubjectId");

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
SELECT '20260603120000_AddUserSubjectId', '10.0.0'
WHERE NOT EXISTS (
  SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260603120000_AddUserSubjectId'
);
