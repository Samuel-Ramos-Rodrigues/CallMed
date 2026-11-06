package com.callmed.app

import android.app.Application
import android.net.Uri
import androidx.lifecycle.AndroidViewModel
import androidx.lifecycle.viewModelScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.update
import org.json.JSONObject
import java.io.ByteArrayOutputStream
import java.util.concurrent.atomic.AtomicLong

class CallMedViewModel(application: Application) : AndroidViewModel(application) {
    private val sessionGeneration = AtomicLong()
    private val api = CallMedApi(SecureTokenStore(application))
    private val mutable = MutableStateFlow(UiState())
    val state = mutable.asStateFlow()
    init { if (api.hasSession()) restore() else mutable.update { it.copy(restoring = false) } }
    private fun fail(e: Exception) {
        if (e is ApiError && e.status in listOf(401, 403)) { sessionGeneration.incrementAndGet(); api.forget(); mutable.value = UiState(restoring = false, error = e.message); return }
        mutable.update { it.copy(restoring = false, busy = false, authenticated = api.hasSession(), error = e.message ?: "Não foi possível concluir agora.") }
    }
    private fun action(work: suspend () -> Unit) {
        if (mutable.value.busy) return
        mutable.update { it.copy(busy = true) }
        viewModelScope.launch {
            try { withContext(Dispatchers.IO) { work() } } catch (e: Exception) { fail(e) }
            finally { mutable.update { it.copy(busy = false, restoring = false) } }
        }
    }
    private fun loadAccount() {
        val profile = api.profile()
        val appointments = api.appointments()
        val photo = if (profile.possuiFoto) api.photo() else null
        mutable.update { it.copy(profile = profile, appointments = appointments, authenticated = true, restoring = false, photo = photo) }
    }
    fun restore() = action { loadAccount() }
    fun login(usuario: String, senha: String) {
        if (usuario.isBlank() || senha.isBlank()) { mutable.update { it.copy(error = "Informe seu CPF/e-mail e sua senha.") }; return }
        action { sessionGeneration.incrementAndGet(); api.login(usuario, senha); loadAccount() }
    }
    fun logout() = action {
        sessionGeneration.incrementAndGet()
        try { api.logout() } catch (_: Exception) { api.forget() }
        mutable.value = UiState(restoring = false)
    }
    fun dismissError() { mutable.update { it.copy(error = null) } }
    fun dismissInfo() { mutable.update { it.copy(info = null) } }
    fun navigate(tab: Tab) {
        mutable.update { it.copy(tab = tab) }
        when (tab) { Tab.AGENDAR -> if (state.value.doctors.isEmpty()) loadDoctors(); Tab.CHAT -> refreshChat(); else -> {} }
    }
    fun refresh() = action { loadAccount(); mutable.update { it.copy(info = "Dados atualizados.") } }
    private fun loadDoctors() = action { mutable.update { it.copy(doctors = api.doctors()) } }
    fun selectDoctor(doctor: Doctor, moving: Int? = null) = action {
        mutable.update { it.copy(doctor = doctor, movingId = moving, selectedDate = "", selectedHour = "", dates = emptyList(), hours = emptyList()) }
        val suffix = moving?.let { "?consultaId=$it" }.orEmpty()
        val dates = api.array("medicos/${doctor.id}/datas$suffix").strings()
        mutable.update { it.copy(dates = dates) }
    }
    fun clearDoctor() { if (!state.value.busy) mutable.update { it.copy(doctor = null, movingId = null, dates = emptyList(), hours = emptyList(), selectedDate = "", selectedHour = "") } }
    fun selectDate(date: String) = action {
        val current = state.value; val doctor = current.doctor ?: return@action
        mutable.update { it.copy(selectedDate = date, selectedHour = "", hours = emptyList()) }
        val suffix = current.movingId?.let { "&consultaId=$it" }.orEmpty()
        val hours = api.array("medicos/${doctor.id}/horarios?data=$date$suffix").strings()
        mutable.update { it.copy(hours = hours) }
    }
    fun selectHour(hour: String) { if (!state.value.busy && hour in state.value.hours) mutable.update { it.copy(selectedHour = hour) } }
    fun startReschedule(appointment: Appointment) {
        mutable.update { it.copy(tab = Tab.AGENDAR) }
        selectDoctor(Doctor(appointment.medicoId, appointment.medico, appointment.especialidade, ""), appointment.id)
    }
    fun book(payment: String) = action {
        val current = state.value; val doctor = current.doctor ?: return@action
        require(current.selectedDate.isNotEmpty() && current.selectedHour.isNotEmpty()) { "Selecione uma data e um horário." }
        val body = JSONObject().put("medicoId", doctor.id).put("data", current.selectedDate).put("horario", current.selectedHour).put("pagamento", payment).put("confirmar", true)
        val path = current.movingId?.let { "consultas/$it/remarcar" } ?: "consultas"
        val response = api.json("POST", path, body)
        val appointments = api.appointments()
        mutable.update { it.copy(appointments = appointments, tab = Tab.CONSULTAS, doctor = null, movingId = null, dates = emptyList(), hours = emptyList(), selectedDate = "", selectedHour = "", info = response.text("mensagem")) }
    }
    fun appointmentAction(appointment: Appointment, actionName: String) = action {
        require(actionName in listOf("cancelar", "confirmar"))
        val response = api.json("POST", "consultas/${appointment.id}/$actionName", JSONObject().put("confirmar", true))
        val appointments = api.appointments()
        mutable.update { it.copy(appointments = appointments, info = response.text("mensagem")) }
    }
    fun saveProfile(nome: String, telefone: String, nascimento: String, temConvenio: Boolean, convenio: String, carteirinha: String, validade: String) = action {
        val body = JSONObject().put("nome", nome.trim()).put("telefone", telefone.trim().ifEmpty { null } ?: JSONObject.NULL).put("dataNascimento", isoDate(nascimento) ?: JSONObject.NULL)
            .put("temConvenio", temConvenio).put("nomeConvenio", convenio.trim()).put("numeroConvenio", carteirinha.trim()).put("validadeConvenio", (if (temConvenio) isoDate(validade) else null) ?: JSONObject.NULL)
        val profile = Patient.from(api.json("PUT", "perfil", body))
        mutable.update { it.copy(profile = profile, info = "Seus dados foram salvos.") }
    }
    fun savePreferences(tema: String, whats: Boolean, email: Boolean, sms: Boolean, canal: String) = action {
        val body = JSONObject().put("tema", tema).put("avisosWhatsApp", whats).put("avisosEmail", email).put("avisosSms", sms).put("canalPreferido", canal)
        val profile = Patient.from(api.json("PUT", "perfil/preferencias", body))
        mutable.update { it.copy(profile = profile, info = "Preferências salvas.") }
    }
    fun changePassword(atual: String, nova: String, confirmar: String) = action {
        require(nova == confirmar) { "A confirmação da senha não coincide." }
        val result = api.json("POST", "perfil/senha", JSONObject().put("senhaAtual", atual).put("novaSenha", nova).put("confirmarSenha", confirmar))
        sessionGeneration.incrementAndGet(); api.forget(); mutable.value = UiState(restoring = false, info = result.text("mensagem"))
    }
    fun uploadPhoto(uri: Uri) = action {
        val resolver = getApplication<Application>().contentResolver
        val mime = resolver.getType(uri) ?: ""
        val bytes = resolver.openInputStream(uri)?.use { stream ->
            val out = ByteArrayOutputStream(); val buffer = ByteArray(8192)
            while (true) { val n = stream.read(buffer); if (n < 0) break; out.write(buffer, 0, n); require(out.size() <= 2 * 1024 * 1024) { "Escolha uma foto de até 2 MB." } }; out.toByteArray()
        } ?: throw IllegalArgumentException("Não foi possível abrir a foto.")
        api.uploadPhoto(bytes, mime)
        val profile = api.profile(); val photo = api.photo()
        mutable.update { it.copy(profile = profile, photo = photo, info = "Foto atualizada.") }
    }
    fun pollChat() {
        if (state.value.busy || !state.value.authenticated) return
        val generation = sessionGeneration.get()
        viewModelScope.launch {
            try { val history = withContext(Dispatchers.IO) { api.history() }; if (generation == sessionGeneration.get() && state.value.authenticated && !state.value.busy) mutable.update { it.copy(chat = history) } }
            catch (e: ApiError) { if (generation == sessionGeneration.get() && e.status in listOf(401, 403)) fail(e) }
            catch (_: Exception) { /* Leitura periódica pode esperar a conexão voltar. */ }
        }
    }
    fun refreshChat() = action { mutable.update { it.copy(chat = api.history()) } }
    fun send(text: String) {
        val message = text.trim(); if (message.isEmpty()) return
        if (message.length > 1000) { mutable.update { it.copy(error = "Use até 1.000 caracteres por mensagem.") }; return }
        action {
            api.json("POST", "atendimento/mensagens", JSONObject().put("mensagem", message))
            mutable.update { it.copy(chat = api.history()) }
        }
    }
    fun endChat() = action { api.json("POST", "atendimento/encerrar", JSONObject()); mutable.update { it.copy(chat = api.history()) } }
    fun rate(note: Int?, comment: String, skip: Boolean) = action {
        val id = state.value.chat.avaliacaoId ?: return@action
        val result = api.json("POST", "atendimento/avaliar", JSONObject().put("id", id).put("nota", note ?: JSONObject.NULL).put("comentario", comment.take(1000)).put("dispensar", skip))
        mutable.update { it.copy(chat = api.history(), info = result.text("mensagem")) }
    }
}
