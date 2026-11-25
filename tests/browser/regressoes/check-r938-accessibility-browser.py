from pathlib import Path
import json, sys, shutil
from playwright.sync_api import sync_playwright
ROOT=Path(sys.argv[1]) if len(sys.argv)>1 else Path(__file__).resolve().parents[3]
css=(ROOT/'src/CallMed.Web/wwwroot/css/callmed-clinic-r93.css').read_text()
js=(ROOT/'src/CallMed.Web/wwwroot/js/core/accessibility.js').read_text()
html='''<!doctype html><html data-theme="__THEME__" lang="pt-BR"><head><meta name="viewport" content="width=device-width,initial-scale=1"><style>__CSS__</style></head>
<body class="callmed-app cm-r5-shell cm-r8-shell cm9-shell cm-r7-shell patient-context"><header class="app-header"><div class="app-header-inner"><nav class="primary-nav"><a class="active">Início</a><a>Agendar</a></nav><div class="header-tools"><div class="cm91-a11y-popover v18-popover"><div class="cm91-popover-intro"><h2>Acessibilidade</h2><p>Adapte o CallMed</p></div><button class="cm91-a11y-option" data-a11y-toggle="contrast"><span class="cm91-a11y-option-copy"><strong>Alto contraste</strong><small>Reforça cores e contornos</small></span><span class="cm91-a11y-status" data-a11y-status>Desativado</span></button><button class="cm91-a11y-option" data-a11y-toggle="simple"><span class="cm91-a11y-option-copy"><strong>Modo simplificado</strong><small>Prioriza ações essenciais</small></span><span class="cm91-a11y-status" data-a11y-status>Desativado</span></button></div></div></div></header>
<main class="app-main"><div class="cm9 cm9-patient cm50-patient r5-patient"><section class="cm21-simple-home"><h1>Olá, Samuel!</h1><p>O que deseja fazer?</p><div class="cm21-simple-actions"><a href="#">Marcar consulta</a><a href="#">Minhas consultas</a><a href="#">Histórico e remarcações</a><button type="button">Falar com atendimento</button><a href="#">Lista de espera</a></div><button type="button" class="btn secondary" data-a11y-toggle="simple">Sair do modo fácil</button></section><section class="patient-standard cm9-patient-standard"><header class="cm9-page-heading"><h1>Meu cuidado.</h1><p>Acompanhe suas consultas</p></header><section class="cm9-journey"><h2>Jornada</h2><p>Confirmação e preparo</p></section><div class="cm9-action-panel"><h3>O que deseja fazer?</h3><a href="#">Agendar consulta</a></div></section></div><section class="cm50-page-header"><h2>Agenda</h2><p>Filtros</p></section><section class="cm-account-card"><h2>Minha conta</h2><input type="text" placeholder="Telefone"></section><table><thead><tr><th>Data</th></tr></thead><tbody><tr><td>23/11/2026</td></tr></tbody></table></main><footer class="app-footer">CallMed · cuidado em conexão</footer><script>__JS__</script></body></html>'''.replace('__CSS__',css).replace('__JS__',js)
DATA='''() => {
 const names=['body','.cm21-simple-home','.cm21-simple-actions > a','.cm21-simple-actions > button','.cm21-simple-home h1','.cm50-page-header','.cm9-journey','.cm9-action-panel','.app-header','.app-footer','table th','table td','.cm91-a11y-popover'];
 const read = s => {const e=document.querySelector(s);if(!e)return null;const c=getComputedStyle(e);return {bg:c.backgroundColor,color:c.color,border:c.borderColor,outline:c.outlineWidth,display:c.display};};
 const root=getComputedStyle(document.documentElement),b=getComputedStyle(document.body);
 return {className:document.documentElement.className,vars:{rootBg:root.getPropertyValue('--bg').trim(),bodyBg:b.getPropertyValue('--bg').trim(),rootText:root.getPropertyValue('--text').trim(),bodyText:b.getPropertyValue('--text').trim(),bodySurface:b.getPropertyValue('--surface').trim()},styles:Object.fromEntries(names.map(s=>[s,read(s)])),scrollWidth:document.documentElement.scrollWidth,viewWidth:innerWidth};
}'''
if __name__ == '__main__':
 output_dir = ROOT / 'artifacts' / 'browser' / 'acessibilidade'
 output_dir.mkdir(parents=True, exist_ok=True)
 results=[]
 with sync_playwright() as pw:
  browser=pw.chromium.launch(headless=True, executable_path=shutil.which('chromium'), args=['--no-sandbox'])
  for theme in ('dark','light'):
   for width in (1440,1024,820,390,320):
    page=browser.new_page(viewport={'width':width,'height':900});page.set_content(html.replace('__THEME__',theme))
    base=page.evaluate(DATA)
    page.locator('[data-a11y-toggle="simple"]').first.click();simplified=page.evaluate(DATA)
    page.locator('[data-a11y-toggle="contrast"]').first.click();both=page.evaluate(DATA)
    page.locator('[data-a11y-toggle="simple"]').first.click();contrast=page.evaluate(DATA)
    if len(sys.argv)>2 and sys.argv[2]=='shot':
     page.screenshot(path=output_dir / f'r938_{theme}_{width}.png',full_page=True)
    results.append({'theme':theme,'width':width,'base':base,'simple':simplified,'simple_and_contrast':both,'contrast_only':contrast})
    page.close()
  browser.close()
 outfile = output_dir / 'r938_accessibility_results.json'
 outfile.write_text(json.dumps(results,ensure_ascii=False,indent=2))
 for r in results:
  print(r['theme'],r['width'],'B body',r['base']['styles']['body']['bg'],'S action',r['simple']['styles']['.cm21-simple-actions > a']['bg'],r['simple']['styles']['.cm21-simple-actions > a']['color'],'HC body',r['contrast_only']['styles']['body']['bg'],'HC card',r['contrast_only']['styles']['.cm50-page-header']['bg'],'HC action',r['simple_and_contrast']['styles']['.cm21-simple-actions > a']['bg'])

 # Assert that active modes change the *computed* CSS used by the user,
 # not merely the accessibility button's aria-pressed text.
 def rgb(value):
  import re
  m=re.match(r'rgba?\((\d+),\s*(\d+),\s*(\d+)',value)
  assert m, value
  return tuple(map(int,m.groups()))
 def luminance(v):
  s=[(x/255)/12.92 if x/255<=.04045 else ((x/255+.055)/1.055)**2.4 for x in v]
  return .2126*s[0]+.7152*s[1]+.0722*s[2]
 def ratio(a,b):
  v,w=sorted((luminance(rgb(a)),luminance(rgb(b))))
  return (w+.05)/(v+.05)
 for v in results:
  name=f'{v["theme"]}@{v["width"]}'
  a=v['simple']['styles']['.cm21-simple-actions > a']
  assert ratio(a['color'],a['bg'])>=4.5,(name,'simple action contrast',a)
  assert v['simple']['styles']['.cm21-simple-actions > a']['bg']!=v['base']['styles']['.cm21-simple-actions > a']['bg'],name+' simple state unchanged'
  assert v['contrast_only']['styles']['body']['bg']!=v['base']['styles']['body']['bg'],name+' high contrast not visible'
  for kind in ['simple','simple_and_contrast','contrast_only']:
   z=v[kind]
   assert z['scrollWidth']<=z['viewWidth'],(name,kind,'horizontal scroll')
  for kind in ['simple_and_contrast','contrast_only']:
   z=v[kind]
   for sel in ('.cm21-simple-actions > a','.cm50-page-header','.cm91-a11y-popover'):
    st=z['styles'][sel]
    assert ratio(st['color'],st['bg'])>=4.5,(name,kind,sel,st)
  assert v['simple_and_contrast']['styles']['.cm21-simple-actions > a']['bg']!=v['simple']['styles']['.cm21-simple-actions > a']['bg'],name+' combined contrast ignored'
 print('R9.3.8: high contrast + simplified mode: 10 tema/viewport combinations and color tests passed')
