from pathlib import Path
from playwright.sync_api import sync_playwright
import shutil, re, json, math
ROOT=Path(__file__).resolve().parents[3]/'src/CallMed.Web/wwwroot/css'
CSS=(ROOT/'callmed-clinic-r93.css').read_text()
DAYS=['Seg 23/11','Ter 24/11','Qua 25/11','Qui 26/11','Sex 27/11','Sáb 28/11','Dom 29/11']
slots=['<a class="cm-agenda-event cm19-cal-event busy"><strong>07:00</strong><span>Paciente teste</span><small>Confirmada</small></a>', '<a class="cm-agenda-event cm19-cal-event free"><strong>07:30</strong><span>Vaga livre</span><small>Disponível</small></a>', '<span class="cm-agenda-event cm19-cal-event blocked"><strong>08:00</strong><span>Bloqueado</span><small>Horário indisponível</small></span>']

def column(i):
  groups=[]
  if i in (0,2,4):
    for doctor in range(4):
      n=8 if doctor==0 and i==0 else (1 if doctor==1 else 4)
      groups.append('<section class="cm-agenda-week-doctor"><h3 class="cm-agenda-week-doctor-name"><span>Profissional nome completo</span><small>Cardiologia</small></h3><div class="cm-agenda-week-slots">'+''.join(slots[j%3] for j in range(n))+'</div></section>')
  else:
    groups.append('<p class="cm-agenda-week-empty">Nenhum horário disponível neste dia para os filtros selecionados.</p>')
  return f'<article class="cm-agenda-week-day {"today" if i==0 else ""}"><header class="cm-agenda-week-day-header"><span>{DAYS[i].split()[0]}</span><strong>{DAYS[i][4:]} 2026</strong>{"<em>Hoje</em>" if i==0 else ""}</header><div class="cm-agenda-week-day-body">'+''.join(groups)+'</div></article>'

BOARD='<section class="cm19-agenda-page r5-agenda-page cm25-screen"><div class="cm19-calendar cm19-card"><p class="cm-agenda-scroll-hint" id="cm-agenda-instrucoes">Navegue pelos dias deslizando a agenda para os lados. A lista de horários tem rolagem própria.</p><div class="cm-agenda-week-scroll" tabindex="0" role="region" aria-label="Agenda por dia e por profissional" aria-describedby="cm-agenda-instrucoes"><div class="cm-agenda-week-board" style="--day-count:7">'+''.join(column(i) for i in range(7))+'</div></div><footer class="cm19-calendar-legend">Disponível Consulta Bloqueado</footer></div></section>'

def luminance(rgb):
 arr=[n/255 for n in rgb];a=[v/12.92 if v<=0.04045 else ((v+.055)/1.055)**2.4 for v in arr];return .2126*a[0]+.7152*a[1]+.0722*a[2]
def contrast(fg,bg):
 def rgb(s):
  p=re.findall(r'[\d.]+',s);return tuple(int(float(x)) for x in p[:3])
 a,b=luminance(rgb(fg)),luminance(rgb(bg));return round((max(a,b)+.05)/(min(a,b)+.05),2)

