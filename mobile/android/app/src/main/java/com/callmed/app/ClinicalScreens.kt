package com.callmed.app

import android.app.Activity
import android.content.Context
import android.content.ContextWrapper
import android.view.WindowManager
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.semantics.*
import androidx.compose.ui.text.input.PasswordVisualTransformation
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.unit.dp
import androidx.lifecycle.Lifecycle
import androidx.lifecycle.LifecycleEventObserver
import androidx.lifecycle.compose.LocalLifecycleOwner
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import kotlinx.coroutines.*
import org.json.JSONArray
import org.json.JSONObject

private fun Context.activity(): Activity? = when(this){is Activity->this;is ContextWrapper->baseContext.activity();else->null}
@Composable internal fun PrivateClinicalWindow() {
    val activity=LocalContext.current.activity()
    DisposableEffect(activity){val previous=activity?.window?.attributes?.flags?.and(WindowManager.LayoutParams.FLAG_SECURE)!=0
        activity?.window?.addFlags(WindowManager.LayoutParams.FLAG_SECURE)
        onDispose{if(!previous)activity?.window?.clearFlags(WindowManager.LayoutParams.FLAG_SECURE)}
    }
}
private fun java.io.InputStream.readClinicalBackup():ByteArray { val out=java.io.ByteArrayOutputStream();val buffer=ByteArray(4096);while(true){val n=read(buffer);if(n<0)break;require(out.size()+n<=20000){"A cópia deve ter até 20 KB."};out.write(buffer,0,n)};return out.toByteArray() }
@Composable internal fun ClinicalScreen(s: UiState,api: CallMedApi,provided:ClinicalPreparation?=null,action:(AppAction)->Unit) {
    PrivateClinicalWindow()
    val id=s.journeyAppointment?.id?:return
    val context=LocalContext.current;val localScope=rememberCoroutineScope();val lifecycle=LocalLifecycleOwner.current.lifecycle
    val c=remember(id,api,provided){provided?:ClinicalPreparation(id,api,localScope,{ClinicalVault(context.applicationContext,it)},{action(AppAction.CheckSession)})}
    val state by c.state.collectAsStateWithLifecycle()
    var password by remember(id){mutableStateOf("")}
    var showFingerprints by remember(id){mutableStateOf(false)}
    val create=rememberLauncherForActivityResult(ActivityResultContracts.CreateDocument("application/json")){uri->
        if(uri==null)c.cancelFile()else c.completeCreate{backup->withContext(Dispatchers.IO){
            context.contentResolver.openOutputStream(uri,"wt")?.use{it.write(backup.toByteArray())}?:error("Não foi possível salvar sua cópia.")
        }}
    }
    val restore=rememberLauncherForActivityResult(ActivityResultContracts.OpenDocument()){uri->
        if(uri==null)c.cancelFile()else c.completeRestore{withContext(Dispatchers.IO){
            context.contentResolver.openInputStream(uri)?.use{String(it.readClinicalBackup(),Charsets.UTF_8)}?:error("Não foi possível abrir a cópia.")
        }}
    }
    LaunchedEffect(c){if(lifecycle.currentState.isAtLeast(Lifecycle.State.STARTED))c.resume()}
    DisposableEffect(lifecycle,c){val observer=LifecycleEventObserver{_,event->
        if(event==Lifecycle.Event.ON_STOP){password="";showFingerprints=false;c.pause()}
        if(event==Lifecycle.Event.ON_START)c.resume()
    };lifecycle.addObserver(observer);onDispose{
        lifecycle.removeObserver(observer);password=""
        if(context.activity()?.isChangingConfigurations==true)c.pause()else c.close()
    }}
    val busy=state.busy;val data=state.data;val key=state.key
    Page{
        PageTitle("PREPARAÇÃO SEGURA","Perguntas do seu profissional","As respostas são criptografadas neste aparelho para você e o profissional.")
        Text(state.status,Modifier.semantics{liveRegion=LiveRegionMode.Polite})
        if(busy)LinearProgressIndicator(Modifier.fillMaxWidth().semantics{contentDescription="Atualizando preparação clínica"})
        if(data!=null)Panel{
            Text("Sua chave clínica",style=MaterialTheme.typography.titleMedium)
            Muted(if(key==null)"Se já respondeu pelo site, restaure a cópia da chave para ler as respostas anteriores." else "Chave protegida neste aparelho. Para abrir conteúdo cifrado com outra chave, restaure a respectiva cópia.")
            OutlinedTextField(password,{password=it},label={Text("Senha da cópia protegida")},visualTransformation=PasswordVisualTransformation(),singleLine=true,enabled=!busy,modifier=Modifier.fillMaxWidth(),keyboardOptions=KeyboardOptions(keyboardType=KeyboardType.Password,autoCorrectEnabled=false))
            if((key==null||data.optJSONArray("revogadas")?.strings()?.contains(key.text("id"))==true)&&!state.vaultUnavailable)Secondary("Criar chave e salvar cópia",!busy&&password.length>=12){
                val pass=password.toCharArray();password="";c.prepareCreate(pass){create.launch("CallMed-chave-clinica-protegida.json")}
            }
            Secondary("Restaurar cópia da chave",!busy&&password.length>=12){
                val pass=password.toCharArray();password="";c.prepareRestore(pass){restore.launch(arrayOf("application/json","text/plain","application/octet-stream"))}
            }
        }
        val f=data?.optJSONObject("formulario")
        if(data!=null&&f!=null&&key!=null){
            val revoked=data.getJSONArray("revogadas").strings().contains(key.getString("id"))
            val editable=data.optBoolean("editar")&&!revoked&&!state.previousFailed
            Panel{
                Text(f.text("titulo"),style=MaterialTheme.typography.titleMedium)
                if(revoked)Notice("Chave revogada. Permite ler respostas anteriores. Restaure ou crie uma chave ativa para os próximos envios.")
                if(state.previousFailed&&data.optBoolean("editar"))Secondary("Preencher novamente sem abrir a resposta anterior",!busy){c.rewrite()}
                JSONArray(f.getString("perguntas")).strings().forEachIndexed{i,q->
                    OutlinedTextField(state.answers["q$i"].orEmpty(),{c.answer("q$i",it)},label={Text(q)},readOnly=!editable,enabled=!busy,modifier=Modifier.fillMaxWidth(),minLines=3,keyboardOptions=KeyboardOptions(autoCorrectEnabled=false))
                }
                Secondary("Conferir chaves dos destinatários",!busy){showFingerprints=!showFingerprints}
                if(showFingerprints){Muted("Confira a impressão da chave do profissional por um canal independente.");data.getJSONArray("chaves").objects().forEach{recipient->Text((if(recipient.text("usuarioId")==data.text("medicoId"))"Profissional: " else "Paciente: ")+recipient.text("impressao"))}}
                if(editable){
                    Row{Checkbox(state.consent,{c.consent(it)},enabled=!busy,modifier=Modifier.semantics{contentDescription="Autorizo o envio das respostas ao profissional responsável"});Text("Autorizo o envio destas respostas ao profissional responsável.",Modifier.weight(1f).padding(top=12.dp))}
                    Primary("Criptografar e enviar respostas",!busy&&state.consent){c.submit()}
                }
            }
        }
        state.records.forEach{(r,text)->Panel{Text("Prontuário · ${r.text("categoria")}",style=MaterialTheme.typography.titleMedium);Text(r.text("criadoEm"));if(r.text("retificaId").isNotBlank())Muted("Retificação de registro anterior");Text(text)}}
        Secondary("Atualizar preparação",!busy&&!c.awaitingFile){c.refresh()}
        Secondary("Voltar à consulta",!busy){action(AppAction.Open(Screen.JOURNEY))}
    }
}

