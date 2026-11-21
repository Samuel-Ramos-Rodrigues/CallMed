ALTER TABLE "Jornadas" ADD COLUMN IF NOT EXISTS "ConsentimentoVersao" text NULL;
ALTER TABLE "Consultas" ADD COLUMN IF NOT EXISTS "DuracaoMinutos" integer NOT NULL DEFAULT 30;
ALTER TABLE "Consultas" ADD COLUMN IF NOT EXISTS "PreparacaoAvisoChave" text NULL;
ALTER TABLE "PepEventos" ADD COLUMN IF NOT EXISTS "FhirEntregueEm" timestamptz NULL;
ALTER TABLE "PepEventos" ADD COLUMN IF NOT EXISTS "FhirTentarEm" timestamptz NULL;
ALTER TABLE "PepEventos" ADD COLUMN IF NOT EXISTS "FhirTentativas" integer NOT NULL DEFAULT 0;
ALTER TABLE "Medicos" ADD COLUMN IF NOT EXISTS "ResponsavelTecnico" boolean NOT NULL DEFAULT false;
ALTER TABLE "Medicos" ADD COLUMN IF NOT EXISTS "Crp" varchar(30) NULL;
ALTER TABLE "Medicos" ADD COLUMN IF NOT EXISTS "RegistroVerificadoEm" timestamptz NULL;
ALTER TABLE "Medicos" ADD COLUMN IF NOT EXISTS "RegistroVerificadoPor" text NULL;
ALTER TABLE "Medicos" ADD COLUMN IF NOT EXISTS "FonteVerificacaoRegistro" varchar(500) NULL;
ALTER TABLE "Medicos" ADD COLUMN IF NOT EXISTS "DuracaoPadraoMinutos" integer NOT NULL DEFAULT 50;
CREATE UNIQUE INDEX IF NOT EXISTS "IX_Medicos_Crp" ON "Medicos" ("Crp") WHERE "Crp" IS NOT NULL;
CREATE TABLE IF NOT EXISTS "RegistrosPsicologicos" (
 "Id" uuid PRIMARY KEY, "ConsultaId" integer NOT NULL REFERENCES "Consultas"("Id") ON DELETE RESTRICT,
 "PacienteId" integer NOT NULL REFERENCES "Pacientes"("Id") ON DELETE RESTRICT,
 "PsicologoId" integer NOT NULL REFERENCES "Medicos"("Id") ON DELETE RESTRICT,
 "AutorUsuarioId" text NOT NULL, "Escopo" text NOT NULL, "Categoria" text NOT NULL,
 "Envelope" text NOT NULL, "Hash" text NOT NULL, "RetificaId" uuid NULL REFERENCES "RegistrosPsicologicos"("Id"), "CriadoEm" timestamptz NOT NULL,
 CONSTRAINT "CK_Registro_Escopo" CHECK ("Escopo" IN ('Prontuario','Reservado')));
CREATE INDEX IF NOT EXISTS "IX_RegistrosPsicologicos_ConsultaId_CriadoEm" ON "RegistrosPsicologicos" ("ConsultaId","CriadoEm");
CREATE UNIQUE INDEX IF NOT EXISTS "IX_RegistrosPsicologicos_RetificaId" ON "RegistrosPsicologicos" ("RetificaId") WHERE "RetificaId" IS NOT NULL;
CREATE TABLE IF NOT EXISTS "AutorizacoesResponsaveis" (
 "Id" uuid PRIMARY KEY, "PacienteId" integer NOT NULL REFERENCES "Pacientes"("Id"), "PsicologoId" integer NOT NULL REFERENCES "Medicos"("Id"),
 "Nome" text NOT NULL, "Vinculo" text NOT NULL, "Referencia" text NOT NULL, "VerificadaPor" text NOT NULL,
 "VerificadaEm" timestamptz NOT NULL, "ValidaAte" timestamptz NOT NULL, "RevogadaEm" timestamptz NULL);
CREATE INDEX IF NOT EXISTS "IX_AutorizacoesResponsaveis_PacienteId_PsicologoId" ON "AutorizacoesResponsaveis" ("PacienteId","PsicologoId");
CREATE TABLE IF NOT EXISTS "GuardasRegistrosPsicologicos" (
 "Id" uuid PRIMARY KEY, "RegistroId" uuid NOT NULL REFERENCES "RegistrosPsicologicos"("Id") ON DELETE RESTRICT,
 "PsicologoId" integer NOT NULL REFERENCES "Medicos"("Id"), "ChaveId" uuid NOT NULL REFERENCES "ChavesClinicas"("Id"),
 "ChaveCifrada" text NOT NULL, "ConcedidaPor" text NOT NULL, "ConcedidaEm" timestamptz NOT NULL);
CREATE UNIQUE INDEX IF NOT EXISTS "IX_GuardasRegistrosPsicologicos_RegistroId_ChaveId" ON "GuardasRegistrosPsicologicos" ("RegistroId","ChaveId");
CREATE INDEX IF NOT EXISTS "IX_GuardasRegistrosPsicologicos_PsicologoId" ON "GuardasRegistrosPsicologicos" ("PsicologoId");
CREATE INDEX IF NOT EXISTS "IX_GuardasRegistrosPsicologicos_ChaveId" ON "GuardasRegistrosPsicologicos" ("ChaveId");
CREATE OR REPLACE FUNCTION callmed_registro_imutavel() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN RAISE EXCEPTION 'Registro publicado: utilize retificacao; exclusao depende de procedimento de custodia.'; END;
$$;
DROP TRIGGER IF EXISTS registro_psicologico_imutavel ON "RegistrosPsicologicos";
CREATE TRIGGER registro_psicologico_imutavel BEFORE UPDATE OR DELETE ON "RegistrosPsicologicos" FOR EACH ROW EXECUTE FUNCTION callmed_registro_imutavel();
