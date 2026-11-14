package com.callmed.app
import android.os.Looper
import androidx.lifecycle.*
import org.junit.Assert.*
import org.junit.Test
import org.junit.runner.RunWith
import org.robolectric.RobolectricTestRunner
import org.robolectric.RuntimeEnvironment
import org.robolectric.Shadows.shadowOf
import org.robolectric.annotation.Config
import org.robolectric.annotation.LooperMode
import java.util.concurrent.TimeUnit
@RunWith(RobolectricTestRunner::class)
@Config(sdk=[35])
@LooperMode(LooperMode.Mode.PAUSED)
class ClinicalSessionTest {
 private fun await(condition:()->Boolean){val end=System.nanoTime()+TimeUnit.SECONDS.toNanos(8);while(!condition()&&System.nanoTime()<end){shadowOf(Looper.getMainLooper()).idle();Thread.sleep(5)};assertTrue(condition())}
 private fun fixture():Triple<CallMedViewModel,CallMedApi,ViewModelStore>{
  val app=RuntimeEnvironment.getApplication();val api=CallMedApi(CallMedApiTest.MemoryStore(Session("test","refresh",Long.MAX_VALUE)),ApiTransport{r->ApiResponse(200,when{r.url.endsWith("/perfil")->"""{"contaId":"ficticio-session","nome":"Paciente fictício","tema":"dark"}""";r.url.endsWith("/consultas")->"[]";else->"{}"}.toByteArray())},"https://callmed.test")
  val store=ViewModelStore();val vm=ViewModelProvider(store,object:ViewModelProvider.Factory{@Suppress("UNCHECKED_CAST")override fun <T:ViewModel> create(modelClass:Class<T>)=CallMedViewModel(app,api,ReadingStore(app)) as T})[CallMedViewModel::class.java];await{vm.state.value.authenticated&&!vm.state.value.busy};return Triple(vm,api,store)
 }
 @Test fun expiredClinicalSessionReturnsToLoginAndDiscardsItsController(){val(vm,api,store)=fixture();try{val controller=vm.clinicalPreparation(7);val reading=vm.state.value.reading;api.forget();vm.handle(AppAction.CheckSession);assertFalse(vm.state.value.authenticated);assertNull(vm.state.value.profile);assertTrue(controller.isClosed);assertEquals(reading,vm.state.value.reading)}finally{store.clear()}}
 @Test fun staleErrorDoesNotCloseAnExistingNewLogin(){val(vm,api,store)=fixture();try{val controller=vm.clinicalPreparation(7);assertTrue(api.hasSession());vm.handle(AppAction.CheckSession);assertTrue(vm.state.value.authenticated);assertFalse(controller.isClosed)}finally{store.clear()}}
 @Test fun pendingPreparationHasTheSameOwnerAcrossActivityRecreation(){val(vm,_,store)=fixture();try{val controller=vm.clinicalPreparation(7);controller.pause();assertSame(controller,vm.clinicalPreparation(7));assertNotSame(controller,vm.clinicalPreparation(8));assertTrue(controller.isClosed)}finally{store.clear()}}
}
