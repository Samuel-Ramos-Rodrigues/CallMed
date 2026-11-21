package com.callmed.app

import android.os.Looper
import androidx.lifecycle.ViewModelStore
import androidx.lifecycle.ViewModelProvider
import org.junit.Assert.*
import org.junit.Test
import org.junit.runner.RunWith
import org.robolectric.RobolectricTestRunner
import org.robolectric.RuntimeEnvironment
import org.robolectric.Shadows.shadowOf
import org.robolectric.annotation.Config
import org.robolectric.annotation.LooperMode
import java.util.concurrent.CountDownLatch
import java.util.concurrent.TimeUnit
import java.util.concurrent.atomic.AtomicBoolean
import java.util.concurrent.atomic.AtomicInteger

@RunWith(RobolectricTestRunner::class)
@Config(sdk = [35])
@LooperMode(LooperMode.Mode.PAUSED)
class ChatRefreshTest {
    private fun await(condition: () -> Boolean) {
        val until = System.nanoTime() + TimeUnit.SECONDS.toNanos(8)
        while (!condition() && System.nanoTime() < until) {
            shadowOf(Looper.getMainLooper()).idle()
            Thread.sleep(5)
        }
        assertTrue("A operação assíncrona não terminou", condition())
    }
    private class ControlledHistory : ApiTransport {
        val slow = AtomicBoolean()
        val started = CountDownLatch(1)
        val release = CountDownLatch(1)
        val reads = AtomicInteger()
        @Volatile var closed = true
        override fun execute(request: ApiRequest): ApiResponse {
            val body = when {
                request.url.endsWith("/perfil") -> """{"contaId":"chat-race-test","nome":"Paciente de teste","tema":"dark"}"""
                request.url.endsWith("/consultas") -> "[]"
                request.url.endsWith("/atendimento/mensagens") -> { closed = false; "{}" }
                request.url.endsWith("/atendimento/historico") -> {
                    reads.incrementAndGet()
                    val snapshot = if (closed) """{"historicoVersao":2,"cicloId":"fechamento-teste","haAnteriores":true,"encerrada":true,"mensagens":[]}"""
                        else """{"historicoVersao":2,"cicloId":"fechamento-teste","haAnteriores":true,"encerrada":false,"mensagens":[{"id":2,"autor":"paciente","texto":"oi"},{"id":3,"autor":"assistente","texto":"Olá! Como posso ajudar?"}]}"""
                    if (slow.getAndSet(false)) {
                        started.countDown()
                        check(release.await(8, TimeUnit.SECONDS))
                    }
                    snapshot
                }
                else -> error("Requisição inesperada: ${request.url}")
            }
            return ApiResponse(200, body.toByteArray())
        }
    }
    private fun fixture(transport: ControlledHistory): Pair<CallMedViewModel, ViewModelStore> {
        val app = RuntimeEnvironment.getApplication()
        val store = CallMedApiTest.MemoryStore(Session("test-access", "test-refresh", Long.MAX_VALUE))
        val api = CallMedApi(store, transport, "https://callmed.test")
        val models = ViewModelStore()
        val vm = ViewModelProvider(models, object : ViewModelProvider.Factory {
            @Suppress("UNCHECKED_CAST") override fun <T : androidx.lifecycle.ViewModel> create(modelClass: Class<T>): T =
                CallMedViewModel(app, api, ReadingStore(app)) as T
        })[CallMedViewModel::class.java]
        await { vm.state.value.authenticated && !vm.state.value.busy }
        vm.handle(AppAction.Navigate(Tab.CHAT))
        await { vm.state.value.screen == Screen.CHAT && !vm.state.value.busy && vm.state.value.chat.hasPrevious }
        return vm to models
    }
    @Test fun oldPollCannotRestoreClosedStatusAfterNewMessage() {
        val transport = ControlledHistory()
        val (vm, models) = fixture(transport)
        try {
            assertTrue(vm.state.value.chat.encerrada)
            transport.slow.set(true)
            val oldPoll = vm.pollChat()!!
            await { transport.started.count == 0L }
            vm.handle(AppAction.ChatDraft("oi"))
            vm.handle(AppAction.Send)
            await { !vm.state.value.busy && vm.state.value.chat.messages.lastOrNull()?.id == 3L }
            assertFalse(vm.state.value.chat.encerrada)
            transport.release.countDown()
            await { oldPoll.isCompleted }
            assertFalse("Um histórico antigo não pode encerrar de novo a conversa", vm.state.value.chat.encerrada)
            assertEquals(3L, vm.state.value.chat.messages.last().id)
            assertEquals("", vm.state.value.chatDraft)
        } finally { transport.release.countDown(); models.clear() }
    }
    @Test fun slowHistoryDoesNotStartOverlappingPolls() {
        val transport = ControlledHistory()
        val (vm, models) = fixture(transport)
        try {
            transport.slow.set(true)
            val poll = vm.pollChat()!!
            await { transport.started.count == 0L }
            val count = transport.reads.get()
            assertNull("Uma atualização lenta já está em andamento", vm.pollChat())
            assertEquals(count, transport.reads.get())
            transport.release.countDown()
            await { poll.isCompleted }
            val next = vm.pollChat()!!
            await { next.isCompleted }
            assertEquals(count + 1, transport.reads.get())
        } finally { transport.release.countDown(); models.clear() }
    }
}
