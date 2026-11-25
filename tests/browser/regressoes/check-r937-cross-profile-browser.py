#!/usr/bin/env python3
"""CSS/JS de modais, alertas e permissões visuais em 4 contextos, sem produção ou escrita real."""
from pathlib import Path
import json, re, shutil
from playwright.sync_api import sync_playwright
R=Path(__file__).resolve().parents[3]
CSS=(R/'src/CallMed.Web/wwwroot/css/callmed-clinic-r93.css').read_text(encoding='utf-8')
JS=(R/'src/CallMed.Web/wwwroot/js/core/feedback.js').read_text(encoding='utf-8')
HTML='''<!doctype html><html lang="pt-BR" data-theme="_THEME_"><head><meta name="viewport" content="width=device-width, initial-scale=1"><style>_CSS_</style></head><body class="callmed-app cm50-app cm-r3-shell cm-r4-shell cm-r5-shell cm-r6-shell cm-r7-shell cm-r8-shell cm9-shell _ROLE_ _CONTEXT_"><main class="app-main"><h1>Exemplo de módulo</h1><button id="open">Mostrar aviso</button><form data-cm-confirm="Cancelar esta consulta?" data-cm-confirm-title="Cancelar consulta" data-cm-confirm-accept="Cancelar consulta" data-cm-confirm-tone="danger" id="delete"><button type="submit">Cancelar consulta</button></form><p id="state">Nenhuma alteração</p><div id="banner" class="alert warning">Aviso: há uma informação importante neste atendimento. Leia antes de continuar.</div><button id="show-modules">Mais funções</button><button id="show-channels">Situação dos canais</button></main><dialog id="modulesDialog" class="modules-dialog" aria-labelledby="modulesTitle"><header><div><span class="eyebrow">TODAS AS FUNÇÕES</span><h2 id="modulesTitle">Para onde você quer ir?</h2><p>Encontre agenda, confirmações, fila de espera, check-in e administração.</p></div><button type="button" class="icon-button" id="modules-close">×</button></header><label class="cm-module-search">Buscar nas funções da sua conta<input type="search" placeholder="Ex.: agenda, salas, pacientes…" /></label><div class="cm-menu-conteudo"><a href="#">Meu perfil</a></div></dialog><dialog id="channelStatusDialog" class="channel-status-dialog" aria-labelledby="channelStatusTitle"><div class="channel-status-head"><div><h2 id="channelStatusTitle">Situação dos canais</h2><p>Disponibilidade de entrada e saída configurada no CallMed.</p></div><button type="button" class="channel-status-close" id="channels-close">×</button></div><div id="channelStatusContent" class="channel-status-grid"><div class="channel-status-loading">Verificando canais...</div></div></dialog><script>_JS_</script></body></html>'''
def luminance(s):
    m=re.search(r'rgba?\((\d+),\s*(\d+),\s*(\d+)',s)
    if m:
        rgb=[int(v)/255 for v in m.groups()]
    else:
        srgb=re.search(r'color\(srgb\s+([\d.]+)\s+([\d.]+)\s+([\d.]+)',s)
        if not srgb:return None
        rgb=[float(v) for v in srgb.groups()]
    return sum((v/12.92 if v<=0.04045 else ((v+0.055)/1.055)**2.4)*w for v,w in zip(rgb,(0.2126,0.7152,0.0722)))
def contrast(a,b):
    x,y=luminance(a),luminance(b)
    if x is None or y is None:return None
    return round((max(x,y)+.05)/(min(x,y)+.05),2)
