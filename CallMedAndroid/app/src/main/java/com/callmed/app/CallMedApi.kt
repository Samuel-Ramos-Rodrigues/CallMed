package com.callmed.app

import org.json.JSONObject
import org.json.JSONArray
import java.net.HttpURLConnection
import java.net.URI
import java.net.URL
import java.io.IOException
import java.io.ByteArrayOutputStream

class ApiError(val status: Int, message: String) : IOException(message)
data class ApiRequest(val method: String, val url: String, val body: ByteArray?, val contentType: String, val token: String?)
data class ApiResponse(val status: Int, val body: ByteArray)
fun interface ApiTransport { fun execute(request: ApiRequest): ApiResponse }
class AndroidTransport : ApiTransport {
    override fun execute(request: ApiRequest): ApiResponse {
        val connection = URL(request.url).openConnection() as HttpURLConnection
        try {
            connection.requestMethod = request.method
            connection.instanceFollowRedirects = false
            connection.connectTimeout = 15000
            connection.readTimeout = if (request.url.endsWith("atendimento/mensagens")) 65000 else 30000
            connection.setRequestProperty("Accept", "application/json")
            connection.setRequestProperty("Cache-Control", "no-store")
            request.token?.let { connection.setRequestProperty("Authorization", "Bearer $it") }
            if (request.body != null) {
                connection.doOutput = true
                connection.setRequestProperty("Content-Type", request.contentType)
                connection.setFixedLengthStreamingMode(request.body.size)
                connection.outputStream.use { it.write(request.body) }
            }
            val code = connection.responseCode
            val input = if (code in 200..299) connection.inputStream else connection.errorStream
            val bytes = input?.use { stream ->
                val out = ByteArrayOutputStream(); val buffer = ByteArray(8192)
                while (true) { val n = stream.read(buffer); if (n < 0) break; out.write(buffer, 0, n); if (out.size() > 3 * 1024 * 1024) throw IOException("Resposta maior que o esperado.") }
                out.toByteArray()
            } ?: ByteArray(0)
            return ApiResponse(code, bytes)
        } finally { connection.disconnect() }
    }
}
class CallMedApi(private val store: TokenStore, private val transport: ApiTransport = AndroidTransport(), baseUrl: String = BuildConfig.API_BASE_URL, private val now: () -> Long = System::currentTimeMillis) {
    private val origin = baseUrl.trimEnd('/')
    private val refreshLock = Any()
    private val sessionLock = Any()
    private var sessionGeneration = 0L
    init { require(URI(origin).scheme == "https" && URI(origin).host != null && URI(origin).rawUserInfo == null) }
    fun hasSession() = synchronized(sessionLock) { store.read() != null }
    fun forget() = synchronized(sessionLock) { sessionGeneration++; store.clear() }
    private fun changed(): Nothing = throw ApiError(401, "Seu acesso mudou. Entre novamente para continuar.")
    fun login(usuario: String, senha: String) {
        val generation = synchronized(sessionLock) { sessionGeneration }
        val response = send("POST", "auth/login", JSONObject().put("usuario", usuario.trim()).put("senha", senha), authenticated = false)
        synchronized(sessionLock) {
            if (sessionGeneration != generation) changed()
            saveTokens(JSONObject(response.toString(Charsets.UTF_8)))
            sessionGeneration++
        }
    }
    fun logout() {
        val generation = synchronized(sessionLock) { sessionGeneration }
        try { json("POST", "auth/logout", JSONObject()) }
        finally { synchronized(sessionLock) { if (sessionGeneration == generation) { sessionGeneration++; store.clear() } } }
    }
    fun renewIdentity(path: String, body: JSONObject) {
        // Reserve a new generation before the server invalidates the old security stamp.
        val generation = synchronized(sessionLock) { sessionGeneration++; sessionGeneration }
        val response = send(if (path.endsWith("email")) "PUT" else "POST", path, body)
        synchronized(sessionLock) {
            if (sessionGeneration != generation) changed()
            saveTokens(JSONObject(response.toString(Charsets.UTF_8)))
            sessionGeneration++
        }
    }
    fun json(method: String, path: String, body: JSONObject? = null): JSONObject = JSONObject(send(method, path, body).toString(Charsets.UTF_8))
    fun array(path: String): JSONArray = JSONArray(send("GET", path).toString(Charsets.UTF_8))
    fun profile() = Patient.from(json("GET", "perfil"))
    fun appointments() = array("consultas").objects().map(Appointment::from)
    fun doctors() = array("medicos").objects().map(Doctor::from)
    fun history(before: Long? = null): ChatState {
        val value = json("GET", "atendimento/historico" + (before?.let { "?antesDeMensagemId=$it" } ?: ""))
        if (value.optInt("historicoVersao") < 2) throw ApiError(426, "O chat precisa de uma atualização pela clínica. Use o site para continuar enquanto isso.")
        return ChatState.from(value)
    }
    fun photo(): ByteArray? = try { send("GET", "perfil/foto") } catch (e: ApiError) { if (e.status == 404) null else throw e }
    fun uploadPhoto(bytes: ByteArray, mime: String) {
        require(bytes.size <= 2 * 1024 * 1024) { "Escolha uma foto de até 2 MB." }
        require(mime == "image/png" || mime == "image/jpeg") { "Escolha uma foto JPG ou PNG." }
        val boundary = "CallMedPhoto${System.nanoTime()}"
        val out = ByteArrayOutputStream()
        out.write(("--$boundary\r\nContent-Disposition: form-data; name=\"foto\"; filename=\"foto\"\r\nContent-Type: $mime\r\n\r\n").toByteArray())
        out.write(bytes); out.write(("\r\n--$boundary--\r\n").toByteArray())
        sendRaw("POST", "perfil/foto", out.toByteArray(), "multipart/form-data; boundary=$boundary", true)
    }
    private fun send(method: String, path: String, body: JSONObject? = null, authenticated: Boolean = true): ByteArray =
        sendRaw(method, path, body?.toString()?.toByteArray(Charsets.UTF_8), "application/json; charset=utf-8", authenticated)
    private fun sendRaw(method: String, path: String, body: ByteArray?, type: String, authenticated: Boolean): ByteArray {
        require(!path.startsWith('/') && !path.contains("..") && !path.contains("://"))
        val (generation, session) = synchronized(sessionLock) {
            sessionGeneration to (if (authenticated) store.read() ?: throw ApiError(401, "Entre novamente para continuar.") else null)
        }
        if (session != null && session.expiresAt <= now() + 20000) refresh(session, generation)
        fun execute(): ApiResponse {
            val token = synchronized(sessionLock) { if (authenticated && sessionGeneration != generation) changed(); if (authenticated) store.read()?.accessToken else null }
            return try { transport.execute(ApiRequest(method, "$origin/api/mobile/v1/$path", body, type, token)) }
                catch (e: IOException) { throw ApiError(0, if (path == "auth/login") "Não foi possível conectar ao CallMed. Verifique a internet e tente entrar novamente." else if (method != "GET") "Não foi possível confirmar a resposta do servidor. Confira suas consultas ou o histórico antes de repetir uma operação." else "Não foi possível conectar ao CallMed. Verifique a internet e tente atualizar.") }
        }
        var response = execute()
        if (authenticated && response.status == 401) {
            val current = synchronized(sessionLock) { if (sessionGeneration != generation) changed(); store.read() ?: changed() }
            refresh(current, generation); response = execute()
        }
        synchronized(sessionLock) {
            if (authenticated && sessionGeneration != generation) changed()
            if (authenticated && response.status in listOf(401, 403)) { sessionGeneration++; store.clear(); throw ApiError(response.status, "Seu acesso foi encerrado ou expirou. Entre novamente.") }
        }
        if (response.status !in 200..299) throw ApiError(response.status, errorMessage(response))
        return response.body
    }
    private fun refresh(previous: Session, generation: Long) = synchronized(refreshLock) {
        val current = synchronized(sessionLock) { if (sessionGeneration != generation) changed(); store.read() ?: changed() }
        if (current.accessToken != previous.accessToken && current.expiresAt > now() + 20000) return@synchronized
        val response = try { transport.execute(ApiRequest("POST", "$origin/api/mobile/v1/auth/refresh", JSONObject().put("refreshToken", current.refreshToken).toString().toByteArray(), "application/json", null)) }
            catch (_: IOException) { throw ApiError(0, "Não foi possível renovar o acesso. Verifique a conexão.") }
        synchronized(sessionLock) {
            // Uma resposta antiga não pode restaurar sessão encerrada nem substituir outra conta.
            if (sessionGeneration != generation) changed()
            if (response.status in listOf(401, 403)) { sessionGeneration++; store.clear(); throw ApiError(401, "Entre novamente para continuar.") }
            if (response.status !in 200..299) throw ApiError(response.status, errorMessage(response))
            saveTokens(JSONObject(response.body.toString(Charsets.UTF_8)))
        }
    }
    private fun saveTokens(json: JSONObject) {
        val access = json.getString("accessToken"); val refresh = json.getString("refreshToken"); val seconds = json.getLong("expiresIn")
        check(access.isNotBlank() && refresh.isNotBlank() && seconds in 1..86400 && json.text("tokenType").equals("Bearer", true)) { "Resposta de autenticação inválida." }
        store.save(Session(access, refresh, now() + seconds * 1000))
    }
    private fun errorMessage(response: ApiResponse): String {
        if (response.status == 404) return runCatching { JSONObject(response.body.toString(Charsets.UTF_8)).text("mensagem") }.getOrDefault("").ifBlank { "Este serviço não está disponível no momento. Tente novamente mais tarde ou entre em contato com a clínica." }
        if (response.status == 429) return "Muitas tentativas em pouco tempo. Aguarde um pouco e tente novamente."
        return runCatching {
            val j = JSONObject(response.body.toString(Charsets.UTF_8))
            j.text("mensagem").ifBlank { j.optJSONObject("errors")?.let { errors -> errors.keys().asSequence().flatMap { (errors.optJSONArray(it) ?: JSONArray()).strings().asSequence() }.take(3).joinToString(" ") }.orEmpty() }
                .ifBlank { "Não foi possível concluir agora. Confira os dados e tente novamente." }
        }.getOrDefault("Não foi possível concluir agora. Tente novamente em alguns instantes.")
    }
}
