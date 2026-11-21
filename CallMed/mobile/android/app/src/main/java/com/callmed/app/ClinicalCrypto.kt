package com.callmed.app

import android.content.Context
import android.security.keystore.KeyGenParameterSpec
import android.security.keystore.KeyProperties
import org.json.JSONArray
import org.json.JSONObject
import java.math.BigInteger
import java.security.KeyFactory
import java.security.KeyPairGenerator
import java.security.KeyStore
import java.security.MessageDigest
import java.security.SecureRandom
import java.security.interfaces.RSAPrivateCrtKey
import java.security.interfaces.RSAPublicKey
import java.security.spec.MGF1ParameterSpec
import java.security.spec.RSAPrivateCrtKeySpec
import java.security.spec.RSAPublicKeySpec
import java.util.Base64
import javax.crypto.Cipher
import javax.crypto.KeyGenerator
import javax.crypto.SecretKeyFactory
import javax.crypto.spec.GCMParameterSpec
import javax.crypto.spec.OAEPParameterSpec
import javax.crypto.spec.PBEKeySpec
import javax.crypto.spec.PSource
import javax.crypto.spec.SecretKeySpec

// Mesmo formato de envelope e backup do navegador. Nenhum texto clínico é persistido.
internal object ClinicalCrypto {
    private val random = SecureRandom()
    private val oaep = OAEPParameterSpec("SHA-256", "MGF1", MGF1ParameterSpec.SHA256, PSource.PSpecified.DEFAULT)
    fun b64(bytes: ByteArray): String = Base64.getEncoder().encodeToString(bytes)
    fun un64(s: String): ByteArray = Base64.getDecoder().decode(s)
    private fun url(n: BigInteger): String = Base64.getUrlEncoder().withoutPadding().encodeToString(n.toByteArray().dropWhile { it == 0.toByte() }.toByteArray())
    private fun number(j: JSONObject, n: String) = BigInteger(1, Base64.getUrlDecoder().decode(j.getString(n)))
    fun generate(user: String): JSONObject {
        val pair = KeyPairGenerator.getInstance("RSA").apply { initialize(3072) }.generateKeyPair()
        val p = pair.public as RSAPublicKey; val k = pair.private as RSAPrivateCrtKey
        val public = JSONObject().put("kty","RSA").put("n",url(p.modulus)).put("e",url(p.publicExponent))
            .put("alg","RSA-OAEP-256").put("ext",true).put("key_ops",JSONArray().put("wrapKey"))
        val private = JSONObject(public.toString()).put("key_ops",JSONArray().put("unwrapKey"))
            .put("d",url(k.privateExponent)).put("p",url(k.primeP)).put("q",url(k.primeQ))
            .put("dp",url(k.primeExponentP)).put("dq",url(k.primeExponentQ)).put("qi",url(k.crtCoefficient))
        return JSONObject().put("usuarioId",user).put("publicJwk",public).put("privateJwk",private)
    }
    private fun aes(mode: Int, key: ByteArray, iv: ByteArray, input: ByteArray, aad: String? = null): ByteArray =
        Cipher.getInstance("AES/GCM/NoPadding").run { init(mode,SecretKeySpec(key,"AES"),GCMParameterSpec(128,iv)); if(aad!=null)updateAAD(aad.toByteArray(Charsets.UTF_8)); doFinal(input) }
    private fun derive(password: CharArray,salt: ByteArray): ByteArray {
        require(password.size>=12){"Use uma senha de pelo menos 12 caracteres para a cópia."}
        val spec=PBEKeySpec(password,salt,310000,256)
        return try { SecretKeyFactory.getInstance("PBKDF2WithHmacSHA256").generateSecret(spec).encoded } finally { spec.clearPassword() }
    }
    fun backup(key: JSONObject,password: CharArray): String {
        val salt=ByteArray(16).also(random::nextBytes);val iv=ByteArray(12).also(random::nextBytes);val derived=derive(password,salt)
        return try { JSONObject().put("formato","CallMedClinicalBackup").put("versao",1).put("kdf","PBKDF2-SHA256-310000")
            .put("salt",b64(salt)).put("iv",b64(iv)).put("cipher",b64(aes(Cipher.ENCRYPT_MODE,derived,iv,key.toString().toByteArray()))).toString() } finally { derived.fill(0) }
    }
    fun restore(backup: String,password: CharArray,user: String): JSONObject {
        require(backup.toByteArray().size<=20000){"A cópia deve ter até 20 KB."}
        val b=JSONObject(backup);require(b.getString("formato")=="CallMedClinicalBackup"&&b.getInt("versao")==1&&b.getString("kdf")=="PBKDF2-SHA256-310000")
        val salt=un64(b.getString("salt"));val iv=un64(b.getString("iv"));require(salt.size==16&&iv.size==12)
        val derived=derive(password,salt)
        val plain=try{aes(Cipher.DECRYPT_MODE,derived,iv,un64(b.getString("cipher")))}finally{derived.fill(0)}
        return try { JSONObject(String(plain,Charsets.UTF_8)).also { v ->
            require(v.getString("usuarioId")==user){"Esta cópia pertence a outra conta."}
            val pub=v.getJSONObject("publicJwk");val priv=v.getJSONObject("privateJwk")
            require(pub.getString("n")==priv.getString("n")&&pub.getString("e")=="AQAB"&&priv.getString("e")=="AQAB")
            // Valida o par com OAEP, incluindo expoentes privados, antes de instalar.
            val probe=ByteArray(32).also(random::nextBytes);require(unwrap(wrap(probe,pub),priv).contentEquals(probe))
        }} finally{plain.fill(0)}
    }
    private fun wrap(raw: ByteArray,j: JSONObject): ByteArray {
        val key=KeyFactory.getInstance("RSA").generatePublic(RSAPublicKeySpec(number(j,"n"),number(j,"e"))) as RSAPublicKey
        require(key.modulus.bitLength()==3072)
        return Cipher.getInstance("RSA/ECB/OAEPPadding").run { init(Cipher.ENCRYPT_MODE,key,oaep);doFinal(raw) }
    }
    private fun unwrap(bytes: ByteArray,j: JSONObject): ByteArray {
        val key=KeyFactory.getInstance("RSA").generatePrivate(RSAPrivateCrtKeySpec(number(j,"n"),number(j,"e"),number(j,"d"),number(j,"p"),number(j,"q"),number(j,"dp"),number(j,"dq"),number(j,"qi")))
        return Cipher.getInstance("RSA/ECB/OAEPPadding").run { init(Cipher.DECRYPT_MODE,key,oaep);doFinal(bytes) }
    }
    fun encrypt(values: JSONObject,aad: String,recipients: JSONArray): String {
        val plain=values.toString().toByteArray(Charsets.UTF_8);require(plain.size<=64000){"Reduza o tamanho das respostas antes de enviar."}
        val raw=ByteArray(32).also(random::nextBytes);val iv=ByteArray(12).also(random::nextBytes)
        return try { val wrapped=JSONArray();recipients.objects().forEach { r -> wrapped.put(JSONObject().put("id",r.getString("id")).put("key",b64(wrap(raw,JSONObject(r.getString("publica")))))) }
            JSONObject().put("versao",1).put("iv",b64(iv)).put("ciphertext",b64(aes(Cipher.ENCRYPT_MODE,raw,iv,plain,aad))).put("destinatarios",wrapped).toString()
        }finally{raw.fill(0);plain.fill(0)}
    }
    fun decrypt(envelope: String,aad: String,key: JSONObject): JSONObject {
        val e=JSONObject(envelope);require(e.getInt("versao")==1)
        val r=e.getJSONArray("destinatarios").objects().firstOrNull { it.getString("id")==key.getString("id") }
            ?:throw IllegalArgumentException("Restaure a cópia da chave usada nesta resposta.")
        val raw=unwrap(un64(r.getString("key")),key.getJSONObject("privateJwk"))
        val plain=try{aes(Cipher.DECRYPT_MODE,raw,un64(e.getString("iv")),un64(e.getString("ciphertext")),aad)}finally{raw.fill(0)}
        return try{JSONObject(String(plain,Charsets.UTF_8))}finally{plain.fill(0)}
    }
}

