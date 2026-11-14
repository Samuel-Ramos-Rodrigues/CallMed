#!/usr/bin/env python3
"""Inventário de UX estática das views Razor (complementa auditar-views-callmed.py).

Executa em todos os arquivos Razor, inclusive Identity. Não supõe que cada
partial seja uma página independente e não tenta interpretar a execução Razor.
As ocorrências potencialmente legítimas são avisos, não falhas de compilação.
"""
from __future__ import annotations
from collections import Counter
from pathlib import Path
import json
import os
import re

ROOT = Path(__file__).resolve().parents[1]
WEB = ROOT / "src" / "CallMed.Web"
PAGES = sorted([*(WEB / "Views").rglob("*.cshtml"), *(WEB / "Areas").rglob("*.cshtml")])
TAG = re.compile(r"<(?!/)([a-z][\w:-]*)\b[^>]*>", re.I | re.S)
ATTR = re.compile(r"""([\w:-]+)\s*=\s*(?:"([^"]*)"|'([^']*)')""", re.I | re.S)
SENSITIVE = re.compile(r"(?<![\w-])on(?:click|change|submit|input|focus|blur|load|keydown|keyup)\s*=", re.I)
SUMMARY = Counter()
FINDINGS = []
PAGES_BY_AREA = Counter()


def report(file: Path, content: str, start: int, rule: str, message: str, severity: str = "review"):
    FINDINGS.append({
        "path": str(file.relative_to(ROOT)).replace("\\", "/"),
        "line": content.count("\n", 0, start) + 1,
        "rule": rule, "severity": severity, "message": message,
    })
    SUMMARY[rule] += 1


for file in PAGES:
    source = file.read_text(encoding="utf-8-sig")
    relative = file.relative_to(WEB).parts
    area = "Identity" if relative[0] == "Areas" else relative[1]
    PAGES_BY_AREA[area] += 1
    SUMMARY["razor_files"] += 1
    if file.name.startswith("_"):
        SUMMARY["partial_or_layout"] += 1
    else:
        SUMMARY["non_partial"] += 1
    id_locations = {}
    label_fors = set(re.findall(r'<label\b[^>]*\bfor\s*=\s*["\']([\w:-]+)["\']', source, re.I))
    for m in TAG.finditer(source):
        tag = m.group(1).lower()
        markup = m.group(0)
        attrs = {a.group(1).lower(): a.group(2) if a.group(2) is not None else a.group(3)
                 for a in ATTR.finditer(markup)}
        SUMMARY["html_tags"] += 1
        if tag == "form":
            SUMMARY["forms"] += 1
        if tag == "table":
            SUMMARY["tables"] += 1
        if tag == "button":
            SUMMARY["buttons"] += 1
        if tag == "img":
            SUMMARY["images"] += 1
            if "alt" not in attrs:
                report(file, source, m.start(), "image_without_alt", "Imagem sem atributo alt.")
        if "style" in attrs:
            value = attrs["style"].strip().lower()
            dynamic = value.startswith("--day-count:") or value.startswith("width:@")
            report(file, source, m.start(),
                   "data_driven_style" if dynamic else "inline_style",
                   "Valor calculado pelo servidor; conferir semântica e acessibilidade." if dynamic
                   else "Estilo fixo inline; mover para CSS.",
                   "info" if dynamic else "review")
        if SENSITIVE.search(markup):
            report(file, source, m.start(), "inline_handler", "Handler JS inline; preferir addEventListener.")
        if tag == "a":
            href = attrs.get("href", "")
            if href.lower().strip().startswith("javascript:"):
                report(file, source, m.start(), "javascript_href", "Link javascript: deve ser removido.", "error")
            elif href.strip() == "#":
                report(file, source, m.start(), "empty_href", "Link placeholder # sem destino.")
            if attrs.get("target") == "_blank" and not any(
                x in attrs.get("rel", "").split() for x in ("noopener", "noreferrer")
            ):
                report(file, source, m.start(), "target_blank_rel", "Nova aba sem rel=noopener.")
        if tag == "input" and attrs.get("type", "").lower() == "search":
            SUMMARY["search_inputs"] += 1
            ident = attrs.get("id", "")
            prefix = source[:m.start()].lower()
            in_label = prefix.rfind("<label") > prefix.rfind("</label>")
            if "aria-label" not in attrs and "aria-labelledby" not in attrs and ident not in label_fors and not in_label:
                report(file, source, m.start(), "search_name_review", "Conferir nome acessível da busca.")
        ident = attrs.get("id")
        # IDs Razor interpolados/gerados por loops não são IDs estáticos confiáveis.
        if ident and "@" not in ident:
            id_locations.setdefault(ident, []).append(m.start())

    for ident, locations in id_locations.items():
        if len(locations) > 1:
            for pos in locations[1:]:
                conditional = bool(re.search(r"@(?:if|switch|else)\b", source))
                report(file, source, pos, "conditional_id_review" if conditional else "duplicate_static_id",
                       f'ID "{ident}" reaparece em código Razor condicional; verificar no DOM.' if conditional
                       else f'ID estático "{ident}" aparece mais de uma vez na mesma view.',
                       "info" if conditional else "review")

