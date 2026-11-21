/* FHIR R4 DocumentReference + Binary: intercâmbio explícito no dispositivo.
 * Não lê URLs externas, não executa HTML e não publica importações sem revisão. */
((global)=>{'use strict';
 const encode=s=>{const bytes=new TextEncoder().encode(s);let out='';for(const b of bytes)out+=String.fromCharCode(b);return btoa(out);};
 const decode=s=>new TextDecoder('utf-8',{fatal:true}).decode(Uint8Array.from(atob(s),c=>c.charCodeAt(0)));
 function exportBundle({namespace,pacienteId,paciente,psicologoId,psicologo,crp,records}){
  if(!/^https:\/\/|^urn:/.test(namespace)||!pacienteId||!psicologoId||!Array.isArray(records)||records.length<1||records.length>200||records.some(r=>r.escopo!=='Prontuario'))throw Error('Selecione apenas registros do prontuário acessíveis nesta sessão.');
  const patient='urn:uuid:'+crypto.randomUUID(),professional='urn:uuid:'+crypto.randomUUID();
  const entry=[{fullUrl:patient,resource:{resourceType:'Patient',identifier:[{system:namespace+':Patient',value:String(pacienteId)}],name:[{text:paciente}]}},
   {fullUrl:professional,resource:{resourceType:'Practitioner',identifier:[{system:namespace+':Practitioner',value:String(psicologoId)},{system:'urn:callmed:crp',value:crp}],name:[{text:psicologo}]}}];
  for(const r of records){
   if(!r.id||!r.text?.texto||typeof r.text.texto!=='string'||r.text.texto.length>20000)throw Error('Registro inválido.');
   const doc='urn:uuid:'+r.id,binary='urn:uuid:'+crypto.randomUUID();
   entry.push({fullUrl:doc,resource:{resourceType:'DocumentReference',identifier:[{system:namespace+':DocumentReference',value:r.id}],status:records.some(x=>x.retificaId===r.id)?'superseded':'current',docStatus:r.retificaId?'amended':'final',type:{text:'Registro psicológico · '+r.categoria},subject:{reference:patient},date:r.criadoEm,author:[{reference:professional}],securityLabel:[{coding:[{system:'http://terminology.hl7.org/CodeSystem/v3-Confidentiality',code:'R',display:'restricted'}]}],...(r.retificaId?{relatesTo:[{code:'replaces',target:{reference:'urn:uuid:'+r.retificaId}}]}:{}),content:[{attachment:{contentType:'text/plain; charset=utf-8',url:binary,title:'Prontuário psicológico · '+r.categoria}}]}});
   entry.push({fullUrl:binary,resource:{resourceType:'Binary',contentType:'text/plain; charset=utf-8',securityContext:{reference:doc},data:encode(r.text.texto)}});
  }
  return {resourceType:'Bundle',type:'collection',timestamp:new Date().toISOString(),entry};
 }
 function preview(text){
  if(typeof text!=='string'||new TextEncoder().encode(text).length>1048576)throw Error('Use um arquivo FHIR de até 1 MB.');
  const bundle=JSON.parse(text);if(bundle.resourceType!=='Bundle'||!['collection','transaction','document'].includes(bundle.type)||!Array.isArray(bundle.entry)||bundle.entry.length>500)throw Error('Use um Bundle FHIR R4 com documentos e conteúdo incluído no próprio arquivo.');
  const entries=bundle.entry;const urls=entries.map(e=>e.fullUrl).filter(Boolean);if(new Set(urls).size!==urls.length)throw Error('Referências duplicadas no arquivo.');
  const resolve=ref=>entries.find(e=>e.fullUrl===ref||`${e.resource?.resourceType}/${e.resource?.id}`===ref)?.resource;
  const docs=entries.filter(e=>e.resource?.resourceType==='DocumentReference').map(({resource:r})=>{
   if(r.status!=='current')return null;
   const patient=resolve(r.subject?.reference);if(patient?.resourceType!=='Patient')throw Error('O documento precisa identificar um paciente incluído no arquivo.');
   if(!Array.isArray(r.content)||r.content.length!==1)throw Error('Selecione um documento com um único conteúdo de texto.');
   const a=r.content[0].attachment;let payload=a?.data,type=a?.contentType;
   if(!payload&&a?.url){const b=resolve(a.url);if(b?.resourceType!=='Binary')throw Error('Inclua o Binary no arquivo. Links externos não são abertos.');payload=b.data;type=b.contentType;}
   if(!payload||!/^text\/plain(?:;\s*charset=utf-8)?$/i.test(type||''))throw Error('A importação aceita somente texto simples UTF-8.');
   const value=decode(payload);if(!value.trim()||value.length>15000)throw Error('O documento deve conter até 15.000 caracteres de texto.');
   return {identifier:r.identifier||[],title:a.title||r.type?.text||'Documento externo',patient:patient.name?.[0]?.text||'Nome não informado',patientIdentifiers:patient.identifier||[],author:(r.author||[]).map(x=>resolve(x.reference)?.name?.[0]?.text||x.display||'Autoria não verificada').join('; '),date:r.date||'',texto:value};
  }).filter(Boolean);
  if(!docs.length||docs.length>100)throw Error('O arquivo precisa conter de 1 a 100 documentos atuais.');return docs;
 }
 global.CallMedFhir={exportBundle,preview};
})(typeof window==='undefined'?globalThis:window);
