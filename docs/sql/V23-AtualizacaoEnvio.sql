ALTER TABLE "MensagensAtendimento" ADD COLUMN IF NOT EXISTS "ChaveEnvio" character varying(160) NULL;
ALTER TABLE "MensagensAtendimento" ADD COLUMN IF NOT EXISTS "ProximaTentativaEm" timestamp with time zone NULL;
ALTER TABLE "MensagensAtendimento" ADD COLUMN IF NOT EXISTS "ReenvioBloqueado" boolean NOT NULL DEFAULT FALSE;
ALTER TABLE "MensagensAtendimento" ADD COLUMN IF NOT EXISTS "VersaoEnvio" uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
CREATE UNIQUE INDEX IF NOT EXISTS "IX_MensagensAtendimento_ChaveEnvio"
    ON "MensagensAtendimento" ("ChaveEnvio") WHERE "ChaveEnvio" IS NOT NULL;
