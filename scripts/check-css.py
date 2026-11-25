#!/usr/bin/env python3
"""Valida CSS R9.3 e páginas offline."""
from pathlib import Path
import re
import sys
root = Path(__file__).resolve().parents[1]
web = root / "src/CallMed.Web"
css = web / "wwwroot/css"
main = "callmed-clinic-r93.css"
expected = [main]
actual = sorted(p.name for p in css.rglob("*.css"))
if actual != expected:
    sys.exit(f"CSS divergentes: {actual} != {expected}")
for name in expected:
    if re.search(r"@import\b", (css / name).read_text(encoding="utf-8"), re.I):
        sys.exit(f"@import proibido: {name}")
head = (web / "Views/Shared/_DocumentHead.cshtml").read_text(encoding="utf-8")
linked = re.findall(r'href="~/css/([\w-]+\.css)"', head)
if linked != [main] or head.count('rel="stylesheet"') != 1:
    sys.exit("O layout deve carregar somente a folha compartilhada")
for file in ["offline.html", "contingencia.html"]:
    source = (web / "wwwroot" / file).read_text(encoding="utf-8")
    for name in expected:
        if f"/css/{name}" not in source:
            sys.exit(f"CSS offline ausente: {file} -> {name}")
for page in [*web.rglob("*.cshtml"), *web.rglob("*.html")]:
    if {"bin", "obj"} & set(page.parts):
        continue
    source = page.read_text(encoding="utf-8")
    if re.search(r"<style\b", source, re.I):
        sys.exit(f"CSS incorporado: {page.relative_to(root)}")
    for tag in re.findall(r"<link\b[^>]*>", source, re.I):
        if re.search(r'\brel=["\']stylesheet["\']', tag, re.I):
            if not re.search(r'href=["\']~?/css/callmed-clinic-r93\.css["\']', tag):
                sys.exit(f"Folha inesperada: {page.relative_to(root)}")
main_css=(css / main).read_text(encoding="utf-8")
for token in [".app-header",".primary-nav","#mksan-ai-widget.open",".cm-confirm-popover"]:
    if token not in main_css:sys.exit("Cobertura R9.3 ausente: "+token)
print("CSS R9.3 + offline: OK")
