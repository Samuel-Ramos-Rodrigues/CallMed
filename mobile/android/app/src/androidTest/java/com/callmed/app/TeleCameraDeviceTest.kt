package com.callmed.app

import android.Manifest
import androidx.test.core.app.ActivityScenario
import androidx.test.ext.junit.runners.AndroidJUnit4
import androidx.test.platform.app.InstrumentationRegistry
import kotlinx.coroutines.*
import org.junit.Assert.*
import org.junit.Test
import org.junit.runner.RunWith
import org.webrtc.SurfaceViewRenderer

@RunWith(AndroidJUnit4::class)
class TeleCameraDeviceTest {
    @Test fun roomStartsCameraAndCanBeReopened() {
        val instrumentation = InstrumentationRegistry.getInstrumentation()
        val context = instrumentation.targetContext
        for (permission in listOf(Manifest.permission.CAMERA, Manifest.permission.RECORD_AUDIO)) {
            instrumentation.uiAutomation.grantRuntimePermission(context.packageName, permission)
        }
        val store = object : TokenStore {
            override fun read() = Session("local", "local", Long.MAX_VALUE)
            override fun save(session: Session) {}
            override fun clear() {}
        }
        val api = CallMedApi(store, ApiTransport { request ->
            val body = when {
                request.url.endsWith("/config") -> """{"iceServers":[]}"""
                request.url.endsWith("/presenca/entrar") -> """{"sessaoId":"teste"}"""
                request.url.contains("/sinais?") -> "[]"
                else -> "{}"
            }
            ApiResponse(200, body.toByteArray())
        }, "https://callmed.invalid")
        ActivityScenario.launch(MainActivity::class.java).use {
            runBlocking {
                withContext(Dispatchers.Main) {
                    val mediaScope=CoroutineScope(SupervisorJob()+Dispatchers.Main)
                    var active=false
                    var status=""
                    val tele=NativeTele(context,api,1,mediaScope,{status=it},{active=it})
                    try {
                        repeat(2) {
                            withTimeout(30_000) { tele.start() }
                            assertTrue(status,active)
                            val local=tele.renderer(true)
                            val remote=tele.renderer(false)
                            assertTrue(local is SurfaceViewRenderer)
                            assertTrue(remote is SurfaceViewRenderer)
                            tele.stop(false)
                            tele.releaseRenderer(local,true)
                            tele.releaseRenderer(remote,false)
                            assertFalse(active)
                        }
                    } finally { tele.release();mediaScope.cancel() }
                }
            }
        }
    }
}
