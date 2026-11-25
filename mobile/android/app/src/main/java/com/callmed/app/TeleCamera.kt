package com.callmed.app

import android.content.Context
import kotlinx.coroutines.CompletableDeferred
import kotlinx.coroutines.withTimeoutOrNull
import org.webrtc.Camera1Enumerator
import org.webrtc.Camera2Enumerator
import org.webrtc.CameraEnumerator
import org.webrtc.CameraVideoCapturer

internal class CameraUnavailable : IllegalStateException("Câmera indisponível")

internal class TeleCameraEvents(private val afterFirstFrameFailure: () -> Unit) :
    CameraVideoCapturer.CameraEventsHandler {
    private val firstFrame = CompletableDeferred<Unit>()

    suspend fun awaitFirstFrame(timeoutMillis: Long = 12_000) {
        withTimeoutOrNull(timeoutMillis) { firstFrame.await() } ?: throw CameraUnavailable()
    }

    fun cancel() { firstFrame.cancel() }
    override fun onFirstFrameAvailable() { firstFrame.complete(Unit) }
    override fun onCameraOpening(name: String) {}
    override fun onCameraClosed() {}

    private fun unavailable() {
        if (!firstFrame.completeExceptionally(CameraUnavailable()) && !firstFrame.isCancelled) {
            afterFirstFrameFailure()
        }
    }

    override fun onCameraDisconnected() = unavailable()
    override fun onCameraError(message: String) = unavailable()
    override fun onCameraFreezed(message: String) = unavailable()
}

internal fun selectCallCamera(context: Context, events: TeleCameraEvents): CameraVideoCapturer {
    fun open(enumerator: CameraEnumerator): CameraVideoCapturer? {
        val devices = enumerator.deviceNames.sortedByDescending(enumerator::isFrontFacing)
        return devices.firstNotNullOfOrNull { enumerator.createCapturer(it, events) }
    }
    try {
        if (Camera2Enumerator.isSupported(context)) open(Camera2Enumerator(context))?.let { return it }
    } catch (error: SecurityException) {
        throw error
    } catch (_: RuntimeException) {
        // Alguns aparelhos anunciam Camera2 mas falham ao enumerar as câmeras.
    }
    return open(Camera1Enumerator(true)) ?: throw CameraUnavailable()
}
