-- Alignement schéma planning avec migration 20260611120000_AddPrimeMirrorColumns
ALTER TABLE "Floors" ADD COLUMN IF NOT EXISTS "PrimePoleId" character varying(64) NULL;
ALTER TABLE "Services" ADD COLUMN IF NOT EXISTS "PrimeCelluleId" character varying(64) NULL;
ALTER TABLE "SubServices" ADD COLUMN IF NOT EXISTS "PrimeServiceId" character varying(64) NULL;

CREATE UNIQUE INDEX IF NOT EXISTS "IX_Floors_PrimePoleId"
  ON "Floors" ("PrimePoleId") WHERE "PrimePoleId" IS NOT NULL;
CREATE UNIQUE INDEX IF NOT EXISTS "IX_Services_PrimeCelluleId"
  ON "Services" ("PrimeCelluleId") WHERE "PrimeCelluleId" IS NOT NULL;
CREATE UNIQUE INDEX IF NOT EXISTS "IX_SubServices_PrimeServiceId"
  ON "SubServices" ("PrimeServiceId") WHERE "PrimeServiceId" IS NOT NULL;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
SELECT '20260611120000_AddPrimeMirrorColumns', '8.0.6'
WHERE NOT EXISTS (
  SELECT 1 FROM "__EFMigrationsHistory"
  WHERE "MigrationId" = '20260611120000_AddPrimeMirrorColumns'
);
