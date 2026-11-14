// HTML real do teste HTTP com CSS/JS reais. O download de interface usa o APK nativo incluído.
// CALLMED_APK_FIXTURES=<fixtures> NODE_PATH=<node_modules> CHROME_PATH=<chromium> node tests/aplicativo-browser.cjs
const {chromium}=require('playwright');
const http=require('node:http'),fs=require('node:fs'),path=require('node:path'),assert=require('node:assert/strict'),crypto=require('node:crypto');
const root=path.resolve(__dirname,'../src/CallMed.Web/wwwroot'),apk=path.resolve(__dirname,'../src/CallMed.Web/App_Data/Aplicativo/CallMed.apk'),fixtures=process.env.CALLMED_APK_FIXTURES;
assert(fixtures,'Informe CALLMED_APK_FIXTURES.');
const server=http.createServer((req,res)=>{
 const url=new URL(req.url,'http://localhost');let file;
 if(url.pathname==='/aplicativo/baixar'){res.setHeader('Content-Type','application/vnd.android.package-archive');res.setHeader('Content-Disposition','attachment; filename="CallMed.apk"');return fs.createReadStream(apk).pipe(res);}
 if(url.pathname==='/service-worker.js'){res.writeHead(404);return res.end();}
 if(url.pathname==='/aplicativo')file=path.join(fixtures,url.searchParams.has('indisponivel')?'indisponivel.html':'aplicativo.html');
 else if(url.pathname==='/')file=path.join(fixtures,'home.html');
 else file=path.join(root,url.pathname);
 if((!file.startsWith(root)&&!file.startsWith(fixtures))||!fs.existsSync(file)||fs.statSync(file).isDirectory()){res.writeHead(404);return res.end();}
 res.setHeader('Content-Type',{'.html':'text/html; charset=utf-8','.js':'text/javascript','.css':'text/css','.svg':'image/svg+xml','.png':'image/png'}[path.extname(file)]||'application/octet-stream');fs.createReadStream(file).pipe(res);
});
(async()=>{
 await new Promise(r=>server.listen(0,'127.0.0.1',r));const origin=`http://127.0.0.1:${server.address().port}`;
 const browser=await chromium.launch({headless:true,executablePath:process.env.CHROME_PATH,args:['--no-sandbox','--disable-dev-shm-usage']});
 const page=await browser.newPage({viewport:{width:1280,height:900}}),errors=[];page.on('pageerror',e=>errors.push(e.message));
 await page.route('**/*',r=>r.request().url().startsWith(origin)?r.continue():r.abort());
 const check=(v,m)=>{assert(v,m);console.log('PASS '+m);};
 try{
  await page.goto(origin+'/');const link=page.locator('.public-install a');await link.click();
  check(new URL(page.url()).pathname==='/aplicativo'&&await page.getByRole('heading',{name:'Aplicativo CallMed',exact:true}).isVisible(),'Página inicial abre a página pública do aplicativo');
  check((await page.locator('.cm-android-details').innerText()).includes('23.2.0')&&await page.getByRole('heading',{name:'Como instalar'}).isVisible(),'Página informa a versão do APK e orienta instalação');
  const downloadPromise=page.waitForEvent('download');await page.locator('.cm-android-download').click();const download=await downloadPromise;
  check(download.suggestedFilename()==='CallMed.apk'&&crypto.createHash('sha256').update(fs.readFileSync(await download.path())).digest('hex')==='ec71f93714b1862cb143a8c14da14a4a0c472f960f772ed109787006af149a1f','Botão inicia download do APK correto em vez de abrir instalação do PWA');
  await page.locator('.cm-android-checksum summary').click();check(await page.locator('.cm-android-checksum code').isVisible(),'Integridade pode ser consultada sem ocupar o fluxo principal');
  for(const width of [320,390,1280]){
   await page.setViewportSize({width,height:900});
   for(const theme of ['light','dark']){
    await page.evaluate(t=>window.CallMedTheme.set(t),theme);
    check(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth+1),`Sem rolagem horizontal em ${width}px, tema ${theme}`);
   }
  }
  if(process.env.CALLMED_APK_SCREENSHOTS){await page.waitForTimeout(700);fs.mkdirSync(process.env.CALLMED_APK_SCREENSHOTS,{recursive:true});await page.screenshot({path:path.join(process.env.CALLMED_APK_SCREENSHOTS,'aplicativo-desktop.png'),fullPage:true});await page.setViewportSize({width:390,height:844});await page.screenshot({path:path.join(process.env.CALLMED_APK_SCREENSHOTS,'aplicativo-celular.png'),fullPage:true});}
  await page.goto(origin+'/aplicativo?indisponivel');check(await page.locator('.cm-android-download').count()===0&&await page.locator('[role=status]').isVisible(),'Pacote indisponível apresenta orientação sem oferecer link quebrado');
  check(errors.length===0,'Interface de download sem erros de JavaScript');
 }finally{await browser.close();server.close();}
})().catch(e=>{console.error(e);server.close();process.exitCode=1;});
