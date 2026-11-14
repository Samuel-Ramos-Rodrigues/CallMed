#!/usr/bin/env python3
"""Verificacoes estruturais sem banco ou rede; executadas pelo CI."""
from pathlib import Path
import re
import sys

root = Path(__file__).resolve().parents[1]
problems = []

def read(path):
    return (root / path).read_text(encoding="utf-8-sig")

def require(condition, description):
    if not condition:
        problems.append(description)

projects = {
    "Domain": "src/CallMed.Domain/CallMed.Domain.csproj",
    "Persistence": "src/CallMed.Persistence/CallMed.Persistence.csproj",
    "Application": "src/CallMed.Application/CallMed.Application.csproj",
    "Web": "src/CallMed.Web/CallMed.Web.csproj",
    "Worker": "src/CallMed.Worker/CallMed.Worker.csproj",
}
for name, path in projects.items():
    require((root / path).is_file(), f"Projeto ausente: {name}")
    if (root / path).is_file():
        xml = read(path)
        require("<TargetFramework>net10.0</TargetFramework>" in xml, f"SDK divergente: {name}")
        refs = re.findall(r'<ProjectReference Include="([^"]+)"', xml)
        expected = {
            "Domain": [],
            "Persistence": ["../CallMed.Domain/CallMed.Domain.csproj"],
            "Application": ["../CallMed.Persistence/CallMed.Persistence.csproj"],
            "Web": ["../CallMed.Application/CallMed.Application.csproj"],
            "Worker": ["../CallMed.Application/CallMed.Application.csproj"],
        }[name]
        require(sorted(refs) == sorted(expected), f"Dependencias inesperadas: {name} ({refs})")

require((root / "src/CallMed.Domain/Contracts/IClinicaClock.cs").exists(),
        "Contrato IClinicaClock deve permanecer no dominio.")
require(not (root / "src/CallMed.Application/Services/Clinica/Contracts/IClinicaClock.cs").exists(),
        "Contrato IClinicaClock duplicado na aplicacao.")

styles = sorted(p.relative_to(root).as_posix()
                for p in (root / "src/CallMed.Web/wwwroot").rglob("*.css"))
expected_styles = sorted(["src/CallMed.Web/wwwroot/css/modulos/" + n for n in ["00-fundamentos.css","10-componentes.css","20-portal.css","30-agenda-atendimento.css","40-navegacao.css","50-identidade-tokens.css","51-identidade-publico.css","60-jornada.css","61-operacao.css","70-paineis.css","80-tema-v39.css","90-modulos-internos.css","95-jornada-unificada.css"]])
require(styles == expected_styles, f"CSS modular inesperado: {styles}")
head = read("src/CallMed.Web/Views/Shared/_DocumentHead.cshtml")
require(head.count('href="~/css/modulos/') == len(expected_styles), "Head deve carregar os modulos de CSS.")
require("callmed-auth-v34.css" not in head and "CallMedAuthCssV34" not in head,
        "Referencia ao CSS legado de autenticacao.")
require("Identidade V34 preservada em escopo" in read("src/CallMed.Web/wwwroot/css/modulos/50-identidade-tokens.css"),
        "Estilos de autenticacao nao consolidados.")

deploy = read("src/CallMed.Application/Infrastructure/Startup/DatabaseDeployment.cs")
version = re.search(r'public const int CurrentVersion = (\d+);', deploy)
require(version is not None, "Versao atual do schema nao encontrada.")
migration_dir = root / "src/CallMed.Application/Infrastructure/Database/Initializers"
versions = sorted(int(n.group(1)) for p in migration_dir.glob("DatabaseSchemaV*Initializer.cs")
                  if (n := re.match(r"DatabaseSchemaV(\d+)Initializer.cs", p.name)))
if version:
    current = int(version.group(1))
    require(versions and max(versions) == current, f"Schema atual {current} diverge dos inicializadores {versions}.")
    require(f'WHERE \\"Version\\"={current}' in deploy, "Validacao do banco diverge da versao atual.")
    for step in versions:
        require(f"DatabaseSchemaV{step}Initializer" in deploy, f"Schema {step} nao registrado no deploy.")

for name in ["AtendimentoController", "ConsultaController", "AtendimentoWebhookController"]:
    controllers = list((root / "src/CallMed.Web/Controllers").rglob(name + "*.cs"))
    require(len(controllers) >= 2, f"Controller nao separado em arquivos parciais: {name}")
    for path in controllers:
        require("partial class " + name in path.read_text(encoding="utf-8-sig"),
                f"Arquivo nao declara tipo partial: {path}")

require((root / "src/CallMed.Application/Modules/Agenda/Services/EncaixeService.Notificacoes.cs").exists(),
        "Rotina de notificacoes de encaixe nao separada.")

print(f"Auditoria estrutural: {len(problems)} problema(s).")
for problem in problems:
    print("FALHA:", problem)
sys.exit(1 if problems else 0)
