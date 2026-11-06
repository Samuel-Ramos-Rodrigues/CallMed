package com.callmed.app

import org.json.JSONArray
import org.json.JSONObject
import java.time.LocalDate
import java.time.format.DateTimeFormatter
import java.util.Locale

fun JSONObject.text(key: String, fallback: String = ""): String = if (isNull(key)) fallback else optString(key, fallback)
fun JSONArray.objects(): List<JSONObject> = (0 until length()).map { getJSONObject(it) }
fun JSONArray.strings(): List<String> = (0 until length()).map { getString(it) }
private val brasil = Locale.forLanguageTag("pt-BR")
fun dateLabel(iso: String): String = runCatching { LocalDate.parse(iso.take(10)).format(DateTimeFormatter.ofPattern("dd/MM/yyyy", brasil)) }.getOrDefault(iso)
fun longDateLabel(iso: String): String = runCatching { LocalDate.parse(iso.take(10)).format(DateTimeFormatter.ofPattern("EEEE, dd 'de' MMMM", brasil)) }.getOrDefault(iso)
fun inputDate(iso: String): String = if (iso.isEmpty()) "" else dateLabel(iso)
fun isoDate(input: String): String? {
    if (input.isBlank()) return null
    return runCatching { LocalDate.parse(input.trim(), DateTimeFormatter.ofPattern("dd/MM/uuuu").withResolverStyle(java.time.format.ResolverStyle.STRICT)).toString() }
        .getOrElse { throw IllegalArgumentException("Use uma data válida no formato dia/mês/ano.") }
}

data class ReadingPreferences(
    val scale: Float = 1f, val contrast: Boolean = false, val spacious: Boolean = false,
    val reduceMotion: Boolean = false, val theme: String = "dark"
) {
    fun payload() = JSONObject().put("textoMaior", scale > 1f).put("altoContraste", contrast)
        .put("modoSimplificado", spacious).put("reduzirAnimacoes", reduceMotion).put("tema", theme)
    fun json() = payload().put("escala", scale.toDouble())
    companion object {
        fun from(j: JSONObject) = ReadingPreferences(j.optDouble("escala", if (j.optBoolean("textoMaior")) 1.25 else 1.0).toFloat().coerceIn(1f, 1.5f),
            j.optBoolean("altoContraste"), j.optBoolean("modoSimplificado"), j.optBoolean("reduzirAnimacoes"), j.text("tema", "dark"))
    }
}