@Composable internal fun PrivacyScreen(api:CallMedApi,action:(AppAction)->Unit){
    PrivateClinicalWindow()
    val scope=rememberCoroutineScope();var rows by remember{mutableStateOf<List<JSONObject>>(emptyList())};var type by remember{mutableStateOf("Acesso")}
    var confirm by remember{mutableStateOf(false)};var busy by remember{mutableStateOf(false)};var status by remember{mutableStateOf("")}
    fun update(){busy=true;scope.launch{try{rows=withContext(Dispatchers.IO){api.array("privacidade").objects()}}catch(e:CancellationException){throw e}catch(e:Exception){if(e is ApiError&&e.status in listOf(401,403))action(AppAction.CheckSession);status=e.message?:"Não foi possível carregar os pedidos."}finally{busy=false}}}
    LaunchedEffect(Unit){update()}
    Page{PageTitle("MINHA CONTA","Meus dados e privacidade")
        Muted("Solicite acesso, correção, análise de eliminação ou revogação de consentimento. A clínica responderá por esta área autenticada. O pedido não apaga automaticamente registros sujeitos a guarda profissional ou obrigação legal.")
        Text(status,Modifier.semantics{liveRegion=LiveRegionMode.Polite})
        listOf("Acesso" to "Acesso e cópia dos meus dados","Correcao" to "Correção de dados","Eliminacao" to "Análise de eliminação","Revogacao" to "Revogação de consentimento").forEach{(value,label)->FilterChip(selected=type==value,onClick={type=value;confirm=false},label={Text(label)},enabled=!busy,modifier=Modifier.fillMaxWidth().heightIn(min=48.dp))}
        Row{Checkbox(confirm,{confirm=it},enabled=!busy,modifier=Modifier.semantics{contentDescription="Quero registrar este pedido em meu nome"});Text("Quero registrar este pedido em meu nome.",Modifier.weight(1f).padding(top=12.dp))}
        Primary("Registrar pedido",confirm&&!busy){busy=true;scope.launch{try{val r=withContext(Dispatchers.IO){api.json("POST","privacidade",JSONObject().put("tipo",type).put("confirmar",true))};status=r.text("mensagem");confirm=false;rows=withContext(Dispatchers.IO){api.array("privacidade").objects()}}catch(e:CancellationException){throw e}catch(e:Exception){if(e is ApiError&&e.status in listOf(401,403))action(AppAction.CheckSession);status=e.message?:"Não foi possível registrar o pedido."}finally{busy=false}}}
        rows.forEach{p->Panel{Text("${p.text("tipo")} · ${p.text("estado")}",style=MaterialTheme.typography.titleMedium);Muted("Protocolo: ${p.text("id")}");Text(p.text("resposta","Aguardando análise da clínica."))}}
        Secondary("Atualizar pedidos",!busy){update()};Secondary("Voltar à conta",!busy){action(AppAction.Open(Screen.ACCOUNT))}
    }
}
