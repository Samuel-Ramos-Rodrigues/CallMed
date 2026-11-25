#!/usr/bin/env python3
"""Teste visual real dos estilos no Chromium local, sem login e sem rede.
Executar: python tests/browser/regressoes/check-r933-browser.py (requer playwright + Chromium)
"""
from pathlib import Path
from tempfile import TemporaryDirectory
from functools import partial
from http.server import ThreadingHTTPServer, SimpleHTTPRequestHandler
from threading import Thread
import json, re, shutil
from playwright.sync_api import sync_playwright
root=Path(__file__).resolve().parents[3]
css=(root/'src/CallMed.Web/wwwroot/css/callmed-clinic-r93.css').as_uri()
js=(root/'src/CallMed.Web/wwwroot/js/atendimento/assistente-flutuante.js').as_uri()
html=r'''<!doctype html><html lang="pt-BR" data-theme="__THEME__">
<head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><link rel="stylesheet" href="__CSS__"></head>
<body class="callmed-app cm-r3-shell cm-r4-shell cm-r5-shell cm-r6-shell cm-r7-shell cm-r8-shell cm9-shell nova-patient patient-context">
<header class="app-header"><div class="app-header-inner"><nav class="primary-nav"><a class="active" href="#">Agendar</a></nav></div></header>
<main id="conteudo" class="app-main">
<section class="r5-booking-page cm25-screen cm19-booking-page cm50-booking">
<header class="cm50-booking-heading"><span class="cm50-overline">AGENDAMENTO DESCOMPLICADO</span><h1>Agende de um jeito simples.</h1><p>Escolha o profissional e o horário.</p></header>
<div class="cm50-booking-layout"><aside class="cm50-booking-guide"><div class="cm-booking-guide-copy"><span class="cm50-overline">PASSO A PASSO</span><h2>Seu caminho em 5 passos</h2><p>Escolha a modalidade e avance no formulário.</p></div><a class="cm-booking-waitlist-link" href="#">Quer uma vaga mais cedo? <strong>Ver lista de espera →</strong></a></aside>
<form class="cm19-booking-card cm19-card cm50-booking-form">
<fieldset class="cm19-booking-section"><legend>Como deseja ser atendido?</legend><p>Escolha onde quer realizar sua consulta.</p><label><input type="radio" checked>Presencial</label></fieldset>
<ol class="cm19-booking-steps"><li data-state="complete"><b>1</b><span>Área de atuação</span></li><li data-state="current"><b>2</b><span>Profissional</span></li><li data-state="upcoming"><b>3</b><span>Data</span></li><li data-state="upcoming"><b>4</b><span>Horário</span></li><li data-state="upcoming"><b>5</b><span>Confirmar</span></li></ol>
<section class="cm19-booking-section"><h2>Profissional</h2><label>Pesquisar<input placeholder="Ex.: profissional ou especialidade"></label></section>
</form></div></section>
<div class="cm9"><section class="cm9-recent"><div class="cm9-recent-item"><strong>Consulta anterior</strong><small>Texto auxiliar de uma consulta anterior</small></div></section><section class="cm9-empty">Sem dados para o período.</section><section class="cm9-activity-grid"><a href="#"><span><strong>Atividade recente</strong><small>Informações da atividade</small></span></a></section></div>
<div class="cm8-patient-now"><h2>Próxima consulta</h2><p>Informações sobre a consulta.</p></div>
</main>
<footer class="app-footer"><span>CallMed · cuidado em conexão</span><nav aria-label="Informações legais"><a href="#app">Aplicativo Android</a><a href="#priv">Privacidade</a><a href="#term">Termos de Serviço</a></nav></footer>
<div class="mksan-floating-actions" data-user-id="visualtest"><button type="button" id="mksan-ai-fab" class="mksan-fab" aria-expanded="false" aria-controls="mksan-ai-widget">Abrir IA</button></div>
<div id="mksan-chat-backdrop" class="mksan-chat-backdrop" hidden aria-hidden="true"></div>
<section id="mksan-ai-widget" class="mksan-ai-widget" role="dialog" aria-hidden="true" aria-label="Assistente virtual CallMed">
<header class="mksan-ai-widget-header"><div class="mksan-widget-brand"><div class="mksan-widget-brand-copy"><small>ASSISTENTE VIRTUAL</small><strong>CallMed</strong><span class="mksan-widget-status ia"><i></i><span>Assistente IA</span></span></div></div><div class="mksan-ai-widget-actions"><button id="mksan-widget-new" type="button">↻</button><button id="mksan-widget-close" type="button">×</button></div></header>
<div id="mksan-widget-messages" class="mksan-ai-widget-messages" role="log"></div>
<div class="mksan-widget-endbar"><span id="mksan-widget-end-status"></span><button id="mksan-widget-end" type="button">Encerrar e avaliar</button></div>
<form id="mksan-widget-form" class="mksan-ai-widget-form"><input type="hidden" name="__RequestVerificationToken" value="demo"><textarea id="mksan-widget-input" placeholder="Digite sua mensagem..."></textarea><button id="mksan-widget-send" type="submit">➤</button></form>
</section><script src="__JS__"></script></body></html>'''
def rgb(color):
    m=re.match(r'rgba?\((\d+),\s*(\d+),\s*(\d+)',color)
    if not m: raise ValueError('RGB não interpretável: '+color)
    return tuple(int(v) for v in m.groups())
