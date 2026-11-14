package com.callmed.app

import org.junit.Assert.*
import org.junit.Test
import org.json.JSONObject
import java.io.IOException

class CallMedApiTest {
    @Test fun forbiddenResourceKeepsSession() {
        val session=Session("valid", "refresh", Long.MAX_VALUE)
        val store=MemoryStore(session)
        val api=CallMedApi(store, ApiTransport { response(403, "{\"mensagem\":\"Sala ainda indisponível.\"}") }, "https://callmed.test")
        try { api.json("GET", "salas/1"); fail("Access must be refused") }
        catch(e: ApiError) { assertEquals(403, e.status); assertEquals(session, store.read()) }
    }
    @Test fun chatRequiresSeparatedHistoryProtocol() {
        val store = MemoryStore(Session("valid", "refresh", Long.MAX_VALUE))
        val old = CallMedApi(store, ApiTransport { response(200, """{"encerrada":true,"mensagens":[]}""") }, "https://callmed.test")
        try { old.history(); fail("Histórico legado não deve ser misturado à conversa atual") } catch(e: ApiError) { assertEquals(426, e.status) }
        val current = CallMedApi(store, ApiTransport { response(200, """{"historicoVersao":2,"cicloId":"ciclo-2","haAnteriores":true,"encerrada":false,"mensagens":[{"id":2,"texto":"Nova pergunta","autor":"paciente"}]}""") }, "https://callmed.test")
        val history = current.history()
        assertEquals("ciclo-2", history.cycle); assertTrue(history.hasPrevious); assertFalse(history.encerrada)
        assertEquals(2L, history.messages.single().id)
    }
    class MemoryStore(var value: Session? = null): TokenStore {
        override fun read() = value
        override fun save(session: Session) { value = session }
        override fun clear() { value = null }
    }
    private fun response(status: Int, body: String) = ApiResponse(status, body.toByteArray())
    private val tokens = """{"tokenType":"Bearer","accessToken":"new-access","refreshToken":"new-refresh","expiresIn":1800}"""
    @Test fun credentialsTravelOnlyInPostBody() {
        val store=MemoryStore(); val requests=mutableListOf<ApiRequest>()
        val api=CallMedApi(store,ApiTransport { r -> requests+=r;response(200,tokens) },"https://callmed.test",{1000L})
        api.login("  paciente@test.invalid  ","senha-secreta")
        val request=requests.single(); assertEquals("POST",request.method);assertFalse(request.url.contains("senha"));assertNull(request.token)
        val payload=JSONObject(requireNotNull(request.body).toString(Charsets.UTF_8))
        assertEquals("paciente@test.invalid",payload.getString("usuario"))
        assertEquals("senha-secreta",payload.getString("senha"))
        assertEquals("new-refresh",store.read()!!.refreshToken)
    }
    @Test fun httpsIsRequired() {
        try { CallMedApi(MemoryStore(),baseUrl="http://callmed.test");fail("HTTP deve falhar") } catch (_:IllegalArgumentException) { }
        try { CallMedApi(MemoryStore(),baseUrl="https://user:pass@callmed.test");fail("Credenciais na URL devem falhar") } catch (_:IllegalArgumentException) { }
    }
    @Test fun expiredTokenIsRefreshedBeforeProtectedRequest() {
        val store=MemoryStore(Session("expired","refresh",1000));val requests=mutableListOf<ApiRequest>()
        val api=CallMedApi(store,ApiTransport {r -> requests+=r;if(r.url.endsWith("auth/refresh"))response(200,tokens)else response(200,"{}")},"https://callmed.test",{2000L})
        api.json("GET","perfil");assertEquals(2,requests.size);assertTrue(requests[0].url.endsWith("auth/refresh"));assertNull(requests[0].token);assertEquals("new-access",requests[1].token)
    }
    @Test fun unauthorizedRetriesOnlyAfterSuccessfulRefresh() {
        val store=MemoryStore(Session("old","refresh",999999));val requests=mutableListOf<ApiRequest>()
        val api=CallMedApi(store,ApiTransport {r -> requests+=r;when(requests.size){1->response(401,"{}");2->response(200,tokens);else->response(200,"{}")}},"https://callmed.test",{1000L})
        api.json("POST","consultas",JSONObject().put("confirmar",true));assertEquals(3,requests.size);assertEquals("new-access",requests.last().token)
    }
    @Test fun failedRefreshClearsSession() {
        val store=MemoryStore(Session("expired","refresh",1000))
        val api=CallMedApi(store,ApiTransport {response(401,"{}")},"https://callmed.test",{2000L})
        try{api.json("GET","perfil");fail()}catch(e:ApiError){assertEquals(401,e.status)};assertNull(store.read())
    }
    @Test fun networkFailureNeverRepeatsMutation() {
        val store=MemoryStore(Session("valid","refresh",999999));var calls=0
        val api=CallMedApi(store,ApiTransport {calls++;throw IOException("conexão perdida")},"https://callmed.test",{1000L})
        try{api.json("POST","consultas",JSONObject());fail()}catch(e:ApiError){assertEquals(0,e.status);assertTrue(e.message!!.contains("Confira suas consultas"))};assertEquals(1,calls)
    }
    @Test fun forbiddenPreservesLocalSession() {
        val store=MemoryStore(Session("valid","refresh",999999))
        val api=CallMedApi(store,ApiTransport {response(403,"{}")},"https://callmed.test",{1000L})
        try{api.json("GET","perfil");fail()}catch(e:ApiError){assertEquals(403,e.status)};assertNotNull(store.read())
    }
    @Test fun logoutClearsLocalSessionEvenWhenOffline() {
        val store=MemoryStore(Session("valid","refresh",999999))
        val api=CallMedApi(store,ApiTransport {throw IOException()},"https://callmed.test",{1000L})
        try{api.logout()}catch(_:ApiError){};assertNull(store.read())
    }
    @Test fun malformedPhotoNeverGoesToNetwork() {
        var calls=0;val api=CallMedApi(MemoryStore(),ApiTransport {calls++;response(200,"{}")},"https://callmed.test")
        try{api.uploadPhoto(ByteArray(2*1024*1024+1),"image/png");fail()}catch(_:IllegalArgumentException){}
        try{api.uploadPhoto(byteArrayOf(1),"image/svg+xml");fail()}catch(_:IllegalArgumentException){};assertEquals(0,calls)
    }
    @Test fun invalidServerResponseDoesNotStoreEmptyTokens() {
        val store=MemoryStore();val api=CallMedApi(store,ApiTransport {response(200,"""{"tokenType":"Bearer","accessToken":"","refreshToken":"","expiresIn":0}""")},"https://callmed.test")
        try{api.login("a","b");fail()}catch(_:IllegalStateException){};assertNull(store.read())
    }
    @Test fun lateRefreshCannotRestoreLoggedOutSession() {
        val store=MemoryStore(Session("expired","old-refresh",1000));lateinit var api:CallMedApi
        api=CallMedApi(store,ApiTransport { api.forget();response(200,tokens) },"https://callmed.test",{2000L})
        try{api.json("GET","perfil");fail()}catch(e:ApiError){assertEquals(401,e.status)}
        assertNull(store.read())
    }
    @Test fun responseFromPreviousAccountCannotClearNewLogin() {
        val store=MemoryStore(Session("old-account","old-refresh",999999));lateinit var api:CallMedApi
        api=CallMedApi(store,ApiTransport { request ->
            if(request.url.endsWith("auth/login"))response(200,tokens)
            else { api.forget();api.login("new-account@test.invalid","password");response(403,"{}") }
        },"https://callmed.test",{1000L})
        try{api.json("GET","perfil");fail()}catch(e:ApiError){assertEquals(401,e.status)}
        assertEquals("new-access",store.read()!!.accessToken)
    }
    @Test fun identityRenewalRejectsLateResponseFromPreviousStamp() {
        val store=MemoryStore(Session("old-account","old-refresh",999999));lateinit var api:CallMedApi
        api=CallMedApi(store,ApiTransport { request ->
            if(request.url.endsWith("perfil/email"))response(200,tokens)
            else { api.renewIdentity("perfil/email",JSONObject().put("novoEmail","novo@example.test"));response(403,"{}") }
        },"https://callmed.test",{1000L})
        try{api.json("GET","perfil");fail()}catch(e:ApiError){assertEquals(401,e.status)}
        assertEquals("new-access",store.read()!!.accessToken)
    }
    @Test fun bookingCannotAdvanceWithoutExplicitSelection() {
        try{BookingState().next();fail()}catch(_:IllegalArgumentException){}
        assertEquals(2,BookingState(specialty="Clínica geral").next().stage)
        assertNull(BookingState(stage=3,movingId=12).previous())
        assertEquals(3,BookingState(stage=4,movingId=12).previous()!!.stage)
    }
    @Test fun pastPendingConsultationIsStillHistoryEvenWhenActionIsAllowed() {
        val c=Appointment.from(JSONObject("""{"id":1,"medicoId":2,"futura":false,"podeRemarcar":true}"""))
        assertFalse(c.futura);assertTrue(c.podeRemarcar)
    }
    @Test fun datesUseStrictCalendarValidation() {
        assertEquals("2024-02-29",isoDate("29/02/2024"));assertNull(isoDate(""));assertEquals("04/11/2026",dateLabel("2026-11-04"))
        for(date in listOf("29/02/2026","31/04/2026","00/01/2026"))try{isoDate(date);fail("Data inválida: $date")}catch(_:IllegalArgumentException){}
    }
    @Test fun jsonNullNeverAppearsAsLiteralNullInProfile() {
        val p=Patient.from(JSONObject("""{"nome":"Alex Teste","telefone":null,"email":null,"validadeConvenio":null}"""))
        assertEquals("",p.telefone);assertEquals("",p.email);assertEquals("",p.validadeConvenio);assertEquals("AT",p.iniciais)
    }
    @Test fun consultationActionsFollowServerFlags() {
        val c=Appointment.from(JSONObject("""{"id":1,"medicoId":2,"data":"2000-01-01","podeRemarcar":true}"""));assertTrue(c.futura)
    }
    @Test fun webFeedbackIsParsedForNativeCard() {
        val c=ChatState.from(JSONObject("""{"modo":"Humano","encerrada":true,"avaliacao":{"id":"rating-1"},"mensagens":[{"id":7,"texto":"Resposta","autor":"equipe"}]}"""))
        assertEquals("rating-1",c.avaliacaoId);assertEquals("equipe",c.messages.single().autor);assertTrue(c.encerrada)
    }
    @Test fun psychologistUsesCrpWithoutRelabelingLegacyCrm() {
        val registered=Doctor.from(JSONObject("""{"id":1,"nome":"Psicóloga fictícia","especialidade":"Psicologia clínica","crp":"12/12345","crm":"CRM-LEGADO"}"""))
        val legacy=Doctor.from(JSONObject("""{"id":2,"nome":"Legado fictício","crm":"CRM-LEGADO"}"""))
        assertEquals("12/12345",registered.crp);assertEquals("",legacy.crp)
    }
    @Test fun clinicalWriteCannotCrossAnAccountSessionChange(){
        val store=MemoryStore(Session("old","refresh",Long.MAX_VALUE));val requests=mutableListOf<ApiRequest>()
        val api=CallMedApi(store,ApiTransport{r->requests+=r;response(200,tokens)},"https://callmed.test")
        val old=api.sessionStamp();api.forget();api.login("outro@test.invalid","senha-ficticia");val count=requests.size
        try{api.jsonInSession(old,"POST","clinico/chave",JSONObject().put("publica","chave fictícia antiga"));fail("Escrita de outra sessão deve ser recusada")}catch(e:ApiError){assertEquals(401,e.status)}
        assertEquals(count,requests.size);assertTrue(api.hasSession());assertFalse(api.sameSession(old));assertEquals("new-access",store.read()!!.accessToken)
    }
    @Test fun clinicalReadStillUsesTheSameSessionAfterRefreshingTokens(){
        val store=MemoryStore(Session("expired","refresh",1));val api=CallMedApi(store,ApiTransport{r->if(r.url.endsWith("auth/refresh"))response(200,tokens)else response(200,"{\"usuarioId\":\"ficticio\"}")},"https://callmed.test")
        val stamp=api.sessionStamp();assertEquals("ficticio",api.jsonInSession(stamp,"GET","clinico/7").getString("usuarioId"));assertTrue(api.sameSession(stamp))
    }

}
