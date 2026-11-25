#!/usr/bin/env python3
"""Auditoria local de interface: componentes reais adaptados de Razor; nao e teste autenticado."""
from pathlib import Path
from playwright.sync_api import sync_playwright
import ast, json, re, shutil, sys
root=Path(__file__).resolve().parents[3]
CSS=(root/'src/CallMed.Web/wwwroot/css/callmed-clinic-r93.css').read_text()
JS=(root/'src/CallMed.Web/wwwroot/js/atendimento/assistente-flutuante.js').read_text()
SHELL=(root/'src/CallMed.Web/wwwroot/js/core/shell.js').read_text()
THEME=(root/'src/CallMed.Web/wwwroot/js/core/theme.js').read_text()
other=(root/'tests/browser/regressoes/check-r933-browser.py')
tree=ast.parse(other.read_text())
original=next(ast.literal_eval(node.value) for node in tree.body if isinstance(node,ast.Assign) and any(isinstance(t,ast.Name) and t.id=='html' for t in node.targets))
home='''<section class="cm9 cm9-patient"><div class="cm9-patient-standard"><div class="cm9-patient-lower">
<section class="cm9-journey"><div class="cm9-section-top"><div><span class="cm9-overline">ACOMPANHAMENTO</span><h2>Sua jornada de cuidado</h2><p>Cada etapa organizada em um só lugar.</p></div><a href="#consultas">Ver consultas ↗</a></div>
<ol class="cm9-journey-list"><li class="cm9-step-complete"><span class="cm9-step-icon">1</span><div><strong>Agendamento</strong><small>Escolha profissional, modalidade e horário</small></div></li><li class="cm9-step-active"><span class="cm9-step-icon">2</span><div><strong>Confirmação e preparo</strong><small>Acompanhe as orientações da sua consulta</small></div></li><li><span class="cm9-step-icon">3</span><div><strong>Check-in</strong><small>Registro de presença quando disponível</small></div></li><li><span class="cm9-step-icon">4</span><div><strong>Atendimento</strong><small>Na clínica ou por teleconsulta</small></div></li></ol></section>
<aside class="cm9-action-panel"><div class="cm9-section-top"><div><span class="cm9-overline">ACESSO RÁPIDO</span><h2>O que deseja fazer?</h2><p>Seus serviços a um toque.</p></div></div><nav class="cm9-action-list"><a href="#agendar"><span class="cm9-action-icon">✚</span><span><strong>Agendar consulta</strong><small>Escolha especialidade, data e horário</small></span><b>›</b></a><a href="#examinar"><span class="cm9-action-icon">▤</span><span><strong>Documentos e exames</strong><small>Acesse seu histórico disponível</small></span><b>›</b></a></nav></aside></div></div></section>'''
dialog='''<button id="open-modules" data-modules-open aria-controls="modulesDialog" aria-expanded="false">Mais funções</button>
<dialog id="modulesDialog" class="modules-dialog" aria-labelledby="modulesTitle"><header><h2 id="modulesTitle">Todas as funções</h2><button data-modules-close aria-label="Fechar menu">×</button></header><label for="cmModuleSearch">Buscar funções<input id="cmModuleSearch" type="search" /></label><p id="cmModuleSearchStatus" role="status"></p><details class="cm-modulo-grupo" open><summary>Meu atendimento</summary><div class="module-grid"><a href="#agendar">Agendar consulta</a><a href="#consulta">Consulta</a></div></details></dialog>
<button id="theme-opener" data-theme-toggle data-popover-trigger="theme">Tema</button><div data-popover="theme" hidden><button data-theme-choice="dark">Escuro</button><button data-theme-choice="light">Claro</button></div>'''
original=original.replace('</main>',home+dialog+'</main>')
original=original.replace('<link rel="stylesheet" href="__CSS__">','<style>'+CSS+'</style>')
# old fixture has JS immediately before closing body; inject scripts through add_script_tag to permit precise initialization
original=original.replace('<script src="__JS__"></script>','')
store="""<script>const _store={}; Object.defineProperty(window,'localStorage',{value:{getItem:k=>_store[k]??null,setItem:(k,v)=>{_store[k]=String(v)},removeItem:k=>delete _store[k]}});</script>"""
original=original.replace('</body>',store+'</body>')
def style(page, selector):
 return page.locator(selector).first.evaluate('''e=>{let s=getComputedStyle(e),r=e.getBoundingClientRect();return {x:r.left,y:r.top,right:r.right,bottom:r.bottom,width:r.width,height:r.height,padding:s.padding,background:s.backgroundColor,color:s.color,visibility:s.visibility}}''')
def rgb(s):
 m=re.match(r'rgba?\((\d+),\s*(\d+),\s*(\d+)',s)
 return tuple(map(int,m.groups())) if m else None
def contrast(a,b):
 a,b=rgb(a),rgb(b)
 if not a or not b:return None
 def l(v):
  return sum((c/255/12.92 if c/255<=.04045 else ((c/255+.055)/1.055)**2.4)*k for c,k in zip(v,[.2126,.7152,.0722]))
 x,y=sorted((l(a),l(b)),reverse=True);return round((x+.05)/(y+.05),2)
