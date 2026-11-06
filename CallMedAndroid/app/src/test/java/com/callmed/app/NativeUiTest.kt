package com.callmed.app

import android.graphics.Bitmap
import android.graphics.Canvas
import androidx.activity.ComponentActivity
import androidx.compose.runtime.*
import androidx.compose.runtime.CompositionLocalProvider
import androidx.compose.ui.graphics.asAndroidBitmap
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.luminance
import androidx.compose.ui.platform.LocalDensity
import androidx.compose.ui.semantics.Role
import androidx.compose.ui.semantics.SemanticsProperties
import androidx.compose.ui.test.*
import androidx.compose.ui.test.junit4.createAndroidComposeRule
import androidx.compose.ui.unit.Density
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
import java.io.File

@RunWith(RobolectricTestRunner::class)
@Config(sdk = [35], qualifiers = "w320dp-h640dp-mdpi")
@GraphicsMode(GraphicsMode.Mode.NATIVE)
@LooperMode(LooperMode.Mode.PAUSED)
class NativeUiTest {
    @get:Rule val ui = createAndroidComposeRule<ComponentActivity>()
    private val patient = Patient.from(JSONObject("""{"contaId":"ui-fixture","nome":"Paciente UI","email":"paciente@example.test","cpf":"52998224725","telefone":"5579999990000","tema":"dark"}"""))
    private val doc = Doctor(1, "Dra. Exemplo de Teste", "Clínica geral", "CRM-TESTE")
    private val appointment = Appointment(1, 1, doc.nome, doc.especialidade, "2026-11-11", "09:00", "Confirmada", "Particular", "", "NaoAplicavel", "", true, false, true)
    private fun sample(screen: Screen = Screen.HOME) = UiState(restoring = false, authenticated = true, profile = patient, appointments = listOf(appointment), doctors = listOf(doc), screen = screen)
    private fun shot(name: String) {
        val dir = System.getenv("CALLMED_NATIVE_SCREENSHOTS") ?: return
        File(dir).mkdirs()
        ui.runOnIdle {
            val view = ui.activity.window.decorView
            val bitmap = Bitmap.createBitmap(view.width, view.height, Bitmap.Config.ARGB_8888)
            view.draw(Canvas(bitmap))
            File(dir, "$name.png").outputStream().use { bitmap.compress(Bitmap.CompressFormat.PNG, 100, it) }
            bitmap.recycle()
        }
    }
    @Test fun homeHasFourTabsAndBookingEntry() {
        ui.setContent { CallMedTheme(ReadingPreferences()) { CallMedApp(sample()) {} } }
        ui.onAllNodes(SemanticsMatcher.expectValue(SemanticsProperties.Role, Role.Tab)).assertCountEquals(4)
        ui.onNodeWithText("Agendar consulta").assertExists()
        ui.onNodeWithText("Próxima consulta").assertExists()
        ui.onNodeWithTag("reading").assertHeightIsAtLeast(48.dp)
        shot("home-native")
    }
    @Test fun bookingUsesSpecialtyDoctorDateTimeThenReview() {
        var state by mutableStateOf(sample(Screen.BOOKING))
        var submitted = false
        ui.setContent { CallMedTheme(state.reading) { CallMedApp(state) { event ->
            state = when(event) {
                is AppAction.Specialty -> state.copy(booking = state.booking.copy(specialty = event.name))
                AppAction.Next -> state.copy(booking = state.booking.next())
                is AppAction.SelectDoctor -> state.copy(booking = state.booking.copy(doctor = event.doctor, stage = 3, dates = listOf("2026-11-11")))
                is AppAction.Date -> state.copy(booking = state.booking.copy(date = event.value, hours = listOf("09:00")))
                is AppAction.Hour -> state.copy(booking = state.booking.copy(hour = event.value))
                AppAction.Book -> { submitted = true; state }
                else -> state
            }
        } } }
        ui.onNodeWithText("Qual especialidade?").assertExists()
        ui.onNodeWithTag("specialty-Clínica geral").performClick()
        ui.onNodeWithText("Continuar").performScrollTo().performClick()
        ui.onNodeWithTag("doctor-1").performScrollTo().performClick()
        ui.onNodeWithText("Qual dia fica melhor?").assertExists()
        ui.onNodeWithTag("date-2026-11-11").performScrollTo().performClick()
        ui.onNodeWithText("Continuar").performScrollTo().performClick()
        ui.onNodeWithTag("hour-09:00").performScrollTo().performClick()
        ui.onNodeWithText("Continuar").performScrollTo().performClick()
        ui.onNodeWithText("Confira antes de confirmar").assertExists()
        assertFalse(submitted)
        ui.onNodeWithText("Agendar e confirmar").performScrollTo().performClick()
        assertTrue(submitted)
    }
    @Test fun readingKeepsProfileDraftAndAllowsReturn() {
        var state by mutableStateOf(sample(Screen.EDIT_PROFILE).copy(profileDraft = ProfileDraft.from(patient)))
        ui.setContent { CallMedTheme(state.reading) { CallMedApp(state) { event -> state = when(event) {
            is AppAction.EditProfile -> state.copy(profileDraft = event.draft)
            is AppAction.Open -> if (event.screen == Screen.READING) state.copy(readingReturn = state.screen, screen = Screen.READING) else state
            is AppAction.Reading -> state.copy(reading = event.value)
            AppAction.FinishReading -> state.copy(screen = state.readingReturn)
            else -> state
        } } } }
        ui.onNodeWithTag("profile-name").performTextReplacement("Nome que ainda não foi salvo")
        ui.onNodeWithTag("reading").performClick()
        ui.onNodeWithTag("text-Grande").performScrollTo().performClick()
        ui.onNodeWithText("Usar estas preferências").performScrollTo().performClick()
        ui.onNodeWithTag("profile-name").assertTextContains("Nome que ainda não foi salvo")
        assertEquals(1.25f, state.reading.scale)
    }
    @Test fun systemFontScaleIsNeverReducedAndControlsRemainReadable() {
        var observed = 0f
        val reading = ReadingPreferences(scale = 1f, contrast = true, spacious = true)
        ui.setContent { CompositionLocalProvider(LocalDensity provides Density(1f, 2f)) {
            CallMedTheme(reading) {
                observed = LocalDensity.current.fontScale
                CallMedApp(sample(Screen.READING).copy(reading = reading)) {}
            }
        } }
        assertEquals(2f, observed)
        ui.onNodeWithText("Acessibilidade").assertExists()
        ui.onNodeWithText("Alto contraste").performScrollTo().assertIsDisplayed()
        ui.onNodeWithText("Usar estas preferências").performScrollTo().assertIsDisplayed()
        ui.onNodeWithTag("reading").assertHeightIsAtLeast(48.dp)
        shot("reading-system-font-200-native")
    }
    @Test fun readingSwitchExposesSwitchRoleAndCurrentState() {
        var state by mutableStateOf(sample(Screen.READING))
        ui.setContent { CallMedTheme(state.reading) { CallMedApp(state) { if (it is AppAction.Reading) state = state.copy(reading = it.value) } } }
        val contrast = ui.onNode(hasText("Alto contraste") and SemanticsMatcher.expectValue(SemanticsProperties.Role, Role.Switch))
        contrast.performScrollTo().assertIsOff().performClick().assertIsOn()
        assertTrue(state.reading.contrast)
    }
    @Test fun cancellationRequiresExplicitConfirmation() {
        val events = mutableListOf<AppAction>()
        ui.setContent { CallMedTheme(ReadingPreferences()) { CallMedApp(sample(Screen.CONSULTAS), events::add) } }
        ui.onNodeWithText("Cancelar consulta").performScrollTo().performClick()
        assertTrue(events.none { it is AppAction.AppointmentChange })
        ui.onNodeWithText("Manter minha consulta").performClick()
        assertTrue(events.none { it is AppAction.AppointmentChange })
        ui.onNodeWithText("Cancelar consulta").performScrollTo().performClick()
        ui.onNodeWithText("Sim, cancelar consulta").performClick()
        assertTrue(events.any { it is AppAction.AppointmentChange && it.action == "cancelar" })
    }
    @Test fun remappingStartsAtDateAndMaintainsDoctor() {
        val moving = BookingState(stage = 3, specialty = doc.especialidade, doctor = doc, dates = listOf("2026-11-11"), movingId = 1, payment = "Convenio")
        ui.setContent { CallMedTheme(ReadingPreferences()) { CallMedApp(sample(Screen.BOOKING).copy(booking = moving)) {} } }
        ui.onNodeWithText("Qual dia fica melhor?").assertExists()
        ui.onNodeWithText(doc.nome).assertExists()
        ui.onNodeWithText("O profissional e a forma de atendimento serão mantidos.").assertExists()
        assertNull(moving.previous())
    }
    @Test fun feedbackRequiresOptInAndNeverBlocksNewMessage() {
        var state by mutableStateOf(sample(Screen.CHAT).copy(chat = ChatState(encerrada = true, avaliacaoId = "survey-test")))
        ui.setContent { CallMedTheme(state.reading) { CallMedApp(state) { state = when(it) {
            AppAction.AcceptFeedback -> state.copy(feedbackAccepted = true)
            is AppAction.FeedbackNote -> state.copy(feedbackNote = it.value)
            is AppAction.ChatDraft -> state.copy(chatDraft = it.text)
            else -> state
        } } } }
        ui.onNodeWithText("Quer avaliar o atendimento?").assertExists()
        ui.onNodeWithText("Sim, quero avaliar").performScrollTo().performClick()
        ui.onNodeWithTag("rating-5").performScrollTo().performClick()
        assertEquals(5, state.feedbackNote)
        ui.onNodeWithTag("chat-draft").performTextInput("Quero continuar o atendimento")
        assertEquals("Quero continuar o atendimento", state.chatDraft)
    }
    @Test fun themeHasLegibleTextPairsInEveryAppearance() {
        fun ratio(a: Color, b: Color): Float { val x = a.luminance(); val y = b.luminance(); return (maxOf(x, y) + .05f) / (minOf(x, y) + .05f) }
        for (dark in listOf(true, false)) for (contrast in listOf(true, false)) {
            val c = callMedColors(dark, contrast)
            for ((text, background) in listOf(c.onSurface to c.surface, c.onBackground to c.background, c.onSurfaceVariant to c.surface,
                c.onPrimary to c.primary, c.onPrimaryContainer to c.primaryContainer, c.onSecondaryContainer to c.secondaryContainer, c.error to c.surface)) {
                assertTrue("Contraste insuficiente: dark=$dark contrast=$contrast ratio=${ratio(text, background)}", ratio(text, background) >= 4.5f)
            }
        }
    }
}
