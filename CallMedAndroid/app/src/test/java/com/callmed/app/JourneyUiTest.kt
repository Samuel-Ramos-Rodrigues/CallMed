package com.callmed.app
import androidx.activity.ComponentActivity
import androidx.compose.ui.test.*
import androidx.compose.ui.test.junit4.createAndroidComposeRule
import androidx.compose.ui.unit.dp
import org.junit.Assert.*
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith
import org.robolectric.RobolectricTestRunner
import org.robolectric.annotation.Config
import org.robolectric.annotation.GraphicsMode
import org.robolectric.annotation.LooperMode
import org.json.JSONObject
@RunWith(RobolectricTestRunner::class)
@Config(sdk=[35],qualifiers="w320dp-h640dp-mdpi")
@GraphicsMode(GraphicsMode.Mode.NATIVE)
@LooperMode(LooperMode.Mode.PAUSED)
class JourneyUiTest {
 @get:Rule val ui=createAndroidComposeRule<ComponentActivity>()
 private val patient=Patient.from(JSONObject("""{"contaId":"fixture","nome":"Paciente Fictício","telefone":"5579999990000","tema":"dark"}"""))
 private val appointment=Appointment(1,1,"Dra. Fictícia","Clínica geral","2026-11-11","09:00","Confirmada","Particular","","NaoAplicavel","",true,false,true)
 @Test fun preRegistrationNeedsExplicitConfirmation(){val events=mutableListOf<AppAction>();val s=UiState(restoring=false,authenticated=true,profile=patient,screen=Screen.JOURNEY,tab=Tab.CONSULTAS,journeyAppointment=appointment,journey=JSONObject("""{"etapa":"Preparação","cadastroConferido":false}"""));ui.setContent{CallMedTheme(ReadingPreferences()){JourneyScreen(s){events+=it}}};ui.onNodeWithText("Confirmar pré-cadastro").performScrollTo().assertIsNotEnabled();ui.onNode(isToggleable()).performScrollTo().performClick();ui.onNodeWithText("Confirmar pré-cadastro").performScrollTo().assertIsEnabled().performClick();assertEquals(AppAction.JourneyMove("cadastro"),events.single())}
 @Test fun checkInAndNativePreparationRemainSeparate(){val events=mutableListOf<AppAction>();val s=UiState(restoring=false,authenticated=true,profile=patient,screen=Screen.JOURNEY,journeyAppointment=appointment,journey=JSONObject("""{"etapa":"Check-in antecipado","cadastroConferido":true,"checkIn":true,"chegada":false,"teleconsulta":true}"""));ui.setContent{CallMedTheme(ReadingPreferences()){JourneyScreen(s){events+=it}}};ui.onNodeWithText("Check-in registrado.").performScrollTo().assertExists();ui.onNodeWithText("Preparar consulta").performScrollTo().assertHeightIsAtLeast(48.dp).performClick();assertEquals(AppAction.Open(Screen.CLINICAL),events.single());ui.onNodeWithText("Entrar na sala").performScrollTo().assertHeightIsAtLeast(48.dp).performClick();assertEquals(AppAction.Open(Screen.VIDEO),events.last())}
 @Test fun offerAcceptanceDispatchesOnlyAfterPatientAction(){val events=mutableListOf<AppAction>();val s=UiState(restoring=false,authenticated=true,profile=patient,screen=Screen.WAITLIST,waitOffers=listOf(JSONObject("""{"id":"oferta-teste","medico":"Dra. Fictícia","data":"2026-11-11","horario":"09:00"}""")));ui.setContent{CallMedTheme(ReadingPreferences()){WaitlistScreen(s){events+=it}}};assertTrue(events.isEmpty());ui.onNodeWithText("Aceitar e agendar").performScrollTo().performClick();assertEquals(AppAction.Offer("oferta-teste",true),events.single())}
}
