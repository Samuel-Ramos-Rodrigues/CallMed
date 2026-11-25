package com.callmed.app
import androidx.activity.ComponentActivity
import androidx.compose.ui.test.*
import androidx.compose.ui.test.junit4.createAndroidComposeRule
import androidx.compose.ui.unit.dp
import org.json.JSONObject
import org.junit.Assert.*
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith
import org.robolectric.RobolectricTestRunner
import org.robolectric.annotation.Config
import org.robolectric.annotation.GraphicsMode
import org.robolectric.annotation.LooperMode
@RunWith(RobolectricTestRunner::class)
@Config(sdk=[35],qualifiers="w320dp-h640dp-mdpi")
@GraphicsMode(GraphicsMode.Mode.NATIVE)
@LooperMode(LooperMode.Mode.PAUSED)
class ClinicalUiTest {
 @get:Rule val ui=createAndroidComposeRule<ComponentActivity>()
 private val a=Appointment(1,1,"Psicóloga fictícia","Psicologia","2026-11-11","09:00","Confirmada","Particular","","NaoAplicavel","",true,false,true)
 private val s=UiState(restoring=false,authenticated=true,journeyAppointment=a,screen=Screen.VIDEO)
 @Test fun waitingRoomDoesNotStartMediaAndJoinIsDisabled(){val requests=java.util.concurrent.CopyOnWriteArrayList<ApiRequest>();val api=CallMedApi(CallMedApiTest.MemoryStore(Session("test","refresh",Long.MAX_VALUE)),ApiTransport{r->requests.add(r);ApiResponse(200,"""{"estado":"aguardando-horario","liberada":false,"podeConsentir":false,"abertura":"2026-11-11T08:30:00"}""".toByteArray())},"https://callmed.test")
  ui.setContent{CallMedTheme(ReadingPreferences(scale=1.5f,contrast=true)){NativeTeleScreen(s,api){}}}
  ui.waitUntil(10000){ui.onAllNodesWithText("Entrar na chamada").fetchSemanticsNodes().isNotEmpty()};ui.onNodeWithText("Entrar na chamada").performScrollTo().assertIsNotEnabled().assertHeightIsAtLeast(48.dp)
  assertTrue(requests.all{it.method=="GET"&&it.url.endsWith("clinico/1/tele")});assertTrue(requests.isNotEmpty())
 }
 @Test fun privacyRequestNeedsExplicitConfirmation(){val requests=java.util.concurrent.CopyOnWriteArrayList<ApiRequest>();val api=CallMedApi(CallMedApiTest.MemoryStore(Session("test","refresh",Long.MAX_VALUE)),ApiTransport{r->requests.add(r);ApiResponse(200,if(r.method=="GET")"[]".toByteArray() else "{}".toByteArray())},"https://callmed.test")
  ui.setContent{CallMedTheme(ReadingPreferences()){PrivacyScreen(api){}}};ui.waitUntil(10000){requests.isNotEmpty()};ui.waitForIdle();ui.onNodeWithText("Registrar pedido").performScrollTo().assertIsNotEnabled();assertTrue(requests.none{it.method=="POST"})
 }
 @Test fun unreadableLocalKeyKeepsTheRestoreButtonAvailable(){
  val api=CallMedApi(CallMedApiTest.MemoryStore(Session("test","refresh",Long.MAX_VALUE)),ApiTransport{ApiResponse(200,"""{"usuarioId":"ficticio","editar":true,"revogadas":[],"chaves":[],"formulario":null}""".toByteArray())},"https://callmed.test")
  val scope=kotlinx.coroutines.CoroutineScope(kotlinx.coroutines.SupervisorJob()+kotlinx.coroutines.Dispatchers.Main.immediate)
  val c=ClinicalPreparation(1,api,scope,{object:ClinicalKeyStore{override fun load():JSONObject?=throw java.io.IOException("cofre fictício inválido");override fun save(value:JSONObject){error("Não deve substituir a chave")}}},{})
  try{ui.setContent{CallMedTheme(ReadingPreferences()){ClinicalScreen(s,api,c){}}};ui.waitUntil(10000){ui.onAllNodesWithText("Restaurar cópia da chave").fetchSemanticsNodes().isNotEmpty()};ui.onNodeWithText("Senha da cópia protegida").performScrollTo().performTextInput("Senha-ficticia-2026!");ui.onNodeWithText("Restaurar cópia da chave").performScrollTo().assertIsEnabled().assertHeightIsAtLeast(48.dp);ui.onNodeWithText("Criar chave e salvar cópia").assertDoesNotExist()}finally{c.close();scope.coroutineContext[kotlinx.coroutines.Job]?.cancel()}
 }
 @Test fun clinicalPrivacyNotifiesTheAccountWhenTheTokenIsRevoked(){
  val events=java.util.concurrent.CopyOnWriteArrayList<AppAction>();val api=CallMedApi(CallMedApiTest.MemoryStore(Session("test","refresh",Long.MAX_VALUE)),ApiTransport{ApiResponse(401,"{}".toByteArray())},"https://callmed.test")
  ui.setContent{CallMedTheme(ReadingPreferences()){PrivacyScreen(api){events+=it}}};ui.waitUntil(10000){events.contains(AppAction.CheckSession)};assertFalse(api.hasSession())
 }

}
