package com.callmed.app

import android.content.Context
import android.media.*
import android.os.Build
import android.os.Handler
import android.os.Looper

internal interface CallAudio {
    fun start(interrupted:(String)->Unit)
    fun stop()
}
internal data class CommunicationOutput(val id:Int,val type:Int)
internal object CommunicationRouting {
    fun privateOutput(type:Int)=type in setOf(
        AudioDeviceInfo.TYPE_WIRED_HEADSET,AudioDeviceInfo.TYPE_WIRED_HEADPHONES,
        AudioDeviceInfo.TYPE_USB_HEADSET,AudioDeviceInfo.TYPE_USB_DEVICE,
        AudioDeviceInfo.TYPE_BLUETOOTH_SCO,AudioDeviceInfo.TYPE_BLUETOOTH_A2DP,
        23,26,27 // HEARING_AID (API 28), BLE_HEADSET e BLE_SPEAKER (API 31).
    )
    fun choose(devices:List<CommunicationOutput>,current:Int?):CommunicationOutput? =
        devices.firstOrNull{it.id==current&&privateOutput(it.type)}
            ?:devices.firstOrNull{privateOutput(it.type)}
            ?:devices.firstOrNull{it.type==AudioDeviceInfo.TYPE_BUILTIN_SPEAKER}
            ?:devices.firstOrNull{it.type==AudioDeviceInfo.TYPE_BUILTIN_EARPIECE}
}

// Usa a API de comunicação no Android 12+. Nas versões anteriores, mantém o
// roteamento de fones do sistema em vez de forçar o alto-falante.
internal class AndroidCallAudio(context:Context):CallAudio {
    private val manager=context.getSystemService(Context.AUDIO_SERVICE) as AudioManager
    private val handler=Handler(Looper.getMainLooper())
    private var focus:AudioFocusRequest?=null
    private var configured=false
    private var oldMode=AudioManager.MODE_NORMAL
    private var oldSpeaker=false
    private var chosenPrivate:Int?=null
    private var previousDevice:AudioDeviceInfo?=null
    private var callback:AudioDeviceCallback?=null
    private var epoch=0L
    override fun start(interrupted:(String)->Unit){
        check(!configured&&focus==null){"O áudio desta chamada já está em uso."}
        oldMode=manager.mode;oldSpeaker=legacySpeaker();val ticket=++epoch
        val request=AudioFocusRequest.Builder(AudioManager.AUDIOFOCUS_GAIN_TRANSIENT)
            .setAudioAttributes(AudioAttributes.Builder().setUsage(AudioAttributes.USAGE_VOICE_COMMUNICATION).setContentType(AudioAttributes.CONTENT_TYPE_SPEECH).build())
            .setOnAudioFocusChangeListener({change->if(configured&&ticket==epoch&&(change==AudioManager.AUDIOFOCUS_LOSS||change==AudioManager.AUDIOFOCUS_LOSS_TRANSIENT))interrupted("Chamada interrompida por outro áudio ou ligação. Entre novamente para continuar.")},handler).build()
        focus=request
        try{
            check(manager.requestAudioFocus(request)==AudioManager.AUDIOFOCUS_REQUEST_GRANTED){"O áudio está em uso por outra ligação. Encerre-a e tente novamente."}
            if(Build.VERSION.SDK_INT>=31)previousDevice=manager.communicationDevice
            configured=true;manager.mode=AudioManager.MODE_IN_COMMUNICATION
            if(Build.VERSION.SDK_INT>=31){
                val available=manager.availableCommunicationDevices
                val chosen=CommunicationRouting.choose(available.map{CommunicationOutput(it.id,it.type)},previousDevice?.id)
                val device=available.firstOrNull{it.id==chosen?.id&&it.type==chosen.type}
                check(device!=null&&manager.setCommunicationDevice(device)){"Não foi possível selecionar o áudio. Confira seus fones e tente novamente."}
                chosenPrivate=if(CommunicationRouting.privateOutput(device.type))device.id else null
            }else{
                val privateDevice=manager.getDevices(AudioManager.GET_DEVICES_OUTPUTS).firstOrNull{CommunicationRouting.privateOutput(it.type)}
                setLegacySpeaker(privateDevice==null);chosenPrivate=privateDevice?.id
            }
            callback=object:AudioDeviceCallback(){
                override fun onAudioDevicesAdded(added:Array<out AudioDeviceInfo>){
                    if(!configured||ticket!=epoch||chosenPrivate!=null||added.none{CommunicationRouting.privateOutput(it.type)})return
                    try{
                        if(Build.VERSION.SDK_INT>=31){
                            val output=manager.availableCommunicationDevices.firstOrNull{CommunicationRouting.privateOutput(it.type)&&added.any{a->a.id==it.id}}
                            check(output!=null&&manager.setCommunicationDevice(output));chosenPrivate=output.id
                        }else{setLegacySpeaker(false);chosenPrivate=added.first{CommunicationRouting.privateOutput(it.type)}.id}
                    }catch(_:Exception){interrupted("Não foi possível trocar para os fones. Confira o áudio e entre novamente.")}
                }
                override fun onAudioDevicesRemoved(removed:Array<out AudioDeviceInfo>){
                    if(configured&&ticket==epoch&&chosenPrivate!=null&&removed.any{it.id==chosenPrivate})interrupted("Fone desconectado. A chamada foi encerrada; confira o áudio antes de entrar novamente.")
                }
            }.also{manager.registerAudioDeviceCallback(it,handler)}
        }catch(e:Exception){stop();throw e}
    }
    override fun stop(){
        val restore=configured;configured=false;epoch++;chosenPrivate=null
        callback?.let{runCatching{manager.unregisterAudioDeviceCallback(it)}};callback=null
        focus?.let{runCatching{manager.abandonAudioFocusRequest(it)}};focus=null
        if(restore){
            if(Build.VERSION.SDK_INT>=31)runCatching{manager.clearCommunicationDevice()}
            else runCatching{setLegacySpeaker(oldSpeaker)}
            runCatching{manager.mode=oldMode}
        }
        previousDevice=null
    }
    @Suppress("DEPRECATION")private fun legacySpeaker()=if(Build.VERSION.SDK_INT<31)manager.isSpeakerphoneOn else false
    @Suppress("DEPRECATION")private fun setLegacySpeaker(value:Boolean){manager.isSpeakerphoneOn=value}
}
