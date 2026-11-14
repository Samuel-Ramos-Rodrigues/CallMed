#!/usr/bin/env python3
"""CallMed NOVA — valida apenas a nova arquitetura CSS (nenhuma folha legada)."""
from pathlib import Path
import re
import sys
root=Path(__file__).resolve().parents[1]
web=root/"src"/"CallMed.Web"
css=web/"wwwroot"/"css"
expected=["nova-00-design.css","nova-10-shell.css","nova-20-fluxos.css","nova-30-acesso.css","nova-40-modulos.css"]
files=sorted((web/"wwwroot").rglob("*.css"))
if sorted(p.name for p in files)!=sorted(expected) or any(p.parent!=css for p in files):
    sys.exit("CSS antigo ainda existe, ou faltam folhas NOVA; esperadas somente cinco.")
for f in files:
    text=f.read_text(encoding="utf-8")
    if re.search(r"@import\b",text,re.I):
        sys.exit(f"@import proibido: {f}")
head=(web/"Views/Shared/_DocumentHead.cshtml").read_text(encoding="utf-8")
linked=re.findall(r"""href=["']~/css/(nova-[\w-]+\.css)["']""",head)
if linked!=expected or head.count("rel=\"stylesheet\"")!=5:
    sys.exit("Cabeçalho deve carregar somente os cinco estilos NOVA em ordem.")
for page in [*web.rglob("*.cshtml"),*web.rglob("*.html")]:
    if {"bin","obj"} & set(page.parts):continue
    source=page.read_text(encoding="utf-8")
    if re.search(r"<style\b",source,re.I):
        sys.exit(f"CSS incorporado: {page.relative_to(root)}")
    for tag in re.findall(r"<link\b[^>]*>",source,re.I):
        if re.search(r'\brel=["\']stylesheet["\']',tag,re.I) and not re.search(r'href=["\']~?/css/nova-[\w-]+\.css["\']',tag):
            sys.exit(f"Folha não NOVA: {page.relative_to(root)}")
print("NOVA CSS: cinco folhas escritas do zero; nenhum CSS legado carregado.")
