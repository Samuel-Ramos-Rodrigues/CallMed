package com.callmed.app

import org.junit.Assert.*
import org.junit.Test

class SessionErrorPolicyTest {
    @Test fun forbiddenBusinessOperationDoesNotEndSession() {
        assertFalse(isSessionExpired(403))
        assertFalse(isSessionExpired(409))
        assertFalse(isSessionExpired(429))
        assertFalse(isSessionExpired(0))
    }
    @Test fun unauthorizedRequiresNewAuthentication() {
        assertTrue(isSessionExpired(401))
    }
}
