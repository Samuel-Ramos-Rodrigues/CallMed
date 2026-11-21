#!/usr/bin/env python3
"""Verifica símbolos reais, a paleta V54 e os layouts V35 compatíveis."""
from pathlib import Path
from xml.etree import ElementTree as ET
import re, hashlib
r=Path(__file__).resolve().parents[1]
web=r/'src/CallMed.Web'
css=(web/'wwwroot/css/nova-00-design.css').read_text()
assert css.startswith('/* CallMed 35 — folha autoral única.')
assert '--primary: #0B5E4F' in css and '--bg: #FFFFFF' in css and '--accent-soft: #E8F4F1' in css
sheets='\n'.join((web/'wwwroot/css'/f).read_text() for f in ['nova-10-shell.css','nova-20-fluxos.css','nova-30-acesso.css','nova-40-modulos.css'])
for token in ['.cm50-ops-grid','.cm50-doctor-grid','.cm50-patient-grid','.cm50-central','.cm50-booking','.identity-shell','.public-how>header','#mksan-ai-widget.open']:
 assert token in sheets,token
sprite=ET.parse(web/'wwwroot/icons/callmed-ui.svg')
syms=sprite.findall('.//{http://www.w3.org/2000/svg}symbol')
assert len(syms)>=45
ids={x.get('id') for x in syms}
refs=set()
for page in (web/'Views').rglob('*.cshtml'):
 refs.update(re.findall(r'/icons/callmed-ui\.svg#([A-Za-z0-9_-]+)',page.read_text(encoding='utf8',errors='ignore')))
assert not refs-ids,refs-ids
assert 'callmed-static-v108-espera-agendamento' in (web/'wwwroot/service-worker.js').read_text()
print(f'V54.1: green palette, V35-compatible components, {len(syms)} SVG symbols, {len(refs)} used; cache OK')
