#!/usr/bin/env python3
"""CallMed V35 — tema original com adaptadores e cinco arquivos de carga."""
from pathlib import Path
import re
import sys
root=Path(__file__).resolve().parents[1]
web=root/"src"/"CallMed.Web"
css=web/"wwwroot"/"css"
expected=["nova-00-design.css","nova-10-shell.css","nova-20-fluxos.css","nova-30-acesso.css","nova-40-modulos.css"]
files=sorted((web/"wwwroot").rglob("*.css"))
if sorted(p.name for p in files)!=sorted(expected) or any(p.parent!=css for p in files):
    sys.exit("Arquivos CSS divergentes: esperados os cinco módulos de compatibilidade V35.")
for f in files:
    text=f.read_text(encoding="utf-8")
    if re.search(r"@import\b",text,re.I):
        sys.exit(f"@import proibido: {f}")
head=(web/"Views/Shared/_DocumentHead.cshtml").read_text(encoding="utf-8")
linked=re.findall(r"""href=["']~/css/(nova-[\w-]+\.css)["']""",head)
if linked!=expected or head.count("rel=\"stylesheet\"")!=5:
    sys.exit("O cabeçalho deve carregar os cinco estilos da V35 em ordem.")
for page in [*web.rglob("*.cshtml"),*web.rglob("*.html")]:
    if {"bin","obj"} & set(page.parts):continue
    source=page.read_text(encoding="utf-8")
    if re.search(r"<style\b",source,re.I):
        sys.exit(f"CSS incorporado: {page.relative_to(root)}")
    for tag in re.findall(r"<link\b[^>]*>",source,re.I):
        if re.search(r'\brel=["\']stylesheet["\']',tag,re.I) and not re.search(r'href=["\']~?/css/nova-[\w-]+\.css["\']',tag):
            sys.exit(f"Folha CSS inesperada: {page.relative_to(root)}")
print("V35: CSS original e adaptadores, sem folhas extras carregadas.")

# V51: a quinta folha agrega regras auditadas sem acrescentar arquivos CSS ao pipeline.
coverage=(css/"nova-40-modulos.css").read_text(encoding="utf8")
required=["#mksan-ai-widget.open",".cm-confirm-popover",".cm-android-page",
          ".cm-feedback-filters",".cm19-record-grid",".tele-grid"]
for name in required:
    if name not in coverage:
        sys.exit("Cobertura V51 ausente: " + name)
print("Compatibilidade para assistente, notificações, Android, avaliações e teleconsulta: OK")