SUMMARY["findings"] = len(FINDINGS)
SUMMARY["errors"] = sum(item["severity"] == "error" for item in FINDINGS)
SUMMARY["reviews"] = sum(item["severity"] == "review" for item in FINDINGS)
SUMMARY["informational"] = sum(item["severity"] == "info" for item in FINDINGS)
result = {
    "summary": dict(SUMMARY),
    "areas": dict(sorted(PAGES_BY_AREA.items())),
    "findings": sorted(FINDINGS, key=lambda item: (item["path"], item["line"], item["rule"])),
}
path = ROOT / "artifacts" / "ui-page-audit.json"
path.parent.mkdir(parents=True, exist_ok=True)
path.write_text(json.dumps(result, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
print("Auditoria UX estática das views Razor")
print(f"Arquivos: {SUMMARY['razor_files']} | Partials/layouts: {SUMMARY['partial_or_layout']}")
print(f"Formulários: {SUMMARY['forms']} | Tabelas: {SUMMARY['tables']} | Imagens: {SUMMARY['images']}")
print(f"Achados: {SUMMARY['findings']} | Revisão: {SUMMARY['reviews']} | Informativos: {SUMMARY['informational']} | Críticos: {SUMMARY['errors']}")
for key, count in sorted(SUMMARY.items()):
    if key in ("razor_files", "partial_or_layout", "non_partial", "findings", "errors",
               "html_tags", "forms", "tables", "buttons", "images", "search_inputs", "reviews", "informational"):
        continue
    print(f"  {key}: {count}")
for issue in result["findings"][:50]:
    print(f"  {issue['severity'].upper()} {issue['path']}:{issue['line']} [{issue['rule']}] {issue['message']}")
if len(FINDINGS) > 50:
    print(f"  ... demais {len(FINDINGS)-50} ocorrências registradas no JSON.")
print(f"Relatório JSON: {path.relative_to(ROOT)}")
summary_file = os.environ.get("GITHUB_STEP_SUMMARY")
if summary_file:
    with open(summary_file, "a", encoding="utf-8") as out:
        out.write(f"\n### Auditoria UX Razor\n\n")
        out.write(f"{SUMMARY['razor_files']} arquivos; {SUMMARY['forms']} formulários; "
                  f"{SUMMARY['tables']} tabelas; {SUMMARY['findings']} avisos; "
                  f"{SUMMARY['errors']} achados críticos.\n\n")
        out.write("| Regra | Ocorrências |\n|---|---:|\n")
        for key in ("inline_style", "data_driven_style", "inline_handler", "image_without_alt",
                    "empty_href", "javascript_href", "target_blank_rel", "search_name_review",
                    "duplicate_static_id", "conditional_id_review"):
            out.write(f"| {key} | {SUMMARY[key]} |\n")
if SUMMARY["errors"]:
    raise SystemExit("Corrija links javascript: antes de integrar.")
