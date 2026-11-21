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

css_root = root / "src/CallMed.Web/wwwroot/css"
expected = [
    "nova-00-design.css", "nova-10-shell.css", "nova-20-fluxos.css",
    "nova-30-acesso.css", "nova-40-modulos.css"
]
styles = sorted(p.relative_to(css_root).as_posix() for p in css_root.rglob("*.css"))
require(styles == sorted(expected), f"Exatamente cinco CSS NOVA, sem folha antiga: {styles}")
head = read("src/CallMed.Web/Views/Shared/_DocumentHead.cshtml")
linked = re.findall(r'href="~/css/(nova-[\w-]+\.css)"', head)
require(linked == expected and head.count('rel="stylesheet"') == 5,
        "O head deve carregar somente os cinco CSS NOVA em ordem.")
require("modules/" not in head and "callmed.css" not in head and "cm43-horizon" not in head,
        "Nao reintroduzir links legados.")
css_joined = "".join((css_root / name).read_text(encoding="utf-8") for name in linked if (css_root / name).exists())
require(".identity-shell" in css_joined and ".form-etapa.ativa" in css_joined
        and ".campos-convenio.ativo" in css_joined,
        "Login, cadastro em etapas e convenio opcional devem ter estilos novos.")
require(":root.callmed-simple-mode" in css_joined and ".cm-booking-calendar" in css_joined,
        "Regras de modo facil e calendario de agendamento ausentes.")

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
