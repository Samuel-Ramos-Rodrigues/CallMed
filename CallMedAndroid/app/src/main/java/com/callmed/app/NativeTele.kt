package com.callmed.app

import android.Manifest
import android.content.Context
import android.content.pm.PackageManager
import android.media.AudioManager
import android.media.AudioAttributes
import android.media.AudioFocusRequest
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.foundation.layout.*
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.semantics.*
import androidx.compose.ui.unit.dp
import androidx.compose.ui.viewinterop.AndroidView
import androidx.lifecycle.Lifecycle
import androidx.lifecycle.LifecycleEventObserver
import androidx.lifecycle.repeatOnLifecycle
import androidx.lifecycle.compose.LocalLifecycleOwner
import kotlinx.coroutines.*
import org.json.JSONObject
import org.webrtc.*
import java.util.UUID
import java.util.concurrent.atomic.AtomicBoolean
import kotlin.coroutines.resume
import kotlin.coroutines.resumeWithException

internal class NativeTele(private val context:Context,private val api:CallMedApi,private val id:Int,
 private val scope:CoroutineScope,private val status:(String)->Unit,private val running:(Boolean)->Unit,
 private val authenticationLost:()->Unit={},private val callAudio:CallAudio=AndroidCallAudio(context)) {
    companion object { private val initialized=AtomicBoolean() }
    private val eglHolder=lazy { EglBase.create() }
    val egl:EglBase get()=eglHolder.value
    private var factory:PeerConnectionFactory?=null;private var pc:PeerConnection?=null
    private var capturer:CameraVideoCapturer?=null;private var helper:SurfaceTextureHelper?=null
    private var videoSource:VideoSource?=null;private var audioSource:AudioSource?=null
    private var video:VideoTrack?=null;private var audio:AudioTrack?=null;private var remoteTrack:VideoTrack?=null
    private val releasedRenderers=java.util.Collections.newSetFromMap(java.util.IdentityHashMap<SurfaceViewRenderer,Boolean>())
    private var localView:SurfaceViewRenderer?=null;private var remoteView:SurfaceViewRenderer?=null
    private var loop:Job?=null;private var active=false;private val attempts=CallAttempt()
    internal val joining get()=attempts.occupied
    private var presenceSession:String?=null;private var presenceState="aguardando"
    private var own="";private var other:String?=null;private var sessionLease=0L;private var offered=false;private var cursor=0L
    private val pendingIce=mutableListOf<IceCandidate>();private val localIce=mutableListOf<IceCandidate>()
    fun renderer(local:Boolean):SurfaceViewRenderer = SurfaceViewRenderer(context).apply {
        init(egl.eglBaseContext,null);setEnableHardwareScaler(true);setMirror(local)
        if(local){localView=this;video?.addSink(this)}else{remoteView=this;remoteTrack?.addSink(this)}
    }
    fun releaseRenderer(view:SurfaceViewRenderer,local:Boolean){if(!releasedRenderers.add(view))return;if(local){video?.removeSink(view);if(localView===view)localView=null}else{remoteTrack?.removeSink(view);if(remoteView===view)remoteView=null};view.release()}
    private suspend fun send(type:String,data:JSONObject?=null,destination:String?=other,sender:String=own,lease:Long=sessionLease){
        val payload=teleSignal(type,data,sender,destination)
        withContext(Dispatchers.IO){api.jsonInSession(lease,"POST","clinico/$id/tele/sinais",JSONObject().put("payload",payload))}
    }
    private fun iceJson(c:IceCandidate)=JSONObject().put("candidate",c.sdp).put("sdpMid",c.sdpMid).put("sdpMLineIndex",c.sdpMLineIndex)
    private suspend fun flushLocal(){val all=localIce.toList();localIce.clear();for(c in all)send("ice",iceJson(c))}
    private suspend fun description(create:Boolean,description:SessionDescription?=null):SessionDescription? = suspendCancellableCoroutine { continuation ->
        val observer=object:SdpObserver {
            override fun onCreateSuccess(s:SessionDescription){if(continuation.isActive)continuation.resume(s)}
            override fun onSetSuccess(){if(continuation.isActive)continuation.resume(null)}
            override fun onCreateFailure(e:String){if(continuation.isActive)continuation.resumeWithException(IllegalStateException("Não foi possível preparar a conexão de vídeo."))}
            override fun onSetFailure(e:String){if(continuation.isActive)continuation.resumeWithException(IllegalStateException("Não foi possível negociar a conexão de vídeo."))}
        }
        val connection=pc
        if(connection==null){continuation.resumeWithException(IllegalStateException("Chamada encerrada."));return@suspendCancellableCoroutine}
        if(create)connection.createOffer(observer,MediaConstraints())
        else if(description?.type==SessionDescription.Type.OFFER)connection.setLocalDescription(observer,description)
        else connection.setRemoteDescription(observer,description)
    }
    suspend fun start(){
        val attempt=attempts.begin()?:return;val lease=api.sessionStamp();sessionLease=lease;running(true);status("Preparando câmera e microfone…")
        try{
            val cfg=withContext(Dispatchers.IO){api.jsonInSession(lease,"GET","clinico/$id/tele/config")}
            if(!attempts.owns(attempt))return
            withContext(Dispatchers.IO){api.jsonInSession(lease,"POST","clinico/$id/tele/entrar",JSONObject())}
            if(!attempts.owns(attempt))return
            if(initialized.compareAndSet(false,true))PeerConnectionFactory.initialize(PeerConnectionFactory.InitializationOptions.builder(context.applicationContext).setEnableInternalTracer(false).createInitializationOptions())
            factory=PeerConnectionFactory.builder().setVideoEncoderFactory(DefaultVideoEncoderFactory(egl.eglBaseContext,true,true))
                .setVideoDecoderFactory(DefaultVideoDecoderFactory(egl.eglBaseContext)).createPeerConnectionFactory()
            val servers=cfg.getJSONArray("iceServers").objects().map{j->
                val urls=j.optJSONArray("urls")?.strings()?:listOf(j.getString("urls"))
                PeerConnection.IceServer.builder(urls).setUsername(j.text("username")).setPassword(j.text("credential")).createIceServer()
            }
            own=UUID.randomUUID().toString();other=null;offered=false;cursor=cfg.optLong("cursor");active=true
            val config=PeerConnection.RTCConfiguration(servers).apply{sdpSemantics=PeerConnection.SdpSemantics.UNIFIED_PLAN}
            pc=factory!!.createPeerConnection(config,object:PeerConnection.Observer{
                override fun onSignalingChange(s:PeerConnection.SignalingState){}
                override fun onIceConnectionChange(s:PeerConnection.IceConnectionState){scope.launch{if(active&&attempts.owns(attempt))when(s){
                    PeerConnection.IceConnectionState.CONNECTED,PeerConnection.IceConnectionState.COMPLETED->{presenceState="conectada";status("Chamada conectada.")}
                    PeerConnection.IceConnectionState.DISCONNECTED->{presenceState="interrompida";status("Conexão interrompida. Aguarde ou saia e entre novamente.")}
                    PeerConnection.IceConnectionState.FAILED->{stop(false);status("Não foi possível conectar as redes. Tente novamente ou entre em contato com a clínica.")}
                    else->{}
                }}}
                override fun onIceConnectionReceivingChange(p:Boolean){}
                override fun onIceGatheringChange(s:PeerConnection.IceGatheringState){}
                override fun onIceCandidate(c:IceCandidate){scope.launch{if(active&&attempts.owns(attempt)){if(other==null)localIce.add(c)else try{send("ice",iceJson(c))}catch(_:Exception){if(active&&attempts.owns(attempt))status("A conexão com a sala foi interrompida.")}}}}
                override fun onIceCandidatesRemoved(c:Array<out IceCandidate>){}
                override fun onAddStream(s:MediaStream){}
                override fun onRemoveStream(s:MediaStream){}
                override fun onDataChannel(c:DataChannel){}
                override fun onRenegotiationNeeded(){}
                override fun onAddTrack(receiver:RtpReceiver,streams:Array<out MediaStream>){scope.launch{
                    if(active&&attempts.owns(attempt)&&(receiver.track() is VideoTrack)){remoteTrack=receiver.track() as VideoTrack;remoteView?.let{remoteTrack?.addSink(it)}}
                }}
            })?:error("Não foi possível criar a conexão de vídeo.")
            val enumerator=Camera2Enumerator(context)
            val device=enumerator.deviceNames.firstOrNull(enumerator::isFrontFacing)?:enumerator.deviceNames.firstOrNull()?:error("Câmera indisponível neste aparelho.")
            capturer=enumerator.createCapturer(device,null)?:error("Câmera indisponível.")
            helper=SurfaceTextureHelper.create("CallMedCamera",egl.eglBaseContext)
            videoSource=factory!!.createVideoSource(false);capturer!!.initialize(helper,context,videoSource!!.capturerObserver)
            capturer!!.startCapture(640,480,24)
            video=factory!!.createVideoTrack("callmed-video",videoSource).also{localView?.let(it::addSink);pc!!.addTrack(it,listOf("callmed"))}
            audioSource=factory!!.createAudioSource(MediaConstraints());audio=factory!!.createAudioTrack("callmed-audio",audioSource).also{pc!!.addTrack(it,listOf("callmed"))}
            callAudio.start{message->scope.launch{if(attempts.owns(attempt)){stop();status(message)}}}

            val registration=withContext(Dispatchers.IO){api.jsonInSession(lease,"POST","clinico/$id/tele/presenca/entrar",JSONObject())}.getString("sessaoId")
            if(!attempts.owns(attempt)){leavePresence(registration,lease);return};presenceSession=registration;presenceState="aguardando"
            send("ready",destination=null);status("Aguardando o profissional entrar na chamada.")
            loop=scope.launch{var rounds=0;var presenceRounds=0
                try{while(active&&attempts.owns(attempt)){
                    val rows=withContext(Dispatchers.IO){api.arrayInSession(lease,"clinico/$id/tele/sinais?depois=$cursor").objects()}
                    for(row in rows){if(!active||!attempts.owns(attempt))break;cursor=maxOf(cursor,row.getLong("id"));val signal=JSONObject(row.getString("payload"))
                        val emitter=signal.text("emissor");val destination=signal.text("destino")
                        if(emitter==own||destination.isNotBlank()&&destination!=own)continue
                        if(other!=null&&other!=emitter)continue
                        when(signal.text("tipo")){
                            "ready"->{other=emitter;if(!offered){offered=true;val offer=description(true)!!;description(false,offer);send("offer",JSONObject().put("type","offer").put("sdp",offer.description));flushLocal()}}
                            "answer"->if(offered&&other==emitter){val answer=signal.getJSONObject("data");description(false,SessionDescription(SessionDescription.Type.ANSWER,answer.getString("sdp")));pendingIce.forEach{pc?.addIceCandidate(it)};pendingIce.clear()}
                            "ice"->{val c=signal.getJSONObject("data");val candidate=IceCandidate(c.text("sdpMid"),c.getInt("sdpMLineIndex"),c.getString("candidate"));if(pc?.remoteDescription!=null)pc?.addIceCandidate(candidate)else pendingIce.add(candidate)}
                            "bye"->{stop(false);status("O profissional saiu. Se precisar continuar, entre na chamada novamente.")}
                        }
                    }
                    if(attempts.owns(attempt)&&!offered&&++rounds%7==0)send("ready",destination=null)
                    if(active&&attempts.owns(attempt)&&presenceRounds++%10==0){val session=presenceSession;if(session!=null)withContext(Dispatchers.IO){api.jsonInSession(lease,"POST","clinico/$id/tele/presenca",JSONObject().put("sessaoId",session).put("estado",presenceState))}}
                    delay(1500)
                }}catch(e:CancellationException){throw e}catch(e:Exception){if(attempts.owns(attempt)){stop(false);if(e is ApiError&&e.status in listOf(401,403)&&!api.hasSession())authenticationLost();status(if(e is ApiError)e.message?:"Acesso à sala encerrado." else "A conexão com a sala foi interrompida. Confira a internet e entre novamente.")}}
            }
        }catch(e:CancellationException){if(attempts.owns(attempt))stop(false);throw e}catch(e:Exception){if(attempts.owns(attempt)){stop(false);if(e is ApiError&&e.status in listOf(401,403)&&!api.hasSession())authenticationLost();status(e.message?:"Não foi possível iniciar a chamada. Confira câmera, microfone e conexão.")}}
    }
    private suspend fun leavePresence(session:String,lease:Long){withContext(Dispatchers.IO){runCatching{api.jsonInSession(lease,"POST","clinico/$id/tele/presenca/sair",JSONObject().put("sessaoId",session))}}}
    fun camera(enabled:Boolean){video?.setEnabled(enabled)}
    fun microphone(enabled:Boolean){audio?.setEnabled(enabled)}
    fun flip(){capturer?.switchCamera(null)}
    fun stop(notify:Boolean=true){
        val previousPresence=presenceSession;presenceSession=null
        val wasActive=active;val previousOwn=own;val previousOther=other;val previousLease=sessionLease;attempts.end();active=false;loop?.cancel();loop=null
        if(previousPresence!=null)scope.launch{leavePresence(previousPresence,previousLease)}
        if(notify&&wasActive)scope.launch{runCatching{send("bye",destination=previousOther,sender=previousOwn,lease=previousLease)}}
        // RtpReceiver.dispose() também destrói seu cachedTrack. Desconecte os
        // renderizadores antes de PeerConnection.dispose(), inclusive no erro.
        runCatching{video?.setEnabled(false)};runCatching{audio?.setEnabled(false)}
        runCatching{video?.let{v->localView?.let(v::removeSink)}}
        runCatching{remoteTrack?.let{v->remoteView?.let(v::removeSink)}};remoteTrack=null
        val connection=pc;pc=null;runCatching{connection?.close()};runCatching{connection?.dispose()}
        val capture=capturer;capturer=null;runCatching{capture?.stopCapture()};runCatching{capture?.dispose()}
        val localVideo=video;video=null;runCatching{localVideo?.dispose()}
        val localAudio=audio;audio=null;runCatching{localAudio?.dispose()}
        val videoInput=videoSource;videoSource=null;runCatching{videoInput?.dispose()}
        val audioInput=audioSource;audioSource=null;runCatching{audioInput?.dispose()}
        val texture=helper;helper=null;runCatching{texture?.dispose()}
        val previousFactory=factory;factory=null;runCatching{previousFactory?.dispose()}
        pendingIce.clear();localIce.clear();runCatching{callAudio.stop()}
        running(false)
    }
    fun release(){stop();localView?.let{releaseRenderer(it,true)};remoteView?.let{releaseRenderer(it,false)};if(eglHolder.isInitialized())egl.release()}
}

