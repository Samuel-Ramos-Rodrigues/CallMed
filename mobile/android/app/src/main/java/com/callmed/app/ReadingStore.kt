package com.callmed.app

import android.content.Context
import org.json.JSONObject

/** Apenas preferências de apresentação; não armazena senha, conversa ou dados clínicos. */
class ReadingStore(context: Context) {
    private val prefs = context.getSharedPreferences("callmed-reading-v1", Context.MODE_PRIVATE)
    data class Record(val value: ReadingPreferences, val pending: Boolean)
    fun global(): ReadingPreferences = readKey("appearance")?.value ?: ReadingPreferences()
    fun read(account: String): Record? = readKey("account:$account")
    private fun readKey(key: String): Record? = runCatching {
        prefs.getString(key, null)?.let { val j = JSONObject(it); Record(ReadingPreferences.from(j), j.optBoolean("pending")) }
    }.getOrNull()
    fun save(account: String?, value: ReadingPreferences, pending: Boolean) {
        val text = value.json().put("pending", pending).toString()
        prefs.edit().putString("appearance", text).apply()
        if (!account.isNullOrBlank()) prefs.edit().putString("account:$account", text).apply()
    }
}
