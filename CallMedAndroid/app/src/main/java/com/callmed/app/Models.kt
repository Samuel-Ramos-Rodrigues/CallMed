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
fun dateLabel(iso: String): String = runCatching { LocalDate.parse(iso).format(DateTimeFormatter.ofPattern("dd/MM/yyyy", brasil)) }.getOrDefault(iso)
fun longDateLabel(iso: String): String = runCatching { LocalDate.parse(iso).format(DateTimeFormatter.ofPattern("EEEE, dd 'de' MMMM", brasil)) }.getOrDefault(iso)
fun inputDate(iso: String): String = if (iso.isEmpty()) "" else dateLabel(iso)
fun isoDate(input: String): String? {
    if (input.isBlank()) return null
    return runCatching { LocalDate.parse(input.trim(), DateTimeFormatter.ofPattern("dd/MM/uuuu").withResolverStyle(java.time.format.ResolverStyle.STRICT)).toString() }
        .getOrElse { throw IllegalArgumentException("Use uma data válida no formato dia/mês/ano.") }
}
data class Patient(
    val nome: String, val email: String, val telefone: String, val cpf: String, val nascimento: String,
    val temConvenio: Boolean, val nomeConvenio: String, val numeroConvenio: String, val validadeConvenio: String,
    val convenioValido: Boolean, val tema: String, val whatsApp: Boolean, val emailAvisos: Boolean, val sms: Boolean,
    val canal: String, val possuiFoto: Boolean
) {
    val primeiroNome: String get() = nome.trim().substringBefore(' ')
    val iniciais: String get() = nome.split(' ').filter { it.isNotBlank() }.take(2).joinToString("") { it.take(1).uppercase() }
    companion object {
        fun from(j: JSONObject) = Patient(j.text("nome"), j.text("email"), j.text("telefone"), j.text("cpf"), j.text("dataNascimento"),
            j.optBoolean("temConvenio"), j.text("nomeConvenio"), j.text("numeroConvenio"), j.text("validadeConvenio"),
            j.optBoolean("convenioValido"), j.text("tema", "dark"), j.optBoolean("avisosWhatsApp", true), j.optBoolean("avisosEmail", true),
            j.optBoolean("avisosSms", true), j.text("canalPreferido", "WhatsApp"), j.optBoolean("possuiFoto"))
    }
}
data class Doctor(val id: Int, val nome: String, val especialidade: String, val crm: String) {
    companion object { fun from(j: JSONObject) = Doctor(j.getInt("id"), j.text("nome"), j.text("especialidade"), j.text("crm")) }
}
data class Appointment(val id: Int, val medicoId: Int, val medico: String, val especialidade: String, val data: String,
    val horario: String, val status: String, val pagamento: String, val convenio: String, val statusConvenio: String,
    val observacaoConvenio: String, val podeCancelar: Boolean, val podeConfirmar: Boolean, val podeRemarcar: Boolean) {
    val futura: Boolean get() = podeCancelar || podeConfirmar || podeRemarcar
    companion object { fun from(j: JSONObject) = Appointment(j.getInt("id"), j.getInt("medicoId"), j.text("medico"), j.text("especialidade"),
        j.text("data"), j.text("horario"), j.text("status"), j.text("tipoPagamento"), j.text("convenioUsado"), j.text("statusConvenio"),
        j.text("observacaoConvenio"), j.optBoolean("podeCancelar"), j.optBoolean("podeConfirmar"), j.optBoolean("podeRemarcar")) }
}
data class ChatMessage(val id: Long, val texto: String, val autor: String)
data class ChatState(val messages: List<ChatMessage> = emptyList(), val modo: String = "IA", val encerrada: Boolean = false, val avaliacaoId: String? = null) {
    companion object {
        fun from(j: JSONObject) = ChatState((j.optJSONArray("mensagens") ?: JSONArray()).objects().map { ChatMessage(it.getLong("id"), it.text("texto"), it.text("autor")) },
            j.text("modo", "IA"), j.optBoolean("encerrada"), j.optJSONObject("avaliacao")?.text("id")?.takeIf { it.isNotBlank() })
    }
}
enum class Tab(val titulo: String, val menu: String) { HOME("Início", "Início"), CONSULTAS("Minhas consultas", "Consultas"), AGENDAR("Agendar consulta", "Agendar"), CHAT("Atendimento", "Chat"), CONTA("Minha conta", "Conta") }
data class UiState(val restoring: Boolean = true, val authenticated: Boolean = false, val busy: Boolean = false,
    val profile: Patient? = null, val appointments: List<Appointment> = emptyList(), val doctors: List<Doctor> = emptyList(),
    val tab: Tab = Tab.HOME, val doctor: Doctor? = null, val dates: List<String> = emptyList(), val selectedDate: String = "",
    val hours: List<String> = emptyList(), val selectedHour: String = "", val movingId: Int? = null,
    val chat: ChatState = ChatState(), val error: String? = null, val info: String? = null, val photo: ByteArray? = null)