profis=[('Paciente','nova-patient','patient-context'),('Médico','nova-doctor','staff-context'),('Atendente','nova-staff','staff-context'),('Administrador','nova-admin','staff-context')]
result=[]
with sync_playwright() as p:
    browser=p.chromium.launch(headless=True,executable_path=shutil.which('chromium'),args=['--no-sandbox','--disable-dev-shm-usage'])
    for role,roleClass,ctx in profis:
        for theme,width,height in [('dark',1440,900),('light',1440,900),('dark',390,760)]:
            page=browser.new_page(viewport={'width':width,'height':height})
            errors=[];page.on('pageerror',lambda e:errors.append(str(e)))
            page.set_content(HTML.replace('_THEME_',theme).replace('_CSS_',CSS).replace('_JS_',JS).replace('_ROLE_',roleClass).replace('_CONTEXT_',ctx),wait_until='domcontentloaded')
            page.evaluate('''() => { window.submitCount=0;document.addEventListener('submit',e=>{if(e.target.id==='delete'){e.preventDefault();window.submitCount++;}},false);document.querySelector('#show-modules').onclick=()=>document.querySelector('#modulesDialog').showModal();document.querySelector('#modules-close').onclick=()=>document.querySelector('#modulesDialog').close();document.querySelector('#show-channels').onclick=()=>document.querySelector('#channelStatusDialog').showModal();document.querySelector('#channels-close').onclick=()=>document.querySelector('#channelStatusDialog').close();document.querySelector('#open').onclick=()=>window.CallMedUI.confirm('Deseja continuar?',{title:'Confirmar ação',confirmText:'Confirmar',cancelText:'Voltar',type:'confirm'}); }''')
            # Global warning banner appears in all profile contexts; touch target 44px.
            b=page.locator('#banner');b.locator('.cm-feedback-banner-close').wait_for()
            dim=b.locator('.cm-feedback-banner-close').bounding_box()
            assert dim['width']>=43.5 and dim['height']>=43.5,('Alvo pequeno de alerta',role,theme,width,dim)
            style=b.evaluate('''e => ({text:getComputedStyle(e.querySelector('.cm-feedback-banner-body')).color,bg:getComputedStyle(e).backgroundColor})''')
            ratio=contrast(style['text'],style['bg'])
            assert ratio is not None and ratio>=4.5,('Contraste do alerta',role,theme,width,ratio,style)
            # Normal confirmation; cancellation by Esc leaves unchanged
            page.locator('#open').click();dialog=page.locator('dialog.cm-feedback-dialog');dialog.wait_for(state='visible')
            rect=dialog.bounding_box();assert abs(rect['x']+rect['width']/2-width/2)<2,('Confirmação desalinhada',role,theme,width,rect)
            page.keyboard.press('Escape');dialog.wait_for(state='detached')
            assert page.evaluate('document.activeElement.id')=='open'
            # Destructive form uses global confirm; no form submission before acceptance
            page.locator('#delete button').click();dialog.wait_for(state='visible')
            assert page.evaluate('window.submitCount')==0,('Alteração antes de confirmar',role,theme,width)
            color=dialog.locator('.cm-feedback-accept').evaluate('''e=>({bg:getComputedStyle(e).backgroundColor,text:getComputedStyle(e).color})''')
            assert contrast(color['bg'],color['text'])>=4.5,('Contraste botão destrutivo',role,theme,width,color)
            assert color['bg'].startswith(('rgb(168, 38, 54)','rgb(187, 52, 67)')),('Perigo sem cor distinta',role,theme,width,color)
            page.locator('dialog.cm-feedback-dialog .secondary').click();dialog.wait_for(state='detached')
            assert page.evaluate('window.submitCount')==0,('Cancelar não interrompeu operação',role,theme,width)
            page.locator('#delete button').click();dialog.wait_for(state='visible');dialog.locator('.cm-feedback-accept').click();dialog.wait_for(state='detached')
            assert page.evaluate('window.submitCount')==1,('Confirmação não submeteu uma vez',role,theme,width)
            # Universal functions search modal and channel modal (staff only for actual app).
            for modal,trig in [('modulesDialog','show-modules')]+([('channelStatusDialog','show-channels')] if role in ('Atendente','Administrador') else []):
                page.locator('#'+trig).click();loc=page.locator('#'+modal);loc.wait_for(state='visible')
                dim=loc.bounding_box();assert dim['x']>=0 and dim['x']+dim['width']<=width+.5,('Modal fora viewport',role,theme,width,modal,dim)
                fg=loc.locator('h2').evaluate('''e=>getComputedStyle(e).color''')
                bg=loc.evaluate('''e=>getComputedStyle(e).backgroundColor''')
                c=contrast(fg,bg)
                assert c is not None and c>=4.5,('Título modal ilegível',role,theme,width,modal,c,fg,bg)
                close=loc.locator('#modules-close' if modal=='modulesDialog' else '#channels-close')
                cbox=close.bounding_box()
                assert cbox and cbox['width']>=43.5 and cbox['height']>=43.5,('X de modal pequeno',role,theme,width,modal,cbox)
                page.keyboard.press('Escape');loc.wait_for(state='hidden')
            assert not errors,('Erro de script',role,theme,width,errors)
            result.append({'perfil':role,'tema':theme,'largura':width,'aviso_contraste':ratio,'botao_perigo_contraste':contrast(color['bg'],color['text']),'confirmacao_seguranca':True,'modal_funcoes':True,'modal_canais':role in ('Atendente','Administrador'),'erros_js':0})
            page.close()
    browser.close()
print(json.dumps(result,ensure_ascii=False,indent=2))
