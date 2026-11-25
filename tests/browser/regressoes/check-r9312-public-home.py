"""Regressão de contraste da homepage pública em estilos reais e HTML da view.

Necessita python playwright + Chromium local (sem servidor .NET).
Este teste não simula login nem substitui homologação do site publicado.
"""
from __future__ import annotations
from pathlib import Path
from playwright.sync_api import sync_playwright
import json, re, shutil

ROOT = Path(__file__).resolve().parents[3]
WEB = ROOT / 'src/CallMed.Web'
CSS = [WEB / 'wwwroot/css/callmed-clinic-r93.css']
view = (WEB/'Views/Home/Index.cshtml').read_text(encoding='utf-8')
start = '<section class="public-hero">'
assert start in view
hero = start + view.split(start, 1)[1].split('</section>', 1)[0] + '</section>'

def rgb(x):
    val = re.match(r'rgba?\(([^)]+)\)', x)
    assert val, x
    return [int(float(v.strip())) for v in val.group(1).split(',')[:3]]

def rel_lum(x):
    a = [(v/255/12.92 if v/255 <= 0.04045 else ((v/255+0.055)/1.055)**2.4) for v in rgb(x)]
    return 0.2126*a[0]+0.7152*a[1]+0.0722*a[2]

def contrast(a,b):
    x,y=rel_lum(a),rel_lum(b)
    return round((max(x,y)+0.05)/(min(x,y)+0.05), 2)

out = []
with sync_playwright() as p:
    browser = p.chromium.launch(headless=True, executable_path=shutil.which('chromium'), args=['--no-sandbox'])
    for theme in ('light', 'dark'):
        for contrast_mode in (False, True):
            for width in (320, 390, 820, 1024, 1440):
                root_class = ' class="callmed-high-contrast"' if contrast_mode else ''
                html = f'<!doctype html><html lang="pt-BR" data-theme="{theme}"{root_class}><head><meta name="viewport" content="width=device-width,initial-scale=1"></head><body class="public-body r4-public r5-public"><main id="conteudo">{hero}</main></body></html>'
                page = browser.new_page(viewport={'width':width,'height':850}, device_scale_factor=1)
                page.set_content(html)
                for css in CSS: page.add_style_tag(path=str(css))
                values = page.evaluate('''() => {
                    const css = selector => getComputedStyle(document.querySelector(selector));
                    const hero = css('.public-hero').backgroundColor;
                    const picks = {
                        title: ['.public-hero-copy h1', 3.0],
                        emphasis: ['.public-hero-copy h1 em', 3.0],
                        subtitle: ['.public-hero-copy > p', 4.5],
                        eyebrow: ['.public-hero-copy > .eyebrow', 4.5],
                        help: ['.public-channel-note', 4.5],
                        helpStrong: ['.public-channel-note strong', 4.5],
                        secondaryLink: ['.public-actions > a.text-link', 4.5],
                    };
                    const items = Object.fromEntries(Object.entries(picks).map(([key,[selector,threshold]]) =>
                       [key,{foreground:css(selector).color,background:hero,required:threshold}]));
                    const button=css('.public-actions > a.btn');
                    items.button={foreground:button.color,background:button.backgroundColor,required:4.5};
                    const card=document.querySelector('.public-hero').getBoundingClientRect();
                    const btn=document.querySelector('.public-actions > a.btn').getBoundingClientRect();
                    return {items,cardWidth:card.width,buttonWidth:btn.width,buttonHeight:btn.height,
                      horizontalOverflow:document.documentElement.scrollWidth > innerWidth+1};
                }''')
                ratios = {k: contrast(v['foreground'],v['background']) for k,v in values['items'].items()}
                low={k:ratios[k] for k,v in values['items'].items() if ratios[k] + 0.015 < v['required']}
                assert not low, (theme,contrast_mode,width,'low contrast',low)
                assert not values['horizontalOverflow'],(theme,contrast_mode,width,'scroll horizontal')
                assert values['buttonHeight'] >= 44,(theme,contrast_mode,width,'small CTA')
                assert values['buttonWidth'] <= values['cardWidth'],(theme,contrast_mode,width,'overflow CTA')
                if width in (390, 1440) and not contrast_mode:
                    folder=ROOT/'artifacts'
                    folder.mkdir(exist_ok=True)
                    page.screenshot(path=str(folder/f'r9312-home-{theme}-{width}.png'),full_page=True)
                out.append({'theme':theme,'highContrast':contrast_mode,'width':width,'ratios':ratios})
                page.close()
    browser.close()
print(json.dumps({'cases':len(out),'minRatio':min(min(row['ratios'].values()) for row in out),'passed':True,'examples':out[:2]},ensure_ascii=False))
(ROOT/'artifacts/r9312-home-contrast-test.json').write_text(json.dumps(out,ensure_ascii=False,indent=2))
