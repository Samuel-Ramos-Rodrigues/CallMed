#!/usr/bin/env python3
"""Verifica CSS modular por ordem explícita sem bundler nem estilos inline."""
from pathlib import Path
import re
import sys

root = Path(__file__).resolve().parents[1]
web = root / "src" / "CallMed.Web"
css_dir = web / "wwwroot" / "css" / "modules"
files = sorted((web / "wwwroot").rglob("*.css"))
if len(files) != 14 or any(p.parent != css_dir for p in files):
    sys.exit(f"Esperados 14 módulos CSS em wwwroot/css/modules: {files}")
for file in files:
    text = file.read_text(encoding="utf-8")
    if re.search(r"@import\\b", text, re.I):
        sys.exit(f"Não usar @import: {file.relative_to(root)}")
head = (web / "Views/Shared/_DocumentHead.cshtml").read_text(encoding="utf-8")
expected = sorted(p.name for p in files)
linked = re.findall(r'href="~/css/modules/([\\w-]+\\.css)"', head)
if sorted(linked) != expected or len(linked) != 14:
    sys.exit("A partial _DocumentHead deve referenciar cada módulo CSS uma vez.")
for page in [*web.rglob("*.cshtml"), *web.rglob("*.html")]:
    if {"bin", "obj"} & set(page.parts):
        continue
    source = page.read_text(encoding="utf-8")
    if re.search(r"<style\\b", source, re.I):
        sys.exit(f"Estilo inline em {page.relative_to(root)}.")
    for tag in re.findall(r"<link\\b[^>]*>", source, re.I):
        if re.search(r"\\brel=[\\\"']stylesheet[\\\"']", tag, re.I):
            if not re.search(r'href=[\\\"\']~/css/modules/[\\w-]+\\.css[\\\"\']', tag):
                sys.exit(f"Folha CSS fora do catálogo em {page.relative_to(root)}")
print(f"CSS modular: {len(files)} módulos, links e views verificados.")
