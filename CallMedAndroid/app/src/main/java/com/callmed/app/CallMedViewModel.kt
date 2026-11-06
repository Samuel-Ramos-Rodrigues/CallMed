package com.callmed.app

import android.app.Application
import android.net.Uri
import androidx.lifecycle.AndroidViewModel
import androidx.lifecycle.viewModelScope
import kotlinx.coroutines.CancellationException
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.Job
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.update
import org.json.JSONObject
import java.io.ByteArrayOutputStream
import java.util.concurrent.atomic.AtomicLong
import java.util.concurrent.atomic.AtomicBoolean

class CallMedViewModel @JvmOverloads constructor(application: Application,
    private val api: CallMedApi = CallMedApi(SecureTokenStore(application)),
    private val readingStore: ReadingStore = ReadingStore(application)) : AndroidViewModel(application) {
    private val sessionGeneration = AtomicLong()
    private val chatGeneration = AtomicLong()
    private val chatPolling = AtomicBoolean()
    private val readingBeforeLogin = AtomicBoolean()
    private val mutable = MutableStateFlow(UiState(reading = readingStore.global()))
    val state = mutable.asStateFlow()

    init { if (api.hasSession()) action { loadAccount() } else mutable.update { it.copy(restoring = false) } }

    private fun fail(e: Exception) {
        if (e is CancellationException) throw e
        if (e is ApiError && e.status in listOf(401, 403)) {
            sessionGeneration.incrementAndGet(); api.forget()
            mutable.value = UiState(restoring = false, reading = state.value.reading, error = e.message)
        } else mutable.update { it.copy(restoring = false, busy = false, authenticated = api.hasSession(), error = e.message ?: "Não foi possível concluir agora.") }
    }
    private fun action(work: suspend () -> Unit) {
        if (state.value.busy) return
        // Uma leitura iniciada antes desta ação não pode sobrescrever seu resultado.
        chatGeneration.incrementAndGet()
        mutable.update { it.copy(busy = true) }
        viewModelScope.launch {
            try { withContext(Dispatchers.IO) { work() } }
            catch (e: Exception) { fail(e) }
            finally { mutable.update { it.copy(busy = false, restoring = false) } }
        }
    }
    private fun applyProfile(p: Patient) {
        if (readingBeforeLogin.getAndSet(false)) readingStore.save(p.contaId, state.value.reading, true)
        val local = readingStore.read(p.contaId)
        val reading = if (local?.pending == true) local.value else p.reading(local?.value?.scale ?: 1.25f)
        readingStore.save(p.contaId, reading, local?.pending == true)
        mutable.update { it.copy(profile = p, authenticated = true, restoring = false, reading = reading) }
    }
    private fun loadAccount() {
        var profile = api.profile()
        val pending = readingStore.read(profile.contaId)
        if (pending?.pending == true) {
            try {
                profile = Patient.from(api.json("PUT", "perfil/preferencias", pending.value.payload()))
                readingStore.save(profile.contaId, pending.value, false)
            } catch (e: ApiError) { if (e.status in listOf(401, 403)) throw e }
        }
        val appointments = api.appointments()
        val photo = if (profile.possuiFoto) api.photo() else null
        applyProfile(profile)
        mutable.update { it.copy(appointments = appointments, photo = photo) }
    }
    fun handle(event: AppAction) {
        when (event) {
            is AppAction.Navigate -> navigate(event.tab)
            is AppAction.Open -> open(event.screen)
            AppAction.Back -> back()
            AppAction.Refresh -> refresh()
            AppAction.Logout -> logout()
            is AppAction.Login -> login(event.user, event.password)
            AppAction.StartBooking -> startBooking()
            is AppAction.Specialty -> mutable.update { it.copy(booking = BookingState(specialty = event.name, payment = if (it.profile?.convenioValido == true) "Convenio" else "Particular")) }
            is AppAction.SelectDoctor -> selectDoctor(event.doctor)
            is AppAction.Date -> selectDate(event.value)
            is AppAction.Hour -> mutable.update { if (event.value in it.booking.hours) it.copy(booking = it.booking.copy(hour = event.value)) else it }
            is AppAction.Payment -> mutable.update { if (event.value in listOf("Particular", "Convenio")) it.copy(booking = it.booking.copy(payment = event.value)) else it }
            AppAction.Next -> try { mutable.update { it.copy(booking = it.booking.next()) } } catch (e: Exception) { fail(e) }
            AppAction.FirstSlot -> firstSlot()
            AppAction.Book -> book()
            is AppAction.Reschedule -> startReschedule(event.appointment)
            is AppAction.AppointmentChange -> appointmentChange(event.appointment, event.action)
            is AppAction.EditProfile -> mutable.update { it.copy(profileDraft = event.draft) }
            AppAction.SaveProfile -> saveProfile()
            is AppAction.Reading -> updateReading(event.value)
            AppAction.FinishReading -> finishReading()
            is AppAction.SaveNotifications -> saveNotifications(event)
            is AppAction.ChangeEmail -> changeEmail(event.email, event.password)
            is AppAction.ChangePassword -> changePassword(event)
            is AppAction.CloseSession -> closeSession(event.id)
            is AppAction.UploadPhoto -> uploadPhoto(event.uri)
            is AppAction.ChatDraft -> mutable.update { it.copy(chatDraft = event.text.take(1000)) }
            AppAction.Send -> send()
            AppAction.EndChat -> endChat()
            AppAction.AcceptFeedback -> mutable.update { it.copy(feedbackAccepted = true) }
            is AppAction.FeedbackNote -> mutable.update { it.copy(feedbackNote = event.value.takeIf { n -> n in 1..5 }) }
            is AppAction.FeedbackComment -> mutable.update { it.copy(feedbackComment = event.value.take(1000)) }
            is AppAction.Rate -> rate(event.skip)
            AppAction.DismissError -> mutable.update { it.copy(error = null) }
            AppAction.DismissInfo -> mutable.update { it.copy(info = null) }
        }
    }
    private fun navigate(tab: Tab) {
        if (state.value.busy) return
        val screen = when (tab) { Tab.HOME -> Screen.HOME; Tab.CONSULTAS -> Screen.CONSULTAS; Tab.CHAT -> Screen.CHAT; Tab.CONTA -> Screen.ACCOUNT }
        mutable.update { it.copy(tab = tab, screen = screen) }
        if (tab == Tab.CHAT) refreshChat()
    }
    private fun open(screen: Screen) {
        if (screen == Screen.READING) {
            if (state.value.screen != Screen.READING) mutable.update { it.copy(readingReturn = it.screen, screen = screen) }
            return
        }
        if (state.value.busy) return
        mutable.update { it.copy(screen = screen, profileDraft = if (screen == Screen.EDIT_PROFILE) it.profileDraft ?: it.profile?.let(ProfileDraft::from) else it.profileDraft) }
        when(screen) { Screen.SECURITY -> loadSessions(); Screen.EXAMS -> loadExams(); else -> {} }
    }
    private fun back() {
        if (state.value.screen == Screen.READING) { finishReading(); return }
        if (state.value.busy) return
        when(state.value.screen) {
            Screen.BOOKING -> {
                val previous = state.value.booking.previous()
                if (previous == null || state.value.booking.stage == 1) navigate(Tab.CONSULTAS)
                else mutable.update { it.copy(booking = previous) }
            }
            Screen.EDIT_PROFILE, Screen.EMAIL -> open(Screen.PROFILE)
            Screen.PASSWORD -> open(Screen.SECURITY)
            Screen.PROFILE, Screen.SECURITY, Screen.NOTIFICATIONS -> open(Screen.ACCOUNT)
            Screen.EXAMS -> navigate(Tab.HOME)
            else -> navigate(Tab.HOME)
        }
    }
    private fun refresh() = when(state.value.screen) {
        Screen.CHAT -> refreshChat()
        Screen.SECURITY -> loadSessions()
        Screen.EXAMS -> loadExams()
        else -> action { loadAccount(); mutable.update { it.copy(info = "Dados atualizados.") } }
    }
    private fun login(usuario: String, senha: String) {
        if (usuario.isBlank() || senha.isBlank()) { mutable.update { it.copy(error = "Informe seu CPF/e-mail e sua senha.") }; return }
        action { sessionGeneration.incrementAndGet(); api.login(usuario, senha); loadAccount() }
    }
    private fun logout() = action {
        sessionGeneration.incrementAndGet()
        try { api.logout() } catch (_: Exception) { api.forget() }
        mutable.value = UiState(restoring = false, reading = state.value.reading)
    }
    private fun startBooking() = action {
        mutable.update { it.copy(screen = Screen.BOOKING, tab = Tab.CONSULTAS, booking = BookingState(payment = if (it.profile?.convenioValido == true) "Convenio" else "Particular")) }
        val doctors = api.doctors()
        mutable.update { it.copy(doctors = doctors) }
    }
    private fun selectDoctor(doctor: Doctor, moving: Appointment? = null) = action {
        val booking = if (moving == null) state.value.booking.copy(doctor = doctor, stage = 3, dates = emptyList(), date = "", hours = emptyList(), hour = "", eligibility = null)
            else BookingState(stage = 3, specialty = doctor.especialidade, doctor = doctor, movingId = moving.id, payment = moving.pagamento)
        mutable.update { it.copy(booking = booking, screen = Screen.BOOKING, tab = Tab.CONSULTAS) }
        val suffix = booking.movingId?.let { "?consultaId=$it" }.orEmpty()
        val dates = api.array("medicos/${doctor.id}/datas$suffix").strings()
        val eligibility = Eligibility.from(api.json("GET", "medicos/${doctor.id}/elegibilidade"))
        mutable.update { it.copy(booking = it.booking.copy(dates = dates, eligibility = eligibility,
            payment = if (it.booking.movingId == null && (!eligibility.eligible || !eligibility.valid)) "Particular" else it.booking.payment)) }
    }
    private fun selectDate(date: String) = action {
        val current = state.value.booking; val doctor = current.doctor ?: return@action
        require(date in current.dates) { "Escolha uma das datas disponíveis." }
        mutable.update { it.copy(booking = it.booking.copy(date = date, hour = "", hours = emptyList())) }
        val suffix = current.movingId?.let { "&consultaId=$it" }.orEmpty()
        val hours = api.array("medicos/${doctor.id}/horarios?data=$date$suffix").strings()
        mutable.update { it.copy(booking = it.booking.copy(hours = hours)) }
    }
    private fun firstSlot() = action {
        val specialty = state.value.booking.specialty
        require(specialty.isNotBlank()) { "Escolha uma especialidade." }
        val slot = api.json("GET", "agenda/primeira-vaga?especialidade=${Uri.encode(specialty)}")
        val doctor = state.value.doctors.firstOrNull { it.id == slot.getInt("medicoId") }
            ?: Doctor(slot.getInt("medicoId"), slot.text("medico"), slot.text("especialidade"), "")
        val date = slot.text("data"); val hour = slot.text("horario")
        val dates = api.array("medicos/${doctor.id}/datas").strings()
        val hours = api.array("medicos/${doctor.id}/horarios?data=$date").strings()
        require(hour in hours) { "A primeira vaga mudou. Escolha outra data e horário." }
        val eligibility = Eligibility.from(api.json("GET", "medicos/${doctor.id}/elegibilidade"))
        mutable.update { it.copy(booking = it.booking.copy(stage = 5, doctor = doctor, dates = dates, date = date, hours = hours, hour = hour,
            eligibility = eligibility, payment = if (eligibility.eligible && eligibility.valid) it.booking.payment else "Particular")) }
    }
    private fun startReschedule(appointment: Appointment) = selectDoctor(Doctor(appointment.medicoId, appointment.medico, appointment.especialidade, ""), appointment)
    private fun book() = action {
        val current = state.value.booking; val doctor = current.doctor ?: return@action
        require(current.stage == 5 && current.date.isNotEmpty() && current.hour.isNotEmpty()) { "Revise os dados antes de agendar." }
        val body = JSONObject().put("medicoId", doctor.id).put("data", current.date).put("horario", current.hour).put("pagamento", current.payment).put("confirmar", true)
        val path = current.movingId?.let { "consultas/$it/remarcar" } ?: "consultas"
        val result = api.json("POST", path, body)
        val booked = Appointment.from(result.getJSONObject("consulta"))
        val appointments = runCatching { api.appointments() }.getOrElse { (state.value.appointments.filterNot { it.id == booked.id } + booked) }
        mutable.update { it.copy(appointments = appointments, screen = Screen.BOOKED, bookedId = booked.id, info = result.text("mensagem")) }
    }
    private fun appointmentChange(appointment: Appointment, operation: String) = action {
        require(operation in listOf("cancelar", "confirmar"))
        val result = api.json("POST", "consultas/${appointment.id}/$operation", JSONObject().put("confirmar", true))
        val updated = Appointment.from(result.getJSONObject("consulta"))
        mutable.update { it.copy(appointments = it.appointments.filterNot { p -> p.id == updated.id } + updated, info = result.text("mensagem")) }
    }
    private fun saveProfile() = action {
        val draft = state.value.profileDraft ?: return@action
        require(draft.name.isNotBlank()) { "Informe seu nome completo." }
        val profile = Patient.from(api.json("PUT", "perfil", draft.payload()))
        applyProfile(profile)
        mutable.update { it.copy(profileDraft = ProfileDraft.from(profile), screen = Screen.PROFILE, info = "Seus dados foram atualizados.") }
    }
    private fun updateReading(value: ReadingPreferences) {
        if (state.value.profile == null) readingBeforeLogin.set(true)
        readingStore.save(state.value.profile?.contaId, value, true)
        mutable.update { it.copy(reading = value) }
    }
    private fun finishReading() {
        mutable.update { it.copy(screen = it.readingReturn) }
        val current = state.value; val p = current.profile ?: return
        readingStore.save(p.contaId, current.reading, true)
        if (current.busy) return // A próxima atualização da conta sincroniza a preferência pendente.
        action {
            try {
                val updated = Patient.from(api.json("PUT", "perfil/preferencias", current.reading.payload()))
                readingStore.save(p.contaId, current.reading, false)
                applyProfile(updated)
                mutable.update { it.copy(info = "Preferências de leitura salvas.") }
            } catch (e: ApiError) {
                if (e.status in listOf(401, 403)) throw e
                mutable.update { it.copy(info = "Leitura aplicada neste aparelho. A sincronização será tentada ao atualizar sua conta.") }
            }
        }
    }
    private fun saveNotifications(e: AppAction.SaveNotifications) = action {
        val body = JSONObject().put("avisosWhatsApp", e.whats).put("avisosEmail", e.email).put("avisosSms", e.sms).put("canalPreferido", e.channel)
        applyProfile(Patient.from(api.json("PUT", "perfil/preferencias", body)))
        mutable.update { it.copy(screen = Screen.ACCOUNT, info = "Preferências de contato salvas.") }
    }
    private fun changeEmail(email: String, password: String) = action {
        require(email.isNotBlank() && password.isNotBlank()) { "Informe o novo e-mail e confirme sua senha atual." }
        sessionGeneration.incrementAndGet()
        api.renewIdentity("perfil/email", JSONObject().put("novoEmail", email.trim()).put("senhaAtual", password))
        loadAccount(); mutable.update { it.copy(screen = Screen.PROFILE, sessions = emptyList(), info = "E-mail atualizado. Os outros acessos foram encerrados.") }
    }
    private fun changePassword(e: AppAction.ChangePassword) = action {
        require(e.password == e.confirmation) { "A confirmação da senha não coincide." }
        require(e.current.isNotBlank() && e.password.length >= 8) { "Informe a senha atual e uma nova senha de pelo menos 8 caracteres." }
        sessionGeneration.incrementAndGet()
        api.renewIdentity("perfil/senha", JSONObject().put("senhaAtual", e.current).put("novaSenha", e.password).put("confirmarSenha", e.confirmation))
        loadAccount(); mutable.update { it.copy(screen = Screen.SECURITY, sessions = emptyList(), info = "Senha alterada. Este aplicativo continua conectado; outros acessos foram encerrados.") }
        loadSessionsNow()
    }
    private fun loadSessionsNow() { val sessions = api.array("perfil/sessoes").objects().map(AccountSession::from); mutable.update { it.copy(sessions = sessions) } }
    private fun loadSessions() = action { loadSessionsNow() }
    private fun closeSession(id: String?) = action {
        val path = if (id == null) "perfil/sessoes/encerrar-outras" else "perfil/sessoes/$id/encerrar"
        val response = api.json("POST", path, JSONObject().put("confirmar", true))
        loadSessionsNow(); mutable.update { it.copy(info = response.text("mensagem")) }
    }
    private fun loadExams() = action {
        val records = api.array("exames").objects().map(ExamRecord::from)
        mutable.update { it.copy(exams = records) }
    }
    private fun uploadPhoto(uri: Uri) = action {
        val resolver = getApplication<Application>().contentResolver
        val mime = resolver.getType(uri) ?: ""
        val bytes = resolver.openInputStream(uri)?.use { stream ->
            val out = ByteArrayOutputStream(); val buffer = ByteArray(8192)
            while (true) { val n = stream.read(buffer); if (n < 0) break; out.write(buffer, 0, n); require(out.size() <= 2 * 1024 * 1024) { "Escolha uma foto de até 2 MB." } }
            out.toByteArray()
        } ?: throw IllegalArgumentException("Não foi possível abrir a foto.")
        api.uploadPhoto(bytes, mime); applyProfile(api.profile()); val photo = api.photo()
        mutable.update { it.copy(photo = photo, info = "Foto atualizada.") }
    }
    private fun history(history: ChatState, expectedGeneration: Long? = null) {
        mutable.update { current ->
            if (expectedGeneration != null && (expectedGeneration != chatGeneration.get() || current.busy || !current.authenticated || current.screen != Screen.CHAT)) current
            else current.copy(chat = history,
            feedbackAccepted = if (history.avaliacaoId == current.chat.avaliacaoId) current.feedbackAccepted else false,
            feedbackNote = if (history.avaliacaoId == current.chat.avaliacaoId) current.feedbackNote else null,
            feedbackComment = if (history.avaliacaoId == current.chat.avaliacaoId) current.feedbackComment else "") }
    }
    fun pollChat(): Job? {
        if (state.value.busy || !state.value.authenticated || state.value.screen != Screen.CHAT) return null
        if (!chatPolling.compareAndSet(false, true)) return null
        val generation = sessionGeneration.get(); val chatVersion = chatGeneration.get()
        return viewModelScope.launch {
            try {
                val result = withContext(Dispatchers.IO) { api.history() }
                if (generation == sessionGeneration.get()) history(result, chatVersion)
            } catch (e: ApiError) { if (generation == sessionGeneration.get() && chatVersion == chatGeneration.get() && e.status in listOf(401, 403)) fail(e) }
            catch (e: Exception) { if (e is CancellationException) throw e }
            finally { chatPolling.set(false) }
        }
    }
    private fun refreshChat() = action { history(api.history()) }
    private fun send() {
        val text = state.value.chatDraft.trim(); if (text.isBlank()) return
        action {
            mutable.update { it.copy(chatDraft = "", sendingChat = true) }
            var accepted = false
            try {
                val response = api.json("POST", "atendimento/mensagens", JSONObject().put("mensagem", text)); accepted = true
                if (response.has("modo") || response.has("encerrada")) history(state.value.chat.copy(
                    modo = response.text("modo", "IA"), encerrada = response.optBoolean("encerrada"),
                    avaliacaoId = response.optJSONObject("avaliacao")?.text("id")?.takeIf(String::isNotBlank)))
                history(api.history())
            } catch (e: Exception) {
                if (!accepted) mutable.update { it.copy(chatDraft = text) }
                throw e
            } finally { mutable.update { it.copy(sendingChat = false) } }
        }
    }
    private fun endChat() = action { api.json("POST", "atendimento/encerrar", JSONObject()); history(api.history()) }
    private fun rate(skip: Boolean) = action {
        val current = state.value; val id = current.chat.avaliacaoId ?: return@action
        if (!skip) require(current.feedbackNote?.let { it in 1..5 } == true) { "Escolha uma nota de 1 a 5." }
        val result = api.json("POST", "atendimento/avaliar", JSONObject().put("id", id).put("nota", current.feedbackNote ?: JSONObject.NULL).put("comentario", current.feedbackComment).put("dispensar", skip))
        history(api.history()); mutable.update { it.copy(info = result.text("mensagem")) }
    }
}
