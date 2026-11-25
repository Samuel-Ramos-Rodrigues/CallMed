#!/usr/bin/env python3
"""Regressão de confirmação compartilhada do CallMed R9.3.6, sem acessar produção."""
from pathlib import Path
from playwright.sync_api import sync_playwright
import json,re,shutil,sys
root=Path(__file__).resolve().parents[3]
css=(root/'src/CallMed.Web/wwwroot/css/callmed-clinic-r93.css').read_text(encoding='utf-8')
js=(root/'src/CallMed.Web/wwwroot/js/core/feedback.js').read_text(encoding='utf-8')
template='''<!doctype html><html lang="pt-BR" data-theme="__THEME__"><head><meta name="viewport" content="width=device-width, initial-scale=1"><style>__CSS__</style></head><body class="callmed-app cm-r3-shell cm-r4-shell cm-r5-shell cm-r6-shell cm-r7-shell cm-r8-shell cm9-shell patient-context"><main class="app-main" style="padding:24px"><h1>Consulta</h1><button id="open-confirm">Limpar mensagens</button><div id="state" role="status">Não alterado</div></main><aside style="position:fixed;bottom:10px;right:12px;width:300px;max-width:50vw;height:160px;background:#1c354c;color:white">Chat aberto</aside><script>__JS__</script></body></html>'''
def lum(v):
 parts=tuple(int(x) for x in re.match(r'rgba?\((\d+),\s*(\d+),\s*(\d+)',v).groups())
 return sum((x/255/12.92 if x/255<=.04045 else ((x/255+.055)/1.055)**2.4)*weight for x,weight in zip(parts,[.2126,.7152,.0722]))
def contrast(a,b):
 l1,l2=sorted((lum(a),lum(b)),reverse=True)
 return round((l1+.05)/(l2+.05),2)
script="""() => {document.querySelector('#open-confirm').addEventListener('click',()=>{window.CallMedUI.confirm('Limpar as mensagens exibidas neste navegador? O histórico do atendimento continuará salvo no sistema.',{title:'Limpar mensagens da tela', confirmText:'Limpar mensagens',cancelText:'Manter mensagens', type:'warning'}).then(ok=>{document.querySelector('#state').textContent=ok?'Aceitou':'Cancelou'})})}"""
out=[]
with sync_playwright() as p:
 b=p.chromium.launch(headless=True,executable_path=shutil.which('chromium'),args=['--no-sandbox','--disable-dev-shm-usage'])
 for theme,width,height in [('dark',1440,900),('light',1440,900),('dark',1024,768),('dark',820,780),('dark',390,740),('light',390,740),('dark',320,680)]:
  page=b.new_page(viewport={'width':width,'height':height})
  errors=[];page.on('pageerror',lambda e:errors.append(str(e)))
  page.set_content(template.replace('__THEME__',theme).replace('__CSS__',css).replace('__JS__',js),wait_until='domcontentloaded')
  page.evaluate(script)
  page.locator('#open-confirm').click()
  dialog=page.locator('dialog.cm-feedback-dialog');dialog.wait_for(state='visible')
  rect=dialog.bounding_box()
  assert rect is not None
  assert abs(rect['x']+rect['width']/2-width/2)<1.5,(theme,width,'modal horizontal deslocado',rect)
  assert abs(rect['y']+rect['height']/2-height/2)<2,(theme,width,'modal vertical deslocado',rect)
  assert rect['x']>=0 and rect['y']>=0,(theme,width,'modal fora da tela')
  attrs=dialog.evaluate('''e=>{const computed=el=>getComputedStyle(el);return {outer:computed(e).backgroundColor,footer:computed(e.querySelector('.cm-feedback-actions')).backgroundColor,text:computed(e.querySelector('.cm-feedback-content p')).color,textBg:computed(e.querySelector('.cm-feedback-content')).backgroundColor,cancel:computed(e.querySelector('.secondary')).color,cancelBg:computed(e.querySelector('.secondary')).backgroundColor,accept:computed(e.querySelector('.cm-feedback-accept')).color,acceptBg:computed(e.querySelector('.cm-feedback-accept')).backgroundColor}}''')
  if theme=='dark':
   assert attrs['outer']!='rgb(255, 255, 255)'
   assert attrs['footer']!='rgb(244, 248, 252)',(theme,width,'faixa clara')
  assert contrast(attrs['text'],attrs['textBg'])>=4.5,(theme,width,'texto de descrição',attrs)
  assert contrast(attrs['cancel'],attrs['cancelBg'])>=4.5,(theme,width,'cancel sem contraste',attrs)
  assert contrast(attrs['accept'],attrs['acceptBg'])>=4.5,(theme,width,'confirm sem contraste',attrs)
  buttons=dialog.locator('button').all()
  for btn in buttons:
   bb=btn.bounding_box()
   assert bb is not None and bb['height']>=44-0.2,(theme,width,'botão pequeno',bb)
  assert page.locator('#open-confirm').is_visible()
  assert dialog.get_attribute('aria-modal')=='true'
  assert dialog.get_attribute('role')=='alertdialog'
  # A saída via Esc é sempre cancelamento seguro.
  page.keyboard.press('Escape')
  page.locator('#state').get_by_text('Cancelou').wait_for()
  assert dialog.count()==0
  assert page.evaluate('document.activeElement.id')=='open-confirm',(theme,width,'foco não restaurado')
  # Cancelamento por clique: sem efeito colateral.
  page.locator('#open-confirm').click()
  page.get_by_role('button',name='Manter mensagens').click()
  assert page.locator('#state').inner_text()=='Cancelou'
  # Confirmação somente por ação explícita.
  page.locator('#open-confirm').click()
  dialog.locator('.cm-feedback-accept').click()
  page.locator('#state').get_by_text('Aceitou').wait_for()
  assert not errors,(theme,width,errors)
  out.append({'tema':theme,'largura':width,'modal_centralizado':True,'faixa':attrs['footer'],'contraste_texto':contrast(attrs['text'],attrs['textBg']),'contraste_cancelar':contrast(attrs['cancel'],attrs['cancelBg']),'contraste_confirmar':contrast(attrs['accept'],attrs['acceptBg']),'Esc_cancelou':True,'confirmacao_explicita':True})
  page.close()
 b.close()
print(json.dumps(out,ensure_ascii=False,indent=2))