// O Android Keystore protege a chave AES que sela o JWK privado em armazenamento interno.
// A cópia portátil só sai do aparelho protegida pela senha escolhida pelo titular.
internal class ClinicalVault(private val context: Context, user: String) : ClinicalKeyStore {
    private val name="callmed.clinical."+MessageDigest.getInstance("SHA-256").digest(user.toByteArray()).joinToString(""){"%02x".format(it)}
    private val file=java.io.File(context.noBackupFilesDir,name)
    private fun key(): java.security.Key {
        val store=KeyStore.getInstance("AndroidKeyStore").apply{load(null)}
        if(!store.containsAlias(name))KeyGenerator.getInstance(KeyProperties.KEY_ALGORITHM_AES,"AndroidKeyStore").apply{
            init(KeyGenParameterSpec.Builder(name,KeyProperties.PURPOSE_ENCRYPT or KeyProperties.PURPOSE_DECRYPT)
                .setBlockModes(KeyProperties.BLOCK_MODE_GCM).setEncryptionPaddings(KeyProperties.ENCRYPTION_PADDING_NONE).setKeySize(256).build());generateKey()
        }
        return store.getKey(name,null)
    }
    override fun save(value: JSONObject) {
        val c=Cipher.getInstance("AES/GCM/NoPadding");c.init(Cipher.ENCRYPT_MODE,key())
        val plain=value.toString().toByteArray();val out=try{c.iv+c.doFinal(plain)}finally{plain.fill(0)}
        val temp=java.io.File(file.path+".tmp");temp.writeBytes(out);check(temp.renameTo(file)){"Não foi possível guardar a chave neste aparelho."}
    }
    override fun load(): JSONObject? {
        if(!file.exists())return null
        val bytes=file.readBytes();require(bytes.size in 29..20000)
        val c=Cipher.getInstance("AES/GCM/NoPadding");c.init(Cipher.DECRYPT_MODE,key(),GCMParameterSpec(128,bytes.copyOfRange(0,12)))
        val plain=c.doFinal(bytes.copyOfRange(12,bytes.size))
        return try{JSONObject(String(plain,Charsets.UTF_8))}finally{plain.fill(0)}
    }
}
