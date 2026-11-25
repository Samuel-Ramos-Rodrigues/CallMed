from pathlib import Path
import re, json, shutil, math
from playwright.sync_api import sync_playwright
ROOT=Path(__file__).resolve().parents[2]
( ROOT / 'validation' ).mkdir(parents=True,exist_ok=True)
css0=(ROOT/'src/CallMed.Web/wwwroot/css/callmed-clinic-r93.css').read_text()
css=css0
# Fixture deliberately uses class names and structure from REAL Razor views.
head='<meta name="viewport" content="width=device-width,initial-scale=1"><style>'+css+'</style>'
core='''<header class="app-header"><div class="app-header-inner"><div class="primary-nav"><a>Início</a></div><div class="header-tools"><div class="v18-popover cm91-profile-menu cm-account-menu"><form><button><svg></svg><span>Sair da conta</span></button></form></div></div></div></header>
<main class="app-main">
<section class="cm9 cm9-doctor"><aside class="cm9-clinical-focus"><div class="cm9-clinical-person"><h2>Paciente em foco</h2></div><div class="cm9-clinical-facts"><div><span>HORÁRIO</span><strong>07:00</strong></div><div><span>DATA</span><strong>23/11/2026</strong></div><div><span>ESTADO</span><strong>Confirmada</strong></div></div></aside></section>
<section class="r3-reception r4-reception"><div class="r3-exception-list"><article class="r3-exception-item"><div class="r3-exception-time"><strong>07:00</strong><small>Horário</small></div><div><strong>Paciente teste</strong><small>Profissional: teste</small><p>Conferir consulta</p></div><a class="r3-outline-button">Ver consulta ↗</a></article></div></section>
<section class="cm-salas"><div class="cm-online-list"><article class="cm-online-card"><div class="cm-online-time"><strong>07:00</strong><span class="cm-online-duration">50 min</span></div><div class="cm-online-info"><h2>PacienteTeste02</h2><p>Arthur Francisco</p><span class="cm-status-pill">Agendada</span></div><dl class="cm-presence"><div><dt>Paciente</dt><dd>Sem presença recente</dd></div><div><dt>Profissional</dt><dd>Sem presença recente</dd></div><div><dt>Conexão</dt><dd>Sem presença recente</dd></div></dl><div class="cm-online-actions"><a class="btn secondary">Detalhes da consulta</a></div><details class="cm-room-history"><summary>Entradas e saídas</summary></details></article></div></section>
</main>
<dialog class="modules-dialog" open><header><div><span>TODAS AS FUNÇÕES</span><h2>Para onde você quer ir?</h2></div><button class="icon-button">×</button></header><div class="cm-menu-conteudo"><details class="cm-modulo-grupo" open><summary>Hoje</summary><nav class="module-grid"><a><b>Pendências da recepção</b></a></nav></details><details class="cm-modulo-grupo"><summary>Agenda inteligente</summary></details><details class="cm-modulo-grupo"><summary>Atendimento</summary></details></div></dialog>
'''
auth='''<div class="identity-shell"><section class="identity-showcase"><h1>Seu cuidado, em um só lugar.</h1></section><section class="identity-panel"><article class="identity-card"><h1>Entrar na CallMed</h1><p>Use seu CPF</p><form class="identity-form"><label>Email<input placeholder="Digite seu CPF ou email"></label><label>Senha<input type="password" placeholder="Digite sua senha"></label><button type="button" class="identity-submit">Entrar</button></form></article></section></div><footer class="auth-legal-footer">CallMed · Cuidado em conexão <a>Privacidade</a></footer>'''

def rgb(s):
 vals=re.findall(r'\d+(?:\.\d+)?',s);return tuple(int(float(x)) for x in vals[:3]) if len(vals)>=3 else None

def contrast(x,y):
 def lum(x):
  q=[n/255 for n in x];q=[v/12.92 if v<=0.04045 else ((v+0.055)/1.055)**2.4 for v in q]
  return .2126*q[0]+.7152*q[1]+.0722*q[2]
 x,y=lum(x),lum(y);return (max(x,y)+.05)/(min(x,y)+.05)