(ROOT.parents[3]/'artifacts').mkdir(parents=True,exist_ok=True)
rows=[]
with sync_playwright() as pw:
 browser=pw.chromium.launch(headless=True,executable_path=shutil.which('chromium'),args=['--no-sandbox'])
 for theme in ('dark','light'):
  for hc in (False,True):
   for w in (390,820,1024,1440):
    p=browser.new_page(viewport={'width':w,'height':900},device_scale_factor=1)
    hcclass='callmed-high-contrast' if hc else ''
    html=f'<!doctype html><html lang="pt-BR" data-theme="{theme}" class="{hcclass}"><head><meta name="viewport" content="width=device-width,initial-scale=1"><style>html,body{{margin:0}} body{{max-width:100%}}</style><style>{CSS}</style></head><body class="callmed-app cm-r3-shell cm-r4-shell cm-r5-shell cm-r6-shell cm-r7-shell cm-r8-shell cm9-shell staff-context"><main class="app-main">{BOARD}</main></body></html>'
    p.set_content(html,wait_until='domcontentloaded')
    data=p.evaluate('''() => {
      const sc=document.querySelector('.cm-agenda-week-scroll'),b=document.querySelector('.cm-agenda-week-board'),c=[...document.querySelectorAll('.cm-agenda-week-day')],get=(s)=>{const e=document.querySelector(s);const a=getComputedStyle(e),r=e.getBoundingClientRect();return{fg:a.color,bg:a.backgroundColor,width:r.width,height:r.height,left:r.left,right:r.right,fontSize:a.fontSize}};
      return{vw:innerWidth,docwidth:document.documentElement.scrollWidth,boxWidth:sc.clientWidth,boxScroll:sc.scrollWidth,boxHeight:sc.clientHeight,boxHeightScroll:sc.scrollHeight,columns:c.length,columnWidths:c.map(x=>Math.round(x.getBoundingClientRect().width)),doctorPerDay:c.map(x=>x.querySelectorAll('.cm-agenda-week-doctor').length), styles:Object.fromEntries(['.cm-agenda-week-day-header','.cm-agenda-week-doctor','.cm-agenda-week-doctor-name span','.cm-agenda-week-doctor-name small','.cm-agenda-event.free','.cm-agenda-event.free span','.cm-agenda-event.free small','.cm-agenda-event.busy','.cm-agenda-event.busy small','.cm-agenda-event.blocked','.cm-agenda-event.blocked small','.cm-agenda-scroll-hint'].map(s=>[s,get(s)]))};
    }''')
    assert data['columns']==7,(theme,w,'missing days')
    assert data['boxScroll']>data['boxWidth'],(theme,w,'no horizontal scroll',data['boxWidth'],data['boxScroll'])
    assert data['docwidth']<=w+2,(theme,w,'document overflows horizontally',data['docwidth'])
    assert data['boxHeightScroll']>data['boxHeight'],(theme,w,'no internal vertical scroll',data['boxHeight'],data['boxHeightScroll'])
    assert data['doctorPerDay']==[4,0,4,0,4,0,0],(theme,w,data['doctorPerDay'])
    st=data['styles']
    for a,b in [('.cm-agenda-week-doctor','.cm-agenda-week-doctor-name span'),('.cm-agenda-week-doctor','.cm-agenda-week-doctor-name small'),('.cm-agenda-event.free','.cm-agenda-event.free span'),('.cm-agenda-event.free','.cm-agenda-event.free small'),('.cm-agenda-event.busy','.cm-agenda-event.busy small'),('.cm-agenda-event.blocked','.cm-agenda-event.blocked small')]:
      val=contrast(st[b]['fg'],st[a]['bg']); assert val>=4.5,(theme,hc,w,a,b,val,st[b]['fg'],st[a]['bg'])
    if hc:
      assert contrast(st['.cm-agenda-event.free']['fg'],st['.cm-agenda-event.free']['bg'])>=15,(theme,w,'hc free failed')
      assert contrast(st['.cm-agenda-event.blocked']['fg'],st['.cm-agenda-event.blocked']['bg'])>=15,(theme,w,'hc blocked failed')
    if theme=='dark' and not hc and w in (1440,390):p.screenshot(path=str(ROOT.parents[3]/'artifacts'/f'Agenda-R9311-escuro-{w}.png'))
    rows.append({'theme':theme,'highContrast':hc,'width':w,'scrollX':data['boxScroll']>data['boxWidth'],'scrollY':data['boxHeightScroll']>data['boxHeight'],'contrasts':{a:contrast(st[b]['fg'],st[a]['bg']) for a,b in [('.cm-agenda-week-doctor','.cm-agenda-week-doctor-name span'),('.cm-agenda-event.free','.cm-agenda-event.free span'),('.cm-agenda-event.busy','.cm-agenda-event.busy small'),('.cm-agenda-event.blocked','.cm-agenda-event.blocked small')]}})
    print('PASSOU',theme,'HC' if hc else 'normal',w,rows[-1]['contrasts'])
    p.close()
 browser.close()
(ROOT.parents[3]/'artifacts'/'r9311-agenda-test-results.json').write_text(json.dumps(rows,indent=2,ensure_ascii=False))
