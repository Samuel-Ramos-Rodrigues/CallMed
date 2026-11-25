package com.callmed.app

import android.Manifest
import android.app.Application
import android.widget.TextView
import kotlinx.coroutines.runBlocking
import org.junit.Assert.*
import org.junit.Test
import org.junit.runner.RunWith
import org.robolectric.RobolectricTestRunner
import org.robolectric.RuntimeEnvironment
import org.robolectric.Shadows
import org.robolectric.annotation.Config
import org.robolectric.annotation.LooperMode

@RunWith(RobolectricTestRunner::class)
@Config(sdk=[35])
@LooperMode(LooperMode.Mode.PAUSED)
class NativeTeleStartupTest {
    private class Audio:CallAudio {
        var starts=0
        override fun start(interrupted:(String)->Unit){starts++}
        override fun stop(){}
    }
    private fun api()=CallMedApi(CallMedApiTest.MemoryStore(Session("test","refresh",Long.MAX_VALUE)),
        ApiTransport{ApiResponse(200,"{}".toByteArray())},"https://callmed.test")

    @Test fun nativeLibraryFailureIsContainedAndCanBeRetried()=runBlocking {
        val context:Application=RuntimeEnvironment.getApplication()
        Shadows.shadowOf(context).grantPermissions(Manifest.permission.CAMERA,Manifest.permission.RECORD_AUDIO)
        val statuses=mutableListOf<String>();val active=mutableListOf<Boolean>();val preparing=mutableListOf<Boolean>();val audio=Audio();var attempts=0
        val tele=NativeTele(context,api(),1,this,{statuses+=it},{active+=it},callAudio=audio,
            initializeRuntime={attempts++;throw UnsatisfiedLinkError("biblioteca fictícia indisponível")},preparing={preparing+=it})
        try {
            tele.start()
            assertFalse(tele.joining)
            assertFalse("A tela não pode renderizar vídeo durante falha de inicialização",active.any{it})
            assertEquals(listOf(true,false),preparing)
            assertTrue(statuses.last().contains("VIDEO-INIT"))
            assertEquals(0,audio.starts)
            tele.start()
            assertEquals(2,attempts)
            assertFalse(tele.joining)
            assertFalse(preparing.last())
        } finally {tele.release()}
    }

    @Test fun revokedPermissionDoesNotInitializeNativeMedia()=runBlocking {
        val context:Application=RuntimeEnvironment.getApplication()
        Shadows.shadowOf(context).denyPermissions(Manifest.permission.CAMERA,Manifest.permission.RECORD_AUDIO)
        var initialized=false;val statuses=mutableListOf<String>();val active=mutableListOf<Boolean>()
        val tele=NativeTele(context,api(),1,this,{statuses+=it},{active+=it},callAudio=Audio(),initializeRuntime={initialized=true})
        try {
            tele.start()
            assertFalse(initialized)
            assertFalse(active.any{it})
            assertFalse(tele.joining)
            assertTrue(statuses.last().contains("Permita câmera e microfone"))
        } finally {tele.release()}
    }

    @Test fun rendererBeforeReadyDoesNotTouchEglOrNativeLibrary()=runBlocking {
        val context:Application=RuntimeEnvironment.getApplication()
        var initialized=false
        val tele=NativeTele(context,api(),1,this,{},{},callAudio=Audio(),initializeRuntime={initialized=true})
        try {
            assertTrue(tele.renderer(true) is TextView)
            assertTrue(tele.renderer(false) is TextView)
            assertFalse(initialized)
        } finally {tele.release()}
    }
}
