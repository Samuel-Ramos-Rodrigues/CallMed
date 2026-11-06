package com.callmed.app

import android.content.Context
import android.security.keystore.KeyGenParameterSpec
import android.security.keystore.KeyProperties
import android.util.Base64
import org.json.JSONObject
import java.security.KeyStore
import javax.crypto.Cipher
import javax.crypto.KeyGenerator
import javax.crypto.SecretKey
import javax.crypto.spec.GCMParameterSpec

data class Session(val accessToken: String, val refreshToken: String, val expiresAt: Long)
interface TokenStore { fun read(): Session?; fun save(session: Session); fun clear() }
class SecureTokenStore(context: Context) : TokenStore {
    private val preferences = context.getSharedPreferences("callmed-native-session", Context.MODE_PRIVATE)
    private val alias = "callmed.native.tokens.v1"
    private fun key(): SecretKey {
        val store = KeyStore.getInstance("AndroidKeyStore").apply { load(null) }
        (store.getKey(alias, null) as? SecretKey)?.let { return it }
        return KeyGenerator.getInstance(KeyProperties.KEY_ALGORITHM_AES, "AndroidKeyStore").apply {
            init(KeyGenParameterSpec.Builder(alias, KeyProperties.PURPOSE_ENCRYPT or KeyProperties.PURPOSE_DECRYPT)
                .setBlockModes(KeyProperties.BLOCK_MODE_GCM).setEncryptionPaddings(KeyProperties.ENCRYPTION_PADDING_NONE).build())
        }.generateKey()
    }
    @Synchronized override fun read(): Session? = try {
        val encoded = preferences.getString("encrypted", null)
        if (encoded == null) null else {
            val bytes = Base64.decode(encoded, Base64.NO_WRAP)
            val cipher = Cipher.getInstance("AES/GCM/NoPadding").apply { init(Cipher.DECRYPT_MODE, key(), GCMParameterSpec(128, bytes.copyOfRange(0, 12))) }
            val value = JSONObject(String(cipher.doFinal(bytes.copyOfRange(12, bytes.size)), Charsets.UTF_8))
            Session(value.getString("access"), value.getString("refresh"), value.getLong("expires"))
        }
    } catch (_: Exception) { clear(); null }
    @Synchronized override fun save(session: Session) {
        val value = JSONObject().put("access", session.accessToken).put("refresh", session.refreshToken).put("expires", session.expiresAt)
        val cipher = Cipher.getInstance("AES/GCM/NoPadding").apply { init(Cipher.ENCRYPT_MODE, key()) }
        val bytes = cipher.iv + cipher.doFinal(value.toString().toByteArray(Charsets.UTF_8))
        check(preferences.edit().putString("encrypted", Base64.encodeToString(bytes, Base64.NO_WRAP)).commit()) { "Não foi possível salvar o acesso com segurança." }
    }
    @Synchronized override fun clear() { preferences.edit().clear().commit() }
}
