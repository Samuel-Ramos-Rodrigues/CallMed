package com.callmed.app

import kotlinx.coroutines.*
import org.json.JSONArray
import org.json.JSONObject
import org.junit.Assert.*
import org.junit.Test
import java.io.IOException
import java.util.concurrent.CopyOnWriteArrayList
import java.util.concurrent.CountDownLatch
import java.util.concurrent.TimeUnit

internal class MemoryClinicalVault:ClinicalKeyStore {
    var value:String?=null;var broken=false;var saves=0
    override fun load():JSONObject? {if(broken)throw IOException("cofre fictício indisponível");return value?.let(::JSONObject)}
    override fun save(value:JSONObject){this.value=value.toString();saves++;broken=false}
}
class ClinicalPreparationTest {
    private class Fixture(scope:CoroutineScope){
        val v=JSONObject(ClinicalPreparationTest::class.java.getResource("/clinical-interop.json")!!.readText())
        val vault=MemoryClinicalVault();val requests=CopyOnWriteArrayList<ApiRequest>()
        var unauthorized=false;var wrongUser=false;var authLost=0
        var slow:CountDownLatch?=null;var release:CountDownLatch?=null
        val store=CallMedApiTest.MemoryStore(Session("test","refresh",Long.MAX_VALUE))
        val api=CallMedApi(store,ApiTransport{r->
            requests+=r
            if(unauthorized)ApiResponse(401,"{}".toByteArray())else{
                val text=when{
                    r.url.endsWith("/chave")->JSONObject().put("id",v.getString("keyId")).toString()
                    r.url.endsWith("/registros")->"{\"registros\":[]}"
                    else-> {
                        val json=JSONObject().put("usuarioId",if(wrongUser)"outra-conta" else v.getString("user"))
                            .put("pacienteId",v.getString("user")).put("medicoId","psicologo-ficticio").put("editar",true)
                            .put("revogadas",JSONArray()).put("chaves",JSONArray())
                            .put("formulario",JSONObject().put("id",2).put("titulo","Perguntas fictícias").put("perguntas","[\"Pergunta fictícia\"]"))
                        val latch=slow
                        if(latch!=null){slow=null;latch.countDown();check(release!!.await(8,TimeUnit.SECONDS))}
                        json.toString()
                    }
                };ApiResponse(200,text.toByteArray())
            }
        },"https://callmed.test")
        val c=ClinicalPreparation(7,api,scope,{vault},{authLost++})
        suspend fun load(){c.resume();idle()}
        suspend fun idle(){withTimeout(10000){while(c.state.value.busy)delay(5)}}
        fun restorePassword()=v.getString("password").toCharArray()
        fun backup()=v.getJSONObject("backup").toString()
        fun registrations()=requests.count{it.url.endsWith("/chave")}
    }
    @Test fun brokenVaultKeepsRecoveryAvailableAndCannotBeSilentlyOverwritten()=runBlocking{
        val f=Fixture(this);f.vault.broken=true;f.load()
        assertNotNull(f.c.state.value.data);assertTrue(f.c.state.value.vaultUnavailable)
        val pass=f.restorePassword();f.c.prepareCreate(pass){fail("Não deve gerar nova chave sobre cofre ilegível")}
        assertTrue(pass.all{it=='\u0000'});assertEquals(0,f.registrations());assertEquals(0,f.vault.saves)
        f.c.prepareRestore(f.restorePassword()){};f.c.pause();f.c.completeRestore{f.backup()}!!.join()
        assertFalse(f.c.state.value.vaultUnavailable);assertNotNull(f.c.state.value.key);assertEquals(1,f.vault.saves);f.c.close()
    }
    @Test fun filePickerKeepsOnlyItsPasswordAndDoesNotReloadBeforeTheResult()=runBlocking{
        val f=Fixture(this);f.load();f.c.answer("q0","texto clínico fictício");f.c.consent(true)
        val pass=f.restorePassword();f.c.prepareRestore(pass){};val before=f.requests.size
        f.c.pause();assertNull(f.c.state.value.data);assertTrue(f.c.state.value.answers.isEmpty());assertFalse(f.c.state.value.consent)
        f.c.resume();delay(20);assertEquals(before,f.requests.size);assertTrue(f.c.awaitingFile);assertTrue(pass.any{it!='\u0000'})
        f.c.completeRestore{f.backup()}!!.join();assertTrue(pass.all{it=='\u0000'});assertNotNull(f.c.state.value.key);f.c.close()
    }
    @Test fun resultBeforeOnStartDoesNotRaceWithReload()=runBlocking{
        val f=Fixture(this);f.load();f.c.prepareRestore(f.restorePassword()){};f.c.pause()
        val result=f.c.completeRestore{delay(30);f.backup()}!!;f.c.resume();result.join()
        assertEquals(1,f.registrations());assertEquals(1,f.vault.saves);assertTrue(f.c.state.value.status.contains("restaurada"));f.c.close()
    }
    @Test fun wrongPasswordReturnsToRecoveryControlsWithoutRegisteringAKey()=runBlocking{
        val f=Fixture(this);f.load();val pass="Senha-incorreta-2026!".toCharArray();f.c.prepareRestore(pass){};f.c.pause()
        f.c.completeRestore{f.backup()}!!.join()
        assertNotNull(f.c.state.value.data);assertFalse(f.c.state.value.busy);assertTrue(f.c.state.value.status.contains("Senha incorreta"));assertEquals(0,f.registrations());assertEquals(0,f.vault.saves);assertTrue(pass.all{it=='\u0000'});f.c.close()
    }
    @Test fun anotherAccountCannotRegisterTheRestoredKey()=runBlocking{
        val f=Fixture(this);f.load();f.c.prepareRestore(f.restorePassword()){};f.c.pause();f.wrongUser=true
        f.c.completeRestore{f.backup()}!!.join();assertEquals(0,f.registrations());assertEquals(0,f.vault.saves);assertTrue(f.c.state.value.status.contains("outra conta"));f.c.close()
    }
    @Test fun cancellingThePickerWipesPasswordAndLeavesStoredKeyIntact()=runBlocking{
        val f=Fixture(this);f.vault.value=f.v.getJSONObject("key").put("id",f.v.getString("keyId")).toString();val original=f.vault.value;f.load()
        val pass=f.restorePassword();f.c.prepareRestore(pass){};f.c.pause();f.c.cancelFile();f.idle()
        assertEquals(original,f.vault.value);assertTrue(pass.all{it=='\u0000'});assertFalse(f.c.awaitingFile);assertNotNull(f.c.state.value.data);assertEquals(0,f.vault.saves);f.c.close()
    }
    @Test fun leavingTheScreenWipesThePendingPasswordAndMakesLateResultsInert()=runBlocking{
        val f=Fixture(this);f.load();val pass=f.restorePassword();f.c.prepareRestore(pass){};f.c.pause();f.c.close()
        assertTrue(pass.all{it=='\u0000'});assertFalse(f.c.awaitingFile);assertNull(f.c.completeRestore{fail("Resultado atrasado não deve ser lido");""});assertNull(f.c.state.value.data);assertEquals(0,f.registrations())
    }
    @Test fun failedBackupWriteDoesNotRegisterOrReplaceTheKey()=runBlocking{
        val f=Fixture(this);f.load();var opened=false;val pass=f.restorePassword();f.c.prepareCreate(pass){opened=true};f.idle();assertTrue(opened)
        f.c.pause();f.c.completeCreate{throw IOException("Não foi possível salvar a cópia fictícia.")}!!.join()
        assertEquals(0,f.registrations());assertEquals(0,f.vault.saves);assertNotNull(f.c.state.value.data);assertTrue(pass.all{it=='\u0000'});f.c.close()
    }
    @Test fun createResultCanCompleteImmediatelyWithoutLosingItsTransfer()=runBlocking{
        val f=Fixture(this);f.load();var result:Job?=null;var saved=""
        f.c.prepareCreate(f.restorePassword()){result=f.c.completeCreate{saved=it}};f.idle();result!!.join()
        assertTrue(JSONObject(saved).getString("formato")=="CallMedClinicalBackup");assertEquals(1,f.registrations());assertEquals(1,f.vault.saves);f.c.close()
    }
    @Test fun revocationClearsClinicalStateAndNotifiesTheAccount()=runBlocking{
        val f=Fixture(this);f.load();f.c.answer("q0","fictício");f.unauthorized=true;f.c.refresh()!!.join()
        assertEquals(1,f.authLost);assertFalse(f.api.hasSession());assertNull(f.c.state.value.data);assertTrue(f.c.state.value.answers.isEmpty());f.c.close()
    }
    @Test fun delayedReadCannotRepopulateTheScreenAfterLeaving()=runBlocking{
        val f=Fixture(this);val started=CountDownLatch(1);val release=CountDownLatch(1);f.slow=started;f.release=release
        f.c.resume();withContext(Dispatchers.IO){assertTrue(started.await(8,TimeUnit.SECONDS))};f.c.pause();f.c.close();release.countDown();delay(40)
        assertNull(f.c.state.value.data);assertTrue(f.c.state.value.answers.isEmpty());assertFalse(f.c.state.value.busy)
    }
}
