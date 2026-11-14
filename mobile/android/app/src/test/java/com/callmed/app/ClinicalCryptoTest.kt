package com.callmed.app
import org.json.JSONArray
import org.json.JSONObject
import org.junit.Assert.*
import org.junit.Test
import java.io.File

class ClinicalCryptoTest {
 private fun vector()=JSONObject(javaClass.getResource("/clinical-interop.json")!!.readText())
 private fun fails(block:()->Unit){var failed=false;try{block()}catch(_:Exception){failed=true};assertTrue("Deveria recusar o conteúdo",failed)}
 @Test fun restoresWebCryptoBackupAndDecryptsBrowserAnswers(){val v=vector();val restored=ClinicalCrypto.restore(v.getJSONObject("backup").toString(),v.getString("password").toCharArray(),v.getString("user")).put("id",v.getString("keyId"));val plain=ClinicalCrypto.decrypt(v.getJSONObject("envelope").toString(),v.getString("aad"),restored);assertEquals(v.getJSONObject("values").getString("q0"),plain.getString("q0"))}
 @Test fun rejectsWrongPassword(){val v=vector();fails{ClinicalCrypto.restore(v.getJSONObject("backup").toString(),"Senha-errada-2026!".toCharArray(),v.getString("user"))}}
 @Test fun rejectsBackupFromAnotherAccount(){val v=vector();fails{ClinicalCrypto.restore(v.getJSONObject("backup").toString(),v.getString("password").toCharArray(),"outra-conta")}}
 @Test fun rejectsDifferentConsultationOrForm(){val v=vector();val key=v.getJSONObject("key").put("id",v.getString("keyId"));fails{ClinicalCrypto.decrypt(v.getJSONObject("envelope").toString(),"8:2",key)};fails{ClinicalCrypto.decrypt(v.getJSONObject("envelope").toString(),"7:3",key)}}
 @Test fun rejectsContentTampering(){val v=vector();val e=v.getJSONObject("envelope");val raw=ClinicalCrypto.un64(e.getString("ciphertext"));raw[0]=(raw[0].toInt() xor 1).toByte();e.put("ciphertext",ClinicalCrypto.b64(raw));fails{ClinicalCrypto.decrypt(e.toString(),v.getString("aad"),v.getJSONObject("key").put("id",v.getString("keyId")))}}
 @Test fun requiresOriginalRecipientKey(){val v=vector();fails{ClinicalCrypto.decrypt(v.getJSONObject("envelope").toString(),v.getString("aad"),v.getJSONObject("key").put("id","outra-chave"))}}
 @Test fun rejectsOversizedPlaintext(){val v=vector();fails{ClinicalCrypto.encrypt(JSONObject().put("q0","x".repeat(65000)),"7:2",JSONArray().put(JSONObject().put("id",v.getString("keyId")).put("publica",v.getJSONObject("key").getJSONObject("publicJwk").toString())))}}
 @Test fun producesEnvelopeAndBackupForIndependentWebCryptoVerification(){val v=vector();val k=v.getJSONObject("key").put("id",v.getString("keyId"));val recipients=JSONArray().put(JSONObject().put("id",v.getString("keyId")).put("publica",k.getJSONObject("publicJwk").toString()));val values=JSONObject().put("q0","Enviado pelo Android: acentuação 🟣.");val envelope=ClinicalCrypto.encrypt(values,v.getString("aad"),recipients);val backup=ClinicalCrypto.backup(k,v.getString("password").toCharArray());assertFalse(envelope.contains("acentuação"));val f=File("build/test-results/clinical-jvm-interop.json");f.parentFile?.mkdirs();f.writeText(JSONObject().put("envelope",JSONObject(envelope)).put("backup",JSONObject(backup)).put("expected",values).toString())}
 @Test fun generatedPairRestoresAndReadsItsOwnEnvelope(){val key=ClinicalCrypto.generate("ficticio").put("id","test-key");val recipients=JSONArray().put(JSONObject().put("id","test-key").put("publica",key.getJSONObject("publicJwk").toString()));val cipher=ClinicalCrypto.encrypt(JSONObject().put("q0","novo"),"7:9",recipients);assertEquals("novo",ClinicalCrypto.decrypt(cipher,"7:9",key).getString("q0"))}
}
