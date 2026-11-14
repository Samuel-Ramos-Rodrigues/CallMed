#!/usr/bin/env python3
"""Audita os módulos CSS e sua ordem, impedindo CSS monolítico/órfão."""
from pathlib import Path
import re
import sys

root = Path(__file__).resolve().parents[1]
web = root / 'src' / 'CallMed.Web'
names = ["00-fundamentos.css","10-componentes.css","20-portal.css","30-agenda-atendimento.css","40-navegacao.css","50-identidade-tokens.css","51-identidade-publico.css","60-jornada.css","61-operacao.css","70-paineis.css","80-tema-v39.css","90-modulos-internos.css","95-jornada-unificada.css"]
expected = [web / 'wwwroot' / 'css' / 'modulos' / n for n in names]
actual = sorted((web / 'wwwroot').rglob('*.css'))
if actual != sorted(expected):
    sys.exit(f'CSS inesperado/ausente: {actual}')
head = (web / 'Views' / 'Shared' / '_DocumentHead.cshtml').read_text()
links = [f'href="~/css/modulos/{name}"' for name in names]
if head.count('rel="stylesheet"') != len(links):
    sys.exit('Quantidade de estilos incorreta no head')
for i, link in enumerate(links):
    if head.count(link) != 1:
        sys.exit(f'Folha ausente/duplicada no head: {link}')
    if i and head.index(link) <= head.index(links[i-1]):
        sys.exit(f'Ordem CSS alterada: {link}')
for file in expected:
    content = file.read_text()
    if not content.strip() or re.search(r'@import\b', content, re.I):
        sys.exit(f'Módulo vazio ou com @import: {file}')
# A sequência dos 12 primeiros módulos deve reconstruir byte a byte o CSS
# pré-migração. SHA é o blob Git do monolito no commit-base da V40.
import hashlib
original = b''.join(file.read_bytes() for file in expected[:-1])
git_sha = hashlib.sha1(b'blob '+str(len(original)).encode()+b'\0'+original).hexdigest()
if git_sha != 'af74bcfb45f83572b3cf3de646ec1182015fb795':
    sys.exit('CSS legado foi alterado durante a divisão modular: '+git_sha)
for page in [*web.rglob('*.cshtml'), *web.rglob('*.html')]:
    if {'bin','obj'} & set(page.parts):
        continue
    content = page.read_text()
    if re.search(r'<style\b', content, re.I):
        sys.exit(f'Proibido <style> em {page.relative_to(root)}')
    if page.name != '_DocumentHead.cshtml' and re.search(r'<link\b[^>]*rel=["\']stylesheet', content, re.I):
        sys.exit(f'Folha CSS fora do head em {page.relative_to(root)}')
sw = (web/'wwwroot/service-worker.js').read_text()
for name in names:
    if "'/css/modulos/"+name+"'" not in sw:
        sys.exit(f'PWA não inclui {name}')
if "'/css/callmed.css'" in sw or "'/css/callmed-auth-v34.css'" in sw:
    sys.exit('PWA ainda referencia CSS removido')
print(f'CSS modular V42: {len(expected)} módulos, ordem e PWA verificados.')
