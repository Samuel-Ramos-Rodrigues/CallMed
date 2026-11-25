package com.callmed.app

import android.app.ActivityManager
import android.app.ApplicationExitInfo
import android.content.Context
import android.os.Build

// O sistema guarda somente esta marca de operação, sem consulta, paciente ou conteúdo da sala.
private val callMarker = "callmed:video:1".toByteArray(Charsets.US_ASCII)

internal class TeleExitReport(private val context: Context) {
    private var marked = false

    fun mark(inCall: Boolean) {
        if (Build.VERSION.SDK_INT < 30 || marked == inCall) return
        try {
            context.getSystemService(ActivityManager::class.java)
                ?.setProcessStateSummary(if (inCall) callMarker else null)
            marked = inCall
        } catch (_: RuntimeException) {
            // O sistema pode limitar a frequência desta operação. O diagnóstico é opcional.
        }
    }

    fun lastCode(): String? {
        if (Build.VERSION.SDK_INT < 30) return null
        return try {
            val manager = context.getSystemService(ActivityManager::class.java) ?: return null
            val last = manager.getHistoricalProcessExitReasons(context.packageName, 0, 1).firstOrNull()
                ?: return null
            if (last.processName != context.applicationInfo.processName) return null
            if (!callMarker.contentEquals(last.processStateSummary)) return null
            when (last.reason) {
                ApplicationExitInfo.REASON_CRASH_NATIVE -> "VIDEO-NATIVO"
                ApplicationExitInfo.REASON_CRASH -> "VIDEO-EXCECAO"
                ApplicationExitInfo.REASON_ANR -> "VIDEO-SEM-RESPOSTA"
                else -> null
            }
        } catch (_: RuntimeException) { null }
    }
}
