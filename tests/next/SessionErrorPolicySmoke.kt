package com.callmed.app

fun main() {
    check(isSessionExpired(401)) { "HTTP 401 deve expirar a sessão" }
    for (status in listOf(403, 404, 409, 429, 0))
        check(!isSessionExpired(status)) { "HTTP $status não pode encerrar a sessão" }
    println("OK: Kotlin V54.1 sessão mantém acesso em 403 e encerra em 401")
}
