package com.callmed.app

/** A forbidden operation (403) is not an invalid session (401). */
internal fun isSessionExpired(httpStatus: Int): Boolean = httpStatus == 401