data class Patient(
    val nome: String, val email: String, val telefone: String, val cpf: String, val nascimento: String,
    val temConvenio: Boolean, val nomeConvenio: String, val numeroConvenio: String, val validadeConvenio: String,
    val convenioValido: Boolean, val tema: String, val whatsApp: Boolean, val emailAvisos: Boolean, val sms: Boolean,
    val canal: String, val possuiFoto: Boolean, val contaId: String = "", val textoMaior: Boolean = false,
    val altoContraste: Boolean = false, val reduzirAnimacoes: Boolean = false, val modoSimplificado: Boolean = false
) {
    val primeiroNome get() = nome.trim().substringBefore(' ')
    val iniciais get() = nome.split(' ').filter(String::isNotBlank).take(2).joinToString("") { it.take(1).uppercase(brasil) }
    fun reading(localScale: Float = 1.25f) = ReadingPreferences(if (textoMaior) localScale.coerceIn(1.25f, 1.5f) else 1f, altoContraste, modoSimplificado, reduzirAnimacoes, tema)
    companion object {
        fun from(j: JSONObject) = Patient(j.text("nome"), j.text("email"), j.text("telefone"), j.text("cpf"), j.text("dataNascimento"),
            j.optBoolean("temConvenio"), j.text("nomeConvenio"), j.text("numeroConvenio"), j.text("validadeConvenio"), j.optBoolean("convenioValido"),
            j.text("tema", "dark"), j.optBoolean("avisosWhatsApp", true), j.optBoolean("avisosEmail", true), j.optBoolean("avisosSms", true),
            j.text("canalPreferido", "WhatsApp"), j.optBoolean("possuiFoto"), j.text("contaId"), j.optBoolean("textoMaior"), j.optBoolean("altoContraste"),
            j.optBoolean("reduzirAnimacoes"), j.optBoolean("modoSimplificado"))
    }
}
data class ProfileDraft(val name: String = "", val phone: String = "", val birth: String = "", val insured: Boolean = false,
    val insurer: String = "", val insuranceNumber: String = "", val expiry: String = "") {
    fun payload() = JSONObject().put("nome", name.trim()).put("telefone", phone.trim().ifBlank { null } ?: JSONObject.NULL)
        .put("dataNascimento", isoDate(birth) ?: JSONObject.NULL).put("temConvenio", insured).put("nomeConvenio", insurer.trim())
        .put("numeroConvenio", insuranceNumber.trim()).put("validadeConvenio", (if (insured) isoDate(expiry) else null) ?: JSONObject.NULL)
    companion object { fun from(p: Patient) = ProfileDraft(p.nome, p.telefone, inputDate(p.nascimento), p.temConvenio, p.nomeConvenio, p.numeroConvenio, inputDate(p.validadeConvenio)) }
}
data class Doctor(val id: Int, val nome: String, val especialidade: String, val crm: String) {
    companion object { fun from(j: JSONObject) = Doctor(j.getInt("id"), j.text("nome"), j.text("especialidade"), j.text("crm")) }
}
data class Eligibility(val eligible: Boolean = true, val valid: Boolean = false, val authorization: Boolean = false, val message: String = "") {
    companion object { fun from(j: JSONObject) = Eligibility(j.optBoolean("elegivel", true), j.optBoolean("possuiConvenioValido"), j.optBoolean("requerAutorizacao"), j.text("mensagem")) }
}
data class Appointment(val id: Int, val medicoId: Int, val medico: String, val especialidade: String, val data: String,
    val horario: String, val status: String, val pagamento: String, val convenio: String, val statusConvenio: String,
    val observacaoConvenio: String, val podeCancelar: Boolean, val podeConfirmar: Boolean, val podeRemarcar: Boolean,
    val dataFutura: Boolean = true) {
    val futura get() = dataFutura && (podeCancelar || podeConfirmar || podeRemarcar)
    companion object { fun from(j: JSONObject) = Appointment(j.getInt("id"), j.getInt("medicoId"), j.text("medico"), j.text("especialidade"),
        j.text("data"), j.text("horario"), j.text("status"), j.text("tipoPagamento"), j.text("convenioUsado"), j.text("statusConvenio"),
        j.text("observacaoConvenio"), j.optBoolean("podeCancelar"), j.optBoolean("podeConfirmar"), j.optBoolean("podeRemarcar"), j.optBoolean("futura", true)) }
}
data class AccountSession(val id: String, val device: String, val created: String, val current: Boolean) {
    companion object { fun from(j: JSONObject) = AccountSession(j.text("id"), j.text("dispositivo"), j.text("criadoEm"), j.optBoolean("atual")) }
}
data class ExamRecord(val id: Int, val name: String, val date: String, val place: String) {
    companion object { fun from(j: JSONObject) = ExamRecord(j.getInt("id"), j.text("nome"), j.text("dataRealizacao").take(10), j.text("localRealizacao")) }
}
data class ChatMessage(val id: Long, val texto: String, val autor: String)
data class ChatState(val messages: List<ChatMessage> = emptyList(), val modo: String = "IA", val encerrada: Boolean = false, val avaliacaoId: String? = null) {
    companion object { fun from(j: JSONObject) = ChatState((j.optJSONArray("mensagens") ?: JSONArray()).objects().map { ChatMessage(it.getLong("id"), it.text("texto"), it.text("autor")) },
        j.text("modo", "IA"), j.optBoolean("encerrada"), j.optJSONObject("avaliacao")?.text("id")?.takeIf(String::isNotBlank)) }
}
enum class Tab(val label: String) { HOME("Início"), CONSULTAS("Consultas"), CHAT("Chat"), CONTA("Conta") }
enum class Screen { HOME, CONSULTAS, BOOKING, CHAT, ACCOUNT, PROFILE, EDIT_PROFILE, EMAIL, SECURITY, PASSWORD, NOTIFICATIONS, READING, EXAMS, BOOKED }
data class BookingState(val stage: Int = 1, val specialty: String = "", val doctor: Doctor? = null,
    val dates: List<String> = emptyList(), val date: String = "", val hours: List<String> = emptyList(), val hour: String = "",
    val movingId: Int? = null, val payment: String = "Particular", val eligibility: Eligibility? = null) {
    fun previous() = if (movingId != null && stage == 3) null else copy(stage = (stage - 1).coerceAtLeast(if (movingId != null) 3 else 1))
    fun next() = when(stage) {
        1 -> { require(specialty.isNotBlank()) { "Escolha uma especialidade." }; copy(stage = 2) }
        3 -> { require(date.isNotBlank()) { "Escolha uma data." }; copy(stage = 4) }
        4 -> { require(hour.isNotBlank()) { "Escolha um horário." }; copy(stage = 5) }
        else -> this
    }
}
data class UiState(val restoring: Boolean = true, val authenticated: Boolean = false, val busy: Boolean = false,
    val profile: Patient? = null, val appointments: List<Appointment> = emptyList(), val doctors: List<Doctor> = emptyList(),
    val tab: Tab = Tab.HOME, val screen: Screen = Screen.HOME, val readingReturn: Screen = Screen.HOME,
    val booking: BookingState = BookingState(), val reading: ReadingPreferences = ReadingPreferences(), val profileDraft: ProfileDraft? = null,
    val chat: ChatState = ChatState(), val chatDraft: String = "", val feedbackAccepted: Boolean = false, val feedbackNote: Int? = null,
    val feedbackComment: String = "", val sessions: List<AccountSession> = emptyList(), val exams: List<ExamRecord> = emptyList(),
    val exam: ExamRecord? = null, val error: String? = null, val info: String? = null, val photo: ByteArray? = null,
    val bookedId: Int? = null)

