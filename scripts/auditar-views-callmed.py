#!/usr/bin/env python3
"""Auditoria estática das páginas Razor e dos links MVC do CallMed.

A auditoria não executa ASP.NET, não substitui dotnet build nem testes de permissões
com contas reais, mas detecta ações e arquivos referenciados que não existem.
"""
from pathlib import Path
import re
import sys

ROOT = Path(__file__).resolve().parents[1] / "src" / "CallMed.Web"
views = sorted(list((ROOT / "Views").rglob("*.cshtml")) + list((ROOT / "Areas").rglob("*.cshtml")))
# MVC partial controllers can implement actions in multiple source files.
controllers = {}
for source in (ROOT / "Controllers").rglob("*Controller*.cs"):
    match = re.fullmatch(r"(.+Controller)(?:\.[^.]+)*\.cs", source.name)
    if match:
        controllers.setdefault(match.group(1).removesuffix("Controller"), []).append(source)
controller_sources = {
    name: "\n".join(p.read_text(encoding="utf-8-sig") for p in sources)
    for name, sources in controllers.items()
}
asset_ext = {".js", ".css", ".svg", ".png", ".ico", ".jpg", ".jpeg", ".webp", ".json"}
errors = []
links_count = 0
assets_count = 0
for view in views:
    content = view.read_text(encoding="utf-8-sig")
    for tag in re.findall(r"<(?:a|form|button)\b[^>]*>", content, flags=re.I | re.S):
        action_match = re.search(r'asp-action\s*=\s*"([^"]+)"', tag)
        if not action_match:
            continue
        action = action_match.group(1)
        controller_match = re.search(r'asp-controller\s*=\s*"([^"]+)"', tag)
        controller = controller_match.group(1) if controller_match else view.parent.name
        if "@" in controller or "@" in action:
            continue  # Expressão Razor dinâmica precisa de validação em execução.
        links_count += 1
        source = controller_sources.get(controller)
        if source is None:
            errors.append(f"{view.relative_to(ROOT)}: Controller {controller} não encontrado")
        elif re.search(r"\b" + re.escape(action) + r"\s*\(", source) is None:
            errors.append(f"{view.relative_to(ROOT)}: ação {controller}.{action} não encontrada")

    for match in re.finditer(r'''(?:src|href)\s*=\s*["']~?/([^"'@?#]+)["']''', content):
        static_path = Path(match.group(1))
        if static_path.suffix.lower() not in asset_ext:
            continue
        assets_count += 1
        if not (ROOT / "wwwroot" / static_path).exists():
            errors.append(f"{view.relative_to(ROOT)}: recurso /{static_path} não encontrado")

layout = (ROOT / "Views/Shared/_Layout.cshtml").read_text(encoding="utf-8-sig")
mobile_path = ROOT / "Views/Shared/_NavegacaoPrincipal.cshtml"
nav_text = mobile_path.read_text(encoding="utf-8-sig") if mobile_path.exists() else layout
if 'callmed-mobile-nav' not in layout or 'Atendimento' not in nav_text:
    errors.append("Navegação mobile: falta o atalho Atendimento por perfil")

print(f"Páginas Razor analisadas: {len(views)}")
print(f"Controllers encontrados: {len(controllers)}")
print(f"Referências MVC verificadas: {links_count}")
print(f"Recursos estáticos verificados: {assets_count}")
print(f"Problemas de referência encontrados: {len(errors)}")
for issue in errors:
    print(" - " + issue)
if errors:
    sys.exit(1)
