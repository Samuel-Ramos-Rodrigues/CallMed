package com.callmed.app
import kotlinx.coroutines.*
import org.junit.Assert.*
import org.junit.Test
import org.junit.runner.RunWith
import org.robolectric.RobolectricTestRunner
import org.robolectric.RuntimeEnvironment
import org.robolectric.annotation.Config
import org.robolectric.annotation.LooperMode
import java.io.IOException
import java.util.concurrent.CopyOnWriteArrayList
import java.util.concurrent.CountDownLatch
import java.util.concurrent.TimeUnit
import java.util.concurrent.atomic.AtomicInteger
@RunWith(RobolectricTestRunner::class)
@Config(sdk=[35])
@LooperMode(LooperMode.Mode.PAUSED)
class NativeTeleRaceTest {
 private class Audio:CallAudio{var starts=0;override fun start(interrupted:(String)->Unit){starts++};override fun stop(){}}
 @Test fun oldNetworkFailureCannotStopTheNextJoin()=runBlocking{
  val one=CountDownLatch(1);val two=CountDownLatch(1);val releaseOne=CountDownLatch(1);val releaseTwo=CountDownLatch(1);val calls=AtomicInteger();val status=CopyOnWriteArrayList<String>();val running=CopyOnWriteArrayList<Boolean>();val audio=Audio()
  val api=CallMedApi(CallMedApiTest.MemoryStore(Session("test","refresh",Long.MAX_VALUE)),ApiTransport{
   when(calls.incrementAndGet()){
    1->{one.countDown();check(releaseOne.await(8,TimeUnit.SECONDS));throw IOException("falha fictícia da tentativa anterior")}
    else->{two.countDown();check(releaseTwo.await(8,TimeUnit.SECONDS));ApiResponse(409,"{\"mensagem\":\"teste encerrado\"}".toByteArray())}
   }
  },"https://callmed.test")
  val tele=NativeTele(RuntimeEnvironment.getApplication(),api,7,this,{status+=it},{running+=it},{},audio)
  var first:Job?=null;var second:Job?=null
  try{
   first=launch{tele.start()};withContext(Dispatchers.IO){assertTrue(one.await(8,TimeUnit.SECONDS))}
   tele.stop(false);second=launch{tele.start()};withContext(Dispatchers.IO){assertTrue(two.await(8,TimeUnit.SECONDS))}
   releaseOne.countDown();first.join()
   assertTrue("A tentativa anterior não pode encerrar a atual",tele.joining);assertEquals(true,running.last());assertEquals("Preparando câmera e microfone…",status.last());assertEquals(0,audio.starts)
  }finally{tele.stop(false);releaseOne.countDown();releaseTwo.countDown();first?.join();second?.join();tele.release()}
 }
 @Test fun rapidDoubleTapProducesOnlyOneConfigurationRequest()=runBlocking{
  val started=CountDownLatch(1);val release=CountDownLatch(1);val calls=AtomicInteger();val api=CallMedApi(CallMedApiTest.MemoryStore(Session("test","refresh",Long.MAX_VALUE)),ApiTransport{calls.incrementAndGet();started.countDown();check(release.await(8,TimeUnit.SECONDS));ApiResponse(409,"{}".toByteArray())},"https://callmed.test")
  val tele=NativeTele(RuntimeEnvironment.getApplication(),api,7,this,{},{},{},Audio());var first:Job?=null
  try{first=launch{tele.start()};withContext(Dispatchers.IO){assertTrue(started.await(8,TimeUnit.SECONDS))};tele.start();assertEquals(1,calls.get())}finally{tele.stop(false);release.countDown();first?.join();tele.release()}
 }
}
