#!/usr/bin/env python3
"""Inventário estático reprodutível CallMed NEXT (não afirma homologação funcional)."""
from __future__ import annotations
from pathlib import Path
from collections import Counter
import json, hashlib, re

root = Path(__file__).resolve().parents[1]
web = root / "src/CallMed.Web"
app = root / "src/CallMed.Application"
views = sorted(web.rglob("*.cshtml"))
controllers = sorted((web / "Controllers").rglob("*Controller*.cs"))
services = sorted(app.rglob("*Service.cs"))
js_files = sorted((web / "wwwroot/js").rglob("*.js"))
css_files = sorted((web / "wwwroot/css").glob("*.css"))
kotlin_files = sorted((root / "mobile/android/app/src/main/java").rglob("*.kt"))
context=(root / "src/CallMed.Persistence/Data/MKSANContext.cs").read_text(encoding="utf-8")
css_paths = [x.relative_to(root).as_posix() for x in css_files]
view_areas=Counter(x.relative_to(web / "Views").parts[0] if (web / "Views") in x.parents else "Identity" for x in views)
api_controllers=[x for x in controllers if "/Api/" in x.as_posix() or "/Mobile/" in x.as_posix()]
source=(root / "src/CallMed.Web/App_Data/Aplicativo/release.json")
release=json.loads(source.read_text()) if source.exists() else {}
apk=root / "src/CallMed.Web/App_Data/Aplicativo/CallMed.apk"
hash_ok=apk.exists() and hashlib.sha256(apk.read_bytes()).hexdigest().lower()==str(release.get("sha256","")).lower()
size_ok=apk.exists() and apk.stat().st_size==release.get("tamanhoBytes")

modules={
 "Identidade/Acesso": ["Views/MinhaConta", "Views/Acessos", "Controllers/Conta"],
 "Agenda/Consultas": ["Views/Consulta", "Views/Disponibilidade", "Modules/Agenda/Agendamento/AgendamentoService.cs"],
 "Otimização/Fila": ["Views/ListaEspera", "Modules/Agenda/FilaDeEspera/ListaEsperaService.cs", "Modules/Agenda/FilaDeEspera/EncaixeService.cs"],
 "Paciente/Jornada": ["Views/Jornada", "Views/Checkin", "Views/Paciente"],
 "Atendimento/Multicanal": ["Views/Atendimento", "Modules/Atendimento/Orquestracao/AtendimentoOrquestradorService.cs"],
 "Automação/IA": ["Views/Atendimento", "Modules/Assistente/Conversa/AgenteClinicaService.cs"],
 "Teleconsulta": ["Views/Salas", "Views/Jornada/Tele.cshtml", "Modules/Salas"],
 "Gestão clínica/Psicologia": ["Views/Psicologia", "Views/Medico", "Views/Convenios"],
 "Integrações/PEP": ["Views/Integracoes", "Modules/IntegracaoPEP"],
 "Indicadores/Auditoria": ["Views/Relatorios", "Views/Auditoria", "Views/Operacao"],
 "Android Kotlin": ["mobile/android/app/src/main/java", "mobile/android/app/src/test"]
}
ignored = {'bin', 'obj', 'build', '.gradle', '.kotlin', '.idea', '.git', '__pycache__', 'node_modules', 'artifacts'}
allpaths=[x.relative_to(root).as_posix() for x in sorted(root.rglob('*')) if x.is_file() and not ignored.intersection(x.relative_to(root).parts)]
module_evidence={key:[p for p in allpaths if any(token in p for token in tokens)][:16] for key,tokens in modules.items()}
report={
 "schema_version":1, "base":"R9.3.15",
 "natureza":"Inventário estático, não homologação operacional",
 "counts":{"razor_views":len(views),"identity_razor_pages":sum("Areas/Identity" in x.as_posix() for x in views),"controllers_source_files":len(controllers),"service_files":len(services),"js_files":len(js_files),"css_files":len(css_files),"kotlin_files":len(kotlin_files),"dbset_declarations":len(re.findall(r"\bDbSet\s*<",context)),"has_index_declarations":sum(p.read_text(encoding="utf8",errors="ignore").count(".HasIndex(") for p in (root / "src/CallMed.Persistence").rglob("*.cs")),"web_api_controller_files":len(api_controllers)},
 "view_areas":dict(sorted(view_areas.items())),
 "modules":{k:{"state":"Presente no código; funcionamento em ambiente real não comprovado por esta auditoria","files":v} for k,v in module_evidence.items()},
 "android_release":{"version":release.get("versao"),"version_code":release.get("versionCode"),"apk_size_matches_metadata":size_ok,"apk_sha256_matches_metadata":hash_ok},
 "stylesheet_paths":css_paths,
 "sensitive_material":"Assinatura APK e configurações de produção não incluídas no inventário; verificar backup fora do repositório"
}
out=root/'artifacts/auditoria/INVENTARIO-ESTATICO.json'
out.parent.mkdir(parents=True,exist_ok=True)
out.write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(json.dumps(report['counts'],ensure_ascii=False))
print('APK integrity:',size_ok and hash_ok,'/ modules:',len(modules))
if not size_ok or not hash_ok:raise SystemExit('APK de distribuição não corresponde ao manifesto release.json')