SCENARIOS=[]
with sync_playwright() as pw:
 browser=pw.chromium.launch(headless=True,executable_path=shutil.which('chromium'),args=['--no-sandbox'])
 for theme in ('dark','light'):
  for width in (1440,1024,820,390):
   page=browser.new_page(viewport={'width':width,'height':900},device_scale_factor=1)
   page.set_content(f'<!doctype html><html lang="pt-BR" data-theme="{theme}"><head>{head}</head><body class="callmed-app cm-r3-shell cm-r4-shell cm-r5-shell cm-r6-shell cm-r7-shell cm-r8-shell cm9-shell staff-context">{core}</body></html>',wait_until='load')
   r=page.evaluate('''() => {
    const css=s=>{const e=document.querySelector(s); if(!e) return null;const c=getComputedStyle(e);const r=e.getBoundingClientRect();return {fg:c.color,bg:c.backgroundColor,display:c.display,gridColumn:c.gridColumnStart,rect:{x:r.x,width:r.width,right:r.right,height:r.height},wordBreak:c.wordBreak};};
    const selectors=['.cm9-clinical-facts','.cm9-clinical-facts strong','.cm9-clinical-facts span','.r3-exception-item','.r3-exception-item strong','.r3-exception-item small','.cm-online-time','.cm-online-time strong','.cm-online-duration','.cm-online-actions','.cm-online-actions .btn','.modules-dialog','.cm-modulo-grupo','.cm-modulo-grupo > summary','.cm-modulo-grupo .module-grid > a','.cm91-profile-menu form button','.cm91-profile-menu form button span'];
    return {viewport:innerWidth,scroll:document.documentElement.scrollWidth,styles:Object.fromEntries(selectors.map(s=>[s,css(s)]))};
   }''')
   st=r['styles']
   for bg,fg in [('.cm9-clinical-facts','.cm9-clinical-facts strong'),('.cm9-clinical-facts','.cm9-clinical-facts span'),('.r3-exception-item','.r3-exception-item strong'),('.r3-exception-item','.r3-exception-item small'),('.cm-modulo-grupo > summary','.cm-modulo-grupo > summary'),('.cm91-profile-menu form button','.cm91-profile-menu form button span')]:
    # self fg/bg selector needs same element not parent. 
    if bg==fg: c1=rgb(st[bg]['bg']);c2=rgb(st[fg]['fg'])
    else: c1=rgb(st[bg]['bg']);c2=rgb(st[fg]['fg'])
    if c1 is not None and c2 is not None:
     ratio=round(contrast(c1,c2),2)
     assert ratio>=4.5,(theme,width,bg,fg,ratio,c1,c2)
   assert r['scroll'] <= width+1,(theme,width,r['scroll'])
   assert st['.cm-online-actions']['gridColumn'] in ('2','1','auto','actions'),(theme,width,st['.cm-online-actions'])
   assert st['.cm-online-actions .btn']['rect']['width']>=130,(theme,width,st['.cm-online-actions .btn'])
   assert st['.cm-online-actions .btn']['rect']['right']<=width+1,(theme,width,st['.cm-online-actions .btn'])
   assert st['.cm-online-time']['rect']['height']>=st['.cm-online-time strong']['rect']['height']+st['.cm-online-duration']['rect']['height']-3,(theme,width)
   if width==1440 and theme=='dark':
    page.screenshot(path=str(ROOT / 'validation/r939-cards-dark.png'),full_page=True)
   SCENARIOS.append({'theme':theme,'width':width,'computed':r})
   page.close()
  # test in high contrast for both themes
  page=browser.new_page(viewport={'width':1440,'height':900})
  page.set_content(f'<!doctype html><html data-theme="{theme}" class="callmed-high-contrast"><head>{head}</head><body class="callmed-app cm-r3-shell cm-r4-shell cm-r5-shell cm-r6-shell cm-r7-shell cm-r8-shell cm9-shell">{core}</body></html>')
  hc=page.evaluate('''() => {const c=s=>{let a=getComputedStyle(document.querySelector(s));return [a.backgroundColor,a.color]};return{clinical:c('.cm9-clinical-facts'),item:c('.r3-exception-item'),module:c('.cm-modulo-grupo')}}''')
  for name,[bg,fg] in hc.items():
   assert contrast(rgb(bg),rgb(fg))>=15,(theme,name,bg,fg)
  page.close()
  # auth case
  page=browser.new_page(viewport={'width':1440,'height':900})
  page.set_content(f'<!doctype html><html data-theme="{theme}"><head>{head}</head><body class="r4-auth identity-body r5-auth">{auth}</body></html>')
  bgs=page.evaluate('''() => Object.fromEntries(['.identity-panel','.identity-card','.auth-legal-footer'].map(s=>[s,getComputedStyle(document.querySelector(s)).backgroundColor]))''')
  assert all(rgb(v) is not None and sum(rgb(v))<400 for v in bgs.values()),(theme,bgs)
  if theme=='dark':page.screenshot(path=str(ROOT / 'validation/r939-login-dark.png'),full_page=True)
  page.close()
 browser.close()
Path(str(ROOT / 'validation/r939-browser-results.json')).write_text(json.dumps(SCENARIOS,indent=2,ensure_ascii=False))
for row in SCENARIOS:
 s=row['computed']['styles'];print('APROVADO',row['theme'],row['width'],'fato',s['.cm9-clinical-facts']['bg'],'reception',s['.r3-exception-item']['bg'],'modal',s['.cm-modulo-grupo']['bg'],'buttonW',round(s['.cm-online-actions .btn']['rect']['width']))
print('12 cenarios: 8 tema/largura + 2 HC + 2 auth')
