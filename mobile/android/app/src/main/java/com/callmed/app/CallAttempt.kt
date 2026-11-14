package com.callmed.app
import org.json.JSONObject

// Ticket de uma tentativa: callbacks de câmera, SDP, ICE ou áudio só podem
// afetar a chamada que os originou, inclusive durante uma nova entrada.
internal class CallAttempt {
    private var next=0L
    private var current:Long?=null
    val occupied get()=current!=null
    fun begin():Long? {if(current!=null)return null;return (++next).also{current=it}}
    fun owns(ticket:Long)=current==ticket
    fun end():Long?=current.also{current=null;next++}
}

internal fun teleSignal(type:String,data:JSONObject?,sender:String,destination:String?):String = JSONObject().put("tipo",type).put("data",data?:JSONObject.NULL).put("emissor",sender).put("destino",destination?:JSONObject.NULL).toString()