@Composable internal fun NativeTeleScreen(s:UiState,api:CallMedApi,action:(AppAction)->Unit){
    PrivateClinicalWindow()
    val id=s.journeyAppointment?.id?:return;val context=LocalContext.current;val scope=rememberCoroutineScope();val lifecycle=LocalLifecycleOwner.current.lifecycle
    var state by remember(id){mutableStateOf<JSONObject?>(null)};var status by remember(id){mutableStateOf("Conferindo sua consulta…")}
    var active by remember(id){mutableStateOf(false)};var agree by remember(id){mutableStateOf(false)};var busy by remember(id){mutableStateOf(false)}
    var camera by remember(id){mutableStateOf(true)};var microphone by remember(id){mutableStateOf(true)}
    val tele=remember(id,api){NativeTele(context,api,id,scope,{status=it},{active=it},{action(AppAction.CheckSession)})}
    suspend fun update(){state=withContext(Dispatchers.IO){api.json("GET","clinico/$id/tele")};status=when(state!!.text("estado")){
        "disponivel"->"Sala liberada. Entre quando estiver em um ambiente privativo."
        "consentimento"->"Leia e aceite o consentimento para esta sessão."
        "aguardando-horario"->"A sala abre 30 minutos antes da consulta. Aguarde o horário informado."
        "presencial"->"Esta consulta está marcada como presencial. Confirme a modalidade com a clínica."
        "encerrada","prazo-encerrado"->"O acesso a esta consulta terminou. Confira seus próximos horários."
        else->state!!.text("bloqueio","A clínica precisa revisar a liberação desta consulta.")
    }}
    fun enter(){if(busy)return;camera=true;microphone=true;scope.launch{tele.start()}}
    val permission=rememberLauncherForActivityResult(ActivityResultContracts.RequestMultiplePermissions()){granted->
        if(listOf(Manifest.permission.CAMERA,Manifest.permission.RECORD_AUDIO).all{granted[it]==true})enter()
        else status="Permita câmera e microfone nas configurações do Android para entrar na chamada."
    }
    LaunchedEffect(id,lifecycle){lifecycle.repeatOnLifecycle(Lifecycle.State.STARTED){
        while(true){
            if(!active&&!busy)try{update()}catch(e:CancellationException){throw e}catch(e:Exception){
                if(e is ApiError&&e.status in listOf(401,403))action(AppAction.CheckSession)
                status=e.message?:"Não foi possível conferir a consulta. A atualização será tentada novamente."
            }
            delay(15000)
        }
    }}
    DisposableEffect(lifecycle,tele){val observer=LifecycleEventObserver{_,event->if(event==Lifecycle.Event.ON_STOP){tele.stop();status="Câmera e microfone desligados ao sair do aplicativo. Entre novamente para continuar."}}
        lifecycle.addObserver(observer);onDispose{lifecycle.removeObserver(observer);tele.release()}}
    Page{
        PageTitle("CONSULTA ONLINE","Sua sessão com o profissional")
        s.journeyAppointment?.let{Text("${it.medico}\n${dateLabel(it.data)} às ${it.horario}")}
        Text(status,Modifier.semantics{liveRegion=LiveRegionMode.Polite})
        if(active){
            Text("Vídeo do profissional",Modifier.semantics{heading()})
            AndroidView(factory={tele.renderer(false)},modifier=Modifier.fillMaxWidth().height(230.dp).semantics{contentDescription="Vídeo do profissional"},onRelease={tele.releaseRenderer(it,false)})
            Text("Sua câmera")
            AndroidView(factory={tele.renderer(true)},modifier=Modifier.fillMaxWidth().height(150.dp).semantics{contentDescription="Prévia da sua câmera"},onRelease={tele.releaseRenderer(it,true)})
            Secondary(if(camera)"Desligar câmera" else "Ligar câmera",true){camera=!camera;tele.camera(camera)}
            Secondary(if(microphone)"Desligar microfone" else "Ligar microfone",true){microphone=!microphone;tele.microphone(microphone)}
            Secondary("Alternar câmera",true){tele.flip()}
            Primary("Sair da chamada",true){tele.stop();status="Você saiu da chamada."}
        }else{
            state?.let{d->
                if(d.optBoolean("podeConsentir"))Panel{
                    Text("Consentimento para esta sessão",style=MaterialTheme.typography.titleMedium)
                    Text(d.getJSONObject("consentimento").getString("texto"))
                    Row{Checkbox(agree,{agree=it},enabled=!busy,modifier=Modifier.semantics{contentDescription="Li e aceito o consentimento para esta sessão"});Text("Li e aceito o consentimento para esta sessão.",Modifier.weight(1f).padding(top=12.dp))}
                    Primary("Aceitar consentimento",agree&&!busy){busy=true;scope.launch{try{
                        withContext(Dispatchers.IO){api.json("POST","clinico/$id/tele/consentimento",JSONObject().put("confirmar",true).put("versao",d.getJSONObject("consentimento").getString("versao")))};agree=false;update()
                    }catch(e:CancellationException){throw e}catch(e:Exception){if(e is ApiError&&e.status in listOf(401,403))action(AppAction.CheckSession);status=e.message?:"Não foi possível registrar o consentimento."}finally{busy=false}}}
                }
                if(d.text("abertura").isNotBlank())Muted("Horários da clínica: de ${teleTime(d.text("abertura"))} até ${teleTime(d.text("encerramento"))}.")
                Primary("Entrar na chamada",d.optBoolean("liberada")&&!busy){
                    if(listOf(Manifest.permission.CAMERA,Manifest.permission.RECORD_AUDIO).all{context.checkSelfPermission(it)==PackageManager.PERMISSION_GRANTED})enter()
                    else permission.launch(arrayOf(Manifest.permission.CAMERA,Manifest.permission.RECORD_AUDIO))
                }
            }
            Secondary("Atualizar situação",!busy){busy=true;scope.launch{try{update()}catch(e:CancellationException){throw e}catch(e:Exception){if(e is ApiError&&e.status in listOf(401,403))action(AppAction.CheckSession);status=e.message?:"Não foi possível atualizar."}finally{busy=false}}}
            Secondary("Voltar à consulta",!busy){action(AppAction.Open(Screen.JOURNEY))}
        }
    }
}
internal fun teleTime(value:String)=runCatching{java.time.LocalDateTime.parse(value.take(19)).format(java.time.format.DateTimeFormatter.ofPattern("dd/MM 'às' HH:mm"))}.getOrDefault(value)
