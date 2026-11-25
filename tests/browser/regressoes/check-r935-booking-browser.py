#!/usr/bin/env python3
"""Smoke do JavaScript real das cinco etapas de agendamento com APIs simuladas; sem chamadas externas."""
from pathlib import Path
from playwright.sync_api import sync_playwright
import re,shutil,json
root=Path(__file__).resolve().parents[3]
view=(root/'src/CallMed.Web/Views/Consulta/_Form.cshtml').read_text()
match=re.search(r'<script>\s*(\(function \(\) \{.*?\}\)\(\);)\s*</script>',view,re.S)
assert match,'Script inline de agendamento não encontrado'
js=match.group(1).replace("'@Model.Id'","'0'").replace("'@Model.MedicoId'","''")
assert '@Model' not in js, 'Razor dinâmico não substituído'
html='''<!doctype html><html lang="pt-BR"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"></head><body>
<form id="cm19BookingForm"><fieldset><legend>Modalidade</legend><label><input type="radio" name="modalidade" value="Presencial" checked required>Presencial</label><label><input type="radio" name="modalidade" value="Online" required>Online</label></fieldset>
<div class="cm-booking-progress"><ol class="cm19-booking-steps"><li data-booking-step="1"><b>1</b><span>Área de atuação</span></li><li data-booking-step="2"><b>2</b><span>Profissional</span></li><li data-booking-step="3"><b>3</b><span>Data</span></li><li data-booking-step="4"><b>4</b><span>Horário</span></li><li data-booking-step="5"><b>5</b><span>Confirmar</span></li></ol><p id="cm-booking-progress-status"></p></div>
<section class="cm19-booking-section" data-section="specialty"><select id="especialidadeSelect"><option value="">Selecione</option><option value="Cardiologia">Cardiologia</option><option value="Urologia">Urologia</option></select><div id="cm19SpecialtyGrid"><button type="button" data-specialty="Cardiologia">Cardiologia</button><button type="button" data-specialty="Urologia">Urologia</button></div></section>
<section class="cm19-booking-section" data-section="doctor"><select id="medicoSelect"><option value="">Selecione uma especialidade primeiro</option></select><div id="cm19DoctorChoices"></div><button type="button" id="primeiraVagaButton"><span id="primeiraVagaStatus"></span></button></section>
<section class="cm19-booking-section" data-section="date"><select id="dataSelect" data-atual=""><option value="">Selecione um profissional</option></select><div id="cm19DateChoices"></div></section>
<section class="cm19-booking-section" data-section="time"><select id="horarioSelect" data-atual=""><option value="">Selecione uma data</option></select><div id="cm19TimeChoices"></div></section>
<section class="cm19-booking-section compact-options"><textarea name="Observacao"></textarea></section><div class="cm19-booking-actions"><button type="submit" disabled>Confirmar agendamento</button></div></form></body></html>'''
mock='''window.__bookingCalls=[];window.fetch=async(url)=>{window.__bookingCalls.push(url);
 if(url.includes('MedicosPorEspecialidade'))return {ok:true,json:async()=>url.includes('Cardiologia')?[{id:1,nome:'Dr. QA Teste',texto:'Dr. QA Teste'}]:[]};
 if(url.includes('DatasDisponiveis'))return {ok:true,json:async()=>[{value:'2026-11-25',text:'25/11/2026'}]};
 if(url.includes('HorariosDisponiveis'))return {ok:true,json:async()=>['09:00','10:00']};
 if(url.includes('OpcoesPorEspecialidade'))return {ok:true,json:async()=>[{medicoId:1,data:'2026-11-25',horario:'09:00',medico:'Dr. QA Teste'}]};
 return {ok:false,json:async()=>({})};};'''
results=[]
with sync_playwright() as p:
 b=p.chromium.launch(executable_path=shutil.which('chromium'),args=['--no-sandbox'])
 for theme in ['dark','light']:
  page=b.new_page(viewport={'width':390,'height':780});errors=[];page.on('pageerror',lambda e:errors.append(str(e)))
  page.set_content(html,wait_until='domcontentloaded')
  page.add_script_tag(content=mock)
  page.add_script_tag(content=js)
  page.wait_for_timeout(100)
  states=[]
  def snap():
   states.append(page.eval_on_selector_all('[data-booking-step]',"es=>es.map(e=>({step:e.dataset.bookingStep,state:e.dataset.state,aria:e.getAttribute('aria-current')}))"))
  snap()
  assert states[-1][0]['state']=='current';assert page.locator('button[type=submit]').is_disabled()
  assert page.locator('[data-section=doctor]').is_hidden()
  page.locator('#especialidadeSelect').select_option('Cardiologia');page.wait_for_timeout(100);snap()
  assert states[-1][1]['state']=='current';assert page.locator('[data-section=doctor]').is_visible()
  assert page.locator('#medicoSelect option').count()==2
  page.locator('#medicoSelect').select_option('1');page.wait_for_timeout(120);snap()
  assert states[-1][2]['state']=='current';assert page.locator('[data-section=date]').is_visible()
  page.locator('#dataSelect').select_option('2026-11-25');page.wait_for_timeout(120);snap()
  assert states[-1][3]['state']=='current';assert page.locator('[data-section=time]').is_visible()
  page.locator('#horarioSelect').select_option('09:00');page.wait_for_timeout(100);snap()
  assert states[-1][4]['state']=='current';assert page.locator('button[type=submit]').is_enabled()
  assert 'Dr. QA Teste' in page.locator('#booking-summary').inner_text()
  page.locator('input[name=modalidade][value=Online]').check();page.wait_for_timeout(50)
  assert page.locator('button[type=submit]').is_enabled()
  assert 'Online' in page.locator('#booking-summary').inner_text()
  # Um novo filtro invalida escolhas posteriores e desativa a conclusão.
  page.locator('#especialidadeSelect').select_option('Urologia');page.wait_for_timeout(120);snap()
  assert page.locator('button[type=submit]').is_disabled()
  assert 'Nenhum profissional' in page.locator('#medicoSelect').inner_text()
  assert page.locator('[data-section=date]').is_hidden()
  assert not errors,errors
  results.append({'theme':theme,'steps':states,'fetch_calls':page.evaluate('window.__bookingCalls.length'),'errors':errors})
  page.close()
 b.close()
print(json.dumps({'result':'PASS','booking_flow':results},ensure_ascii=False,indent=2))
