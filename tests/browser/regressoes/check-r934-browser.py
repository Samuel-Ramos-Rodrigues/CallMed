#!/usr/bin/env python3
"""Smoke visual isolado para o chat no canto e o tema escuro. Não requer login/rede."""
from pathlib import Path
from playwright.sync_api import sync_playwright
import ast, shutil, re, json
root=Path(__file__).resolve().parents[3]
output_dir = root / 'artifacts' / 'browser' / 'chat'
output_dir.mkdir(parents=True, exist_ok=True)
css=(root/'src/CallMed.Web/wwwroot/css/callmed-clinic-r93.css').read_text(encoding='utf-8')
js=(root/'src/CallMed.Web/wwwroot/js/atendimento/assistente-flutuante.js').read_text(encoding='utf-8')
previous=(root/'tests/browser/regressoes/check-r933-browser.py').read_text(encoding='utf-8')
node=next(n for n in ast.parse(previous).body if isinstance(n,ast.Assign) and any(isinstance(t,ast.Name) and t.id=='html' for t in n.targets))
markup=ast.literal_eval(node.value)
# Mais componentes para detectar recuos e contraste da tela inicial do paciente.
home='''<section class="cm9 cm9-patient"><div class="cm9-patient-standard"><div class="cm9-patient-lower">
<section class="cm9-journey"><div class="cm9-section-top"><div><span class="cm9-overline">ACOMPANHAMENTO</span><h2>Sua jornada de cuidado</h2><p>Cada etapa organizada em um só lugar.</p></div><a href="#">Ver consultas</a></div><ol class="cm9-journey-list"><li class="cm9-step-complete"><span class="cm9-step-icon">1</span><div><strong>Agendamento</strong><small>Escolha profissional, modalidade e horário</small></div></li><li class="cm9-step-active"><span class="cm9-step-icon">2</span><div><strong>Confirmação e preparo</strong><small>Acompanhe as orientações da sua consulta</small></div></li></ol></section>
<aside class="cm9-action-panel"><div class="cm9-section-top"><div><span class="cm9-overline">ACESSO RÁPIDO</span><h2>O que deseja fazer?</h2><p>Seus serviços a um toque.</p></div></div><nav class="cm9-action-list"><a href="#"><span class="cm9-action-icon">+</span><span><strong>Agendar consulta</strong><small>Escolha especialidade, data e horário</small></span><b>›</b></a></nav></aside>
</div></div></section>'''
markup=markup.replace('</main>',home+'</main>')
store="""<script>const store={}; Object.defineProperty(window,'localStorage',{value:{getItem:k=>store[k]??null,setItem:(k,v)=>{store[k]=String(v)},removeItem:k=>delete store[k]}});</script>"""
markup=markup.replace('<link rel="stylesheet" href="__CSS__">', '<style>'+css+'</style>')
markup=markup.replace('<script src="__JS__"></script>',store+'<script>'+js+'</script>')
with sync_playwright() as p:
 b=p.chromium.launch(headless=True,executable_path=shutil.which('chromium'),args=['--no-sandbox','--disable-dev-shm-usage'])
 out=[]
 for theme,w,h in [('dark',1440,900),('light',1440,900),('dark',820,800),('dark',390,780)]:
  page=b.new_page(viewport={'width':w,'height':h},device_scale_factor=1)
  errs=[];page.on('pageerror',lambda e:errs.append(str(e)))
  page.set_content(markup.replace('__THEME__',theme),wait_until='load');page.wait_for_timeout(180)
  def metrics(selector):
   return page.locator(selector).first.evaluate("""e=>{let r=e.getBoundingClientRect(),s=getComputedStyle(e);return {x:r.x,y:r.y,right:r.right,bottom:r.bottom,width:r.width,height:r.height,bg:s.backgroundColor,color:s.color,padding:s.padding,display:s.display}}""")
  main1=metrics('.app-main'); guide=metrics('.cm50-booking-guide');form=metrics('.cm50-booking-form')
  assert form['y']>=guide['bottom']+8,('Guia/form sobrepostos',w,guide,form)
  journey=metrics('.cm9-journey');action=metrics('.cm9-action-panel')
  assert float(re.search(r'\d+(\.\d+)?',journey['padding']).group())>=17,('Painel sem recuo',w,journey)
  assert float(re.search(r'\d+(\.\d+)?',action['padding']).group())>=17,('Atalhos sem recuo',w,action)
  active=metrics('.cm9-step-active');activeStrong=metrics('.cm9-step-active strong')
  assert activeStrong['color']!='rgb(255, 255, 255)' or active['bg']!='rgb(217, 246, 242)',('Etapa ilegível',theme,active,activeStrong)
  page.locator('#mksan-ai-fab').click();page.wait_for_timeout(120)
  chat=metrics('#mksan-ai-widget');main2=metrics('.app-main')
  assert abs(main1['width']-main2['width'])<2,('Chat reduziu layout',w,main1,main2)
  assert page.locator('#mksan-chat-backdrop').is_hidden(),('Backdrop abriu',w)
  assert not page.locator('html').evaluate("e=>e.classList.contains('mksan-chat-modal')"),('Virou modal',w)
  assert page.locator('#mksan-ai-widget').get_attribute('aria-modal') is None,('aria-modal indevido',w)
  assert chat['x']>w/2 if w>=800 else chat['x']>=0,('Widget não está à direita',w,chat)
  assert chat['right']<=w+1 and chat['bottom']<=h+1,('Widget saiu da tela',w,h,chat)
  assert chat['height']<h*.85,('Chat grande demais',w,h,chat)
  assert page.locator('#mksan-widget-input').is_visible()
  page.keyboard.press('Escape');page.wait_for_timeout(80)
  assert page.locator('#mksan-ai-widget').get_attribute('aria-hidden')=='true'
  page.locator('#mksan-ai-fab').click();page.locator('#mksan-widget-close').click()
  assert page.locator('#mksan-ai-widget').get_attribute('aria-hidden')=='true'
  assert not errs,errs
  if theme=='dark' and w in [1440,390]:
   page.screenshot(path=str(output_dir / f'r934-{w}.png'),full_page=False)
  out.append({'theme':theme,'width':w,'widget':chat,'guideBottom':round(guide['bottom']),'formTop':round(form['y']),'journeyPadding':journey['padding']})
  page.close()
 b.close()
print(json.dumps(out,ensure_ascii=False,indent=2))
