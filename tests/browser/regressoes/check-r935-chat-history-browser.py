#!/usr/bin/env python3
"""Teste sem rede: abertura do chat com historico maior expande e permite fechar."""
from pathlib import Path
from playwright.sync_api import sync_playwright
import ast,shutil,json
root=Path(__file__).resolve().parents[3]
tree=ast.parse((root/'tests/browser/regressoes/check-r933-browser.py').read_text())
markup=next(ast.literal_eval(n.value) for n in tree.body if isinstance(n,ast.Assign) and any(isinstance(t,ast.Name) and t.id=='html' for t in n.targets))
css=(root/'src/CallMed.Web/wwwroot/css/callmed-clinic-r93.css').read_text()
js=(root/'src/CallMed.Web/wwwroot/js/atendimento/assistente-flutuante.js').read_text()
markup=markup.replace('<link rel="stylesheet" href="__CSS__">','<style>'+css+'</style>').replace('<script src="__JS__"></script>','')
store="""<script>const _store={}; Object.defineProperty(window,'localStorage',{value:{getItem:k=>_store[k]??null,setItem:(k,v)=>{_store[k]=String(v)},removeItem:k=>delete _store[k]}});</script>"""
markup=markup.replace('</body>',store+'</body>')
with sync_playwright() as p:
 browser=p.chromium.launch(executable_path=shutil.which('chromium'),args=['--no-sandbox'])
 for width in [390,1440]:
  page=browser.new_page(viewport={'width':width,'height':780})
  page.set_content(markup.replace('__THEME__','dark'))
  page.evaluate('''()=>{localStorage.setItem('mksan-ai-session-visualtest','qa-history');const h=Array.from({length:6},(_,i)=>({who:i%2?'user':'bot',text:'Mensagem de teste '+i,at:Date.now()}));localStorage.setItem('mksan-ai-history-visualtest-qa-history',JSON.stringify(h));}''')
  page.add_script_tag(content=js)
  page.locator('#mksan-ai-fab').click();page.wait_for_timeout(150)
  assert page.locator('#mksan-ai-widget').get_attribute('data-density')=='expanded'
  size=page.locator('#mksan-ai-widget').evaluate('e=>e.getBoundingClientRect().height')
  assert size>=470, (width,size)
  assert page.locator('.mksan-widget-msg').count()==6
  page.locator('#mksan-widget-close').click()
  assert page.locator('#mksan-ai-widget').get_attribute('aria-hidden')=='true'
  print(json.dumps({'width':width,'expanded_height':size,'messages':6,'close':'OK'},ensure_ascii=False))
  page.close()
 browser.close()
