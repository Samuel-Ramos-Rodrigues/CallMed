#!/usr/bin/env python3
"""Validate the single authored stylesheet and its references; no bundling step."""
from pathlib import Path
import re,sys
root=Path(__file__).resolve().parents[1]
web=root/'src'/'CallMed.Web'
files=sorted((web/'wwwroot').rglob('*.css'))
if files != [web/'wwwroot/css/callmed.css']:
    sys.exit('O site deve conter somente wwwroot/css/callmed.css.')
text=files[0].read_text()
if re.search(r'@import\b',text,re.I):sys.exit('A folha única não pode importar outro CSS.')
for page in [*web.rglob('*.cshtml'),*web.rglob('*.html')]:
    if {'bin','obj'} & set(page.parts):continue
    source=page.read_text()
    if re.search(r'<style\b',source,re.I):sys.exit(f'Estilo incorporado em {page.relative_to(root)}.')
    for link in re.findall(r'<link\b[^>]*>',source,re.I):
        if re.search(r'\brel=["\']stylesheet["\']',link,re.I) and not re.search(r'href=["\']~?/css/callmed\.css["\']',link):
            sys.exit(f'Folha adicional em {page.relative_to(root)}.')
print(f'CSS único autoral: {len(text.encode())} bytes; referências das telas verificadas.')
