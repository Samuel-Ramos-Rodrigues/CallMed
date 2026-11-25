#!/usr/bin/env python3
"""Verifica os componentes da folha compartilhada, os símbolos e o cache offline."""
from pathlib import Path
from xml.etree import ElementTree as ET
import re, hashlib
r=Path(__file__).resolve().parents[1]
web=r/'src/CallMed.Web'
css=(web/'wwwroot/css/callmed-clinic-r93.css').read_text(encoding='utf8')
for token in ['.cm9-activity-grid','.cm8-central','.identity-shell','.public-hero','#mksan-ai-widget.open','.cm-offer-actions']:
 assert token in css, token
sprite=ET.parse(web/'wwwroot/icons/callmed-ui.svg')
syms=sprite.findall('.//{http://www.w3.org/2000/svg}symbol')
assert len(syms)>=45
ids={x.get('id') for x in syms}
refs=set()
for page in (web/'Views').rglob('*.cshtml'):
 refs.update(re.findall(r'/icons/callmed-ui\.svg#([A-Za-z0-9_-]+)',page.read_text(encoding='utf8',errors='ignore')))
assert not refs-ids,refs-ids
assert "'/css/callmed-clinic-r93.css'" in (web/'wwwroot/service-worker.js').read_text()
print(f'Componentes CallMed, {len(syms)} SVG symbols, {len(refs)} used; cache OK')
