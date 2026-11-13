// Chaves exclusivamente fictícias de interoperabilidade. Nunca usar em produção.
const {webcrypto:c}=require('node:crypto'),fs=require('node:fs');
(async()=>{const e=new TextEncoder(),b64=b=>Buffer.from(b).toString('base64');
const pair=await c.subtle.generateKey({name:'RSA-OAEP',modulusLength:3072,publicExponent:new Uint8Array([1,0,1]),hash:'SHA-256'},true,['wrapKey','unwrapKey']);
const publicJwk=await c.subtle.exportKey('jwk',pair.publicKey),privateJwk=await c.subtle.exportKey('jwk',pair.privateKey),user='ficticio-clinical-interop',keyId='8c076807-0021-44d3-a0bf-a8ff9a314dd9';
const password='Senha-fictícia-2026!',key={usuarioId:user,publicJwk,privateJwk},salt=c.getRandomValues(new Uint8Array(16)),iv=c.getRandomValues(new Uint8Array(12));
const material=await c.subtle.importKey('raw',e.encode(password),'PBKDF2',false,['deriveKey']);const derived=await c.subtle.deriveKey({name:'PBKDF2',salt,iterations:310000,hash:'SHA-256'},material,{name:'AES-GCM',length:256},false,['encrypt']);
const backup={formato:'CallMedClinicalBackup',versao:1,kdf:'PBKDF2-SHA256-310000',salt:b64(salt),iv:b64(iv),cipher:b64(await c.subtle.encrypt({name:'AES-GCM',iv},derived,e.encode(JSON.stringify(key))))};
const aes=await c.subtle.generateKey({name:'AES-GCM',length:256},true,['encrypt','decrypt']),aad='7:2',values={q0:'Resposta fictícia em português 🟣.'},answerIv=c.getRandomValues(new Uint8Array(12));
const envelope={versao:1,iv:b64(answerIv),ciphertext:b64(await c.subtle.encrypt({name:'AES-GCM',iv:answerIv,additionalData:e.encode(aad)},aes,e.encode(JSON.stringify(values)))),destinatarios:[{id:keyId,key:b64(await c.subtle.wrapKey('raw',aes,pair.publicKey,{name:'RSA-OAEP'}))}]};
fs.writeFileSync('CallMedAndroid/app/src/test/resources/clinical-interop.json',JSON.stringify({password,user,keyId,aad,values,key,backup,envelope},null,2));console.log('Fixture WebCrypto criada com identidade fictícia.');})();