out=[];fail=[]
with sync_playwright() as p:
 b=p.chromium.launch(executable_path=shutil.which('chromium'),args=['--no-sandbox','--disable-dev-shm-usage'])
 for theme,w,h in [('dark',1440,900),('light',1440,900),('dark',1024,768),('dark',820,850),('dark',390,780),('dark',320,700),('dark',720,900)]:
  page=b.new_page(viewport={'width':w,'height':h},device_scale_factor=1)
  errors=[];page.on('pageerror',lambda e: errors.append(str(e)))
  page.set_content(original.replace('__THEME__',theme),wait_until='domcontentloaded')
  page.add_script_tag(content=JS)
  page.add_script_tag(content=SHELL)
  page.add_script_tag(content=THEME)
  page.evaluate('(theme)=>window.CallMedTheme.set(theme)',theme)
  page.wait_for_timeout(100)
  before=style(page,'.app-main')
  guide=style(page,'.cm50-booking-guide');form=style(page,'.cm50-booking-form')
  journey=style(page,'.cm9-journey');action=style(page,'.cm9-action-panel');active=style(page,'.cm9-step-active');active_title=style(page,'.cm9-step-active strong');active_desc=style(page,'.cm9-step-active small')
  footer=style(page,'.app-footer');footer_text=style(page,'.app-footer span')
  doc=page.evaluate('({width:document.documentElement.scrollWidth,client:document.documentElement.clientWidth})')
  if doc['width']>w+3:fail.append(f'{theme}/{w} overflow horizontal em {doc}')
  if form['y']<guide['bottom']+5:fail.append(f'{theme}/{w} guia sobreposto ao formulário')
  for n,r in [('painel jornada',journey),('painel ação',action)]:
   m=re.search(r'[\d.]+',r['padding']);pad=float(m.group(0)) if m else 0
   if pad<17:fail.append(f'{theme}/{w} falta de padding {n}: {pad}')
  if True:
   for n,c in [('etapa título',contrast(active['background'],active_title['color'])),('etapa descrição',contrast(active['background'],active_desc['color'])),('rodapé',contrast(footer['background'],footer_text['color']))]:
    if c is None or c<4.5:fail.append(f'{theme}/{w} contraste insuficiente {n}: {c}')
  page.locator('#mksan-ai-fab').click();page.wait_for_timeout(90)
  chat=style(page,'#mksan-ai-widget');after=style(page,'.app-main')
  if abs(before['width']-after['width'])>2:fail.append(f'{theme}/{w} chat alterou largura')
  if chat['right']>w+1 or chat['x']<-1 or chat['y']<-1 or chat['bottom']>h+1:fail.append(f'{theme}/{w} chat fora da viewport: {chat}')
  if page.locator('#mksan-chat-backdrop').is_visible():fail.append(f'{theme}/{w} backdrop indevido')
  if page.locator('#mksan-widget-input').is_visible() is False:fail.append(f'{theme}/{w} campo chat oculto')
  for btn in ['#mksan-widget-close','#mksan-widget-new','#mksan-widget-send']:
   bb=style(page,btn)
   if min(bb['width'],bb['height'])<43.9:fail.append(f'{theme}/{w} alvo de toque {btn} menor que 44px: {bb}')
  if page.locator('#mksan-ai-widget').get_attribute('data-density')!='compact':fail.append(f'{theme}/{w} IA inicial não compacta')
  page.keyboard.press('Escape');page.wait_for_timeout(50)
  if page.locator('#mksan-ai-widget').get_attribute('aria-hidden')!='true':fail.append(f'{theme}/{w} Esc não fecha chat')
  if page.evaluate('document.activeElement.id')!='mksan-ai-fab':fail.append(f'{theme}/{w} foco não retornou à bolha')
  # Verifica alternância real de tema via clique (e não só texto do tooltip).
  page.locator('#theme-opener').click()
  page.locator('[data-theme-choice="light"]').click()
  if page.locator('html').get_attribute('data-theme')!='light':fail.append(f'{theme}/{w} tema claro não aplicado via controle')
  if not page.locator('#theme-opener').get_attribute('aria-label').startswith('Tema claro'):fail.append(f'{theme}/{w} tooltip tema claro dessincronizado')
  page.locator('#theme-opener').click()
  page.locator('[data-theme-choice="dark"]').click()
  if page.locator('html').get_attribute('data-theme')!='dark':fail.append(f'{theme}/{w} tema escuro não aplicado via controle')
  if not page.locator('#theme-opener').get_attribute('aria-label').startswith('Tema escuro'):fail.append(f'{theme}/{w} tooltip tema escuro dessincronizado')
  page.keyboard.press('Control+k');page.wait_for_timeout(50)
  if not page.locator('#modulesDialog').evaluate('e=>e.open'):fail.append(f'{theme}/{w} Control+K não abriu')
  if page.evaluate('document.activeElement.id')!='cmModuleSearch':fail.append(f'{theme}/{w} foco não entrou na busca')
  page.keyboard.press('Escape')
  if page.locator('#modulesDialog').evaluate('e=>e.open'):fail.append(f'{theme}/{w} Esc não fechou menu')
  if errors:fail.append(f'{theme}/{w} JS pageerror: {errors}')
  out.append({'theme':theme,'width':w,'doc':doc,'guide':{k:round(guide[k]) if isinstance(guide[k],float) else guide[k] for k in ['y','bottom']},'formY':round(form['y']),'chat':{k:round(chat[k]) for k in ['x','y','right','bottom','width','height']},'journeyPad':journey['padding'],'contrastActiveTitle':contrast(active['background'],active_title['color']),'contrastActiveDesc':contrast(active['background'],active_desc['color']),'contrastFooter':contrast(footer['background'],footer_text['color'])})
  if w in (390,820,1024,1440) and theme=='dark':
   path = root / 'artifacts' / 'browser' / 'responsividade' / f'R935-local-pre-{w}.png'
   path.parent.mkdir(parents=True, exist_ok=True)
   page.screenshot(path=str(path),full_page=False)
  page.close()
 b.close()
print(json.dumps({'tests':out,'failures':fail},ensure_ascii=False,indent=2))
if fail:sys.exit(1)