def contrast(c1,c2):
    def lum(c):
        xs=[v/255 for v in rgb(c)]
        ys=[x/12.92 if x<=0.04045 else ((x+0.055)/1.055)**2.4 for x in xs]
        return sum(a*b for a,b in zip(ys,[.2126,.7152,.0722]))
    L1,L2=sorted((lum(c1),lum(c2)),reverse=True)
    return (L1+.05)/(L2+.05)
css_content=(root/'src/CallMed.Web/wwwroot/css/callmed-clinic-r93.css').read_text(encoding='utf-8')
js_content=(root/'src/CallMed.Web/wwwroot/js/atendimento/assistente-flutuante.js').read_text(encoding='utf-8')
with sync_playwright() as p:
        chromium=shutil.which('chromium')
        b=p.chromium.launch(headless=True,executable_path=chromium,args=['--no-sandbox','--allow-file-access-from-files','--disable-dev-shm-usage'])
        for theme,width,height in [('dark',1440,900),('light',1440,900),('dark',820,850),('dark',390,780)]:
            page=b.new_page(viewport={'width':width,'height':height},device_scale_factor=1)
            errors=[];page.on('pageerror',lambda exc:errors.append(str(exc)))
            polyfill="""<script>const _store={}; Object.defineProperty(window,'localStorage',{value:{getItem:k=>_store[k]??null,setItem:(k,v)=>{_store[k]=String(v)},removeItem:k=>delete _store[k]}});</script>"""
            content=(html.replace('__THEME__',theme)
                        .replace('<link rel="stylesheet" href="__CSS__">', '<style>'+css_content+'</style>')
                        .replace('<script src="__JS__"></script>',polyfill+'<script>'+js_content+'</script>'))
            page.set_content(content,wait_until='load');page.wait_for_timeout(250)
            def style(selector):
                return page.locator(selector).first.evaluate('e=>{let s=getComputedStyle(e),r=e.getBoundingClientRect();return {background:s.backgroundColor,color:s.color,width:r.width,height:r.height,x:r.x,y:r.y}}')
            footer=style('.app-footer');label=style('.app-footer span');link=style('.app-footer a');
            recent=style('.cm9-recent-item');mini=style('.cm9-recent-item small');cm8=style('.cm8-patient-now');cm8title=style('.cm8-patient-now h2');
            if theme=='dark':
                assert contrast(footer['background'],label['color'])>=4.5,('Rodapé sem contraste',footer,label)
                assert contrast(footer['background'],link['color'])>=4.5,('Link do rodapé ilegível',footer,link)
                assert contrast(recent['background'],mini['color'])>=4.5,('Subtexto de atividade ilegível',recent,mini)
                assert contrast(cm8['background'],cm8title['color'])>=4.5,('Texto do card R8 ilegível',cm8,cm8title)
            mainBefore=style('.app-main')
            page.locator('#mksan-ai-fab').click()
            page.wait_for_timeout(250)
            mainAfter=style('.app-main');chat=style('#mksan-ai-widget')
            assert not page.locator('html').evaluate("e=>e.classList.contains('mksan-chat-modal')")
            assert page.locator('#mksan-chat-backdrop').is_hidden(), 'Backdrop deveria permanecer oculto'
            assert page.locator('#mksan-ai-widget').get_attribute('aria-modal') is None
            assert abs(mainBefore['width']-mainAfter['width'])<2,('Chat comprimiu conteúdo',mainBefore,mainAfter)
            assert chat['x']>=-1 and chat['x']+chat['width']<=width+1,('Chat escapou da tela',chat,width)
            assert chat['y']>=-1 and chat['y']+chat['height']<=height+1,('Chat escapou vertical',chat,height)
            if width>=741: assert chat['x']>width/2,('Chat não está no canto direito',chat)
            if width<=740: assert chat['width']<=width-10,('Chat mobile saiu da viewport',chat)
            bubble=style('#mksan-widget-messages .mksan-widget-msg');bubbleText=style('#mksan-widget-messages .mksan-widget-msg p')
            if theme=='dark': assert contrast(bubble['background'],bubbleText['color'])>=4.5,('IA ilegível',bubble,bubbleText)
            page.keyboard.press('Escape');page.wait_for_timeout(100)
            assert page.locator('#mksan-ai-widget').get_attribute('aria-hidden')=='true'
            assert page.locator('#mksan-chat-backdrop').is_hidden()
            assert page.locator('#mksan-ai-fab').get_attribute('aria-expanded')=='false'
            # Fechamento pelo botão X também deve funcionar.
            page.locator('#mksan-ai-fab').click();page.locator('#mksan-widget-close').click()
            assert page.locator('#mksan-ai-widget').get_attribute('aria-hidden')=='true'
            assert not errors,(theme,width,errors)
            print(json.dumps({'theme':theme,'viewport':width,'footer':footer['background'],'footer_label_contrast':round(contrast(footer['background'],label['color']),2),'footer_link_contrast':round(contrast(footer['background'],link['color']),2),'card_text_contrast':round(contrast(recent['background'],mini['color']),2),'chat_ancorado_no_canto':width>=741,'main_width_unchanged':True,'chat':{k:round(chat[k]) for k in ['x','y','width','height']}},ensure_ascii=False))
            page.close()
        b.close()
print('R9.3.4 — testes de estilo COMPUTADO, contraste e interação no Chromium local: OK')