sealed interface AppAction {
    data class Navigate(val tab: Tab): AppAction
    data class Open(val screen: Screen): AppAction
    data object Back: AppAction
    data object Refresh: AppAction
    data object Logout: AppAction
    data class Login(val user: String, val password: String): AppAction
    data object StartBooking: AppAction
    data class Specialty(val name: String): AppAction
    data class SelectDoctor(val doctor: Doctor): AppAction
    data class Date(val value: String): AppAction
    data class Hour(val value: String): AppAction
    data class Payment(val value: String): AppAction
    data object Next: AppAction
    data object FirstSlot: AppAction
    data object Book: AppAction
    data class Reschedule(val appointment: Appointment): AppAction
    data class AppointmentChange(val appointment: Appointment, val action: String): AppAction
    data class EditProfile(val draft: ProfileDraft): AppAction
    data object SaveProfile: AppAction
    data class Reading(val value: ReadingPreferences): AppAction
    data object FinishReading: AppAction
    data class SaveNotifications(val whats: Boolean, val email: Boolean, val sms: Boolean, val channel: String): AppAction
    data class ChangeEmail(val email: String, val password: String): AppAction
    data class ChangePassword(val current: String, val password: String, val confirmation: String): AppAction
    data class CloseSession(val id: String?): AppAction
    data class UploadPhoto(val uri: android.net.Uri): AppAction
    data class ChatDraft(val text: String): AppAction
    data object Send: AppAction
    data object EndChat: AppAction
    data object AcceptFeedback: AppAction
    data class FeedbackNote(val value: Int): AppAction
    data class FeedbackComment(val value: String): AppAction
    data class Rate(val skip: Boolean): AppAction
    data object DismissError: AppAction
    data object DismissInfo: AppAction
}
