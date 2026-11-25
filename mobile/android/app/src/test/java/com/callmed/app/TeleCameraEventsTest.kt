package com.callmed.app

import kotlinx.coroutines.CancellationException
import kotlinx.coroutines.async
import kotlinx.coroutines.runBlocking
import kotlinx.coroutines.yield
import org.junit.Assert.*
import org.junit.Test

class TeleCameraEventsTest {
    @Test fun openingCameraDoesNotMakeVideoReady() = runBlocking {
        val events=TeleCameraEvents { fail("unexpected camera failure") }
        val pending=async { events.awaitFirstFrame() }
        events.onCameraOpening("0")
        yield()
        assertFalse(pending.isCompleted)
        events.onFirstFrameAvailable()
        pending.await()
    }

    @Test fun startupErrorResumesWaitingEntryAndDoesNotStopAnotherCall() = runBlocking {
        val events=TeleCameraEvents { fail("startup error should reach the waiting entry only") }
        events.onCameraError("camera busy")
        assertTrue(runCatching { events.awaitFirstFrame() }.exceptionOrNull() is CameraUnavailable)
    }

    @Test fun stalledCameraProducesRetryableError() = runBlocking {
        val events=TeleCameraEvents { fail("unexpected post-entry callback") }
        assertTrue(runCatching { events.awaitFirstFrame(5) }.exceptionOrNull() is CameraUnavailable)
    }

    @Test fun leaveUnblocksStartupAndIgnoresLateHardwareErrors() = runBlocking {
        val events=TeleCameraEvents { fail("camera callback after leaving") }
        events.cancel()
        events.onCameraDisconnected()
        assertTrue(runCatching { events.awaitFirstFrame() }.exceptionOrNull() is CancellationException)
    }

    @Test fun hardwareFailureAfterFirstFrameReachesActiveCall() = runBlocking {
        var failures=0
        val events=TeleCameraEvents { failures++ }
        events.onFirstFrameAvailable()
        events.awaitFirstFrame()
        events.onCameraFreezed("camera stalled")
        assertEquals(1,failures)
    }
}
