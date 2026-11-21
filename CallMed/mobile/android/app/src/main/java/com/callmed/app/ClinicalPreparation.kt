package com.callmed.app

import kotlinx.coroutines.*
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.asStateFlow
import org.json.JSONObject

internal interface ClinicalKeyStore {
    fun load():JSONObject?
    fun save(value:JSONObject)
}
internal data class ClinicalPreparationState(
    val data:JSONObject?=null,val key:JSONObject?=null,
    val records:List<Pair<JSONObject,String>> = emptyList(),val answers:Map<String,String> = emptyMap(),
    val consent:Boolean=false,val busy:Boolean=false,val previousFailed:Boolean=false,val vaultUnavailable:Boolean=false,
    val status:String="Carregando sua preparação…"
)

// Vive no ViewModel da conta. Pausar a tela apaga o texto, mas não perde uma
// transferência de chave que aguarda o seletor de arquivos do sistema.
internal class ClinicalPreparation(
    private val id:Int,private val api:CallMedApi,private val scope:CoroutineScope,
    private val vault:(String)->ClinicalKeyStore,private val authenticationLost:()->Unit
) {
    private val mutable=MutableStateFlow(ClinicalPreparationState())
    val state=mutable.asStateFlow()
    private val ownerSession=api.sessionStamp()
    private var generation=0L
    private var job:Job?=null
    private var closed=false
    private sealed interface Transfer {
        data class Create(val key:JSONObject,val backup:String):Transfer
        data class Restore(val user:String,val password:CharArray):Transfer
    }
    private var transfer:Transfer?=null
    internal val awaitingFile get()=transfer!=null
    internal val isClosed get()=closed
    private fun current(n:Long)=!closed&&n==generation
    private fun wipe(t:Transfer?){if(t is Transfer.Restore)t.password.fill('\u0000')}
    private fun authenticatedError(e:Exception):Boolean {
        if(e is ApiError&&e.status in listOf(401,403)&&!api.hasSession()){
            mutable.value=ClinicalPreparationState(status="Seu acesso terminou. Entre novamente para continuar.")
            authenticationLost();return true
        };return false
    }
    private fun work(recover:Boolean=false,block:suspend(Long)->Unit):Job? {
        if(closed||mutable.value.busy)return null
        val n=++generation;mutable.value=mutable.value.copy(busy=true)
        return scope.launch(start=CoroutineStart.LAZY){
            try{block(n)}catch(e:CancellationException){throw e}catch(e:Exception){
                if(current(n)&&!authenticatedError(e)){
                    if(!api.sameSession(ownerSession)){mutable.value=ClinicalPreparationState(status="Seu acesso mudou. Abra a consulta novamente pela sua conta.");return@launch}
                    val message=clinicalMessage(e)
                    if(recover)try{load(n)}catch(c:CancellationException){throw c}catch(f:Exception){if(authenticatedError(f))return@launch}
                    if(current(n))mutable.value=mutable.value.copy(status=message)
                }
            }finally{if(current(n)){mutable.value=mutable.value.copy(busy=false);job=null}}
        }.also{job=it;it.start()}
    }
    fun resume(){if(!closed&&!awaitingFile&&!mutable.value.busy)work{load(it)}}
    fun refresh()=work{load(it)}
    fun pause(){
        generation++;job?.cancel();job=null
        mutable.value=ClinicalPreparationState(status="Preparação protegida. Atualize para continuar.")
    }
    fun close(){pause();closed=true;wipe(transfer);transfer=null}
    fun consent(value:Boolean){if(!mutable.value.busy)mutable.value=mutable.value.copy(consent=value)}
    fun answer(name:String,value:String){
        if(mutable.value.busy||value.length>2500||!Regex("q(?:[0-9]|1[0-9])").matches(name))return
        mutable.value=mutable.value.copy(answers=mutable.value.answers+(name to value))
    }
    fun rewrite(){if(!mutable.value.busy)mutable.value=mutable.value.copy(previousFailed=false,answers=emptyMap(),consent=false,status="Preencha as novas respostas. A resposta anterior continua preservada.")}
    private suspend fun load(n:Long){
        val d=withContext(Dispatchers.IO){api.jsonInSession(ownerSession,"GET","clinico/$id")};if(!current(n))return
        // Mantém os controles de restauração disponíveis mesmo se o cofre local falhar.
        mutable.value=ClinicalPreparationState(data=d,busy=true)
        var damaged=false
        val key=try{withContext(Dispatchers.IO){vault(d.getString("usuarioId")).load()}}
            catch(e:CancellationException){throw e}catch(_:Exception){damaged=true;null}
        if(!current(n))return
        var answers=emptyMap<String,String>();var previousFailed=false;var message:String?=null
        val form=d.optJSONObject("formulario")
        if(key!=null&&form!=null&&d.text("resposta").isNotBlank())try{
            val plain=withContext(Dispatchers.Default){ClinicalCrypto.decrypt(d.getString("resposta"),"$id:${form.getInt("id")}",key)}
            answers=plain.keys().asSequence().associateWith{plain.text(it)}
        }catch(e:CancellationException){throw e}catch(e:Exception){previousFailed=true;message=clinicalMessage(e)}
        var records=emptyList<Pair<JSONObject,String>>()
        if(key!=null){
            val rows=withContext(Dispatchers.IO){api.jsonInSession(ownerSession,"GET","clinico/$id/registros").getJSONArray("registros").objects()}
            records=withContext(Dispatchers.Default){rows.map{record->
                ensureActive()
                record to try{
                    val aad="${record.getInt("consultaId")}:registro:${record.getString("id")}:${record.getString("escopo")}:${record.getString("categoria")}:${record.text("retificaId")}"
                    val plain=ClinicalCrypto.decrypt(record.getString("envelope"),aad,key)
                    "${plain.text("texto")}\n${plain.text("psicologo")} · CRP ${plain.text("crp")}"
                }catch(e:CancellationException){throw e}catch(e:Exception){clinicalMessage(e)}
            }}
        }
        if(!current(n))return
        message=message?:when{
            damaged->"Não foi possível abrir a chave deste aparelho. Restaure sua cópia protegida; a chave anterior não foi substituída."
            d.text("impedimento").isNotBlank()->d.text("impedimento")
            key==null->"Crie ou restaure sua chave clínica para abrir as respostas."
            form==null->"O profissional ainda não publicou perguntas para esta consulta."
            d.optBoolean("editar")->"Preencha apenas o que considerar necessário."
            else->"Preparação disponível apenas para leitura."
        }
        mutable.value=ClinicalPreparationState(d,key,records,answers,false,true,previousFailed,damaged,message)
    }
    private suspend fun install(value:JSONObject,n:Long){
        val user=value.getString("usuarioId")
        val verified=withContext(Dispatchers.IO){api.jsonInSession(ownerSession,"GET","clinico/$id")}
        require(verified.getString("usuarioId")==user){"Esta cópia pertence a outra conta."}
        if(!current(n))return
        val registered=withContext(Dispatchers.IO){api.jsonInSession(ownerSession,"POST","clinico/chave",JSONObject().put("publica",value.getJSONObject("publicJwk").toString()))}
        if(!current(n))return
        value.put("id",registered.getString("id"))
        try{withContext(Dispatchers.IO){vault(user).save(value)}}
        catch(e:CancellationException){throw e}
        catch(e:Exception){throw IllegalStateException("Não foi possível guardar a chave neste aparelho. Guarde sua cópia protegida e tente restaurá-la novamente.",e)}
        load(n)
    }
    fun prepareCreate(password:CharArray,open:()->Unit){
        if(closed||mutable.value.busy||awaitingFile||mutable.value.vaultUnavailable){password.fill('\u0000');return}
        val user=mutable.value.data?.text("usuarioId").orEmpty()
        if(user.isBlank()){password.fill('\u0000');return}
        work{n->try{
            val key=withContext(Dispatchers.Default){ClinicalCrypto.generate(user)}
            val backup=withContext(Dispatchers.Default){ClinicalCrypto.backup(key,password)}
            if(current(n)){transfer=Transfer.Create(key,backup);mutable.value=mutable.value.copy(busy=false);try{open()}catch(e:Exception){transfer=null;throw e}}
        }finally{password.fill('\u0000')}}
    }
    fun prepareRestore(password:CharArray,open:()->Unit){
        if(closed||mutable.value.busy||awaitingFile){password.fill('\u0000');return}
        val user=mutable.value.data?.text("usuarioId").orEmpty()
        if(user.isBlank()||password.size<12){password.fill('\u0000');return}
        transfer=Transfer.Restore(user,password)
        try{open()}catch(e:Exception){wipe(transfer);transfer=null;mutable.value=mutable.value.copy(status=clinicalMessage(e))}
    }
    fun cancelFile(){wipe(transfer);transfer=null;work(recover=true){n->load(n);if(current(n))mutable.value=mutable.value.copy(status="Seleção de arquivo cancelada. Sua chave foi mantida.")}}
    fun completeCreate(write:suspend(String)->Unit):Job? {
        val value=transfer as? Transfer.Create?:return null
        // Se ON_START acontecer primeiro, ele verá awaitingFile e não iniciará uma leitura concorrente.
        if(mutable.value.busy)return null
        transfer=null
        return work(recover=true){n->
            write(value.backup)
            if(current(n)){install(value.key,n);if(current(n))mutable.value=mutable.value.copy(status="Chave criada. Guarde a cópia protegida e a senha em locais seguros.")}
        }
    }
    fun completeRestore(read:suspend()->String):Job? {
        val value=transfer as? Transfer.Restore?:return null
        if(mutable.value.busy)return null
        transfer=null
        val result=work(recover=true){n->try{
            val text=read()
            val restored=withContext(Dispatchers.Default){ClinicalCrypto.restore(text,value.password,value.user)}
            if(current(n)){install(restored,n);if(current(n))mutable.value=mutable.value.copy(status="Chave restaurada neste aparelho.")}
        }finally{wipe(value)}}
        if(result==null)wipe(value)
        return result
    }
    fun submit()=work{n->
        val s=mutable.value;val d=s.data?:error("Atualize sua consulta.");val form=d.getJSONObject("formulario");val k=s.key?:error("Configure sua chave clínica.")
        require(s.consent&&d.optBoolean("editar")&&!s.previousFailed&&!d.getJSONArray("revogadas").strings().contains(k.getString("id"))){"Confira a autorização e a situação da preparação."}
        val recipients=d.getJSONArray("chaves")
        require(recipients.objects().any{it.text("usuarioId")==d.text("medicoId")}&&recipients.objects().any{it.text("usuarioId")==d.text("pacienteId")}){"Você e o profissional precisam configurar as chaves antes do envio."}
        val values=JSONObject();s.answers.forEach{(name,text)->values.put(name,text)}
        val envelope=withContext(Dispatchers.Default){ClinicalCrypto.encrypt(values,"$id:${form.getInt("id")}",recipients)}
        withContext(Dispatchers.IO){api.jsonInSession(ownerSession,"POST","clinico/$id/resposta",JSONObject().put("formularioId",form.getInt("id")).put("envelope",envelope).put("consentiu",true))}
        load(n);if(current(n))mutable.value=mutable.value.copy(status="Respostas enviadas com criptografia para você e o profissional.")
    }
}
internal fun clinicalMessage(e:Throwable)=when(e){
    is javax.crypto.AEADBadTagException,is java.security.GeneralSecurityException->"Senha incorreta ou cópia inválida. Confira sua chave clínica."
    else->e.message?:"Não foi possível concluir. Tente novamente."
}
