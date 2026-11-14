package com.callmed.app
import android.content.Context
import android.media.AudioDeviceInfo
import android.media.AudioManager
import org.json.JSONObject
import org.junit.Assert.*
import org.junit.Test
import org.junit.runner.RunWith
import org.robolectric.RobolectricTestRunner
import org.robolectric.RuntimeEnvironment
import org.robolectric.Shadows.shadowOf
import org.robolectric.annotation.Config
import org.robolectric.annotation.LooperMode
import org.robolectric.shadows.AudioDeviceInfoBuilder

class CallAttemptTest {
 @Test fun doubleJoinCannotStartTwoPreparations(){val a=CallAttempt();assertNotNull(a.begin());assertNull(a.begin());assertTrue(a.occupied)}
 @Test fun oldCallbacksLoseAuthorityAfterLeavingAndRejoining(){val a=CallAttempt();val old=a.begin()!!;a.end();val next=a.begin()!!;assertFalse(a.owns(old));assertTrue(a.owns(next));assertNotEquals(old,next)}
 @Test fun repeatedStopIsSafeAndDoesNotReuseTickets(){val a=CallAttempt();val old=a.begin()!!;a.end();a.end();val next=a.begin()!!;assertFalse(a.owns(old));assertTrue(a.owns(next))}
 @Test fun terminalSignalKeepsTheIdentityOfItsOriginalCall(){val old=teleSignal("bye",null,"call-1","psych-1");val new=teleSignal("ready",null,"call-2",null);assertEquals("call-1",JSONObject(old).getString("emissor"));assertEquals("psych-1",JSONObject(old).getString("destino"));assertTrue(JSONObject(new).isNull("destino"))}
}
@RunWith(RobolectricTestRunner::class)
@Config(sdk=[35])
@LooperMode(LooperMode.Mode.PAUSED)
class CallAudioTest {
 private fun device(type:Int)=AudioDeviceInfoBuilder.newBuilder().setType(type).build()
 private fun manager()=RuntimeEnvironment.getApplication().getSystemService(Context.AUDIO_SERVICE) as AudioManager
 @Test fun modernAudioKeepsConnectedHeadphonesAndClearsItsSelectionOnStop(){val m=manager();val shadow=shadowOf(m);val speaker=device(AudioDeviceInfo.TYPE_BUILTIN_SPEAKER);val headset=device(AudioDeviceInfo.TYPE_WIRED_HEADSET);shadow.setAvailableCommunicationDevices(listOf(speaker,headset));val a=AndroidCallAudio(RuntimeEnvironment.getApplication());a.start{};assertEquals(AudioDeviceInfo.TYPE_WIRED_HEADSET,m.communicationDevice!!.type);a.stop();assertNull(m.communicationDevice);assertEquals(AudioManager.MODE_NORMAL,m.mode);a.stop()}
 @Test fun currentExternalDeviceWinsOverAnotherConnectedDevice(){val devices=listOf(CommunicationOutput(1,AudioDeviceInfo.TYPE_BUILTIN_SPEAKER),CommunicationOutput(2,AudioDeviceInfo.TYPE_WIRED_HEADSET),CommunicationOutput(3,AudioDeviceInfo.TYPE_BLUETOOTH_SCO));assertEquals(3,CommunicationRouting.choose(devices,3)!!.id)}
 @Test fun withoutHeadphonesSpeakerIsChosenExplicitly(){val devices=listOf(CommunicationOutput(1,AudioDeviceInfo.TYPE_BUILTIN_EARPIECE),CommunicationOutput(2,AudioDeviceInfo.TYPE_BUILTIN_SPEAKER));assertEquals(2,CommunicationRouting.choose(devices,1)!!.id)}
 @Test fun deniedFocusDoesNotChangeAnotherCallAudioMode(){val m=manager();val shadow=shadowOf(m);m.mode=AudioManager.MODE_IN_CALL;shadow.setNextFocusRequestResponse(AudioManager.AUDIOFOCUS_REQUEST_FAILED);val a=AndroidCallAudio(RuntimeEnvironment.getApplication());var failed=false;try{a.start{}}catch(_:IllegalStateException){failed=true};assertTrue(failed);assertEquals(AudioManager.MODE_IN_CALL,m.mode);a.stop();assertEquals(AudioManager.MODE_IN_CALL,m.mode)}
 @Test fun oldFocusLossCannotInterruptANewCall(){val m=manager();val shadow=shadowOf(m);shadow.setAvailableCommunicationDevices(listOf(device(AudioDeviceInfo.TYPE_BUILTIN_SPEAKER)));val a=AndroidCallAudio(RuntimeEnvironment.getApplication());var interruptions=0;a.start{interruptions++};val old=shadow.lastAudioFocusRequest.listener;a.stop();a.start{interruptions++};old!!.onAudioFocusChange(AudioManager.AUDIOFOCUS_LOSS);assertEquals(0,interruptions);shadow.lastAudioFocusRequest.listener!!.onAudioFocusChange(AudioManager.AUDIOFOCUS_LOSS);assertEquals(1,interruptions);a.stop()}
}
