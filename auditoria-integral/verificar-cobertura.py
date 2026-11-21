#!/usr/bin/env python3
"""Verifica o inventário, os hashes e as faixas automaticamente processadas.

Não chama varredura automatizada de revisão semântica. Mudanças em qualquer um
 dos 724 arquivos da base, incluindo os 40 binários, são verificadas.
"""
from pathlib import Path
import hashlib, json, sys
here = Path(__file__).resolve().parent
root = Path(sys.argv[1]).resolve() if len(sys.argv)>1 else here.parent
files = json.loads((here/'inventario-completo.json').read_text(encoding='utf8'))['resultado']
coverage = json.loads((here/'cobertura-linhas.json').read_text(encoding='utf8'))['arquivos']
post = json.loads((here/'inventario-pos-correcao.json').read_text(encoding='utf8'))
assert len(files)==724, 'Inventario deve conter as 724 entradas da fonte original'
assert len(coverage)==sum(r['textual'] for r in files)
byfile={x['arquivo']:x for x in coverage}
assert set(byfile)=={r['arquivo'] for r in files if r['textual']}
updated={x['arquivo']:x['sha_corrigido'] for x in post['alteracoes']}
fail=[];modified=[];pending=0
for r in files:
 name=r['arquivo'];p=root/name
 if not p.is_file():fail.append('Arquivo ausente: '+name);continue
 actual=hashlib.sha256(p.read_bytes()).hexdigest()
 expected=updated.get(name,r['sha256_original'])
 if actual!=expected:fail.append('Hash divergente do checkpoint: '+name)
 if actual!=r['sha256_original']:modified.append(name)
 if not r['textual']:continue
 c=byfile[name];n=r['total_linhas'];ranges=c['faixas_processadas_automaticamente']
 if n and ranges!=[[1,n]]:fail.append('Linhas nao cobertas pela varredura: '+name)
 if c['status'].find('pendente')>=0:pending+=1
if set(modified)!=set(updated):fail.append('Arquivos alterados diferem do changelog do checkpoint')
print(f'Arquivos: {len(files)}; texto: {len(coverage)}; binarios: {len(files)-len(coverage)}; modificados esperados: {len(updated)}')
print(f'Arquivos textuais que ainda exigem revisao semantica: {pending} (nao inclui revisoes parciais, cujas lacunas permanecem)')
if fail:
 print('VERIFICACAO FALHOU:',*fail[:25],sep='\n- ')
 sys.exit(1)
print('OK: hashes e faixas de varredura automaticamente registradas; nao e prova de revisao semantica integral.')
